using LowObservable.Core.Estimation;
using LowObservable.Core.Levels;
using LowObservable.Core.Maths;
using Xunit;

namespace LowObservable.Core.Tests
{
    public class ParticleFilterTests
    {
        [Fact]
        public void PositionFixesPullTheCloudToTheTarget()
        {
            var plan = BuiltInLevels.Load("open-room");
            var pf = new ParticleFilter2D(plan, 500, new Rng(1));
            pf.InitializeUniform();
            var target = new Vec2(12.3, 4.6);
            var rng = new Rng(2);
            for (int k = 0; k < 10; k++)
            {
                pf.PredictRandomWalk(0.1, 0.5);
                pf.UpdatePositionFix(new Vec2(target.X + rng.NextGaussian() * 0.5, target.Y + rng.NextGaussian() * 0.5), 0.5);
                pf.ResampleIfNeeded();
            }
            Assert.True(Vec2.Distance(pf.Mean(), target) < 0.5);
            Assert.True(pf.Spread() < 1.0);
        }

        [Fact]
        public void TwoIndistinguishableSoundsSplitTheCloud()
        {
            // The decoy trick: two equally loud sources make a bimodal belief whose mean is in between.
            var plan = BuiltInLevels.Load("open-room");
            var pf = new ParticleFilter2D(plan, 2000, new Rng(4));
            pf.InitializeUniform();
            var a = new Vec2(3.5, 5.5);
            var b = new Vec2(15.5, 5.5);
            pf.Update(p =>
            {
                double da = (p - a).LengthSquared, db = (p - b).LengthSquared;
                return System.Math.Exp(-da / 0.5) + System.Math.Exp(-db / 0.5);
            });
            pf.Resample();
            double massA = pf.MassWithin(a, 1.5);
            double massB = pf.MassWithin(b, 1.5);
            Assert.InRange(massA, 0.3, 0.7);
            Assert.InRange(massB, 0.3, 0.7);
            Assert.True(pf.MassWithin(pf.Mean(), 1.5) < 0.1, "The mean of a split cloud should be where nobody is.");
        }

        [Fact]
        public void NegativeInformationRemovesMass()
        {
            var plan = BuiltInLevels.Load("open-room");
            var pf = new ParticleFilter2D(plan, 1000, new Rng(9));
            pf.InitializeUniform();
            var looked = new Vec2(5, 5);
            double before = pf.MassWithin(looked, 3);
            pf.Update(p => Vec2.Distance(p, looked) < 3 ? 0.05 : 1.0);
            Assert.True(pf.MassWithin(looked, 3) < before * 0.2);
        }

        [Fact]
        public void ResampleRestoresEffectiveSampleSize()
        {
            var plan = BuiltInLevels.Load("open-room");
            var pf = new ParticleFilter2D(plan, 400, new Rng(6));
            pf.InitializeUniform();
            pf.UpdatePositionFix(new Vec2(10, 5), 0.3, outlierWeight: 0.0001);
            Assert.True(pf.EffectiveSampleSize() < 100);
            pf.Resample();
            Assert.Equal(400, pf.EffectiveSampleSize(), 6);
        }

        [Fact]
        public void ParticlesStayOutOfWalls()
        {
            var plan = BuiltInLevels.Load("annex-b");
            var pf = new ParticleFilter2D(plan, 300, new Rng(12));
            pf.InitializeUniform();
            for (int k = 0; k < 50; k++) pf.PredictRandomWalk(0.2, 2.0);
            foreach (var p in pf.Positions) Assert.False(plan.IsWallAt(p));
        }

        [Fact]
        public void ImpossibleEvidenceResetsInsteadOfDividingByZero()
        {
            var plan = BuiltInLevels.Load("open-room");
            var pf = new ParticleFilter2D(plan, 100, new Rng(1));
            pf.InitializeUniform();
            pf.Update(p => 0.0);
            double sum = 0;
            foreach (var w in pf.Weights) sum += w;
            Assert.Equal(1.0, sum, 9);
        }
    }
}
