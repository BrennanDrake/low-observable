using LowObservable.Core.Estimation;
using LowObservable.Core.Maths;
using LowObservable.Core.Robot;
using LowObservable.Core.Sensors;
using Xunit;

namespace LowObservable.Core.Tests
{
    public class EkfTests
    {
        private static LandmarkObservation Observe(Pose2 truth, Vec2 landmark, Rng rng, double rs, double bs)
        {
            var rel = landmark - truth.Position;
            return new LandmarkObservation
            {
                Range = rel.Length + rng.NextGaussian() * rs,
                Bearing = Angle.Wrap(rel.Angle - truth.Theta + rng.NextGaussian() * bs),
                RangeSigma = rs,
                BearingSigma = bs,
                Appearance = -1,
            };
        }

        [Fact]
        public void PredictGrowsUncertaintyUpdateShrinksIt()
        {
            var ekf = EkfLocalizer.WithSigmas(new Pose2(0, 0, 0), 0.1, 0.02);
            double before = ekf.PositionSpread;
            for (int i = 0; i < 50; i++) ekf.Predict(new OdometryDelta(0.1, 0.0), 0.001, 0.0005);
            double afterMove = ekf.PositionSpread;
            Assert.True(afterMove > before);

            var truth = ekf.Mean;
            var rng = new Rng(3);
            ekf.Update(Observe(truth, new Vec2(8, 2), rng, 0.05, 0.02), new Vec2(8, 2));
            ekf.Update(Observe(truth, new Vec2(3, -4), rng, 0.05, 0.02), new Vec2(3, -4));
            Assert.True(ekf.PositionSpread < afterMove);
        }

        [Fact]
        public void HeadingErrorMakesLateralUncertaintyGrow()
        {
            // Driving along +X with an uncertain heading: the ellipse should stretch sideways (Y), the classic banana.
            var ekf = EkfLocalizer.WithSigmas(new Pose2(0, 0, 0), 0.01, 5 * Angle.Deg2Rad);
            for (int i = 0; i < 100; i++) ekf.Predict(new OdometryDelta(0.1, 0.0), 1e-6, 1e-8);
            var e = ekf.PositionEllipse();
            Assert.True(ekf.Covariance[1, 1] > 10 * ekf.Covariance[0, 0]);
            Assert.True(System.Math.Abs(System.Math.Abs(e.Orientation) - System.Math.PI / 2) < 0.1);
        }

        [Fact]
        public void ConvergesToTruthFromWrongGuess()
        {
            var truth = new Pose2(2, 1, 0.3);
            var ekf = EkfLocalizer.WithSigmas(new Pose2(2.6, 0.5, 0.2), 1.0, 0.3);
            var landmarks = new[] { new Vec2(6, 1), new Vec2(2, 6), new Vec2(-3, -2) };
            var rng = new Rng(11);
            for (int k = 0; k < 20; k++)
                foreach (var lm in landmarks) ekf.Update(Observe(truth, lm, rng, 0.05, 0.01), lm);
            Assert.Equal(truth.X, ekf.Mean.X, 1);
            Assert.Equal(truth.Y, ekf.Mean.Y, 1);
            Assert.Equal(truth.Theta, ekf.Mean.Theta, 1);
        }

        [Fact]
        public void BearingInnovationIsWrapped()
        {
            var ekf = EkfLocalizer.WithSigmas(new Pose2(0, 0, 0), 0.1, 0.05);
            // Landmark almost directly behind: predicted bearing about +pi, measured about -pi.
            var lm = new Vec2(-5, 0.01);
            var obs = new LandmarkObservation { Range = 5, Bearing = -System.Math.PI + 0.001, RangeSigma = 0.05, BearingSigma = 0.02 };
            var inn = ekf.ComputeInnovation(obs, lm);
            Assert.True(System.Math.Abs(inn.Residual[1, 0]) < 0.01);
        }

        [Fact]
        public void FilterIsConsistentOverMonteCarloRuns()
        {
            // Truth and filter share the noise model, so the average NEES should be about 3 (3 states).
            const int runs = 60;
            double neesSum = 0;
            int samples = 0;
            var landmarks = new[] { new Vec2(5, 5), new Vec2(-5, 5), new Vec2(0, -6), new Vec2(8, -2) };
            for (int run = 0; run < runs; run++)
            {
                var rng = new Rng((ulong)(1000 + run));
                var truth = new Pose2(0, 0, 0);
                var ekf = EkfLocalizer.WithSigmas(truth, 0.05, 0.01);
                for (int step = 0; step < 200; step++)
                {
                    var u = new OdometryDelta(0.05, 0.02);
                    OdometryModel.NoiseVariances(u, 1.0, 0.0025, 0.0008, 0.004, out double dv, out double rv);
                    var actual = new OdometryDelta(u.Distance + rng.NextGaussian() * System.Math.Sqrt(dv), u.Rotation + rng.NextGaussian() * System.Math.Sqrt(rv));
                    truth = OdometryModel.Apply(truth, actual);
                    ekf.Predict(u, dv, rv);
                    if (step % 10 == 0)
                    {
                        var lm = landmarks[(step / 10) % landmarks.Length];
                        ekf.Update(Observe(truth, lm, rng, 0.1, 0.02), lm);
                    }
                    neesSum += Consistency.PoseNees(truth, ekf.Mean, ekf.Covariance);
                    samples++;
                }
            }
            double avg = neesSum / samples;
            Assert.InRange(avg, 2.3, 3.8);
        }

        [Fact]
        public void OverconfidentFilterHasHighNees()
        {
            // Truth slips 8x more than the filter believes (a slick floor it does not know about).
            var rng = new Rng(5);
            var truth = new Pose2(0, 0, 0);
            var ekf = EkfLocalizer.WithSigmas(truth, 0.05, 0.01);
            double nees = 0;
            for (int step = 0; step < 300; step++)
            {
                var u = new OdometryDelta(0.05, 0.0);
                OdometryModel.NoiseVariances(u, 1.0, 0.0025, 0.0008, 0.004, out double dv, out double rv);
                var actual = new OdometryDelta(u.Distance + rng.NextGaussian() * System.Math.Sqrt(8 * dv), rng.NextGaussian() * System.Math.Sqrt(8 * rv));
                truth = OdometryModel.Apply(truth, actual);
                ekf.Predict(u, dv, rv);
                nees += Consistency.PoseNees(truth, ekf.Mean, ekf.Covariance);
            }
            Assert.True(nees / 300 > 6.0);
        }
    }
}
