using LowObservable.Core.Maths;
using LowObservable.Core.Stages;
using LowObservable.Core.Tracking;
using LowObservable.Core.World;
using Xunit;

namespace LowObservable.Core.Tests
{
    /// <summary>The four WARDEN trackers, and the textbook weaknesses each level is built around.</summary>
    public class TrackerTests
    {
        // A 20 x 10 room split by a wall at x = 10 with a door gap at the top.
        private const string Room = @"
map:
######################
#S.........#.........#
#..........#.........#
#..........#.........#
#..........#.........#
#..........#.........#
#..........#.........#
#..........#.........#
#....................#
#...................X#
######################
";

        private static FloorPlan Plan() => StageParser.Parse("room", Room).CreatePlan();

        private static SensorFootprint Disc(Vec2 c, double r) => new SensorFootprint(c, r, p => Vec2.Distance(p, c) <= r);

        [Fact]
        public void LastKnownTrustsEveryReportAndGivesUpWhenItLooks()
        {
            var t = new LastKnownTracker();
            Assert.False(t.HasEstimate);
            t.ReportFix(new Vec2(3, 3), 5.0, 0.5);
            Assert.True(t.HasEstimate);
            Assert.Equal(new Vec2(3, 3), t.BestGuess);

            // A wildly noisy report is believed just as much: no noise model.
            t.ReportFix(new Vec2(15, 8), 50.0, 0.9);
            Assert.Equal(new Vec2(15, 8), t.BestGuess);

            // No motion model: time passing changes nothing.
            t.Predict(30);
            Assert.Equal(new Vec2(15, 8), t.BestGuess);

            t.ReportMiss(Disc(new Vec2(2, 2), 3), 0.1);
            Assert.True(t.HasEstimate);
            t.ReportMiss(Disc(new Vec2(14, 8), 3), 0.1);
            Assert.False(t.HasEstimate);
        }

        [Fact]
        public void GridStaysNormalizedAndConcentratesOnAFix()
        {
            var g = new OccupancyGridTracker(Plan());
            Assert.Equal(1.0, g.MassWithin(new Vec2(10, 5), 100), 6);
            g.ReportFix(new Vec2(4.5, 4.5), 0.6, 0.001);
            Assert.True(g.MassWithin(new Vec2(4.5, 4.5), 2.0) > 0.8);
            Assert.Equal(1.0, g.MassWithin(new Vec2(10, 5), 100), 6);
        }

        [Fact]
        public void GridDiffusesButWallsHoldItBack()
        {
            var g = new OccupancyGridTracker(Plan());
            g.ResetAround(new Vec2(8.5, 3.5), 0.4);
            double before = g.Spread;
            for (int k = 0; k < 15; k++) g.Predict(0.2);
            Assert.True(g.Spread > before + 0.5);
            // 3 s at 1.6 m/s: the heat should be mostly on the near side of the wall (the gap is far up).
            Assert.True(g.MassWithin(new Vec2(5, 4), 4) > g.MassWithin(new Vec2(15, 4), 4) * 5);
        }

        [Fact]
        public void GridLearnsFromAbsence()
        {
            var g = new OccupancyGridTracker(Plan());
            g.ReportFix(new Vec2(4.5, 4.5), 2.0, 0.01);
            double near = g.MassWithin(new Vec2(4.5, 4.5), 1.5);
            g.ReportMiss(Disc(new Vec2(4.5, 4.5), 1.5), 0.05);
            Assert.True(g.MassWithin(new Vec2(4.5, 4.5), 1.5) < near * 0.2);
            Assert.Equal(1.0, g.MassWithin(new Vec2(10, 5), 100), 6);
        }

        [Fact]
        public void KalmanAveragesTwoDistantReportsIntoTheEmptyMiddle()
        {
            var k = new KalmanTracker(new Vec2(10, 5), 10);
            k.ResetAround(new Vec2(10, 5), 8);
            Assert.Equal(FixResult.Accepted, k.ReportFix(new Vec2(4, 5), 2.0, 0.01));
            k.Predict(0.5);
            Assert.Equal(FixResult.Accepted, k.ReportFix(new Vec2(12, 5), 2.0, 0.01));
            // One Gaussian can't hold both: the mean lands between them, far from each.
            Assert.InRange(k.Mean.X, 6.5, 10.0);
            Assert.True(Vec2.Distance(k.Mean, new Vec2(4, 5)) > 2.0);
            Assert.True(Vec2.Distance(k.Mean, new Vec2(12, 5)) > 2.0);
        }

        [Fact]
        public void KalmanGatesOutTheTruthOnceItIsConfidentlyWrong()
        {
            var k = new KalmanTracker(new Vec2(10, 5), 10);
            k.ResetAround(new Vec2(3, 3), 0.3);
            var before = k.Mean;
            Assert.Equal(FixResult.Rejected, k.ReportFix(new Vec2(16, 8), 0.3, 0.01));
            Assert.Equal(before, k.Mean);
            Assert.Equal(1, k.RejectedCount);
        }

        [Fact]
        public void KalmanPredictsStraightAheadAndIgnoresAbsence()
        {
            var k = new KalmanTracker(new Vec2(10, 5), 10);
            k.ResetAround(new Vec2(2, 5), 0.5);
            for (int i = 0; i <= 10; i++)
            {
                k.ReportFix(new Vec2(2 + 0.4 * i, 5), 0.3, 0.01);
                k.Predict(0.2);
            }
            // It has learned ~2 m/s east. Two seconds without reports: it keeps going.
            var at = k.Mean;
            for (int i = 0; i < 10; i++) k.Predict(0.2);
            Assert.True(k.Mean.X > at.X + 1.5);
            var mean = k.Mean;
            k.ReportMiss(Disc(mean, 3), 0.01);
            Assert.Equal(mean, k.Mean);
        }

        [Fact]
        public void KalmanMassWithinMatchesTheClosedForm()
        {
            var k = new KalmanTracker(new Vec2(10, 5), 10);
            k.ResetAround(new Vec2(5, 5), 1.0);
            // Isotropic Gaussian: P(|x - mean| < r) = 1 - exp(-r^2 / 2 sigma^2).
            double r = 1.5, expected = 1 - System.Math.Exp(-r * r / 2);
            Assert.InRange(k.MassWithin(new Vec2(5, 5), r), expected - 0.03, expected + 0.03);
        }

        [Fact]
        public void ParticlesSplitOnTwoDistantReports()
        {
            var p = new ParticleTracker(Plan(), 600, new Rng(3));
            p.ReportFix(new Vec2(4, 4), 1.0, 0.01);
            p.ReportFix(new Vec2(16, 4), 1.0, 0.01);
            Assert.True(p.MassWithin(new Vec2(4, 4), 3) > 0.2);
            Assert.True(p.MassWithin(new Vec2(16, 4), 3) > 0.2);
        }

        [Fact]
        public void AConfidentWrongReportStarvesTheSwarm()
        {
            var p = new ParticleTracker(Plan(), 500, new Rng(4)) { InjectionPerSecond = 0.02 };
            var forged = new Vec2(16, 8);
            var truth = new Vec2(3, 2);
            p.ReportFix(forged, 0.3, 0.00001);
            for (int k = 0; k < 20; k++) p.Predict(0.1);
            Assert.True(p.MassWithin(forged, 3.0) > 0.7, $"near forged: {p.MassWithin(forged, 3.0)}");
            Assert.True(p.MassWithin(truth, 2.5) < 0.02, $"near truth: {p.MassWithin(truth, 2.5)}");
        }
    }
}
