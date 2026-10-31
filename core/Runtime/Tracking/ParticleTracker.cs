using LowObservable.Core.Estimation;
using LowObservable.Core.Maths;
using LowObservable.Core.World;

namespace LowObservable.Core.Tracking
{
    /// <summary>
    /// SWARM v4: a sequential importance resampling particle filter (see <see cref="ParticleFilter2D"/>).
    /// Each dot is one hypothesis of where the target is. Prediction random-walks the dots (walls block them),
    /// fixes and misses reweight them, and resampling clones the likely ones.
    ///
    /// It holds as many hypotheses as it needs, so a decoy splits it instead of fooling it outright. Its
    /// weakness is depletion: after a very confident report, almost every dot sits in one place. If the
    /// target isn't there, only the slow trickle of random injected dots can find it again.
    /// </summary>
    public sealed class ParticleTracker : IThreatTracker
    {
        private readonly ParticleFilter2D _pf;
        private double _accum;
        private double _injectDebt;

        public const double PredictPeriod = 0.1;

        public double AssumedSpeed { get; set; } = 1.6;

        /// <summary>Fraction of dots replaced with uniformly random ones per second (recovery from depletion).</summary>
        public double InjectionPerSecond { get; set; } = 0.02;

        /// <summary>Spread (m) above which the swarm is considered lost.</summary>
        public double LostSpread { get; set; } = 7.0;

        public TrackerKind Kind => TrackerKind.Particle;
        public ParticleFilter2D Filter => _pf;

        public ParticleTracker(FloorPlan plan, int count, Rng rng)
        {
            _pf = new ParticleFilter2D(plan, count, rng);
            _pf.InitializeUniform();
        }

        public bool HasEstimate => _pf.Spread() < LostSpread;
        public Vec2 BestGuess => _pf.Mode();
        public double Spread => _pf.Spread();

        public void Reset() => _pf.InitializeUniform();

        public void ResetAround(Vec2 center, double sigma) => _pf.InitializeGaussian(center, sigma);

        public void Predict(double dt)
        {
            _accum += dt;
            if (_accum < PredictPeriod) return;
            _pf.PredictRandomWalk(_accum, AssumedSpeed);
            _injectDebt += InjectionPerSecond * _pf.Count * _accum;
            _accum = 0;
            int n = (int)_injectDebt;
            if (n > 0)
            {
                _injectDebt -= n;
                _pf.InjectUniform((double)n / _pf.Count);
            }
        }

        public FixResult ReportFix(Vec2 measured, double sigma, double outlierWeight)
        {
            _pf.UpdatePositionFix(measured, sigma, outlierWeight);
            _pf.ResampleIfNeeded();
            return FixResult.Accepted;
        }

        public void ReportMiss(SensorFootprint footprint, double missLikelihood)
        {
            double r2 = footprint.Range * footprint.Range;
            var origin = footprint.Origin;
            var covers = footprint.Covers;
            _pf.Update(p => (p - origin).LengthSquared <= r2 && covers(p) ? missLikelihood : 1.0);
            _pf.ResampleIfNeeded();
        }

        public double MassWithin(Vec2 center, double radius) => _pf.MassWithin(center, radius);
    }
}
