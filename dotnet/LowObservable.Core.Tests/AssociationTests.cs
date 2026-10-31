using System.Collections.Generic;
using LowObservable.Core.Estimation;
using LowObservable.Core.Maths;
using LowObservable.Core.Sensors;
using LowObservable.Core.World;
using Xunit;

namespace LowObservable.Core.Tests
{
    public class AssociationTests
    {
        private static LandmarkObservation Perfect(Pose2 truth, Vec2 lm, int appearance)
        {
            var rel = lm - truth.Position;
            return new LandmarkObservation { Range = rel.Length, Bearing = Angle.Wrap(rel.Angle - truth.Theta), RangeSigma = 0.1, BearingSigma = 0.03, Appearance = appearance };
        }

        [Fact]
        public void ConfidentWhenBeliefIsTight()
        {
            var truth = new Pose2(0, 0, 0);
            var ekf = EkfLocalizer.WithSigmas(truth, 0.05, 0.01);
            var lms = new List<Landmark> { new Landmark(0, new Vec2(5, 0), 0, 'a'), new Landmark(1, new Vec2(5, 3), 0, 'a') };
            var r = DataAssociation.Associate(ekf, Perfect(truth, lms[0].Position, 0), lms, 9.21, 3.0);
            Assert.Equal(AssociationOutcome.Confident, r.Outcome);
            Assert.Equal(0, r.Best.Landmark.Id);
        }

        [Fact]
        public void AmbiguousWhenLostAmongLookalikes()
        {
            var truth = new Pose2(0, 0, 0);
            var ekf = EkfLocalizer.WithSigmas(truth, 3.0, 0.3);
            var lms = new List<Landmark> { new Landmark(0, new Vec2(5, 0), 0, 'a'), new Landmark(1, new Vec2(5, 1.5), 0, 'a') };
            var r = DataAssociation.Associate(ekf, Perfect(truth, lms[0].Position, 0), lms, 9.21, 3.0);
            Assert.Equal(AssociationOutcome.Ambiguous, r.Outcome);
            Assert.Equal(2, r.Candidates.Count);
        }

        [Fact]
        public void AppearanceRulesOutOtherLandmarks()
        {
            var truth = new Pose2(0, 0, 0);
            var ekf = EkfLocalizer.WithSigmas(truth, 3.0, 0.3);
            var lms = new List<Landmark> { new Landmark(0, new Vec2(5, 0), 0, 'a'), new Landmark(1, new Vec2(5, 1.5), 1, 'b') };
            var r = DataAssociation.Associate(ekf, Perfect(truth, lms[1].Position, 1), lms, 9.21, 3.0);
            Assert.Equal(AssociationOutcome.Confident, r.Outcome);
            Assert.Equal(1, r.Best.Landmark.Id);
        }

        [Fact]
        public void FarOutlierIsRejected()
        {
            var truth = new Pose2(0, 0, 0);
            var ekf = EkfLocalizer.WithSigmas(truth, 0.05, 0.01);
            var lms = new List<Landmark> { new Landmark(0, new Vec2(5, 0), 0, 'a') };
            var obs = Perfect(truth, new Vec2(0, 7), -1);
            var r = DataAssociation.Associate(ekf, obs, lms, 9.21, 3.0);
            Assert.Equal(AssociationOutcome.NoMatch, r.Outcome);
        }
    }
}
