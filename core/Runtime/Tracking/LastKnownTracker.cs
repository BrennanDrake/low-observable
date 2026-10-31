using LowObservable.Core.Maths;

namespace LowObservable.Core.Tracking
{
    /// <summary>
    /// SIGHTLINE v1. The simplest possible tracker and the cell block's: remember the last place the
    /// target was reported and send units there. It has no notion of uncertainty (every report is taken
    /// as exact, whatever the sensor) and no motion model (it never imagines that the target moved).
    /// When a sensor looks straight at the remembered point and sees nothing, it gives up.
    ///
    /// It teaches by failing: break line of sight and walk away, and units walk to an empty spot.
    /// </summary>
    public sealed class LastKnownTracker : IThreatTracker
    {
        public TrackerKind Kind => TrackerKind.LastKnown;

        public bool HasEstimate { get; private set; }
        public Vec2 BestGuess { get; private set; }

        /// <summary>Seconds since the last accepted report.</summary>
        public double Age { get; private set; }

        /// <summary>True for one query after a sensor cleared the remembered point (used by the exploit detector).</summary>
        public bool LostThisStep { get; private set; }

        public double Spread => HasEstimate ? 0.0 : double.PositiveInfinity;

        public void Reset()
        {
            HasEstimate = false;
            Age = 0;
        }

        public void ResetAround(Vec2 center, double sigma)
        {
            BestGuess = center;
            HasEstimate = true;
            Age = 0;
        }

        public void Predict(double dt)
        {
            // No motion model: the estimate does not move or grow. That's the whole weakness.
            Age += dt;
            LostThisStep = false;
        }

        public FixResult ReportFix(Vec2 measured, double sigma, double outlierWeight)
        {
            // Every report is believed completely. A thrown cup is as good as a sighting.
            BestGuess = measured;
            HasEstimate = true;
            Age = 0;
            return FixResult.Accepted;
        }

        public void ReportMiss(SensorFootprint footprint, double missLikelihood)
        {
            if (!HasEstimate) return;
            if (Vec2.Distance(footprint.Origin, BestGuess) > footprint.Range) return;
            if (!footprint.Covers(BestGuess)) return;
            HasEstimate = false;
            LostThisStep = true;
        }

        public double MassWithin(Vec2 center, double radius)
        {
            return HasEstimate && Vec2.Distance(center, BestGuess) <= radius ? 1.0 : 0.0;
        }
    }
}
