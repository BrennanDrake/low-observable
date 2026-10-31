using LowObservable.Core.Maths;

namespace LowObservable.Core.Simulation
{
    public enum EpisodeOutcome
    {
        Running,
        /// <summary>Docked at the uplink.</summary>
        Escaped,
        /// <summary>The Seeker reached the drone's true position.</summary>
        Captured,
        TimedOut,
    }

    public enum EpisodeEventType
    {
        PingFired,
        PingOnCooldown,
        CameraFrame,
        /// <summary>A measurement was matched to a landmark and fused into the belief.</summary>
        ObservationApplied,
        /// <summary>A measurement fell outside every landmark's gate and was discarded.</summary>
        ObservationRejected,
        /// <summary>Several landmarks could explain a measurement; waiting for the player or guessed.</summary>
        ObservationAmbiguous,
        ListenStarted,
        ListenHeardSeeker,
        ListenHeardNothing,
        DecoyThrown,
        DecoyBurst,
        NoDecoysLeft,
        Bump,
        DockFailed,
        Docked,
        SeekerHeard,
        SeekerSaw,
        SeekerModeChanged,
        Captured,
        TimedOut,
    }

    /// <summary>
    /// Something that happened during a tick. The UI uses these for effects and sounds; the run
    /// recorder logs them for the reveal timeline. Fields not relevant to a type are left default.
    /// </summary>
    public struct EpisodeEvent
    {
        public double Time;
        public EpisodeEventType Type;
        /// <summary>World position associated with the event (true position: only show it on the reveal screen if it is ground truth).</summary>
        public Vec2 Position;
        /// <summary>Type-specific number: NIS for observations, bearing for listen, mode for Seeker changes.</summary>
        public double Value;
        /// <summary>Landmark id for observation events, otherwise -1.</summary>
        public int LandmarkId;
        /// <summary>For ObservationApplied: whether the match was actually the right landmark (ground truth, reveal only).</summary>
        public bool AssociationCorrect;

        public override string ToString() => $"[{Time:0.00}s] {Type} {Position} v={Value:0.###} lm={LandmarkId}";
    }
}
