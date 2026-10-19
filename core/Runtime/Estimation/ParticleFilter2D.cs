using System;
using System.Collections.Generic;
using LowObservable.Core.Maths;
using LowObservable.Core.World;

namespace LowObservable.Core.Estimation
{
    /// <summary>
    /// Sequential importance resampling (SIR) particle filter over a 2D position on a floor plan.
    /// The Seeker uses it to track the player; it is the red cloud on screen.
    ///
    /// Each particle is one hypothesis "the drone is here". Predict diffuses particles (the Seeker
    /// does not know how you moved), updates multiply weights by the likelihood of what the Seeker
    /// sensed (or did not sense), and resampling clones likely hypotheses and drops unlikely ones.
    /// Unlike a Kalman filter this can hold several separate blobs, which is why decoys can split it.
    /// </summary>
    public sealed class ParticleFilter2D
    {
        private readonly FloorPlan _plan;
        private readonly Rng _rng;
        private readonly List<(int i, int j)> _freeCells;
        private Vec2[] _positions;
        private double[] _weights;
        private Vec2[] _scratch;

        public int Count => _positions.Length;
        public IReadOnlyList<Vec2> Positions => _positions;
        public IReadOnlyList<double> Weights => _weights;

        /// <summary>Particles closer than this to a wall are treated as inside it.</summary>
        public double ParticleRadius { get; set; } = 0.15;

        public ParticleFilter2D(FloorPlan plan, int count, Rng rng)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            _plan = plan;
            _rng = rng;
            _freeCells = plan.FreeCells();
            _positions = new Vec2[count];
            _weights = new double[count];
            _scratch = new Vec2[count];
        }

        /// <summary>Spreads particles uniformly over the free floor: "I have no idea where you are."</summary>
        public void InitializeUniform()
        {
            for (int k = 0; k < Count; k++) _positions[k] = SampleFreePoint();
            SetUniformWeights();
        }

        /// <summary>Gaussian blob around a point, e.g. where the Seeker first heard you.</summary>
        public void InitializeGaussian(Vec2 center, double sigma)
        {
            for (int k = 0; k < Count; k++)
            {
                Vec2 p = center;
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    var candidate = new Vec2(_rng.Gaussian(center.X, sigma), _rng.Gaussian(center.Y, sigma));
                    if (_plan.IsFree(candidate, ParticleRadius)) { p = candidate; break; }
                }
                _positions[k] = p;
            }
            SetUniformWeights();
        }

        /// <summary>
        /// Prediction: each particle takes a random step. With no motion model for the player, a
        /// random walk whose spread grows like speed * sqrt(dt) is the honest assumption. Steps into walls are rejected.
        /// </summary>
        public void PredictRandomWalk(double dt, double assumedSpeed)
        {
            double sigma = assumedSpeed * System.Math.Sqrt(dt) * 0.8;
            for (int k = 0; k < Count; k++)
            {
                var p = _positions[k];
                var next = new Vec2(p.X + _rng.NextGaussian() * sigma, p.Y + _rng.NextGaussian() * sigma);
                if (_plan.IsFree(next, ParticleRadius) && _plan.LineOfSight(p, next)) _positions[k] = next;
            }
        }

        /// <summary>Measurement update with an arbitrary likelihood p(z | particle position).</summary>
        public void Update(Func<Vec2, double> likelihood)
        {
            double sum = 0;
            for (int k = 0; k < Count; k++)
            {
                _weights[k] *= likelihood(_positions[k]);
                sum += _weights[k];
            }
            NormalizeOrReset(sum);
        }

        /// <summary>
        /// Update from a noisy position fix (a heard sound or a sighting): isotropic Gaussian likelihood,
        /// mixed with a small uniform floor so one wrong fix cannot kill every particle.
        /// </summary>
        public void UpdatePositionFix(Vec2 measured, double sigma, double outlierWeight = 0.02)
        {
            double inv2s2 = 1.0 / (2.0 * sigma * sigma);
            Update(p =>
            {
                double d2 = (p - measured).LengthSquared;
                return (1.0 - outlierWeight) * System.Math.Exp(-d2 * inv2s2) + outlierWeight;
            });
        }

        /// <summary>Effective sample size 1 / sum(w^2). Near Count: diverse. Near 1: one particle carries everything.</summary>
        public double EffectiveSampleSize()
        {
            double s = 0;
            for (int k = 0; k < Count; k++) s += _weights[k] * _weights[k];
            return s > 0 ? 1.0 / s : 0;
        }

        /// <summary>
        /// Low-variance (systematic) resampling: one random offset, then evenly spaced picks through
        /// the cumulative weights. Cheaper and less noisy than drawing each particle independently.
        /// </summary>
        public void Resample()
        {
            int n = Count;
            double step = 1.0 / n;
            double u = _rng.NextDouble() * step;
            double c = _weights[0];
            int i = 0;
            for (int m = 0; m < n; m++)
            {
                double target = u + m * step;
                while (target > c && i < n - 1)
                {
                    i++;
                    c += _weights[i];
                }
                _scratch[m] = _positions[i];
            }
            var t = _positions;
            _positions = _scratch;
            _scratch = t;
            SetUniformWeights();
        }

        /// <summary>Resample only when the effective sample size drops below a fraction of Count.</summary>
        public bool ResampleIfNeeded(double essFraction = 0.5)
        {
            if (EffectiveSampleSize() >= essFraction * Count) return false;
            Resample();
            return true;
        }

        /// <summary>
        /// Replaces a fraction of particles with uniform samples. This is the fix for particle
        /// deprivation: without it, a filter that has collapsed on the wrong spot can never recover.
        /// </summary>
        public void InjectUniform(double fraction)
        {
            int n = (int)System.Math.Round(Count * fraction);
            double w = 1.0 / Count;
            for (int k = 0; k < n; k++)
            {
                int idx = _rng.NextInt(Count);
                _positions[idx] = SampleFreePoint();
                _weights[idx] = w;
            }
            double sum = 0;
            for (int k = 0; k < Count; k++) sum += _weights[k];
            NormalizeOrReset(sum);
        }

        public Vec2 Mean()
        {
            double x = 0, y = 0;
            for (int k = 0; k < Count; k++) { x += _weights[k] * _positions[k].X; y += _weights[k] * _positions[k].Y; }
            return new Vec2(x, y);
        }

        /// <summary>Weighted 2x2 covariance of the particle cloud.</summary>
        public Matrix CovarianceMatrix()
        {
            var m = Mean();
            double xx = 0, xy = 0, yy = 0;
            for (int k = 0; k < Count; k++)
            {
                double dx = _positions[k].X - m.X, dy = _positions[k].Y - m.Y;
                xx += _weights[k] * dx * dx;
                xy += _weights[k] * dx * dy;
                yy += _weights[k] * dy * dy;
            }
            return new Matrix(2, 2, xx, xy, xy, yy);
        }

        /// <summary>Scalar spread in meters (sqrt of covariance trace). Low = the Seeker is sure where you are.</summary>
        public double Spread()
        {
            var c = CovarianceMatrix();
            return System.Math.Sqrt(System.Math.Max(c[0, 0] + c[1, 1], 0));
        }

        /// <summary>
        /// Center of the most probable grid cell. Better than the mean for a multi-modal cloud: the mean
        /// of two blobs is often in a wall between them, which is exactly where a decoy wants the Seeker to look.
        /// </summary>
        public Vec2 Mode()
        {
            var mass = new Dictionary<int, double>();
            var sumX = new Dictionary<int, double>();
            var sumY = new Dictionary<int, double>();
            int bestKey = 0;
            double best = -1;
            for (int k = 0; k < Count; k++)
            {
                _plan.WorldToCell(_positions[k], out int i, out int j);
                int key = i * _plan.Height + j;
                mass.TryGetValue(key, out double m);
                sumX.TryGetValue(key, out double sx);
                sumY.TryGetValue(key, out double sy);
                m += _weights[k];
                mass[key] = m;
                sumX[key] = sx + _weights[k] * _positions[k].X;
                sumY[key] = sy + _weights[k] * _positions[k].Y;
                if (m > best) { best = m; bestKey = key; }
            }
            return best > 0 ? new Vec2(sumX[bestKey] / best, sumY[bestKey] / best) : Mean();
        }

        /// <summary>Probability mass within a radius of a point; useful for "how sure is it that I'm here".</summary>
        public double MassWithin(Vec2 center, double radius)
        {
            double r2 = radius * radius, m = 0;
            for (int k = 0; k < Count; k++) if ((_positions[k] - center).LengthSquared <= r2) m += _weights[k];
            return m;
        }

        private void SetUniformWeights()
        {
            double w = 1.0 / Count;
            for (int k = 0; k < Count; k++) _weights[k] = w;
        }

        private void NormalizeOrReset(double sum)
        {
            if (sum <= 1e-300 || double.IsNaN(sum))
            {
                // Every hypothesis was ruled out: the Seeker's model was wrong. Start over rather than divide by zero.
                InitializeUniform();
                return;
            }
            for (int k = 0; k < Count; k++) _weights[k] /= sum;
        }

        private Vec2 SampleFreePoint()
        {
            var (i, j) = _freeCells[_rng.NextInt(_freeCells.Count)];
            double s = _plan.CellSize;
            return new Vec2((i + _rng.NextDouble()) * s, (j + _rng.NextDouble()) * s);
        }
    }
}
