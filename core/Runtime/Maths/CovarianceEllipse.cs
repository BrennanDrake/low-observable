namespace LowObservable.Core.Maths
{
    /// <summary>
    /// Geometry of the uncertainty ellipse for a 2x2 position covariance. This is what the player
    /// actually sees: the blue ellipse around the drone is drawn from these numbers.
    /// </summary>
    public readonly struct CovarianceEllipse
    {
        /// <summary>Center of the ellipse (the mean).</summary>
        public readonly Vec2 Center;

        /// <summary>One-sigma semi-axis lengths, major first.</summary>
        public readonly double SemiMajor;
        public readonly double SemiMinor;

        /// <summary>Angle of the major axis in radians, counter-clockwise from +X.</summary>
        public readonly double Orientation;

        public CovarianceEllipse(Vec2 center, double semiMajor, double semiMinor, double orientation)
        {
            Center = center;
            SemiMajor = semiMajor;
            SemiMinor = semiMinor;
            Orientation = orientation;
        }

        /// <summary>
        /// Builds the ellipse from the top-left 2x2 block of a covariance. Uses the closed-form
        /// eigen-decomposition of a symmetric 2x2 matrix.
        /// </summary>
        public static CovarianceEllipse FromCovariance(Vec2 mean, Matrix covariance)
        {
            double a = covariance[0, 0];
            double b = covariance[0, 1];
            double c = covariance[1, 1];
            return FromComponents(mean, a, b, c);
        }

        public static CovarianceEllipse FromComponents(Vec2 mean, double varX, double covXY, double varY)
        {
            double half = 0.5 * (varX + varY);
            double diff = 0.5 * (varX - varY);
            double root = System.Math.Sqrt(diff * diff + covXY * covXY);
            double l1 = System.Math.Max(half + root, 0.0);
            double l2 = System.Math.Max(half - root, 0.0);
            double orientation = 0.5 * System.Math.Atan2(2.0 * covXY, varX - varY);
            return new CovarianceEllipse(mean, System.Math.Sqrt(l1), System.Math.Sqrt(l2), orientation);
        }

        /// <summary>
        /// Scale factor for a confidence region in 2D. A 2D Gaussian puts 39% of its mass inside the
        /// 1-sigma ellipse, 86% inside 2 sigma, 99% inside 3.03 sigma.
        /// </summary>
        public static double SigmaForConfidence(double probability)
        {
            // For 2 dof the chi-square CDF is 1 - exp(-x/2), so the radius is sqrt(-2 ln(1-p)).
            return System.Math.Sqrt(-2.0 * System.Math.Log(1.0 - probability));
        }

        /// <summary>Point on the n-sigma ellipse at parameter t in [0, 2pi). Handy for line renderers.</summary>
        public Vec2 PointAt(double t, double sigmas = 1.0)
        {
            var local = new Vec2(System.Math.Cos(t) * SemiMajor * sigmas, System.Math.Sin(t) * SemiMinor * sigmas);
            return Center + local.Rotated(Orientation);
        }

        /// <summary>Area of the 1-sigma ellipse in square meters; a quick scalar "how lost am I".</summary>
        public double Area => System.Math.PI * SemiMajor * SemiMinor;
    }
}
