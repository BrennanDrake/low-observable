using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LowObservable.Core.Levels;
using LowObservable.Core.Simulation;
using LowObservable.Core.World;

namespace LowObservable.Sim
{
    /// <summary>
    /// Headless simulator: runs autopilot episodes without Unity and reports outcomes. Use it to check
    /// balance after a tuning change, to generate run logs for tools/plot_run.py, and in CI.
    ///
    ///   dotnet run --project dotnet/LowObservable.Sim -- --level annex-b --policy all --seeds 50
    ///   dotnet run --project dotnet/LowObservable.Sim -- --policy ghost --seeds 1 --seed-start 7 --out experiments/runs
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "stage") return StageCli.Run(args.Skip(1).ToArray());
            var opts = Options.Parse(args);
            if (opts.Help)
            {
                Console.WriteLine(Options.Usage);
                return 0;
            }

            FloorPlan plan = opts.LevelFile != null
                ? FloorPlanParser.Parse(Path.GetFileNameWithoutExtension(opts.LevelFile), File.ReadAllText(opts.LevelFile))
                : BuiltInLevels.Load(opts.Level);

            var policies = opts.Policy == "all"
                ? new[] { "reckless", "cautious", "ghost", "passive" }
                : opts.Policy.Split(',');

            if (opts.OutDir != null) Directory.CreateDirectory(opts.OutDir);
            TextWriter summaryFile = opts.OutDir != null ? new StreamWriter(Path.Combine(opts.OutDir, "summary.csv")) : null;
            summaryFile?.WriteLine("policy," + RunRecorder.SummaryHeader);

            Console.WriteLine($"Level {plan.Name} ({plan.Width}x{plan.Height}, {plan.Landmarks.Count} landmarks), seeds {opts.SeedStart}..{opts.SeedStart + (ulong)opts.Seeds - 1}");
            Console.WriteLine("policy      escaped  captured  timeout   avg_time  avg_err  inside95  wrong_assoc  pings  seen");

            foreach (var policy in policies)
            {
                var summaries = new List<RunSummary>();
                for (int k = 0; k < opts.Seeds; k++)
                {
                    ulong seed = opts.SeedStart + (ulong)k;
                    var tuning = TuningParameters.Default();
                    foreach (var kv in opts.Overrides) ApplyOverride(tuning, kv.Key, kv.Value);
                    var episode = new Episode(plan, new EpisodeConfig { Seed = seed, Record = true, Tuning = tuning, SeekerStartsAlerted = opts.Alerted });
                    var pilot = new Autopilot(plan, AutopilotProfile.ByName(policy));
                    pilot.RunToEnd(episode);
                    var s = episode.Recorder.Summary;
                    summaries.Add(s);
                    summaryFile?.WriteLine(policy + "," + RunRecorder.SummaryRow(s));

                    if (opts.OutDir != null && opts.WriteRuns)
                    {
                        string stem = Path.Combine(opts.OutDir, $"{plan.Name}_{policy}_seed{seed}");
                        using (var w = new StreamWriter(stem + "_samples.csv")) episode.Recorder.WriteSamplesCsv(w);
                        using (var w = new StreamWriter(stem + "_events.csv")) episode.Recorder.WriteEventsCsv(w);
                    }
                }
                PrintRow(policy, summaries);
            }

            summaryFile?.Dispose();
            if (opts.OutDir != null) Console.WriteLine($"Wrote results to {opts.OutDir}");
            return 0;
        }

        /// <summary>Sets a public TuningParameters field by name, e.g. PingLoudness=16. Angles are in radians.</summary>
        private static void ApplyOverride(TuningParameters tuning, string name, string value)
        {
            var field = typeof(TuningParameters).GetField(name);
            if (field == null) throw new ArgumentException($"TuningParameters has no field '{name}'.");
            object parsed = field.FieldType == typeof(int)
                ? int.Parse(value, CultureInfo.InvariantCulture)
                : (object)double.Parse(value, CultureInfo.InvariantCulture);
            field.SetValue(tuning, parsed);
        }

        private static void PrintRow(string policy, List<RunSummary> s)
        {
            double n = s.Count;
            string Pct(EpisodeOutcome o) => (s.Count(x => x.Outcome == o) / n).ToString("P0", CultureInfo.InvariantCulture);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-10} {1,8} {2,9} {3,8} {4,9:0.0}s {5,7:0.00}m {6,8:P0} {7,12:0.0} {8,6:0.0} {9,5:0.0}",
                policy, Pct(EpisodeOutcome.Escaped), Pct(EpisodeOutcome.Captured), Pct(EpisodeOutcome.TimedOut),
                s.Average(x => x.Duration), s.Average(x => x.AverageError), s.Average(x => x.FractionInsideEllipse),
                s.Average(x => (double)x.WrongAssociations), s.Average(x => (double)x.Pings), s.Average(x => (double)x.TimesSeen)));
        }

        private sealed class Options
        {
            public string Level = "annex-b";
            public string LevelFile;
            public string Policy = "all";
            public int Seeds = 20;
            public ulong SeedStart = 1;
            public string OutDir;
            public bool WriteRuns = true;
            public bool Help;
            public bool Alerted;
            public List<KeyValuePair<string, string>> Overrides = new List<KeyValuePair<string, string>>();

            public const string Usage =
@"LowObservable.Sim - headless episodes with the autopilot

  --level NAME        built-in level: annex-b (default) or open-room
  --level-file PATH   load an ASCII level file instead
  --policy LIST       reckless, cautious, ghost, passive, comma-separated, or 'all' (default)
  --seeds N           episodes per policy (default 20)
  --seed-start N      first seed (default 1)
  --out DIR           write summary.csv and per-run samples/events CSVs to DIR
  --summary-only      with --out, skip the per-run CSVs
  --set NAME=VALUE    override a TuningParameters field (repeatable), e.g. --set PingLoudness=16
  --alerted           the Seeker starts knowing roughly where you entered";

            public static Options Parse(string[] args)
            {
                var o = new Options();
                for (int i = 0; i < args.Length; i++)
                {
                    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
                    switch (args[i])
                    {
                        case "--level": o.Level = Next(); break;
                        case "--level-file": o.LevelFile = Next(); break;
                        case "--policy": o.Policy = Next(); break;
                        case "--seeds": o.Seeds = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                        case "--seed-start": o.SeedStart = ulong.Parse(Next(), CultureInfo.InvariantCulture); break;
                        case "--out": o.OutDir = Next(); break;
                        case "--summary-only": o.WriteRuns = false; break;
                        case "--alerted": o.Alerted = true; break;
                        case "--set":
                            var parts = Next().Split('=');
                            if (parts.Length != 2) throw new ArgumentException("--set expects NAME=VALUE");
                            o.Overrides.Add(new KeyValuePair<string, string>(parts[0], parts[1]));
                            break;
                        case "-h":
                        case "--help": o.Help = true; break;
                        default: throw new ArgumentException($"Unknown option {args[i]}\n{Usage}");
                    }
                }
                return o;
            }
        }
    }
}
