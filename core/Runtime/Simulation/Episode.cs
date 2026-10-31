using System;
using System.Collections.Generic;
using LowObservable.Core.Agents;
using LowObservable.Core.Estimation;
using LowObservable.Core.Maths;
using LowObservable.Core.Robot;
using LowObservable.Core.Sensors;
using LowObservable.Core.World;

namespace LowObservable.Core.Simulation
{
    public enum AssociationPolicy
    {
        /// <summary>Ambiguous matches use the best candidate automatically. Fast, and occasionally wrong.</summary>
        AutoBest,
        /// <summary>Ambiguous matches pause the episode until the player picks a landmark (real-time with pause).</summary>
        PauseForPlayer,
    }

    public sealed class EpisodeConfig
    {
        public ulong Seed = 1;
        public TuningParameters Tuning = TuningParameters.Default();
        public AssociationPolicy Association = AssociationPolicy.AutoBest;
        /// <summary>If true the Seeker starts with a rough idea of where you entered.</summary>
        public bool SeekerStartsAlerted = false;
        public double SeekerAlertSigma = 4.0;
        /// <summary>Fixed simulation step. Unity's FixedUpdate should match it (default 50 Hz).</summary>
        public double FixedDt = 0.02;
        public bool Record = true;
        public double RecordPeriod = 0.1;
    }

    /// <summary>A measurement waiting for the player to say which landmark it was.</summary>
    public sealed class PendingAssociation
    {
        public LandmarkObservation Observation;
        public List<AssociationCandidate> Candidates;
    }

    /// <summary>
    /// One attempt at one floor: the complete game simulation with no engine dependency.
    ///
    /// Two worlds live side by side here and must never leak into each other:
    ///   - <b>Truth</b>: <see cref="TruePose"/>, the Seeker's real position, real landmarks.
    ///   - <b>Belief</b>: <see cref="Filter"/> (the player's EKF) and the Seeker's particle filter.
    /// Rendering code should draw the player's belief, the Seeker's belief, and only those truths
    /// the player could know (the blueprint, bumps, the Seeker when it is visible). The reveal
    /// screen gets everything from <see cref="Recorder"/> afterwards.
    ///
    /// Call <see cref="Step"/> once per fixed tick with the player's command.
    /// </summary>
    public sealed class Episode
    {
        private readonly TuningParameters _t;
        private readonly EpisodeConfig _config;
        private readonly Rng _motionRng;
        private readonly PlayerSensors _sensors;
        private readonly List<EpisodeEvent> _tickEvents = new List<EpisodeEvent>();
        private readonly List<NoiseEvent> _tickNoises = new List<NoiseEvent>();
        private readonly List<Decoy> _decoys = new List<Decoy>();
        private readonly Queue<PendingAssociation> _pending = new Queue<PendingAssociation>();

        private double _pingCooldown;
        private double _dockCooldown;
        private double _cameraTimer;
        private double _footstepTimer;
        private double _listenLock;
        private double _recordTimer;
        private SeekerMode _lastSeekerMode;

        public FloorPlan Plan { get; }
        public TuningParameters Tuning => _t;
        public double Time { get; private set; }
        public int TickCount { get; private set; }
        public EpisodeOutcome Outcome { get; private set; } = EpisodeOutcome.Running;

        /// <summary>Ground truth pose of the drone. Do not render this during play.</summary>
        public Pose2 TruePose { get; private set; }
        /// <summary>The player's belief about its own pose.</summary>
        public EkfLocalizer Filter { get; }
        /// <summary>The hunter, including its belief about the player (<c>Seeker.Belief</c>).</summary>
        public SeekerAgent Seeker { get; }

        public MovementMode CurrentMode { get; private set; } = MovementMode.Walk;
        public int DecoysLeft { get; private set; }
        public double PingCooldownRemaining => System.Math.Max(0, _pingCooldown);
        public bool IsListening => _listenLock > 0;
        public IReadOnlyList<Decoy> Decoys => _decoys;

        /// <summary>Events from the most recent Step. Cleared at the start of each Step.</summary>
        public IReadOnlyList<EpisodeEvent> TickEvents => _tickEvents;

        /// <summary>True while an ambiguous observation waits for <see cref="ResolvePending"/>. The sim does not advance meanwhile.</summary>
        public bool AwaitingAssociation => _pending.Count > 0;
        public PendingAssociation CurrentPending => _pending.Count > 0 ? _pending.Peek() : null;

        public ConsistencyTracker Consistency { get; } = new ConsistencyTracker();
        public RunRecorder Recorder { get; }
        public int CorrectAssociations { get; private set; }
        public int WrongAssociations { get; private set; }

        /// <summary>Last Listen result, for the HUD bearing indicator.</summary>
        public SeekerBearing LastListen { get; private set; }

        public Episode(FloorPlan plan, EpisodeConfig config = null)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _config = config ?? new EpisodeConfig();
            _t = _config.Tuning;
            _motionRng = Rng.Derive(_config.Seed, "player-motion");
            _sensors = new PlayerSensors(plan, _t, Rng.Derive(_config.Seed, "player-sensors"));

            TruePose = new Pose2(plan.PlayerStart, plan.PlayerStartHeading);
            Filter = EkfLocalizer.WithSigmas(TruePose, _t.InitialPositionSigma, _t.InitialHeadingSigma);

            Seeker = new SeekerAgent(plan, _t, Rng.Derive(_config.Seed, "seeker"));
            Seeker.InitializeBelief(_config.SeekerStartsAlerted, plan.PlayerStart, _config.SeekerAlertSigma);
            _lastSeekerMode = Seeker.Mode;

            DecoysLeft = _t.DecoyCharges;
            if (_config.Record) Recorder = new RunRecorder(plan.Name, _config.Seed);
            RecordSample();
        }

        public double FixedDt => _config.FixedDt;

        /// <summary>Advances the simulation one fixed tick.</summary>
        public void Step(PlayerCommand command)
        {
            _tickEvents.Clear();
            _tickNoises.Clear();
            if (Outcome != EpisodeOutcome.Running || AwaitingAssociation) return;

            double dt = _config.FixedDt;
            Time += dt;
            TickCount++;
            CurrentMode = command.Mode;

            _pingCooldown -= dt;
            _dockCooldown -= dt;
            _listenLock -= dt;

            HandleAction(command);
            if (Outcome != EpisodeOutcome.Running) { RecordSample(); Finish(); return; }

            if (!IsListening) MovePlayer(command, dt);

            _cameraTimer -= dt;
            if (_cameraTimer <= 0)
            {
                _cameraTimer = _t.CameraPeriod;
                var frame = _sensors.Camera(TruePose, Time);
                if (frame.Count > 0) Emit(EpisodeEventType.CameraFrame, Filter.Mean.Position, frame.Count);
                ProcessObservations(frame);
            }

            UpdateDecoys(dt);
            UpdateSeeker(dt);

            if (Vec2.Distance(Seeker.Pose.Position, TruePose.Position) <= _t.CaptureRadius)
            {
                Outcome = EpisodeOutcome.Captured;
                Emit(EpisodeEventType.Captured, TruePose.Position, 0);
            }
            else if (Time >= _t.MaxEpisodeSeconds)
            {
                Outcome = EpisodeOutcome.TimedOut;
                Emit(EpisodeEventType.TimedOut, TruePose.Position, 0);
            }

            Consistency.Add(TruePose.Position, Filter.Mean.Position, Filter.Covariance);
            _recordTimer -= dt;
            if (_recordTimer <= 0 || Outcome != EpisodeOutcome.Running)
            {
                _recordTimer = _config.RecordPeriod;
                RecordSample();
            }
            if (Outcome != EpisodeOutcome.Running) Finish();
        }

        /// <summary>
        /// Answers the current pending association. Pass a landmark id from the candidates, or -1 to
        /// throw the measurement away. The measurement is fused against the current (paused) belief.
        /// </summary>
        public void ResolvePending(int landmarkId)
        {
            if (_pending.Count == 0) return;
            var p = _pending.Dequeue();
            _tickEvents.Clear();
            if (landmarkId < 0)
            {
                Emit(EpisodeEventType.ObservationRejected, Filter.Mean.Position, 0, p.Observation.TrueLandmarkId);
                return;
            }
            var lm = Plan.GetLandmark(landmarkId);
            if (lm == null) throw new ArgumentException($"No landmark with id {landmarkId}.");
            ApplyObservation(p.Observation, lm, Filter.ComputeInnovation(p.Observation, lm.Position));
        }

        // ------------------------------------------------------------------ actions

        private void HandleAction(PlayerCommand command)
        {
            switch (command.Action)
            {
                case PlayerAction.Ping:
                    if (_pingCooldown > 0) { Emit(EpisodeEventType.PingOnCooldown, Filter.Mean.Position, _pingCooldown); break; }
                    _pingCooldown = _t.PingCooldown;
                    MakeNoise(TruePose.Position, _t.PingLoudness, NoiseSource.Ping);
                    var obs = _sensors.Ping(TruePose, Time);
                    Emit(EpisodeEventType.PingFired, Filter.Mean.Position, obs.Count);
                    ProcessObservations(obs);
                    break;

                case PlayerAction.Listen:
                    if (IsListening) break;
                    _listenLock = _t.ListenDuration;
                    Emit(EpisodeEventType.ListenStarted, Filter.Mean.Position, 0);
                    LastListen = _sensors.Listen(TruePose, Seeker.Pose.Position, Time);
                    Emit(LastListen.Heard ? EpisodeEventType.ListenHeardSeeker : EpisodeEventType.ListenHeardNothing,
                        Filter.Mean.Position, LastListen.Bearing);
                    break;

                case PlayerAction.ThrowDecoy:
                    if (DecoysLeft <= 0) { Emit(EpisodeEventType.NoDecoysLeft, Filter.Mean.Position, 0); break; }
                    DecoysLeft--;
                    var landed = ThrowLanding(command.DecoyTargetLocal);
                    _decoys.Add(new Decoy(landed, _t.DecoyBursts, 0.5));
                    Emit(EpisodeEventType.DecoyThrown, landed, DecoysLeft);
                    break;

                case PlayerAction.Dock:
                    if (_dockCooldown > 0) break;
                    _dockCooldown = _t.DockCooldown;
                    if (Vec2.Distance(TruePose.Position, Plan.Uplink) <= _t.DockRadius)
                    {
                        Outcome = EpisodeOutcome.Escaped;
                        Emit(EpisodeEventType.Docked, TruePose.Position, 0);
                    }
                    else
                    {
                        // Ground-truth feedback: you are not where you think you are. Also noisy.
                        MakeNoise(TruePose.Position, _t.FailedDockLoudness, NoiseSource.FailedDock);
                        Emit(EpisodeEventType.DockFailed, Filter.Mean.Position, 0);
                    }
                    break;
            }
        }

        private Vec2 ThrowLanding(Vec2 local)
        {
            double len = local.Length;
            if (len > _t.DecoyThrowRange) local = local / len * _t.DecoyThrowRange;
            var target = TruePose.LocalToWorld(local);
            var from = TruePose.Position;
            var landed = from;
            for (int k = 1; k <= 40; k++)
            {
                var p = Vec2.Lerp(from, target, k / 40.0);
                if (!Plan.IsFree(p, 0.1)) break;
                landed = p;
            }
            return landed;
        }

        // ------------------------------------------------------------------ motion

        private void MovePlayer(PlayerCommand command, double dt)
        {
            double forward = Clamp(command.Forward, -1, 1);
            double turn = Clamp(command.Turn, -1, 1);
            var commanded = new OdometryDelta(forward * _t.SpeedFor(command.Mode) * dt, turn * _t.TurnRate * dt);
            if (commanded.IsZero) { _footstepTimer = 0; return; }

            double modeScale = _t.NoiseScaleFor(command.Mode);

            // Truth: the wheels slip according to where the drone really is.
            double trueScale = modeScale * (Plan.IsSlickAt(TruePose.Position) ? _t.SlickNoiseScale : 1.0);
            OdometryModel.NoiseVariances(commanded, trueScale, _t.OdomDistanceVariancePerMeter,
                _t.OdomHeadingVariancePerMeter, _t.OdomHeadingVariancePerRadian, out double dVar, out double rVar);
            var actual = new OdometryDelta(
                commanded.Distance + _motionRng.NextGaussian() * System.Math.Sqrt(dVar),
                commanded.Rotation + _motionRng.NextGaussian() * System.Math.Sqrt(rVar));
            var next = OdometryModel.Apply(TruePose, actual);

            if (Plan.IsFree(next.Position, _t.PlayerRadius))
            {
                TruePose = next;
            }
            else
            {
                // Slide along walls if one axis is free; otherwise stop. Either way the drone felt the bump.
                var slideX = new Vec2(next.X, TruePose.Y);
                var slideY = new Vec2(TruePose.X, next.Y);
                if (Plan.IsFree(slideX, _t.PlayerRadius)) TruePose = new Pose2(slideX, next.Theta);
                else if (Plan.IsFree(slideY, _t.PlayerRadius)) TruePose = new Pose2(slideY, next.Theta);
                else TruePose = new Pose2(TruePose.Position, next.Theta);
                Emit(EpisodeEventType.Bump, TruePose.Position, 0);
            }

            // Belief: the filter only knows the commanded odometry, and inflates noise only where it *thinks* it is slick.
            double believedScale = modeScale * (Plan.IsSlickAt(Filter.Mean.Position) ? _t.SlickNoiseScale : 1.0);
            OdometryModel.NoiseVariances(commanded, believedScale, _t.OdomDistanceVariancePerMeter,
                _t.OdomHeadingVariancePerMeter, _t.OdomHeadingVariancePerRadian, out double bdVar, out double brVar);
            Filter.Predict(commanded, bdVar, brVar);

            if (System.Math.Abs(commanded.Distance) > 0)
            {
                _footstepTimer -= dt;
                if (_footstepTimer <= 0)
                {
                    _footstepTimer = _t.FootstepPeriod;
                    MakeNoise(TruePose.Position, _t.LoudnessFor(command.Mode), NoiseSource.Footstep);
                }
            }
        }

        // ------------------------------------------------------------------ association

        private void ProcessObservations(List<LandmarkObservation> observations)
        {
            foreach (var obs in observations)
            {
                var result = DataAssociation.Associate(Filter, obs, Plan.Landmarks, _t.AssociationGate, _t.AmbiguityRatio);
                switch (result.Outcome)
                {
                    case AssociationOutcome.NoMatch:
                        Emit(EpisodeEventType.ObservationRejected, Filter.Mean.Position, 0, obs.TrueLandmarkId);
                        break;
                    case AssociationOutcome.Confident:
                        ApplyObservation(obs, result.Best.Landmark, result.Best.Innovation);
                        break;
                    case AssociationOutcome.Ambiguous:
                        Emit(EpisodeEventType.ObservationAmbiguous, Filter.Mean.Position, result.Candidates.Count, obs.TrueLandmarkId);
                        if (_config.Association == AssociationPolicy.PauseForPlayer)
                            _pending.Enqueue(new PendingAssociation { Observation = obs, Candidates = result.Candidates });
                        else
                            ApplyObservation(obs, result.Best.Landmark, result.Best.Innovation);
                        break;
                }
            }
        }

        private void ApplyObservation(LandmarkObservation obs, Landmark landmark, Innovation innovation)
        {
            Filter.Update(innovation);
            bool correct = landmark.Id == obs.TrueLandmarkId;
            if (correct) CorrectAssociations++; else WrongAssociations++;
            _tickEvents.Add(new EpisodeEvent
            {
                Time = Time,
                Type = EpisodeEventType.ObservationApplied,
                Position = landmark.Position,
                Value = innovation.Nis,
                LandmarkId = landmark.Id,
                AssociationCorrect = correct,
            });
            Recorder?.AddEvent(_tickEvents[_tickEvents.Count - 1]);
        }

        // ------------------------------------------------------------------ decoys and Seeker

        private void UpdateDecoys(double dt)
        {
            for (int k = _decoys.Count - 1; k >= 0; k--)
            {
                var d = _decoys[k];
                d.Timer -= dt;
                if (d.Timer <= 0 && d.BurstsRemaining > 0)
                {
                    d.BurstsRemaining--;
                    d.Timer = _t.DecoyBurstPeriod;
                    MakeNoise(d.Position, _t.DecoyLoudness, NoiseSource.Decoy);
                    Emit(EpisodeEventType.DecoyBurst, d.Position, d.BurstsRemaining);
                }
                if (d.BurstsRemaining <= 0 && d.Timer <= 0) _decoys.RemoveAt(k);
            }
        }

        private void UpdateSeeker(double dt)
        {
            Seeker.Predict(dt);
            var perception = new SeekerPerception();
            foreach (var noise in _tickNoises)
            {
                if (Seeker.Hear(noise, ref perception))
                    Emit(EpisodeEventType.SeekerHeard, perception.HeardAt, (double)noise.Source);
            }
            Seeker.Look(TruePose.Position, dt, ref perception);
            if (perception.Saw) Emit(EpisodeEventType.SeekerSaw, perception.SawAt, 0);
            Seeker.Act(dt);
            if (Seeker.Mode != _lastSeekerMode)
            {
                _lastSeekerMode = Seeker.Mode;
                Emit(EpisodeEventType.SeekerModeChanged, Seeker.Pose.Position, (double)Seeker.Mode);
            }
        }

        private void MakeNoise(Vec2 position, double loudness, NoiseSource source)
        {
            _tickNoises.Add(new NoiseEvent { Time = Time, Position = position, Loudness = loudness, Source = source });
        }

        // ------------------------------------------------------------------ bookkeeping

        private void Emit(EpisodeEventType type, Vec2 position, double value, int landmarkId = -1)
        {
            var e = new EpisodeEvent { Time = Time, Type = type, Position = position, Value = value, LandmarkId = landmarkId };
            _tickEvents.Add(e);
            Recorder?.AddEvent(e);
        }

        private void RecordSample()
        {
            if (Recorder == null) return;
            var c = Filter.Covariance;
            var sc = Seeker.Belief.CovarianceMatrix();
            Recorder.AddSample(new RunSample
            {
                Time = Time,
                TrueX = TruePose.X,
                TrueY = TruePose.Y,
                TrueTheta = TruePose.Theta,
                EstX = Filter.Mean.X,
                EstY = Filter.Mean.Y,
                EstTheta = Filter.Mean.Theta,
                CovXX = c[0, 0],
                CovXY = c[0, 1],
                CovYY = c[1, 1],
                CovTT = c[2, 2],
                SeekerX = Seeker.Pose.X,
                SeekerY = Seeker.Pose.Y,
                SeekerLook = Seeker.LookHeading,
                SeekerMode = (int)Seeker.Mode,
                SeekerBeliefX = Seeker.Belief.Mean().X,
                SeekerBeliefY = Seeker.Belief.Mean().Y,
                SeekerBeliefSpread = System.Math.Sqrt(System.Math.Max(sc[0, 0] + sc[1, 1], 0)),
                SeekerEss = Seeker.Belief.EffectiveSampleSize(),
                Mode = (int)CurrentMode,
            });
        }

        private void Finish()
        {
            Recorder?.Finish(Outcome, Time, Consistency, CorrectAssociations, WrongAssociations);
        }

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    /// <summary>A thrown decoy that periodically emits a ping-like sound.</summary>
    public sealed class Decoy
    {
        public Vec2 Position { get; }
        public int BurstsRemaining;
        public double Timer;

        public Decoy(Vec2 position, int bursts, double firstBurstDelay)
        {
            Position = position;
            BurstsRemaining = bursts;
            Timer = firstBurstDelay;
        }
    }
}
