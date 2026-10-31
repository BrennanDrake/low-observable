using System.Collections.Generic;
using LowObservable.Core.Maths;
using LowObservable.Core.Simulation;
using LowObservable.Core.World;

namespace LowObservable.Core.Sensors
{
    /// <summary>A noisy bearing to the Seeker from the Listen action, in the drone frame.</summary>
    public struct SeekerBearing
    {
        public double Time;
        public bool Heard;
        public double Bearing;
        public double BearingSigma;
    }

    /// <summary>
    /// Generates the player's measurements from the <b>true</b> world state. Each sensor has its own
    /// noise shape: the ping is precise but anonymous and loud, the camera knows what it sees but is
    /// poor at range and blind in the dark. Only the measurements, never the truth, reach the filter.
    /// </summary>
    public sealed class PlayerSensors
    {
        private readonly FloorPlan _plan;
        private readonly TuningParameters _t;
        private readonly Rng _rng;
        private int _nextObservationId;

        public PlayerSensors(FloorPlan plan, TuningParameters tuning, Rng rng)
        {
            _plan = plan;
            _t = tuning;
            _rng = rng;
        }

        /// <summary>Active ping: range and bearing to every landmark within range and line of sight. No identity.</summary>
        public List<LandmarkObservation> Ping(Pose2 truePose, double time)
        {
            var list = new List<LandmarkObservation>();
            foreach (var lm in _plan.Landmarks)
            {
                double range = Vec2.Distance(truePose.Position, lm.Position);
                if (range > _t.PingRange || range < 1e-3) continue;
                if (!_plan.LineOfSight(truePose.Position, lm.Position)) continue;
                list.Add(Make(SensorKind.Ping, truePose, lm, time, _t.PingRangeSigma, _t.PingBearingSigma, appearance: -1));
            }
            return list;
        }

        /// <summary>
        /// Passive camera: landmarks inside the field of view, in range, in line of sight and on a lit cell.
        /// Reports appearance (so association is easier) but range is much noisier than the ping.
        /// </summary>
        public List<LandmarkObservation> Camera(Pose2 truePose, double time)
        {
            var list = new List<LandmarkObservation>();
            foreach (var lm in _plan.Landmarks)
            {
                if (_plan.IsDarkAt(lm.Position)) continue;
                var rel = lm.Position - truePose.Position;
                double range = rel.Length;
                if (range > _t.CameraRange || range < 1e-3) continue;
                double bearing = Angle.Difference(rel.Angle, truePose.Theta);
                if (System.Math.Abs(bearing) > 0.5 * _t.CameraFieldOfView) continue;
                if (!_plan.LineOfSight(truePose.Position, lm.Position)) continue;
                double rangeSigma = _t.CameraRangeSigmaBase + _t.CameraRangeSigmaPerMeter * range;
                list.Add(Make(SensorKind.Camera, truePose, lm, time, rangeSigma, _t.CameraBearingSigma, lm.Appearance));
            }
            return list;
        }

        /// <summary>Listen: a noisy bearing to the Seeker if it is within hearing range (walls muffle it).</summary>
        public SeekerBearing Listen(Pose2 truePose, Vec2 trueSeeker, double time)
        {
            var result = new SeekerBearing { Time = time, BearingSigma = _t.ListenBearingSigma };
            double dist = Vec2.Distance(truePose.Position, trueSeeker);
            double range = _plan.LineOfSight(truePose.Position, trueSeeker) ? _t.ListenRange : _t.ListenRange * _t.WallMuffling;
            if (dist > range) return result;
            double trueBearing = Angle.Difference((trueSeeker - truePose.Position).Angle, truePose.Theta);
            result.Heard = true;
            result.Bearing = Angle.Wrap(trueBearing + _rng.NextGaussian() * _t.ListenBearingSigma);
            return result;
        }

        private LandmarkObservation Make(SensorKind kind, Pose2 truePose, Landmark lm, double time,
            double rangeSigma, double bearingSigma, int appearance)
        {
            var rel = lm.Position - truePose.Position;
            double range = rel.Length;
            double bearing = Angle.Difference(rel.Angle, truePose.Theta);
            return new LandmarkObservation
            {
                Id = _nextObservationId++,
                Kind = kind,
                Time = time,
                Range = System.Math.Max(0.01, range + _rng.NextGaussian() * rangeSigma),
                Bearing = Angle.Wrap(bearing + _rng.NextGaussian() * bearingSigma),
                RangeSigma = rangeSigma,
                BearingSigma = bearingSigma,
                Appearance = appearance,
                TrueLandmarkId = lm.Id,
            };
        }
    }
}
