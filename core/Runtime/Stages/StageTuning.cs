using LowObservable.Core.Maths;

namespace LowObservable.Core.Stages
{
    /// <summary>
    /// Every number that shapes the escape game's feel. SI units (meters, seconds, radians). Kept apart
    /// from <see cref="Simulation.TuningParameters"/>, which belongs to the original drone mode.
    /// See docs/tuning.md (escape section) for what each group does.
    /// </summary>
    [System.Serializable]
    public sealed class StageTuning
    {
        // ---------------------------------------------------------------- Player
        public double SneakSpeed = 1.3;
        public double WalkSpeed = 2.6;
        public double RunSpeed = 4.6;
        /// <summary>Seconds of running on a full tank. Running is for bursts, not for crossing a level.</summary>
        public double StaminaSeconds = 2.5;
        /// <summary>Stamina regained per second while not running.</summary>
        public double StaminaRecovery = 0.5;
        public double PlayerRadius = 0.3;
        /// <summary>A footstep sound every this many meters travelled.</summary>
        public double StrideLength = 0.9;
        /// <summary>Audible radius (m) of one footstep per mode, with clear line of sight.</summary>
        public double SneakLoudness = 0.8;
        public double WalkLoudness = 3.5;
        public double RunLoudness = 9.0;
        public double InteractRange = 1.3;
        public double HideLoudness = 1.5;
        public int CarryLimit = 3;

        // ---------------------------------------------------------------- Objects in the world
        public double ThrowRange = 8.0;
        public double ThrowSpeed = 14.0;
        public double ThrowLoudness = 7.0;
        public double DoorLoudness = 5.0;
        public double SwitchLoudness = 2.5;
        public double TerminalUseSeconds = 2.5;
        public double TerminalBeepPeriod = 0.5;
        public double TerminalBeepLoudness = 6.0;

        // ---------------------------------------------------------------- Tools
        /// <summary>Noisemaker bursts are as loud as running footsteps, so sensors can't tell them apart.</summary>
        public double NoisemakerLoudness = 9.0;
        public int NoisemakerBursts = 4;
        public double NoisemakerBurstPeriod = 1.2;
        public int NoisemakerCharges = 2;
        public double NoisemakerStickRadius = 0.8;
        public double JammerRadius = 4.0;
        public double JammerDuration = 20.0;
        /// <summary>WARDEN notices interference: a weak fix on the jammer every this many seconds.</summary>
        public double JammerInterferencePeriod = 3.0;
        public double JammerInterferenceSigma = 3.0;
        public int JammerCharges = 1;
        public double MirrorLength = 1.2;
        public double MirrorPlaceRange = 2.0;
        public int MirrorCharges = 2;
        public double ForgeRange = 14.0;
        public double ForgeSigma = 0.3;

        // ---------------------------------------------------------------- Sensors
        public double CameraRange = 8.0;
        public double CameraFieldOfView = 70 * Angle.Deg2Rad;
        public double CameraPeriod = 0.2;
        public double SwivelPeriod = 6.0;
        /// <summary>Any vision sensor sees into dark cells only this close.</summary>
        public double DarkVisionRange = 1.5;
        public double VisionSigma = 0.3;
        /// <summary>Weight kept by belief a sensor could see but didn't find the player in (negative information).</summary>
        public double MissLikelihood = 0.08;
        /// <summary>Microphone sensitivity: multiplier on a sound's audible radius.</summary>
        public double MicRadius = 1.3;
        /// <summary>Patrol units' hearing sensitivity (same scale as MicRadius).</summary>
        public double UnitHearing = 1.0;
        public double HearingSigmaBase = 0.6;
        public double HearingSigmaPerMeter = 0.15;
        public double WallMuffling = 0.45;
        public double RoombaSpeed = 0.9;
        public double RoombaBumpRange = 0.8;
        public double RoombaSigma = 0.8;
        public double RoombaCooldown = 3.0;
        public double LaserSigma = 0.3;

        // ---------------------------------------------------------------- Patrol units
        public double PatrolSpeed = 1.6;
        public double InvestigateSpeed = 2.6;
        public double ChaseSpeed = 3.8;
        public double GuardTurnRate = 5.0;
        public double GuardVisionRange = 7.5;
        public double GuardFieldOfView = 100 * Angle.Deg2Rad;
        public double GuardVisionPeriod = 0.2;
        public double CaptureRadius = 0.7;
        public double SearchDuration = 6.0;
        public double ChaseMemory = 1.5;
        public double LockerCheckSeconds = 1.2;

        // ---------------------------------------------------------------- WARDEN
        public double DispatchPeriod = 0.5;
        public double DispatchRadius = 3.0;
        /// <summary>Belief mass within DispatchRadius of the best guess needed to send a unit.</summary>
        public double DispatchConfidence = 0.3;
        /// <summary>Above this, every unit converges.</summary>
        public double AllUnitsConfidence = 0.75;
        public double LockerCheckMass = 0.25;

        /// <summary>
        /// A patrol only answers a dispatch to a point within this distance of itself (meters). Hunters have no
        /// leash. Keeps patrols as local obstacles to read and lure, and makes hunters the units that chase the belief.
        /// </summary>
        public double PatrolLeash = 8.0;
        public double TrackerAssumedSpeed = 1.6;
        public int ParticleCount = 500;
        public double ParticleInjectionPerSecond = 0.02;
        public double KalmanAccelerationNoise = 0.8;
        public double KalmanGate = 13.8;
        /// <summary>Probability a sighting or a heard sound is junk (outlier floor for trackers that model it).</summary>
        public double SensorOutlierWeight = 0.01;
        public double ForgeOutlierWeight = 0.00001;

        // ---------------------------------------------------------------- Scoring
        public double ExposureRadius = 1.5;
        public double SampleInterval = 0.25;

        public static StageTuning Default() => new StageTuning();
    }
}
