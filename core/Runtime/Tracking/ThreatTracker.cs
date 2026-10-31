using System;
using LowObservable.Core.Maths;

namespace LowObservable.Core.Tracking
{
    /// <summary>The algorithm WARDEN uses to track the player. Changes by zone; see docs/design/escape-concept.md.</summary>
    public enum TrackerKind
    {
        /// <summary>SIGHTLINE v1: a single last-known position. No uncertainty, no motion model.</summary>
        LastKnown,
        /// <summary>HEATMAP v2: histogram Bayes filter over the floor grid.</summary>
        OccupancyGrid,
        /// <summary>KESTREL v3: Kalman filter with a constant-velocity model and Mahalanobis gating.</summary>
        Kalman,
        /// <summary>SWARM v4: sequential importance resampling particle filter.</summary>
        Particle,
    }

    /// <summary>
    /// Where a sensor could have seen the target, for negative information ("I looked here and saw
    /// nothing"). <see cref="Covers"/> is the sensor's own model of what it can see, which is not always
    /// what it really sees (mirrors), and that gap is a gameplay mechanic.
    /// </summary>
    public readonly struct SensorFootprint
    {
        /// <summary>Sensor position, used to skip cells that are out of range quickly.</summary>
        public readonly Vec2 Origin;
        public readonly double Range;
        public readonly Func<Vec2, bool> Covers;

        public SensorFootprint(Vec2 origin, double range, Func<Vec2, bool> covers)
        {
            Origin = origin;
            Range = range;
            Covers = covers;
        }
    }

    /// <summary>Result of reporting a position fix to a tracker.</summary>
    public enum FixResult
    {
        Accepted,
        /// <summary>The tracker judged the measurement an outlier and ignored it (KESTREL's gate).</summary>
        Rejected,
    }

    /// <summary>
    /// WARDEN's belief about where the player is. Every sensor in a zone feeds one tracker (a shared
    /// belief) and every unit acts on it. Implementations differ in representation, motion model and
    /// how they treat negative information and outliers; those differences are what the player exploits.
    /// </summary>
    public interface IThreatTracker
    {
        TrackerKind Kind { get; }

        /// <summary>Forget everything: the target could be anywhere.</summary>
        void Reset();

        /// <summary>Start from a rough position (e.g. the player was detained here).</summary>
        void ResetAround(Vec2 center, double sigma);

        /// <summary>Prediction step: time passes with no evidence.</summary>
        void Predict(double dt);

        /// <summary>
        /// Measurement update from a position fix (a sighting, a heard sound, a bump).
        /// <paramref name="sigma"/> is the sensor's 1-sigma error in meters. <paramref name="outlierWeight"/>
        /// is the probability the report is junk; trackers that model outliers mix it in as a uniform floor.
        /// </summary>
        FixResult ReportFix(Vec2 measured, double sigma, double outlierWeight);

        /// <summary>Negative information: a sensor that would have detected the target inside its footprint did not.</summary>
        void ReportMiss(SensorFootprint footprint, double missLikelihood);

        /// <summary>False when the tracker has nothing useful (never seen, lost, or spread over the whole floor).</summary>
        bool HasEstimate { get; }

        /// <summary>Where units should go: the single most likely position.</summary>
        Vec2 BestGuess { get; }

        /// <summary>Probability the target is within <paramref name="radius"/> of <paramref name="center"/>.</summary>
        double MassWithin(Vec2 center, double radius);

        /// <summary>Spread in meters (square root of the covariance trace); 0 for a point estimate.</summary>
        double Spread { get; }
    }
}
