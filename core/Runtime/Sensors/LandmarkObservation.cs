namespace LowObservable.Core.Sensors
{
    public enum SensorKind
    {
        Ping,
        Camera,
    }

    /// <summary>
    /// A range-and-bearing measurement of some landmark, in the drone's own frame. Crucially it does
    /// <b>not</b> say which landmark it was: deciding that is data association, and getting it wrong is
    /// one of the main ways to lose. <see cref="TrueLandmarkId"/> exists only for the reveal screen and tests.
    /// </summary>
    public struct LandmarkObservation
    {
        /// <summary>Unique id for this observation within an episode.</summary>
        public int Id;
        public SensorKind Kind;
        public double Time;

        /// <summary>Distance to the landmark in meters.</summary>
        public double Range;
        /// <summary>Bearing in the drone frame, radians, counter-clockwise from the drone's forward axis.</summary>
        public double Bearing;

        public double RangeSigma;
        public double BearingSigma;

        /// <summary>Appearance class seen by the camera, or -1 when the sensor cannot tell (ping).</summary>
        public int Appearance;

        /// <summary>Ground truth; never use it for gameplay decisions. Reveal screen and tests only.</summary>
        public int TrueLandmarkId;
    }
}
