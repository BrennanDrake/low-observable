namespace LowObservable.Core.Maths
{
    /// <summary>Angle helpers. All angles in the core are radians, counter-clockwise from +X.</summary>
    public static class Angle
    {
        public const double Pi = System.Math.PI;
        public const double TwoPi = 2.0 * System.Math.PI;
        public const double Deg2Rad = System.Math.PI / 180.0;
        public const double Rad2Deg = 180.0 / System.Math.PI;

        /// <summary>
        /// Wraps an angle into (-pi, pi]. Every bearing innovation must be wrapped, otherwise a
        /// measurement of 179 degrees against a prediction of -179 degrees looks like a 358 degree error
        /// and the filter diverges. This is one of the classic EKF bugs.
        /// </summary>
        public static double Wrap(double radians)
        {
            double a = System.Math.IEEERemainder(radians, TwoPi);
            if (a <= -Pi) a += TwoPi;
            if (a > Pi) a -= TwoPi;
            return a;
        }

        /// <summary>Smallest signed difference a - b, wrapped to (-pi, pi].</summary>
        public static double Difference(double a, double b) => Wrap(a - b);
    }
}
