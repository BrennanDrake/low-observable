using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using LowObservable.Core.Maths;
using LowObservable.Core.Stages;
using LowObservable.Core.Tracking;

namespace LowObservable.Sim
{
    /// <summary>
    /// Headless runner for the escape campaign:
    ///
    ///   dotnet run --project dotnet/LowObservable.Sim -- stage --stage all --policy all --seeds 10
    ///   dotnet run --project dotnet/LowObservable.Sim -- stage --check                 (reachability, sensors off)
    ///   dotnet run --project dotnet/LowObservable.Sim -- stage --stage 04-open-plan --policy sneaker --dump build/frames
    /// </summary>
    public static class StageCli
    {
        private const string Usage =
@"LowObservable.Sim stage - headless runs of the escape levels

  --dir PATH          stage files (default levels/stages)
  --stage ID|all      one stage id (file name without .lvl) or all (default)
  --policy LIST       walker, sneaker, runner, comma-separated, or all (default)
  --seeds N           runs per policy (default 10)
  --seed-start N      first seed (default 1)
  --tracker KIND      play every stage against this tracker instead (lastknown|grid|kalman|particle)
  --tools LIST        tools the player owns from the start, comma-separated (default: all earlier pickups)
  --set NAME=VALUE    override a StageTuning field (repeatable)
  --check             reachability check: walker with every sensor off must escape each stage
  --dump DIR          write a JSON-lines frame dump of the first seed per policy (for tools/render_stage.py)";

        public static int Run(string[] args)
        {
            string dir = "levels/stages", only = "all", policyArg = "all", dump = null, toolsArg = null;
            int seeds = 10;
            ulong seedStart = 1;
            bool check = false;
            TrackerKind? trackerOverride = null;
            var overrides = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < args.Length; i++)
            {
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
                switch (args[i])
                {
                    case "--dir": dir = Next(); break;
                    case "--stage": only = Next(); break;
                    case "--policy": policyArg = Next(); break;
                    case "--seeds": seeds = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--seed-start": seedStart = ulong.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--tracker": trackerOverride = StageParser.ParseTracker(Next()); break;
                    case "--tools": toolsArg = Next(); break;
                    case "--set":
                        var parts = Next().Split('=');
                        overrides.Add(new KeyValuePair<string, string>(parts[0], parts[1]));
                        break;
                    case "--check": check = true; break;
                    case "--dump": dump = Next(); break;
                    case "-h":
                    case "--help": Console.WriteLine(Usage); return 0;
                    default: throw new ArgumentException($"Unknown option {args[i]}\n{Usage}");
                }
            }

            var files = Directory.GetFiles(dir, "*.lvl").OrderBy(f => f, StringComparer.Ordinal).ToList();
            var stages = files.Select(f => StageParser.Parse(Path.GetFileNameWithoutExtension(f), File.ReadAllText(f))).ToList();

            // Tools found in earlier levels are owned in later ones, as in the campaign.
            var owned = new Dictionary<string, List<ToolKind>>();
            var soFar = new List<ToolKind>();
            foreach (var s in stages)
            {
                owned[s.Id] = new List<ToolKind>(soFar);
                foreach (var e in s.OfType(EntityType.Pickup)) { ToolInfo.TryParse(e.Get("tool"), out var t); soFar.Add(t); }
            }

            if (only != "all") stages = stages.Where(s => s.Id == only).ToList();
            if (stages.Count == 0) { Console.Error.WriteLine($"No stage '{only}' in {dir}."); return 2; }

            if (check) return Check(stages, owned);

            var policies = policyArg == "all" ? new[] { BotPolicy.Walker, BotPolicy.Sneaker, BotPolicy.Runner }
                : policyArg.Split(',').Select(p => (BotPolicy)Enum.Parse(typeof(BotPolicy), p, true)).ToArray();
            if (dump != null) Directory.CreateDirectory(dump);

            Console.WriteLine("stage                 tracker      policy   escaped  avg_time  captures  detected  exposure  exploits");
            foreach (var def in stages)
            {
                foreach (var policy in policies)
                {
                    int escaped = 0;
                    double time = 0, captures = 0, detections = 0, exposure = 0;
                    var exploits = new HashSet<ExploitId>();
                    for (int k = 0; k < seeds; k++)
                    {
                        var tuning = StageTuning.Default();
                        foreach (var kv in overrides) ApplyOverride(tuning, kv.Key, kv.Value);
                        var tools = toolsArg != null ? ParseTools(toolsArg) : owned[def.Id];
                        var stage = new Stage(def, new StageConfig { Seed = seedStart + (ulong)k, Tuning = tuning, UnlockedTools = tools, TrackerOverride = trackerOverride });
                        var bot = new StageBot(stage, policy);
                        StreamWriter w = dump != null && k == 0 ? new StreamWriter(Path.Combine(dump, $"{def.Id}_{policy.ToString().ToLowerInvariant()}.jsonl")) : null;
                        double frameTimer = 0;
                        while (stage.Outcome == StageOutcome.Running && stage.Time < 400)
                        {
                            stage.Step(bot.NextInput());
                            if (w == null) continue;
                            frameTimer -= Stage.Dt;
                            if (frameTimer <= 0) { frameTimer = 0.2; w.WriteLine(Frame(stage)); }
                        }
                        w?.Dispose();
                        var r = StageResult.From(stage);
                        if (r.Escaped) escaped++;
                        time += r.Time;
                        captures += r.Captures;
                        detections += r.Detections;
                        exposure += r.AverageExposure;
                        foreach (var e in r.Exploits) exploits.Add(e);
                    }
                    double n = seeds;
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-21} {1,-12} {2,-8} {3,7:P0} {4,8:0.0}s {5,9:0.0} {6,9:0.0} {7,9:0.00}  {8}",
                        def.Id, (trackerOverride ?? def.Tracker).ToString(), policy.ToString().ToLowerInvariant(), escaped / n, time / n, captures / n, detections / n, exposure / n,
                        string.Join(",", exploits.OrderBy(e => e))));
                }
            }
            return 0;
        }

        private static int Check(List<StageDefinition> stages, Dictionary<string, List<ToolKind>> owned)
        {
            int failures = 0;
            foreach (var def in stages)
            {
                var t = StageTuning.Default();
                t.CameraRange = 0;
                t.GuardVisionRange = 0;
                t.MicRadius = 0;
                t.UnitHearing = 0;
                t.RoombaBumpRange = 0;
                t.CaptureRadius = 0;
                var stage = new Stage(def, new StageConfig { Seed = 1, Tuning = t, UnlockedTools = owned[def.Id] });
                var bot = new StageBot(stage, BotPolicy.Walker);
                bool ok = bot.RunToEnd(400);
                Console.WriteLine($"{def.Id,-24} {(ok ? "ok" : "FAILED")}  {stage.Time,6:0.0}s  {(ok ? "" : "stuck heading for marker '" + bot.CurrentGoalMarker + "'")}");
                if (!ok) failures++;
            }
            return failures == 0 ? 0 : 1;
        }

        private static List<ToolKind> ParseTools(string s)
        {
            var list = new List<ToolKind>();
            foreach (var p in s.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (ToolInfo.TryParse(p, out var t)) list.Add(t);
            return list;
        }

        private static void ApplyOverride(StageTuning tuning, string name, string value)
        {
            var field = typeof(StageTuning).GetField(name) ?? throw new ArgumentException($"StageTuning has no field '{name}'.");
            object parsed = field.FieldType == typeof(int) ? int.Parse(value, CultureInfo.InvariantCulture) : (object)double.Parse(value, CultureInfo.InvariantCulture);
            field.SetValue(tuning, parsed);
        }

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        private static string P(Vec2 v) => "[" + F(v.X) + "," + F(v.Y) + "]";

        /// <summary>One frame of state for the offline renderer. Hand-written JSON keeps the core dependency-free.</summary>
        private static string Frame(Stage s)
        {
            var sb = new StringBuilder();
            sb.Append("{\"t\":").Append(F(s.Time));
            sb.Append(",\"player\":").Append(P(s.Player)).Append(",\"hidden\":").Append(s.Hidden ? "true" : "false");
            sb.Append(",\"units\":[").Append(string.Join(",", s.Units.Select(u => "[" + F(u.Pose.Position.X) + "," + F(u.Pose.Position.Y) + "," + F(u.LookHeading) + ",\"" + u.Mode + "\"]"))).Append(']');
            sb.Append(",\"cameras\":[").Append(string.Join(",", s.Cameras.Select(c => "[" + F(c.Position.X) + "," + F(c.Position.Y) + "," + F(c.Heading) + "," + F(c.FieldOfView) + "," + F(c.Range) + "]"))).Append(']');
            sb.Append(",\"roombas\":[").Append(string.Join(",", s.Roombas.Select(r => P(r.Position)))).Append(']');
            sb.Append(",\"mirrors\":[").Append(string.Join(",", s.Mirrors.Select(m => "[" + F(m.A.X) + "," + F(m.A.Y) + "," + F(m.B.X) + "," + F(m.B.Y) + "]"))).Append(']');
            sb.Append(",\"jammers\":[").Append(string.Join(",", s.Jammers.Select(j => P(j.Position)))).Append(']');
            sb.Append(",\"decoys\":[").Append(string.Join(",", s.Noisemakers.Select(n => P(n.Position)))).Append(']');
            sb.Append(",\"doors\":[").Append(string.Join(",", s.Doors.Where(d => d.Open).Select(d => "[" + d.I + "," + d.J + "]"))).Append(']');
            sb.Append(",\"dark\":[").Append(string.Join(",", DarkCells(s))).Append(']');
            sb.Append(",\"belief\":{\"kind\":\"").Append(s.Tracker.Kind).Append("\",\"has\":").Append(s.Tracker.HasEstimate ? "true" : "false");
            switch (s.Tracker)
            {
                case LastKnownTracker lk:
                    sb.Append(",\"point\":").Append(P(lk.BestGuess));
                    break;
                case KalmanTracker kf:
                    var e = kf.Ellipse;
                    sb.Append(",\"ellipse\":[").Append(F(e.Center.X)).Append(',').Append(F(e.Center.Y)).Append(',').Append(F(e.SemiMajor)).Append(',').Append(F(e.SemiMinor)).Append(',').Append(F(e.Orientation)).Append(']');
                    sb.Append(",\"velocity\":").Append(P(kf.Velocity));
                    break;
                case OccupancyGridTracker g:
                    var cells = new List<string>();
                    for (int i = 0; i < g.Width; i++)
                        for (int j = 0; j < g.Height; j++)
                            if (g.Probability(i, j) > 0.002) cells.Add("[" + i + "," + j + "," + F(g.Probability(i, j)) + "]");
                    sb.Append(",\"cells\":[").Append(string.Join(",", cells)).Append(']');
                    break;
                case ParticleTracker pt:
                    var pos = pt.Filter.Positions;
                    var pts = new List<string>();
                    for (int k = 0; k < pos.Count; k += 2) pts.Add(P(pos[k]));
                    sb.Append(",\"particles\":[").Append(string.Join(",", pts)).Append(']');
                    break;
            }
            sb.Append('}');
            var texts = s.Events.Where(e => e.Type != StageEventType.Footstep && e.Type != StageEventType.Heard && e.Type != StageEventType.TerminalProgress).Select(e => "\"" + (e.Type + ": " + (e.Text ?? "")).Replace("\"", "'") + "\"");
            sb.Append(",\"events\":[").Append(string.Join(",", texts)).Append(']');
            var rings = s.Events.Where(e => e.Radius > 0 && (e.Type == StageEventType.Footstep || e.Type == StageEventType.Noise || e.Type == StageEventType.ThrowLanded || e.Type == StageEventType.TerminalProgress || e.Type == StageEventType.DoorOpened))
                .Select(e => "[" + F(e.Position.X) + "," + F(e.Position.Y) + "," + F(e.Radius) + "]");
            sb.Append(",\"rings\":[").Append(string.Join(",", rings)).Append(']');
            sb.Append('}');
            return sb.ToString();
        }

        private static IEnumerable<string> DarkCells(Stage s)
        {
            for (int i = 0; i < s.Plan.Width; i++)
                for (int j = 0; j < s.Plan.Height; j++)
                    if (!s.Plan.IsWall(i, j) && s.Plan.IsDarkAt(s.Plan.CellCenter(i, j))) yield return "[" + i + "," + j + "]";
        }
    }
}
