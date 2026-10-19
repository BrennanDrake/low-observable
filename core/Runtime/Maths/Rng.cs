using System;

namespace LowObservable.Core.Maths
{
    /// <summary>
    /// Deterministic random number generator (xoshiro256**, seeded with SplitMix64). The core never
    /// uses System.Random or UnityEngine.Random, because their sequences differ between runtimes and
    /// the game depends on seeded runs replaying identically (reveal screen, bug reports, daily seeds).
    /// </summary>
    public sealed class Rng
    {
        private ulong _s0, _s1, _s2, _s3;
        private bool _hasSpareGaussian;
        private double _spareGaussian;

        public Rng(ulong seed)
        {
            ulong x = seed;
            _s0 = SplitMix64(ref x);
            _s1 = SplitMix64(ref x);
            _s2 = SplitMix64(ref x);
            _s3 = SplitMix64(ref x);
        }

        /// <summary>Creates an independent stream derived from this seed and a label, e.g. "player-motion".</summary>
        public static Rng Derive(ulong seed, string stream)
        {
            ulong h = 1469598103934665603UL; // FNV-1a
            foreach (char ch in stream)
            {
                h ^= ch;
                h *= 1099511628211UL;
            }
            return new Rng(seed ^ h);
        }

        public ulong NextULong()
        {
            ulong result = RotL(_s1 * 5, 7) * 9;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotL(_s3, 45);
            return result;
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

        public double Range(double min, double max) => min + (max - min) * NextDouble();

        /// <summary>Uniform int in [0, maxExclusive).</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextDouble() * maxExclusive);
        }

        /// <summary>Standard normal sample (Marsaglia polar method).</summary>
        public double NextGaussian()
        {
            if (_hasSpareGaussian)
            {
                _hasSpareGaussian = false;
                return _spareGaussian;
            }
            double u, v, s;
            do
            {
                u = NextDouble() * 2.0 - 1.0;
                v = NextDouble() * 2.0 - 1.0;
                s = u * u + v * v;
            } while (s >= 1.0 || s == 0.0);
            double m = System.Math.Sqrt(-2.0 * System.Math.Log(s) / s);
            _spareGaussian = v * m;
            _hasSpareGaussian = true;
            return u * m;
        }

        public double Gaussian(double mean, double stdDev) => mean + stdDev * NextGaussian();

        /// <summary>Samples from N(mean, covariance) using a Cholesky factor.</summary>
        public Matrix SampleMultivariate(Matrix mean, Matrix covariance)
        {
            if (!covariance.TryCholesky(out var l))
                throw new InvalidOperationException("Covariance is not positive definite.");
            var z = new Matrix(mean.Rows, 1);
            for (int i = 0; i < mean.Rows; i++) z[i, 0] = NextGaussian();
            return mean + l * z;
        }

        private static ulong RotL(ulong x, int k) => (x << k) | (x >> (64 - k));

        private static ulong SplitMix64(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
