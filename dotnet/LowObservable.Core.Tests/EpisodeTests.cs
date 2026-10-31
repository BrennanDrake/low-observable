using LowObservable.Core.Levels;
using LowObservable.Core.Maths;
using LowObservable.Core.Robot;
using LowObservable.Core.Simulation;
using Xunit;

namespace LowObservable.Core.Tests
{
    public class EpisodeTests
    {
        [Fact]
        public void SameSeedReplaysIdentically()
        {
            var plan = BuiltInLevels.Load("annex-b");
            Episode Run()
            {
                var e = new Episode(plan, new EpisodeConfig { Seed = 77 });
                new Autopilot(plan, AutopilotProfile.Cautious()).RunToEnd(e, 3000);
                return e;
            }
            var a = Run();
            var b = Run();
            Assert.Equal(a.Outcome, b.Outcome);
            Assert.Equal(a.TickCount, b.TickCount);
            Assert.Equal(a.TruePose.X, b.TruePose.X);
            Assert.Equal(a.Filter.Mean.Y, b.Filter.Mean.Y);
        }

        [Fact]
        public void StandingStillKeepsBeliefFixed()
        {
            var plan = BuiltInLevels.Load("open-room");
            var e = new Episode(plan, new EpisodeConfig { Seed = 1 });
            double spread = e.Filter.PositionSpread;
            for (int i = 0; i < 20; i++) e.Step(PlayerCommand.Idle);
            Assert.True(e.Filter.PositionSpread <= spread + 1e-9);
        }

        [Fact]
        public void DrivingBlindAccumulatesUncertainty()
        {
            var plan = BuiltInLevels.Load("annex-b");
            var e = new Episode(plan, new EpisodeConfig { Seed = 2 });
            double before = e.Filter.PositionSpread;
            for (int i = 0; i < 100; i++) e.Step(PlayerCommand.Drive(1, 0, MovementMode.Sprint));
            Assert.True(e.Filter.PositionSpread > before);
        }

        [Fact]
        public void PingProducesObservationsAndANoise()
        {
            var plan = BuiltInLevels.Load("open-room");
            var e = new Episode(plan, new EpisodeConfig { Seed = 3 });
            e.Step(PlayerCommand.Idle.With(PlayerAction.Ping));
            bool fired = false, applied = false;
            foreach (var ev in e.TickEvents)
            {
                if (ev.Type == EpisodeEventType.PingFired) fired = true;
                if (ev.Type == EpisodeEventType.ObservationApplied) applied = true;
            }
            Assert.True(fired);
            Assert.True(applied);
        }

        [Fact]
        public void PingCooldownIsEnforced()
        {
            var plan = BuiltInLevels.Load("open-room");
            var e = new Episode(plan, new EpisodeConfig { Seed = 3 });
            e.Step(PlayerCommand.Idle.With(PlayerAction.Ping));
            e.Step(PlayerCommand.Idle.With(PlayerAction.Ping));
            Assert.Contains(e.TickEvents, ev => ev.Type == EpisodeEventType.PingOnCooldown);
        }

        [Fact]
        public void PingingRepeatedlyBringsTheSeeker()
        {
            var plan = BuiltInLevels.Load("open-room");
            var e = new Episode(plan, new EpisodeConfig { Seed = 4 });
            for (int i = 0; i < 3000 && e.Outcome == EpisodeOutcome.Running; i++)
                e.Step(PlayerCommand.Idle.With(PlayerAction.Ping));
            Assert.Equal(EpisodeOutcome.Captured, e.Outcome);
        }

        [Fact]
        public void DockingAwayFromTheUplinkFails()
        {
            var plan = BuiltInLevels.Load("open-room");
            var e = new Episode(plan, new EpisodeConfig { Seed = 5 });
            e.Step(PlayerCommand.Idle.With(PlayerAction.Dock));
            Assert.Equal(EpisodeOutcome.Running, e.Outcome);
            Assert.Contains(e.TickEvents, ev => ev.Type == EpisodeEventType.DockFailed);
        }

        [Fact]
        public void DecoysAreLimited()
        {
            var plan = BuiltInLevels.Load("open-room");
            var e = new Episode(plan, new EpisodeConfig { Seed = 6 });
            var throwCmd = PlayerCommand.Idle.With(PlayerAction.ThrowDecoy);
            throwCmd.DecoyTargetLocal = new Vec2(3, 0);
            for (int i = 0; i < e.Tuning.DecoyCharges; i++) e.Step(throwCmd);
            Assert.Equal(0, e.DecoysLeft);
            e.Step(throwCmd);
            Assert.Contains(e.TickEvents, ev => ev.Type == EpisodeEventType.NoDecoysLeft);
        }

        [Fact]
        public void PauseForPlayerWaitsUntilResolved()
        {
            // Two identical-looking landmarks and a wide belief: the first camera frame is ambiguous.
            const string twins = @"
###################
#a.......a.......K#
#.................#
#...>.............#
#..............U..#
###################";
            var plan = World.FloorPlanParser.Parse("twins", twins);
            var e = new Episode(plan, new EpisodeConfig { Seed = 8, Association = AssociationPolicy.PauseForPlayer });
            e.Filter.Reset(e.Filter.Mean, Matrix.Diagonal(40, 40, 0.05));
            e.Step(PlayerCommand.Idle);
            Assert.True(e.AwaitingAssociation, "Expected an ambiguous observation with a wide belief.");
            Assert.True(e.CurrentPending.Candidates.Count >= 2);

            int ticks = e.TickCount;
            e.Step(PlayerCommand.Idle);
            Assert.Equal(ticks, e.TickCount);

            e.ResolvePending(e.CurrentPending.Candidates[0].Landmark.Id);
            while (e.AwaitingAssociation) e.ResolvePending(-1);
            e.Step(PlayerCommand.Idle);
            Assert.Equal(ticks + 1, e.TickCount);
        }

        [Fact]
        public void RecorderWritesCsv()
        {
            var plan = BuiltInLevels.Load("open-room");
            var e = new Episode(plan, new EpisodeConfig { Seed = 9 });
            new Autopilot(plan, AutopilotProfile.Reckless()).RunToEnd(e, 5000);
            var w = new System.IO.StringWriter();
            e.Recorder.WriteSamplesCsv(w);
            string csv = w.ToString();
            Assert.StartsWith(RunRecorder.SampleHeader, csv);
            Assert.True(e.Recorder.Samples.Count > 2);
            if (e.Outcome != EpisodeOutcome.Running) Assert.NotNull(e.Recorder.Summary);
        }

        [Fact]
        public void OpenRoomIsWinnable()
        {
            var plan = BuiltInLevels.Load("open-room");
            int escaped = 0;
            foreach (var profile in new[] { AutopilotProfile.Reckless(), AutopilotProfile.Cautious(), AutopilotProfile.Ghost() })
            {
                for (ulong seed = 1; seed <= 5; seed++)
                {
                    var e = new Episode(plan, new EpisodeConfig { Seed = seed, Record = false });
                    if (new Autopilot(plan, profile).RunToEnd(e) == EpisodeOutcome.Escaped) escaped++;
                }
            }
            Assert.True(escaped >= 1, "The spike room should be winnable by some play style at least sometimes.");
        }
    }
}
