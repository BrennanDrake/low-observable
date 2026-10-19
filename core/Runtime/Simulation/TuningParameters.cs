using LowObservable.Core.Maths;

namespace LowObservable.Core.Simulation
{
    /// <summary>
    /// Every number that shapes the game feel, in one place. Values are SI (meters, seconds, radians).
    /// The Unity package exposes this through a ScriptableObject so designers can tune without code,
    /// and the headless simulator (dotnet/LowObservable.Sim) can sweep them to check balance.
    /// See docs/tuning.md for what each group does and how changing it changes play.
    /// </summary>
    [System.Serializable]
    public sealed class TuningParameters
    {
        // ---------------------------------------------------------------- Player motion
        public double CreepSpeed = 0.6;
        public double WalkSpeed = 1.4;
        public double SprintSpeed = 2.8;
        public double TurnRate = 2.4;            // rad/s at full turn input
        public double PlayerRadius = 0.25;

        /// <summary>
        /// Odometry noise (Probabilistic Robotics style, variance grows with distance travelled and
        /// rotation). Distance variance per meter moved, scaled per mode below.
        /// </summary>
        public double OdomDistanceVariancePerMeter = 0.0025;
        public double OdomHeadingVariancePerMeter = 0.0008;   // rad^2 of heading drift per meter driven
        public double OdomHeadingVariancePerRadian = 0.0040;  // rad^2 per radian turned

        /// <summary>Multiplies odometry noise by mode: sprinting slips more, creeping is precise.</summary>
        public double CreepNoiseScale = 0.5;
        public double WalkNoiseScale = 1.0;
        public double SprintNoiseScale = 3.0;

        /// <summary>
        /// Extra slip on slick cells. The player's filter only inflates its noise if it *believes* it is on a
        /// slick cell, so drifting onto ice unknowingly makes the filter overconfident. That is intended.
        /// </summary>
        public double SlickNoiseScale = 8.0;

        // ---------------------------------------------------------------- Player sensors
        public double PingRange = 9.0;
        public double PingRangeSigma = 0.05;
        public double PingBearingSigma = 1.0 * Angle.Deg2Rad;
        public double PingCooldown = 1.5;

        public double CameraRange = 6.0;
        public double CameraFieldOfView = 100.0 * Angle.Deg2Rad;
        public double CameraRangeSigmaBase = 0.15;
        public double CameraRangeSigmaPerMeter = 0.08;
        public double CameraBearingSigma = 2.0 * Angle.Deg2Rad;
        public double CameraPeriod = 0.5;

        public double ListenDuration = 1.0;      // seconds the player is locked in place
        public double ListenRange = 16.0;
        public double ListenBearingSigma = 12.0 * Angle.Deg2Rad;

        public int DecoyCharges = 2;
        public double DecoyThrowRange = 5.0;
        public double DecoyBurstPeriod = 2.0;
        public int DecoyBursts = 3;

        /// <summary>Squared-Mahalanobis gate for associating a measurement with a landmark (2 dof, 99%).</summary>
        public double AssociationGate = ChiSquare.Quantile(2, 0.99);

        /// <summary>If the second-best candidate is within this ratio of the best, the match is "ambiguous".</summary>
        public double AmbiguityRatio = 3.0;

        // ---------------------------------------------------------------- Acoustics (how loud you are)
        public double CreepLoudness = 1.2;       // audible radius in meters
        public double WalkLoudness = 4.5;
        public double SprintLoudness = 10.0;
        public double FootstepPeriod = 0.5;
        public double PingLoudness = 26.0;
        public double DecoyLoudness = 26.0;
        public double FailedDockLoudness = 6.0;
        public double WallMuffling = 0.45;       // audible radius multiplier without line of sight

        /// <summary>How precisely the Seeker localizes a sound: sigma = base + perMeter * distance.</summary>
        public double HearingSigmaBase = 0.4;
        public double HearingSigmaPerMeter = 0.12;

        // ---------------------------------------------------------------- Seeker
        public double SeekerSearchSpeed = 1.3;
        public double SeekerHuntSpeed = 2.1;
        public double SeekerTurnRate = 3.0;
        public double SeekerVisionRange = 7.0;
        public double SeekerFieldOfView = 110.0 * Angle.Deg2Rad;
        public double SeekerDarkVisionRange = 1.5;   // sees you in the dark only this close
        public double SeekerVisionSigma = 0.25;
        public double SeekerVisionPeriod = 0.2;
        public double CaptureRadius = 0.6;

        public int SeekerParticleCount = 600;
        /// <summary>How fast the Seeker assumes you could be moving; drives how quickly its cloud spreads.</summary>
        public double SeekerAssumedPlayerSpeed = 1.6;
        /// <summary>Weight multiplier for particles the Seeker can see but where it sees nothing (negative information).</summary>
        public double SeekerNegativeInfoWeight = 0.08;
        public double SeekerInjectionFraction = 0.01;
        /// <summary>Belief spread (meters, sqrt of position covariance trace) below which the Seeker switches to hunting.</summary>
        public double SeekerHuntSpread = 2.0;
        public double SeekerReplanPeriod = 0.5;
        public double SeekerScanAmplitude = 50.0 * Angle.Deg2Rad;

        // ---------------------------------------------------------------- Objective
        public double DockRadius = 0.6;
        /// <summary>Seconds between dock attempts, so a lost drone cannot spam the dock (and its noise).</summary>
        public double DockCooldown = 1.0;
        public double MaxEpisodeSeconds = 600.0;

        /// <summary>Initial uncertainty of the player's own belief.</summary>
        public double InitialPositionSigma = 0.1;
        public double InitialHeadingSigma = 2.0 * Angle.Deg2Rad;

        public static TuningParameters Default() => new TuningParameters();

        public TuningParameters Clone() => (TuningParameters)MemberwiseClone();

        public double SpeedFor(Robot.MovementMode mode)
        {
            switch (mode)
            {
                case Robot.MovementMode.Creep: return CreepSpeed;
                case Robot.MovementMode.Sprint: return SprintSpeed;
                default: return WalkSpeed;
            }
        }

        public double NoiseScaleFor(Robot.MovementMode mode)
        {
            switch (mode)
            {
                case Robot.MovementMode.Creep: return CreepNoiseScale;
                case Robot.MovementMode.Sprint: return SprintNoiseScale;
                default: return WalkNoiseScale;
            }
        }

        public double LoudnessFor(Robot.MovementMode mode)
        {
            switch (mode)
            {
                case Robot.MovementMode.Creep: return CreepLoudness;
                case Robot.MovementMode.Sprint: return SprintLoudness;
                default: return WalkLoudness;
            }
        }
    }
}
