using LowObservable.Core.Maths;

namespace LowObservable.Core.Estimation
{
    /// <summary>
    /// Filter consistency metrics. A filter is consistent when its errors are about as big as its
    /// covariance claims. The reveal screen scores this, so being honestly unsure beats being
    /// confidently wrong.
    /// </summary>
    public static class Consistency
    {
        /// <summary>
        /// Normalised estimation error squared for the pose: e^T P^-1 e with e = truth - estimate
        /// (heading wrapped). For a consistent 3-state filter its average is about 3.
        /// </summary>
        public static double PoseNees(Pose2 truth, Pose2 estimate, Matrix covariance)
        {
            var e = Matrix.ColumnVector(truth.X - estimate.X, truth.Y - estimate.Y, Angle.Wrap(truth.Theta - estimate.Theta));
            return covariance.Inverse().QuadraticForm(e);
        }

        /// <summary>Position-only NEES (2 dof): what the player sees as "is the dot inside my ellipse".</summary>
        public static double PositionNees(Vec2 truth, Vec2 estimate, Matrix covariance)
        {
            var p = new Matrix(2, 2, covariance[0, 0], covariance[0, 1], covariance[1, 0], covariance[1, 1]);
            var e = Matrix.ColumnVector(truth.X - estimate.X, truth.Y - estimate.Y);
            return p.Inverse().QuadraticForm(e);
        }

        /// <summary>
        /// Two-sided acceptance interval for the average of n independent NEES samples with k dof
        /// (n * average ~ chi-square(n k)). Returns bounds on the average.
        /// </summary>
        public static void AverageNeesBounds(int dof, int samples, double confidence, out double lower, out double upper)
        {
            double alpha = 1.0 - confidence;
            int n = dof * samples;
            lower = ChiSquare.Quantile(n, alpha / 2.0) / samples;
            upper = ChiSquare.Quantile(n, 1.0 - alpha / 2.0) / samples;
        }
    }

    /// <summary>Running tally used by the reveal screen's score.</summary>
    public sealed class ConsistencyTracker
    {
        private readonly double _gate;

        public int Samples { get; private set; }
        public int InsideGate { get; private set; }
        public double NeesSum { get; private set; }
        public double ErrorSum { get; private set; }
        public double MaxError { get; private set; }

        /// <param name="gate">Position NEES gate for "truth was inside my ellipse". Default: 95% for 2 dof.</param>
        public ConsistencyTracker(double gate = 5.991)
        {
            _gate = gate;
        }

        public void Add(Vec2 truth, Vec2 estimate, Matrix covariance)
        {
            double nees = Consistency.PositionNees(truth, estimate, covariance);
            double err = Vec2.Distance(truth, estimate);
            Samples++;
            NeesSum += nees;
            ErrorSum += err;
            if (err > MaxError) MaxError = err;
            if (nees <= _gate) InsideGate++;
        }

        public double AverageNees => Samples == 0 ? 0 : NeesSum / Samples;
        public double AverageError => Samples == 0 ? 0 : ErrorSum / Samples;
        /// <summary>Fraction of time the truth was inside the 95% ellipse. A consistent filter scores about 0.95.</summary>
        public double FractionInside => Samples == 0 ? 0 : (double)InsideGate / Samples;
    }
}
