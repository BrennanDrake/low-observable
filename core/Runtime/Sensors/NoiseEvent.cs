using LowObservable.Core.Maths;

namespace LowObservable.Core.Sensors
{
    public enum NoiseSource
    {
        Footstep,
        Ping,
        Decoy,
        FailedDock,
        Bump,
    }

    /// <summary>
    /// A sound in the world. The Seeker hears it if it is within <see cref="Loudness"/> meters
    /// (less through walls), and then gets a noisy fix on where it came from. A decoy's sound is
    /// deliberately identical to a ping's: the Seeker cannot tell them apart, so it splits its belief.
    /// </summary>
    public struct NoiseEvent
    {
        public double Time;
        public Vec2 Position;
        /// <summary>Audible radius in meters with clear line of sight.</summary>
        public double Loudness;
        public NoiseSource Source;
    }
}
