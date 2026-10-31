using LowObservable.Core.Maths;

namespace LowObservable.Core.Stages
{
    public enum StageEventType
    {
        Footstep,
        Noise,
        Sighted,
        Heard,
        Bump,
        LaserTripped,
        ReportRejected,
        Captured,
        CheckpointReached,
        NoteRead,
        ToolUnlocked,
        KeycardTaken,
        DoorOpened,
        TerminalProgress,
        TerminalDone,
        LightsToggled,
        Hid,
        Unhid,
        PickedUp,
        ThrowLanded,
        NoisemakerAttached,
        JammerPlaced,
        MirrorPlaced,
        MirrorTaken,
        Forged,
        GuardBark,
        Exploit,
        Escaped,
        Denied,
    }

    /// <summary>
    /// Something that happened this tick, for audio, barks, the HUD and the replay. Views read
    /// <see cref="Stage.Events"/> after each step; the list is cleared at the start of the next step.
    /// </summary>
    public struct StageEvent
    {
        public StageEventType Type;
        public double Time;
        public Vec2 Position;
        /// <summary>Free text: a bark, a note, a tool id, an exploit id.</summary>
        public string Text;
        /// <summary>Index of the unit involved, or -1.</summary>
        public int Unit;
        /// <summary>Audible radius for noises.</summary>
        public double Radius;
        /// <summary>Machine-readable detail (an exploit id, a tool id, a report origin).</summary>
        public string Tag;

        public override string ToString() => $"{Time:0.00} {Type} {Position} {Text}";
    }
}
