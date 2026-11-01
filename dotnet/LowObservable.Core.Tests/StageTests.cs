using System;
using System.Linq;
using LowObservable.Core.Campaign;
using LowObservable.Core.Maths;
using LowObservable.Core.Stages;
using LowObservable.Core.Tracking;
using Xunit;

namespace LowObservable.Core.Tests
{
    public class StageTests
    {
        private static Stage Make(string text, TrackerKind tracker = TrackerKind.LastKnown, StageTuning tuning = null, params ToolKind[] tools)
        {
            var def = StageParser.Parse("test", text);
            return new Stage(def, new StageConfig { Seed = 7, Tuning = tuning, UnlockedTools = tools, TrackerOverride = tracker });
        }

        private static void Run(Stage s, double seconds, StageInput input = default)
        {
            for (double t = 0; t < seconds; t += Stage.Dt) s.Step(input);
        }

        private static void Press(Stage s, StageInput input)
        {
            s.Step(input);
        }

        [Fact]
        public void ParserReadsHeaderMapAndEntities()
        {
            var def = StageParser.Parse("x", @"
title: Test Room
zone: Somewhere
tracker: kalman
map:
#######
#S.a.X#
#######
entities:
camera a dir=90 range=5
");
            Assert.Equal("Test Room", def.Title);
            Assert.Equal(TrackerKind.Kalman, def.Tracker);
            Assert.Single(def.Entities);
            Assert.Equal(new Vec2(3.5, 1.5), def.Entities[0].Position);
            Assert.Single(def.Exits);
        }

        [Fact]
        public void ParserRejectsMistakes()
        {
            Assert.Throws<FormatException>(() => StageParser.Parse("x", "map:\n#####\n#..X#\n#####\n"));
            Assert.Throws<FormatException>(() => StageParser.Parse("x", "map:\n#####\n#SaX#\n#####\n"));
            Assert.Throws<FormatException>(() => StageParser.Parse("x", "map:\n#####\n#SaX#\n#####\nentities:\nguard a route=b\n"));
            Assert.Throws<FormatException>(() => StageParser.Parse("x", "map:\n#####\n#SaX#\n#####\nentities:\nwidget a\n"));
        }

        [Fact]
        public void BuiltInStagesMatchTheLevelFiles()
        {
            var dir = FindRepoDir("levels/stages");
            var files = System.IO.Directory.GetFiles(dir, "*.lvl").OrderBy(f => f, StringComparer.Ordinal).ToArray();
            Assert.Equal(files.Select(System.IO.Path.GetFileNameWithoutExtension).ToArray(), BuiltInStages.Ids);
            foreach (var f in files)
                Assert.True(System.IO.File.ReadAllText(f) == BuiltInStages.Sources[System.IO.Path.GetFileNameWithoutExtension(f)],
                    $"{f} changed; run scripts/pc/gen_stages.py");
        }

        [Fact]
        public void EveryCampaignLevelIsReachableWithSensorsOff()
        {
            var owned = new System.Collections.Generic.List<ToolKind>();
            foreach (var def in BuiltInStages.LoadAll())
            {
                var t = StageTuning.Default();
                t.CameraRange = 0;
                t.GuardVisionRange = 0;
                t.MicRadius = 0;
                t.UnitHearing = 0;
                t.RoombaBumpRange = 0;
                t.CaptureRadius = 0;
                var stage = new Stage(def, new StageConfig { Seed = 1, Tuning = t, UnlockedTools = owned });
                Assert.True(new StageBot(stage, BotPolicy.Walker).RunToEnd(300), $"{def.Id} is not reachable");
                owned.AddRange(stage.ToolsFoundHere);
            }
            Assert.Contains(ToolKind.Noisemaker, owned);
            Assert.Contains(ToolKind.Jammer, owned);
            Assert.Contains(ToolKind.Mirror, owned);
        }

        [Fact]
        public void SameSeedSameRun()
        {
            var def = BuiltInStages.Load("03-the-yard");
            string Play()
            {
                var s = new Stage(def, new StageConfig { Seed = 11 });
                new StageBot(s, BotPolicy.Runner).RunToEnd(60);
                return $"{s.Player} {s.Captures} {s.Detections} {s.Tracker.BestGuess}";
            }
            Assert.Equal(Play(), Play());
        }

        private const string Corridor = @"
map:
####################
#S.................#
#..................#
#.........a.......X#
####################
entities:
camera a dir=180 range=12 fov=60
";

        [Fact]
        public void CameraSightingMovesTheTrackerAndHidingDoesNot()
        {
            var s = Make(Corridor);
            Run(s, 0.5);
            Assert.True(s.Tracker.HasEstimate);
            Assert.True(Vec2.Distance(s.Tracker.BestGuess, s.Player) < 1.5);
            Assert.True(s.Detections > 0);
        }

        [Fact]
        public void MirrorMakesTheCameraReportYourReflection()
        {
            // Camera looks west along y = 1.5. A mirror at x = 6 angled 45° shows it the player standing north of
            // the mirror, so it reports the player at the reflection: on its own axis, west of the mirror.
            var s = Make(@"
map:
####################
#....S.............#
#..................#
#..................#
#..................#
#.................a#
#.................X#
####################
entities:
camera a dir=180 range=16 fov=10
", TrackerKind.LastKnown, null, ToolKind.Mirror);
            // Player starts at (5.5, 6.5) and sneaks south; the mirror goes 2 m below them, angled like '\'
            // so the camera's westward line of sight bends north onto the player. The narrow cone can't see them directly.
            Run(s, 1.0, new StageInput { Move = new Vec2(0, -1), Mode = MoveMode.Sneak });
            var player = s.Player;
            var mirrorAt = new Vec2(player.X, 2.5);
            s.Step(new StageInput { UsePressed = true, Tool = ToolKind.Mirror, Aim = mirrorAt, MirrorAngle = -Math.PI / 4 });
            Assert.Single(s.Mirrors);
            Run(s, 0.5);
            Assert.Contains(ExploitId.MirrorSighting, s.Exploits.Found);
            // Reported far from the player, roughly on the camera's line of sight (y ≈ 2.5).
            Assert.True(Vec2.Distance(s.Tracker.BestGuess, s.Player) > 1.5);
        }

        [Fact]
        public void JammedCamerasReportNothingNotEvenNegativeInformation()
        {
            const string map = @"
map:
################
#S.a..........X#
################
entities:
camera a dir=0 range=10 fov=40
";
            // Without a jammer, walking into the cone gets you seen.
            var plain = Make(map);
            Run(plain, 3.0, new StageInput { Move = new Vec2(1, 0), Mode = MoveMode.Sneak });
            Assert.True(plain.Detections > 0);

            var s = Make(map, TrackerKind.OccupancyGrid, null, ToolKind.Jammer);
            s.Step(new StageInput { UsePressed = true, Tool = ToolKind.Jammer, Aim = new Vec2(3.5, 1.5) });
            Assert.Single(s.Jammers);
            Run(s, 3.0, new StageInput { Move = new Vec2(1, 0), Mode = MoveMode.Sneak });
            Assert.Equal(0, s.Detections);
            Assert.Contains(ExploitId.JammedSilence, s.Exploits.Found);
        }

        [Fact]
        public void HiddenPlayersAreInvisible()
        {
            var s = Make(@"
map:
##########
#SH...a.X#
##########
entities:
camera a dir=180 range=8 fov=90
");
            s.Step(new StageInput { InteractPressed = true });
            Assert.True(s.Hidden);
            int before = s.Detections;
            Run(s, 2.0);
            Assert.Equal(before, s.Detections);
        }

        [Fact]
        public void KeycardsOpenDoorsAndTerminalsNeedHolding()
        {
            var s = Make(@"
map:
############
#Sk..D..T.X#
######E#####
#..........#
############
entities:
keycard k name=red
door D key=red
terminal T opens=E
door E
");
            Run(s, 0.4, new StageInput { Move = new Vec2(1, 0) });
            s.Step(new StageInput { InteractPressed = true });
            Assert.Contains("red", s.Keys);
            Run(s, 2.0, new StageInput { Move = new Vec2(1, 0) });
            Assert.True(s.Doors[0].Open);
            // Walk to the terminal and hold.
            while (s.Player.X < 8.2) s.Step(new StageInput { Move = new Vec2(1, 0) });
            Run(s, 1.0, new StageInput { InteractHeld = true });
            Assert.False(s.Doors[1].Open);
            Run(s, 2.0, new StageInput { InteractHeld = true });
            Assert.True(s.Doors[1].Open);
            Assert.True(s.Terminals[0].Done);
        }

        [Fact]
        public void GuardsCatchAndYouRespawnAtTheCheckpoint()
        {
            var s = Make(@"
map:
############
#S.C....G.X#
############
entities:
guard G route=G
");
            while (s.Captures == 0 && s.Time < 20) s.Step(new StageInput { Move = new Vec2(1, 0) });
            Assert.Equal(1, s.Captures);
            Assert.True(Vec2.Distance(s.Player, new Vec2(3.5, 1.5)) < 0.01);
        }

        [Fact]
        public void ThrowingMakesNoiseWhereItLandsAndCanBePickedUpAgain()
        {
            var s = Make(@"
map:
################
#So............#
#..............#
#.........m...X#
################
entities:
mic m sensitivity=2
");
            Run(s, 0.3, new StageInput { Move = new Vec2(1, 0), Mode = MoveMode.Sneak });
            s.Step(new StageInput { InteractPressed = true });
            Assert.Equal(1, s.Carried);
            s.Step(new StageInput { UsePressed = true, Tool = ToolKind.Throw, Aim = new Vec2(9, 2) });
            Run(s, 1.0);
            Assert.Equal(0, s.Carried);
            Assert.True(s.Tracker.HasEstimate);
            Assert.True(Vec2.Distance(s.Tracker.BestGuess, new Vec2(9, 2)) < 2.5);
            Assert.Contains(s.Throwables, t => !t.Taken && Vec2.Distance(t.Position, new Vec2(9, 2)) < 1.0);
        }

        [Fact]
        public void RunningRunsOutOfStamina()
        {
            var s = Make(Corridor);
            Run(s, 4.0, new StageInput { Move = new Vec2(1, 0), Mode = MoveMode.Run });
            Assert.Equal(0, s.Stamina, 3);
            Assert.Equal(MoveMode.Walk, s.Mode);
        }

        [Fact]
        public void CampaignProgressRoundTripsAndUnlocksInOrder()
        {
            var campaign = CampaignDefinition.FromStages(BuiltInStages.LoadAll());
            var p = new CampaignProgress();
            Assert.True(p.IsUnlocked(campaign, campaign.Levels[0].Id));
            Assert.False(p.IsUnlocked(campaign, campaign.Levels[1].Id));

            var r = new StageResult { StageId = campaign.Levels[0].Id, Escaped = true, Time = 42.5, Captures = 0, Detections = 3 };
            r.Exploits.Add(ExploitId.SlipAway);
            r.ToolsFound.Add(ToolKind.Noisemaker);
            r.NotesRead.Add(2);
            var fresh = p.Apply(r, TrackerKind.LastKnown);
            Assert.Equal(new[] { ExploitId.SlipAway }, fresh.ToArray());
            Assert.True(p.IsUnlocked(campaign, campaign.Levels[1].Id));
            Assert.Equal(1, p.ExploitsFound(TrackerKind.LastKnown));

            var copy = CampaignProgress.Deserialize(p.Serialize());
            Assert.Equal(p.Serialize(), copy.Serialize());
            Assert.True(copy.Record(campaign.Levels[0].Id).Untouched);
            Assert.False(copy.Record(campaign.Levels[0].Id).Undetected);
            Assert.Contains(ToolKind.Noisemaker, copy.Tools);
            Assert.Equal(campaign.Levels[1].Id, copy.NextLevel(campaign).Id);
        }

        [Fact]
        public void EveryExploitHasJournalText()
        {
            foreach (ExploitId id in Enum.GetValues(typeof(ExploitId)))
            {
                var info = Exploits.Get(id);
                Assert.NotNull(info);
                Assert.False(string.IsNullOrWhiteSpace(info.WhyItWorks));
            }
            foreach (TrackerKind k in Enum.GetValues(typeof(TrackerKind)))
            {
                Assert.NotEmpty(Exploits.ForTracker(k));
                Assert.NotNull(TrackerSpecs.For(k).ProductName);
            }
        }

        private static string FindRepoDir(string relative)
        {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, relative))) dir = dir.Parent;
            Assert.NotNull(dir);
            return System.IO.Path.Combine(dir.FullName, relative);
        }

        [Fact]
        public void HuntersSweepAnUnsureBeliefWhilePatrolsKeepToTheirLeash()
        {
            // Player sealed in a closet on the left; a patrol and a hunter far away on the right.
            var stage = Make(@"
map:
##############################
#S#..........................#
###..........................#
#............................#
#.........................P.p#
#.........................K.k#
#...........................X#
##############################
entities:
guard P route=p
guard K route=k role=hunter
", TrackerKind.OccupancyGrid);
            // An unsure belief far from both units: too weak to dispatch anyone.
            stage.Tracker.ResetAround(new Vec2(5.5, 4.5), 2.5);
            for (int k = 0; k < 50; k++) stage.Step(new StageInput());
            var patrol = stage.Units[0];
            var hunter = stage.Units[1];
            Assert.False(patrol.Hunter);
            Assert.True(hunter.Hunter);
            Assert.Equal(GuardMode.Patrol, patrol.Mode);
            Assert.Equal(GuardMode.Investigate, hunter.Mode);
            Assert.True(Vec2.Distance(hunter.Goal, new Vec2(5.5, 4.5)) < 3.0, $"hunter goal {hunter.Goal}");

            // A confident belief beyond the patrol's leash: the patrol still ignores it.
            stage.Tracker.ResetAround(new Vec2(4.5, 3.5), 0.4);
            for (int k = 0; k < 50; k++) stage.Step(new StageInput());
            Assert.Equal(GuardMode.Patrol, patrol.Mode);
        }
    }
}
