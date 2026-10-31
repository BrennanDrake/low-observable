using LowObservable.Core.Maths;
using LowObservable.Core.Robot;
using LowObservable.Core.Sensors;

namespace LowObservable.Core.Estimation
{
    /// <summary>Innovation of one measurement against the current belief.</summary>
    public readonly struct Innovation
    {
        /// <summary>Measurement minus prediction (range, wrapped bearing), as a 2x1 column.</summary>
        public readonly Matrix Residual;
        /// <summary>Innovation covariance S = H P H^T + R.</summary>
        public readonly Matrix Covariance;
        /// <summary>Measurement Jacobian H (2x3).</summary>
        public readonly Matrix Jacobian;
        /// <summary>Measurement noise R (2x2).</summary>
        public readonly Matrix Noise;
        /// <summary>Normalised innovation squared: residual^T S^-1 residual. ~chi-square(2) when everything is right.</summary>
        public readonly double Nis;

        public Innovation(Matrix residual, Matrix covariance, Matrix jacobian, Matrix noise, double nis)
        {
            Residual = residual;
            Covariance = covariance;
            Jacobian = jacobian;
            Noise = noise;
            Nis = nis;
        }
    }

    /// <summary>
    /// Extended Kalman filter over the drone pose (x, y, theta) against known landmark positions.
    /// This is the player's own belief, drawn as the blue ellipse.
    ///
    /// Predict: x = f(x, u),  P = G P G^T + V M V^T   (uncertainty grows when you move)
    /// Update:  K = P H^T S^-1, x = x + K nu, P = (I - K H) P (I - K H)^T + K R K^T   (shrinks when you sense)
    ///
    /// The Joseph-form covariance update is used because it stays symmetric positive definite
    /// even with a slightly wrong gain, which matters when players feed it bad associations.
    /// </summary>
    public sealed class EkfLocalizer
    {
        public Pose2 Mean { get; private set; }
        public Matrix Covariance { get; private set; }

        public EkfLocalizer(Pose2 initialMean, Matrix initialCovariance)
        {
            Mean = initialMean;
            Covariance = initialCovariance.Clone();
        }

        public static EkfLocalizer WithSigmas(Pose2 initialMean, double positionSigma, double headingSigma)
        {
            double p = positionSigma * positionSigma;
            return new EkfLocalizer(initialMean, Matrix.Diagonal(p, p, headingSigma * headingSigma));
        }

        /// <summary>Prediction step from odometry with the given control noise variances.</summary>
        public void Predict(OdometryDelta u, double distanceVariance, double rotationVariance)
        {
            if (u.IsZero) return;
            var g = OdometryModel.JacobianState(Mean, u);
            var v = OdometryModel.JacobianControl(Mean, u);
            var m = Matrix.Diagonal(distanceVariance, rotationVariance);
            Mean = OdometryModel.Apply(Mean, u);
            Covariance = (g * Covariance * g.Transpose() + v * m * v.Transpose()).Symmetrized();
        }

        /// <summary>Expected range and bearing to a landmark from the current mean.</summary>
        public void PredictMeasurement(Vec2 landmark, out double range, out double bearing)
        {
            double dx = landmark.X - Mean.X;
            double dy = landmark.Y - Mean.Y;
            range = System.Math.Sqrt(dx * dx + dy * dy);
            bearing = Angle.Wrap(System.Math.Atan2(dy, dx) - Mean.Theta);
        }

        /// <summary>Computes the innovation of an observation if it came from the given landmark.</summary>
        public Innovation ComputeInnovation(LandmarkObservation obs, Vec2 landmark)
        {
            double dx = landmark.X - Mean.X;
            double dy = landmark.Y - Mean.Y;
            double q = System.Math.Max(dx * dx + dy * dy, 1e-9);
            double r = System.Math.Sqrt(q);
            double predictedBearing = Angle.Wrap(System.Math.Atan2(dy, dx) - Mean.Theta);

            var h = new Matrix(2, 3,
                -dx / r, -dy / r, 0,
                dy / q, -dx / q, -1);
            var noise = Matrix.Diagonal(obs.RangeSigma * obs.RangeSigma, obs.BearingSigma * obs.BearingSigma);
            var s = (h * Covariance * h.Transpose() + noise).Symmetrized();
            var residual = Matrix.ColumnVector(obs.Range - r, Angle.Wrap(obs.Bearing - predictedBearing));
            double nis = s.Inverse().QuadraticForm(residual);
            return new Innovation(residual, s, h, noise, nis);
        }

        /// <summary>Applies the update for an innovation computed against the current state.</summary>
        public void Update(Innovation innovation)
        {
            var p = Covariance;
            var h = innovation.Jacobian;
            var k = p * h.Transpose() * innovation.Covariance.Inverse();
            var dx = k * innovation.Residual;
            Mean = new Pose2(Mean.X + dx[0, 0], Mean.Y + dx[1, 0], Mean.Theta + dx[2, 0]);
            var ikh = Matrix.Identity(3) - k * h;
            Covariance = (ikh * p * ikh.Transpose() + k * innovation.Noise * k.Transpose()).Symmetrized();
        }

        /// <summary>Convenience: innovation plus update in one call. Returns the NIS of the applied measurement.</summary>
        public double Update(LandmarkObservation obs, Vec2 landmark)
        {
            var inn = ComputeInnovation(obs, landmark);
            Update(inn);
            return inn.Nis;
        }

        /// <summary>The 2x2 position block of the covariance, as an ellipse for drawing.</summary>
        public CovarianceEllipse PositionEllipse() => CovarianceEllipse.FromCovariance(Mean.Position, Covariance);

        /// <summary>One-sigma heading uncertainty in radians.</summary>
        public double HeadingSigma => System.Math.Sqrt(System.Math.Max(Covariance[2, 2], 0));

        /// <summary>Scalar "how lost am I": sqrt of the position variance trace, in meters.</summary>
        public double PositionSpread => System.Math.Sqrt(System.Math.Max(Covariance[0, 0] + Covariance[1, 1], 0));

        /// <summary>Overwrites the belief. Used for scripted events (respawn) and tests.</summary>
        public void Reset(Pose2 mean, Matrix covariance)
        {
            Mean = mean;
            Covariance = covariance.Clone();
        }
    }
}
