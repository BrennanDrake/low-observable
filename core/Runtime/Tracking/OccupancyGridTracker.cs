using LowObservable.Core.Maths;
using LowObservable.Core.World;

namespace LowObservable.Core.Tracking
{
    /// <summary>
    /// HEATMAP v2: a histogram (grid) Bayes filter, the same idea as Damián Isla's occupancy maps and
    /// robotics occupancy grids. Each free floor cell holds P(target in cell).
    ///
    ///   Predict: probability diffuses to the 4 neighbours (walls reflect it back), so the heat map
    ///            spreads at roughly the target's assumed speed.
    ///   Update:  p(cell) ∝ p(cell) · p(z | cell), with a Gaussian likelihood around a fix plus an outlier floor.
    ///   Miss:    cells a sensor could see are multiplied by the miss likelihood (negative information).
    ///
    /// It can hold several hot spots at once, and it does learn from absence, which is why hiding in a
    /// room it has just swept is the zone's signature trick. Darkness is the other: a camera that can't
    /// see a dark cell can't clear it, so belief seeps in unopposed.
    /// </summary>
    public sealed class OccupancyGridTracker : IThreatTracker
    {
        private readonly FloorPlan _plan;
        private readonly bool[] _free;
        private double[] _p;
        private double[] _scratch;
        private int _freeCount;
        private double _accum;

        /// <summary>Diffusion is applied at this period (seconds).</summary>
        public const double PredictPeriod = 0.2;

        /// <summary>Target speed the diffusion is matched to (m/s).</summary>
        public double AssumedSpeed { get; set; } = 1.6;

        /// <summary>Below this peak probability per cell, the tracker considers itself lost.</summary>
        public double LostThreshold { get; set; } = 0.012;

        public TrackerKind Kind => TrackerKind.OccupancyGrid;
        public int Width => _plan.Width;
        public int Height => _plan.Height;

        public OccupancyGridTracker(FloorPlan plan)
        {
            _plan = plan;
            int n = plan.Width * plan.Height;
            _free = new bool[n];
            _p = new double[n];
            _scratch = new double[n];
            for (int i = 0; i < plan.Width; i++)
                for (int j = 0; j < plan.Height; j++)
                    if (!plan.IsWall(i, j)) { _free[Index(i, j)] = true; _freeCount++; }
            Reset();
        }

        /// <summary>
        /// Re-reads which cells are walls, after a door opens or closes. New cells start empty; probability
        /// in cells that became walls is dropped.
        /// </summary>
        public void RefreshFreeCells()
        {
            _freeCount = 0;
            double sum = 0;
            for (int i = 0; i < _plan.Width; i++)
                for (int j = 0; j < _plan.Height; j++)
                {
                    int k = Index(i, j);
                    _free[k] = !_plan.IsWall(i, j);
                    if (_free[k]) _freeCount++;
                    else _p[k] = 0;
                    sum += _p[k];
                }
            Normalize(sum);
        }

        /// <summary>Probability that the target is in cell (i, j).</summary>
        public double Probability(int i, int j) => _plan.InBounds(i, j) ? _p[Index(i, j)] : 0.0;

        public double MaxProbability
        {
            get
            {
                double m = 0;
                for (int k = 0; k < _p.Length; k++) if (_p[k] > m) m = _p[k];
                return m;
            }
        }

        public bool HasEstimate => MaxProbability >= LostThreshold;

        public Vec2 BestGuess
        {
            get
            {
                // Peak of a 3x3 box-smoothed map: a single noisy cell shouldn't steer units.
                double best = -1;
                int bi = 0, bj = 0;
                for (int i = 0; i < _plan.Width; i++)
                {
                    for (int j = 0; j < _plan.Height; j++)
                    {
                        if (!_free[Index(i, j)]) continue;
                        double s = 0;
                        for (int di = -1; di <= 1; di++)
                            for (int dj = -1; dj <= 1; dj++)
                                s += Probability(i + di, j + dj) * (di == 0 && dj == 0 ? 2.0 : 1.0);
                        if (s > best) { best = s; bi = i; bj = j; }
                    }
                }
                return _plan.CellCenter(bi, bj);
            }
        }

        public double Spread
        {
            get
            {
                double mx = 0, my = 0;
                ForEachFree((i, j, p) => { var c = _plan.CellCenter(i, j); mx += p * c.X; my += p * c.Y; });
                double v = 0;
                ForEachFree((i, j, p) => { var c = _plan.CellCenter(i, j); v += p * ((c.X - mx) * (c.X - mx) + (c.Y - my) * (c.Y - my)); });
                return System.Math.Sqrt(System.Math.Max(v, 0));
            }
        }

        public void Reset()
        {
            for (int k = 0; k < _p.Length; k++) _p[k] = _free[k] ? 1.0 / _freeCount : 0.0;
        }

        public void ResetAround(Vec2 center, double sigma)
        {
            double inv = 1.0 / (2 * sigma * sigma), sum = 0;
            for (int i = 0; i < _plan.Width; i++)
            {
                for (int j = 0; j < _plan.Height; j++)
                {
                    int k = Index(i, j);
                    if (!_free[k]) { _p[k] = 0; continue; }
                    _p[k] = System.Math.Exp(-(_plan.CellCenter(i, j) - center).LengthSquared * inv) + 1e-9;
                    sum += _p[k];
                }
            }
            Normalize(sum);
        }

        public void Predict(double dt)
        {
            _accum += dt;
            if (_accum < PredictPeriod) return;
            double step = _accum;
            _accum = 0;

            // Match a random walk: per-axis variance grows by (0.8 v)^2 dt. Moving a fraction a of each cell's
            // mass to its 4 neighbours adds a/2 cells^2 of variance per axis, so a = 2 (0.8 v)^2 dt / cell^2.
            double sigmaCells = 0.8 * AssumedSpeed / _plan.CellSize;
            double a = 2.0 * sigmaCells * sigmaCells * step;
            int sub = 1;
            while (a / sub > 0.8) sub++;
            double frac = a / sub;
            for (int s = 0; s < sub; s++) Diffuse(frac);
        }

        private void Diffuse(double a)
        {
            System.Array.Clear(_scratch, 0, _scratch.Length);
            int w = _plan.Width, h = _plan.Height;
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    int k = Index(i, j);
                    if (!_free[k]) continue;
                    double m = _p[k];
                    if (m <= 0) continue;
                    double give = m * a * 0.25;
                    double keep = m - 4 * give;
                    // Mass headed into a wall stays put: walls reflect.
                    keep += Push(i + 1, j, give);
                    keep += Push(i - 1, j, give);
                    keep += Push(i, j + 1, give);
                    keep += Push(i, j - 1, give);
                    _scratch[k] += keep;
                }
            }
            var t = _p;
            _p = _scratch;
            _scratch = t;
        }

        private double Push(int i, int j, double amount)
        {
            if (!_plan.InBounds(i, j)) return amount;
            int k = Index(i, j);
            if (!_free[k]) return amount;
            _scratch[k] += amount;
            return 0;
        }

        public FixResult ReportFix(Vec2 measured, double sigma, double outlierWeight)
        {
            double s = System.Math.Max(sigma, 0.35 * _plan.CellSize);
            double inv = 1.0 / (2 * s * s), sum = 0;
            for (int i = 0; i < _plan.Width; i++)
            {
                for (int j = 0; j < _plan.Height; j++)
                {
                    int k = Index(i, j);
                    if (!_free[k]) continue;
                    double like = (1 - outlierWeight) * System.Math.Exp(-(_plan.CellCenter(i, j) - measured).LengthSquared * inv) + outlierWeight;
                    _p[k] *= like;
                    sum += _p[k];
                }
            }
            Normalize(sum);
            return FixResult.Accepted;
        }

        public void ReportMiss(SensorFootprint footprint, double missLikelihood)
        {
            _plan.WorldToCell(footprint.Origin, out int ci, out int cj);
            int r = (int)System.Math.Ceiling(footprint.Range / _plan.CellSize) + 1;
            double sum = 0;
            bool changed = false;
            for (int i = System.Math.Max(0, ci - r); i <= System.Math.Min(_plan.Width - 1, ci + r); i++)
            {
                for (int j = System.Math.Max(0, cj - r); j <= System.Math.Min(_plan.Height - 1, cj + r); j++)
                {
                    int k = Index(i, j);
                    if (!_free[k] || _p[k] <= 0) continue;
                    if (footprint.Covers(_plan.CellCenter(i, j))) { _p[k] *= missLikelihood; changed = true; }
                }
            }
            if (!changed) return;
            for (int k = 0; k < _p.Length; k++) sum += _p[k];
            Normalize(sum);
        }

        public double MassWithin(Vec2 center, double radius)
        {
            double r2 = radius * radius, m = 0;
            ForEachFree((i, j, p) => { if ((_plan.CellCenter(i, j) - center).LengthSquared <= r2) m += p; });
            return m;
        }

        private void Normalize(double sum)
        {
            if (sum <= 1e-300 || double.IsNaN(sum)) { Reset(); return; }
            for (int k = 0; k < _p.Length; k++) _p[k] /= sum;
        }

        private delegate void CellAction(int i, int j, double p);

        private void ForEachFree(CellAction action)
        {
            for (int i = 0; i < _plan.Width; i++)
                for (int j = 0; j < _plan.Height; j++)
                {
                    int k = Index(i, j);
                    if (_free[k]) action(i, j, _p[k]);
                }
        }

        private int Index(int i, int j) => i * _plan.Height + j;
    }
}
