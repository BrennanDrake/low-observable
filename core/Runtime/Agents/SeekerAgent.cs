using System.Collections.Generic;
using LowObservable.Core.Estimation;
using LowObservable.Core.Maths;
using LowObservable.Core.Sensors;
using LowObservable.Core.Simulation;
using LowObservable.Core.World;

namespace LowObservable.Core.Agents
{
    public enum SeekerMode
    {
        /// <summary>Belief is spread out: walk toward the most likely area while sweeping the camera.</summary>
        Search,
        /// <summary>Belief is tight: move fast straight at it.</summary>
        Hunt,
    }

    /// <summary>What the Seeker perceived this tick, for UI barks, audio cues and the reveal timeline.</summary>
    public struct SeekerPerception
    {
        public bool Heard;
        public NoiseSource HeardSource;
        public Vec2 HeardAt;
        public bool Saw;
        public Vec2 SawAt;
    }

    /// <summary>
    /// WARDEN's hunter unit. It never knows where you are; it runs a particle filter on your position
    /// and acts on that belief. Everything it does is driven by three inputs:
    ///   1. sounds you make (footsteps, pings, failed docks) and decoys you throw,
    ///   2. its camera seeing you (a strong fix),
    ///   3. its camera <i>not</i> seeing you where it is looking (negative information that clears particles).
    /// The player wins by managing what the Seeker can observe.
    /// </summary>
    public sealed class SeekerAgent
    {
        private readonly FloorPlan _plan;
        private readonly TuningParameters _t;
        private readonly Rng _rng;
        private readonly GridPathfinder _pathfinder;
        private List<Vec2> _path = new List<Vec2>();
        private int _pathIndex;
        private double _replanTimer;
        private double _visionTimer;
        private double _scanPhase;
        private double _predictAccumulator;

        /// <summary>The Seeker's belief is propagated at this period (seconds) rather than every physics tick.</summary>
        public const double PredictPeriod = 0.1;

        public Pose2 Pose { get; private set; }
        /// <summary>Direction the Seeker's camera points. Sweeps side to side while searching.</summary>
        public double LookHeading { get; private set; }
        public ParticleFilter2D Belief { get; }
        public SeekerMode Mode { get; private set; } = SeekerMode.Search;
        public Vec2 Goal { get; private set; }
        public IReadOnlyList<Vec2> Path => _path;

        public SeekerAgent(FloorPlan plan, TuningParameters tuning, Rng rng)
        {
            _plan = plan;
            _t = tuning;
            _rng = rng;
            _pathfinder = new GridPathfinder(plan);
            Pose = new Pose2(plan.SeekerStart, 0);
            LookHeading = 0;
            Belief = new ParticleFilter2D(plan, tuning.SeekerParticleCount, rng);
            Goal = plan.SeekerStart;
        }

        /// <summary>Starts the Seeker knowing roughly where you entered (alerted) or knowing nothing.</summary>
        public void InitializeBelief(bool alerted, Vec2 entryPoint, double sigma = 4.0)
        {
            if (alerted) Belief.InitializeGaussian(entryPoint, sigma);
            else Belief.InitializeUniform();
        }

        /// <summary>Prediction step: the Seeker assumes you kept moving in some unknown direction.</summary>
        public void Predict(double dt)
        {
            // Accumulate and propagate at 10 Hz: a random walk over 0.1 s is statistically the same as five
            // 0.02 s steps, and it saves most of the per-particle wall checks.
            _predictAccumulator += dt;
            if (_predictAccumulator < PredictPeriod) return;
            Belief.PredictRandomWalk(_predictAccumulator, _t.SeekerAssumedPlayerSpeed);
            _predictAccumulator = 0;
        }

        /// <summary>Hearing: if the sound reaches the Seeker it gets a noisy fix on the source.</summary>
        public bool Hear(NoiseEvent noise, ref SeekerPerception perception)
        {
            double dist = Vec2.Distance(Pose.Position, noise.Position);
            bool los = _plan.LineOfSight(Pose.Position, noise.Position);
            double audible = los ? noise.Loudness : noise.Loudness * _t.WallMuffling;
            if (dist > audible) return false;

            double sigma = _t.HearingSigmaBase + _t.HearingSigmaPerMeter * dist;
            var measured = new Vec2(noise.Position.X + _rng.NextGaussian() * sigma, noise.Position.Y + _rng.NextGaussian() * sigma);
            Belief.UpdatePositionFix(measured, sigma);
            Belief.ResampleIfNeeded();
            perception.Heard = true;
            perception.HeardSource = noise.Source;
            perception.HeardAt = measured;
            return true;
        }

        /// <summary>
        /// Vision, run every SeekerVisionPeriod. If the player is visible: strong position fix. If not:
        /// down-weight every particle the camera would have seen. Absence of evidence is evidence.
        /// </summary>
        public void Look(Vec2 truePlayer, double dt, ref SeekerPerception perception)
        {
            _visionTimer -= dt;
            if (_visionTimer > 0) return;
            _visionTimer = _t.SeekerVisionPeriod;

            if (CanSee(truePlayer))
            {
                double s = _t.SeekerVisionSigma;
                var measured = new Vec2(truePlayer.X + _rng.NextGaussian() * s, truePlayer.Y + _rng.NextGaussian() * s);
                Belief.UpdatePositionFix(measured, s * 2.0, outlierWeight: 0.001);
                perception.Saw = true;
                perception.SawAt = measured;
            }
            else
            {
                double w = _t.SeekerNegativeInfoWeight;
                Belief.Update(p => CanSee(p) ? w : 1.0);
            }
            Belief.ResampleIfNeeded();
            Belief.InjectUniform(_t.SeekerInjectionFraction);
        }

        /// <summary>True if a point is inside the Seeker's view cone, in range, in line of sight, and lit (or very close).</summary>
        public bool CanSee(Vec2 point)
        {
            var rel = point - Pose.Position;
            double dist = rel.Length;
            if (dist > _t.SeekerVisionRange) return false;
            if (dist > 0.3 && System.Math.Abs(Angle.Difference(rel.Angle, LookHeading)) > 0.5 * _t.SeekerFieldOfView) return false;
            if (_plan.IsDarkAt(point) && dist > _t.SeekerDarkVisionRange) return false;
            return _plan.LineOfSight(Pose.Position, point);
        }

        /// <summary>Decides where to go from the current belief and moves there.</summary>
        public void Act(double dt)
        {
            _replanTimer -= dt;
            if (_replanTimer <= 0)
            {
                _replanTimer = _t.SeekerReplanPeriod;
                Mode = Belief.Spread() < _t.SeekerHuntSpread ? SeekerMode.Hunt : SeekerMode.Search;
                Goal = Belief.Mode();
                _path = _pathfinder.FindPath(Pose.Position, Goal);
                _pathIndex = 0;
            }

            double speed = Mode == SeekerMode.Hunt ? _t.SeekerHuntSpeed : _t.SeekerSearchSpeed;
            double heading = Pose.Theta;
            var pos = Pose.Position;
            double budget = speed * dt;
            while (budget > 1e-9 && _pathIndex < _path.Count)
            {
                var target = _path[_pathIndex];
                var to = target - pos;
                double d = to.Length;
                if (d < 1e-6) { _pathIndex++; continue; }
                double desired = to.Angle;
                double turn = Angle.Difference(desired, heading);
                double maxTurn = _t.SeekerTurnRate * dt;
                heading += System.Math.Max(-maxTurn, System.Math.Min(maxTurn, turn));
                if (System.Math.Abs(turn) > 1.2) break; // turn mostly in place before driving off at an angle
                double step = System.Math.Min(budget, d);
                pos += to / d * step;
                budget -= step;
                if (step >= d - 1e-9) _pathIndex++;
            }

            if (_pathIndex >= _path.Count)
            {
                // Arrived (or no path): rotate on the spot so the camera clears the surroundings.
                heading += _t.SeekerTurnRate * 0.5 * dt;
            }
            Pose = new Pose2(pos, heading);

            if (Mode == SeekerMode.Search)
            {
                _scanPhase += dt * 2.2;
                LookHeading = Angle.Wrap(Pose.Theta + _t.SeekerScanAmplitude * System.Math.Sin(_scanPhase));
            }
            else
            {
                var toGoal = Goal - Pose.Position;
                LookHeading = toGoal.LengthSquared > 0.01 ? toGoal.Angle : Pose.Theta;
            }
        }
    }
}
