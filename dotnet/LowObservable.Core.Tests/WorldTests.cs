using LowObservable.Core.Levels;
using LowObservable.Core.Maths;
using LowObservable.Core.World;
using Xunit;

namespace LowObservable.Core.Tests
{
    public class WorldTests
    {
        private const string Tiny = @"
; comment lines are ignored
#######
#a.#.U#
#>.#..#
#..,.K#
#######";

        [Fact]
        public void ParserReadsDimensionsAndSpecialCells()
        {
            var plan = FloorPlanParser.Parse("tiny", Tiny);
            Assert.Equal(7, plan.Width);
            Assert.Equal(5, plan.Height);
            Assert.Single(plan.Landmarks);
            Assert.Equal(new Vec2(1.5, 3.5), plan.Landmarks[0].Position);
            Assert.Equal(0, plan.Landmarks[0].Appearance);
            Assert.Equal(new Vec2(1.5, 2.5), plan.PlayerStart);
            Assert.Equal(new Vec2(5.5, 3.5), plan.Uplink);
            Assert.Equal(new Vec2(5.5, 1.5), plan.SeekerStart);
            Assert.True(plan.IsDarkAt(new Vec2(3.5, 1.5)));
            Assert.True(plan.IsWallAt(new Vec2(3.5, 3.5)));
        }

        [Fact]
        public void UppercaseLandmarksAreDark()
        {
            var plan = FloorPlanParser.Parse("dark", "#####\n#>A.#\n#.UK#\n#####");
            Assert.True(plan.IsDarkAt(plan.Landmarks[0].Position));
            Assert.Equal(0, plan.Landmarks[0].Appearance);
        }

        [Fact]
        public void MissingStartIsAnError()
        {
            Assert.Throws<System.FormatException>(() => FloorPlanParser.Parse("bad", "####\n#UK#\n####"));
        }

        [Fact]
        public void LineOfSightIsBlockedByWalls()
        {
            var plan = FloorPlanParser.Parse("tiny", Tiny);
            Assert.True(plan.LineOfSight(new Vec2(1.5, 1.5), new Vec2(2.5, 3.5)));
            Assert.False(plan.LineOfSight(new Vec2(1.5, 3.5), new Vec2(5.5, 3.5)));
            Assert.True(plan.LineOfSight(new Vec2(1.5, 1.5), new Vec2(5.5, 1.5)));
        }

        [Fact]
        public void PathfinderGoesAroundWalls()
        {
            var plan = FloorPlanParser.Parse("tiny", Tiny);
            var path = new GridPathfinder(plan).FindPath(plan.PlayerStart, plan.Uplink);
            Assert.NotEmpty(path);
            Assert.Equal(plan.Uplink, path[path.Count - 1]);
            foreach (var p in path) Assert.False(plan.IsWallAt(p));
        }

        [Theory]
        [InlineData("annex-b")]
        [InlineData("open-room")]
        public void BuiltInLevelsAreSolvable(string name)
        {
            var plan = BuiltInLevels.Load(name);
            var pf = new GridPathfinder(plan);
            Assert.NotEmpty(pf.FindPath(plan.PlayerStart, plan.Uplink));
            Assert.NotEmpty(pf.FindPath(plan.SeekerStart, plan.PlayerStart));
            Assert.True(plan.Landmarks.Count >= 6);
        }

        [Fact]
        public void AnnexBHasAliasedLandmarks()
        {
            var plan = BuiltInLevels.Load("annex-b");
            int sameAsFirst = 0;
            foreach (var l in plan.Landmarks) if (l.Appearance == plan.Landmarks[0].Appearance) sameAsFirst++;
            Assert.True(sameAsFirst >= 2, "The twin labs need landmarks that look identical.");
        }
    }
}
