using System.Collections.Generic;
using LowObservable.Core.Sensors;
using LowObservable.Core.World;

namespace LowObservable.Core.Estimation
{
    /// <summary>One landmark that could explain an observation, scored by squared Mahalanobis distance.</summary>
    public readonly struct AssociationCandidate
    {
        public readonly Landmark Landmark;
        public readonly Innovation Innovation;
        public double Mahalanobis2 => Innovation.Nis;

        public AssociationCandidate(Landmark landmark, Innovation innovation)
        {
            Landmark = landmark;
            Innovation = innovation;
        }
    }

    public enum AssociationOutcome
    {
        /// <summary>Exactly one landmark is plausible, or the best is far better than the rest.</summary>
        Confident,
        /// <summary>Two or more landmarks are similarly plausible. The player has to decide (or the filter guesses).</summary>
        Ambiguous,
        /// <summary>No landmark is inside the gate: an outlier, a spoof, or the belief is badly wrong.</summary>
        NoMatch,
    }

    public sealed class AssociationResult
    {
        public AssociationOutcome Outcome;
        /// <summary>Gated candidates, best first.</summary>
        public List<AssociationCandidate> Candidates = new List<AssociationCandidate>();
        public AssociationCandidate Best => Candidates[0];
    }

    /// <summary>
    /// Gated nearest-neighbour data association. For each landmark compatible with the observation
    /// (same appearance if the sensor reports one), compute the innovation and keep it if its NIS is
    /// below the chi-square gate. The gate is in the belief's own units: a lost robot (big P) accepts
    /// far-away matches, which is exactly why being lost makes association errors more likely.
    /// </summary>
    public static class DataAssociation
    {
        public static AssociationResult Associate(EkfLocalizer filter, LandmarkObservation obs,
            IReadOnlyList<Landmark> landmarks, double gate, double ambiguityRatio)
        {
            var result = new AssociationResult();
            foreach (var lm in landmarks)
            {
                if (obs.Appearance >= 0 && lm.Appearance != obs.Appearance) continue;
                var inn = filter.ComputeInnovation(obs, lm.Position);
                if (inn.Nis <= gate) result.Candidates.Add(new AssociationCandidate(lm, inn));
            }
            result.Candidates.Sort((a, b) => a.Mahalanobis2.CompareTo(b.Mahalanobis2));

            if (result.Candidates.Count == 0) result.Outcome = AssociationOutcome.NoMatch;
            else if (result.Candidates.Count == 1) result.Outcome = AssociationOutcome.Confident;
            else
            {
                // Compare likelihoods, not distances: exp(-d2/2) ratio > ambiguityRatio means the best clearly wins.
                double d1 = result.Candidates[0].Mahalanobis2;
                double d2 = result.Candidates[1].Mahalanobis2;
                double likelihoodRatio = System.Math.Exp(0.5 * (d2 - d1));
                result.Outcome = likelihoodRatio >= ambiguityRatio ? AssociationOutcome.Confident : AssociationOutcome.Ambiguous;
            }
            return result;
        }
    }
}
