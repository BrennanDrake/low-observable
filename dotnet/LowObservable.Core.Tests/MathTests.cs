using LowObservable.Core.Maths;
using Xunit;

namespace LowObservable.Core.Tests
{
    public class MathTests
    {
        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(3.5 * System.Math.PI, -0.5 * System.Math.PI)]
        [InlineData(-3.5 * System.Math.PI, 0.5 * System.Math.PI)]
        [InlineData(System.Math.PI, System.Math.PI)]
        [InlineData(-System.Math.PI, System.Math.PI)]
        public void WrapMapsIntoHalfOpenInterval(double input, double expected)
        {
            Assert.Equal(expected, Angle.Wrap(input), 9);
        }

        [Fact]
        public void BearingDifferenceAcrossTheSeamIsSmall()
        {
            double d = Angle.Difference(179 * Angle.Deg2Rad, -179 * Angle.Deg2Rad);
            Assert.Equal(-2 * Angle.Deg2Rad, d, 9);
        }

        [Fact]
        public void InverseTimesMatrixIsIdentity()
        {
            var a = new Matrix(3, 3, 4, 1, 0.5, 1, 3, 0.2, 0.5, 0.2, 2);
            var product = a * a.Inverse();
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    Assert.Equal(r == c ? 1.0 : 0.0, product[r, c], 9);
        }

        [Fact]
        public void CholeskyReconstructsAndRejectsIndefinite()
        {
            var a = new Matrix(2, 2, 4, 2, 2, 3);
            Assert.True(a.TryCholesky(out var l));
            var back = l * l.Transpose();
            Assert.Equal(4, back[0, 0], 9);
            Assert.Equal(2, back[0, 1], 9);
            Assert.Equal(3, back[1, 1], 9);
            Assert.False(new Matrix(2, 2, 1, 2, 2, 1).TryCholesky(out _));
        }

        [Fact]
        public void DeterminantMatchesClosedForm()
        {
            var a = new Matrix(2, 2, 3, 1, 2, 4);
            Assert.Equal(10, a.Determinant(), 9);
        }

        [Fact]
        public void EllipseAxesMatchDiagonalCovariance()
        {
            var e = CovarianceEllipse.FromComponents(Vec2.Zero, 4.0, 0.0, 1.0);
            Assert.Equal(2.0, e.SemiMajor, 9);
            Assert.Equal(1.0, e.SemiMinor, 9);
            Assert.Equal(0.0, e.Orientation, 9);
        }

        [Fact]
        public void EllipseOrientationFollowsCorrelation()
        {
            var e = CovarianceEllipse.FromComponents(Vec2.Zero, 1.0, 0.9, 1.0);
            Assert.Equal(System.Math.PI / 4, e.Orientation, 6);
            Assert.True(e.SemiMajor > e.SemiMinor);
        }

        [Fact]
        public void ChiSquareQuantilesAreClose()
        {
            Assert.Equal(5.991, ChiSquare.Quantile(2, 0.95), 3);
            Assert.Equal(7.815, ChiSquare.Quantile(3, 0.95), 1);
            Assert.Equal(1.960, ChiSquare.NormalQuantile(0.975), 3);
        }

        [Fact]
        public void RngIsDeterministicAndGaussianIsStandard()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 100; i++) Assert.Equal(a.NextULong(), b.NextULong());

            var r = new Rng(7);
            int n = 50000;
            double sum = 0, sum2 = 0;
            for (int i = 0; i < n; i++) { double g = r.NextGaussian(); sum += g; sum2 += g * g; }
            Assert.InRange(sum / n, -0.02, 0.02);
            Assert.InRange(sum2 / n, 0.97, 1.03);
        }

        [Fact]
        public void DerivedStreamsDiffer()
        {
            Assert.NotEqual(Rng.Derive(1, "a").NextULong(), Rng.Derive(1, "b").NextULong());
        }

        [Fact]
        public void PoseFrameTransformsRoundTrip()
        {
            var pose = new Pose2(2, 3, System.Math.PI / 2);
            var world = pose.LocalToWorld(new Vec2(1, 0));
            Assert.Equal(2, world.X, 9);
            Assert.Equal(4, world.Y, 9);
            var local = pose.WorldToLocal(world);
            Assert.Equal(1, local.X, 9);
            Assert.Equal(0, local.Y, 9);
        }
    }
}
