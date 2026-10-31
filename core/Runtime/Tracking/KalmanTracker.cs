using LowObservable.Core.Maths;

namespace LowObservable.Core.Tracking
{
    /// <summary>
    /// KESTREL v3: a linear Kalman filter on the target's position and velocity, x = [px, py, vx, vy].
    ///
    ///   Predict:  x ← F x,  P ← F P Fᵀ + Q      (constant velocity with white-noise acceleration q)
    ///   Update:   y = z − H x,  S = H P Hᵀ + R,  K = P Hᵀ S⁻¹,  x ← x + K y,  P ← (I − K H) P
    ///   Gate:     reject z if yᵀ S⁻¹ y &gt; γ (Mahalanobis distance, γ = χ²₂ quantile)
    ///
    /// Strengths: smooth, cheap, predicts motion. Weaknesses the offices teach, all straight from the math:
    ///   - One Gaussian: two reports far apart get averaged, so units search the empty midpoint.
    ///   - The gate: once the ellipse is tight, a report outside it is thrown away as an outlier, even a true one.
    ///   - Constant velocity: break line of sight while running and turn; the estimate keeps going straight.
    ///   - No negative information: a linear-Gaussian filter has no way to use "looked and saw nothing".
    ///   - No walls: the ellipse happily covers rooms you can't reach from where you were.
    /// </summary>
    public sealed class KalmanTracker : IThreatTracker
    {
        private Matrix _x = new Matrix(4, 1);
        private Matrix _p = Matrix.Identity(4);
        private readonly Vec2 _worldCenter;
        private readonly double _worldRadius;

        /// <summary>Spectral density of the white-noise acceleration (m²/s³). Higher = expects jinkier targets.</summary>
        public double AccelerationNoise { get; set; } = 0.8;

        /// <summary>Velocity decays toward zero at this rate (1/s), so a lost estimate doesn't fly off forever.</summary>
        public double VelocityDamping { get; set; } = 0.25;

        /// <summary>χ²₂ gate on the squared Mahalanobis distance. 13.8 is the 99.9% quantile.</summary>
        public double Gate { get; set; } = 13.8;

        /// <summary>Position spread (m) above which the filter considers itself lost.</summary>
        public double LostSpread { get; set; } = 7.0;

        public int RejectedCount { get; private set; }
        public Vec2 LastRejected { get; private set; }

        /// <summary>Mahalanobis distance squared of the last reported fix (accepted or not).</summary>
        public double LastMahalanobis2 { get; private set; }

        public TrackerKind Kind => TrackerKind.Kalman;

        public KalmanTracker(Vec2 worldCenter, double worldRadius)
        {
            _worldCenter = worldCenter;
            _worldRadius = worldRadius;
            Reset();
        }

        public Vec2 Mean => new Vec2(_x[0, 0], _x[1, 0]);
        public Vec2 Velocity => new Vec2(_x[2, 0], _x[3, 0]);

        /// <summary>Top-left 2x2 block of P: the position covariance drawn as the ellipse.</summary>
        public Matrix PositionCovariance => new Matrix(2, 2, _p[0, 0], _p[0, 1], _p[1, 0], _p[1, 1]);

        public CovarianceEllipse Ellipse => CovarianceEllipse.FromCovariance(Mean, PositionCovariance);

        public bool HasEstimate => Spread < LostSpread;
        public Vec2 BestGuess => Mean;
        public double Spread => System.Math.Sqrt(System.Math.Max(_p[0, 0] + _p[1, 1], 0));

        public void Reset()
        {
            // "Somewhere in the building": mean at the centre, huge position variance, unknown velocity.
            _x = Matrix.ColumnVector(_worldCenter.X, _worldCenter.Y, 0, 0);
            double v = _worldRadius * _worldRadius;
            _p = Matrix.Diagonal(v, v, 1.0, 1.0);
        }

        public void ResetAround(Vec2 center, double sigma)
        {
            _x = Matrix.ColumnVector(center.X, center.Y, 0, 0);
            _p = Matrix.Diagonal(sigma * sigma, sigma * sigma, 1.0, 1.0);
        }

        public void Predict(double dt)
        {
            if (dt <= 0) return;
            double d = System.Math.Exp(-VelocityDamping * dt);
            var f = new Matrix(4, 4,
                1, 0, dt, 0,
                0, 1, 0, dt,
                0, 0, d, 0,
                0, 0, 0, d);
            double q = AccelerationNoise, dt2 = dt * dt, dt3 = dt2 * dt;
            // Discrete white-noise-acceleration process noise for one axis: q [dt³/3 dt²/2; dt²/2 dt].
            var qm = new Matrix(4, 4,
                q * dt3 / 3, 0, q * dt2 / 2, 0,
                0, q * dt3 / 3, 0, q * dt2 / 2,
                q * dt2 / 2, 0, q * dt, 0,
                0, q * dt2 / 2, 0, q * dt);
            _x = f * _x;
            _p = (f * _p * f.Transpose() + qm).Symmetrized();
            // Keep the "lost" state bounded so the ellipse doesn't grow to absurd sizes.
            double cap = _worldRadius * _worldRadius;
            if (_p[0, 0] > cap) _p[0, 0] = cap;
            if (_p[1, 1] > cap) _p[1, 1] = cap;
        }

        public FixResult ReportFix(Vec2 measured, double sigma, double outlierWeight)
        {
            var y = Matrix.ColumnVector(measured.X - _x[0, 0], measured.Y - _x[1, 0]);
            double r = sigma * sigma;
            var s = new Matrix(2, 2, _p[0, 0] + r, _p[0, 1], _p[1, 0], _p[1, 1] + r);
            var sInv = s.Inverse();
            double d2 = sInv.QuadraticForm(y);
            LastMahalanobis2 = d2;
            if (d2 > Gate)
            {
                RejectedCount++;
                LastRejected = measured;
                return FixResult.Rejected;
            }
            // K = P Hᵀ S⁻¹ with H = [I 0]: P Hᵀ is the first two columns of P.
            var pht = new Matrix(4, 2,
                _p[0, 0], _p[0, 1],
                _p[1, 0], _p[1, 1],
                _p[2, 0], _p[2, 1],
                _p[3, 0], _p[3, 1]);
            var k = pht * sInv;
            _x = _x + k * y;
            var kh = new Matrix(4, 4);
            for (int i = 0; i < 4; i++)
            {
                kh[i, 0] = k[i, 0];
                kh[i, 1] = k[i, 1];
            }
            _p = ((Matrix.Identity(4) - kh) * _p).Symmetrized();
            return FixResult.Accepted;
        }

        public void ReportMiss(SensorFootprint footprint, double missLikelihood)
        {
            // Deliberately nothing. A Kalman filter's measurement model is "z = Hx + noise"; there is no
            // measurement for "I didn't see it". This is one of KESTREL's documented weaknesses.
        }

        /// <summary>
        /// Gaussian probability inside a disc, by numerical integration on a small polar grid
        /// (8 rings x 16 spokes, midpoint rule). Accurate to a few percent, which is plenty for gameplay.
        /// </summary>
        public double MassWithin(Vec2 center, double radius)
        {
            var cov = PositionCovariance;
            double det = cov.Determinant();
            if (det <= 1e-12) return Vec2.Distance(center, Mean) <= radius ? 1.0 : 0.0;
            var inv = cov.Inverse();
            double norm = 1.0 / (2 * System.Math.PI * System.Math.Sqrt(det));
            const int rings = 8, spokes = 16;
            double mass = 0;
            for (int a = 0; a < rings; a++)
            {
                double r0 = radius * a / rings, r1 = radius * (a + 1) / rings, rm = 0.5 * (r0 + r1);
                double area = System.Math.PI * (r1 * r1 - r0 * r0) / spokes;
                for (int b = 0; b < spokes; b++)
                {
                    double th = (b + 0.5) * 2 * System.Math.PI / spokes;
                    double dx = center.X + rm * System.Math.Cos(th) - _x[0, 0];
                    double dy = center.Y + rm * System.Math.Sin(th) - _x[1, 0];
                    double q = inv[0, 0] * dx * dx + 2 * inv[0, 1] * dx * dy + inv[1, 1] * dy * dy;
                    mass += norm * System.Math.Exp(-0.5 * q) * area;
                }
            }
            return System.Math.Min(mass, 1.0);
        }
    }
}
