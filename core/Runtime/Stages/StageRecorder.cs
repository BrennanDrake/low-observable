using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LowObservable.Core.Maths;
using LowObservable.Core.Tracking;

namespace LowObservable.Core.Stages
{
    /// <summary>One replay frame: the truth and WARDEN's belief side by side.</summary>
    public struct StageSample
    {
        public double Time;
        public Vec2 Player;
        public bool Hidden;
        public bool HasEstimate;
        public Vec2 BestGuess;
        public double Spread;
        public double Exposure;
        public Vec2[] Units;
    }

    /// <summary>Records a stage for the results screen, the replay scrubber and the headless runner.</summary>
    public sealed class StageRecorder
    {
        public readonly List<StageSample> Samples = new List<StageSample>();
        public readonly List<(double time, StageEventType type, Vec2 pos, string text)> EventLog = new List<(double, StageEventType, Vec2, string)>();
        public int Reports { get; private set; }
        public int RejectedReports { get; private set; }
        public int Captures { get; private set; }

        public void Sample(Stage stage)
        {
            var units = new Vec2[stage.Units.Count];
            for (int k = 0; k < units.Length; k++) units[k] = stage.Units[k].Pose.Position;
            Samples.Add(new StageSample
            {
                Time = stage.Time,
                Player = stage.Player,
                Hidden = stage.Hidden,
                HasEstimate = stage.Tracker.HasEstimate,
                BestGuess = stage.Tracker.BestGuess,
                Spread = stage.Tracker.Spread,
                Exposure = stage.Exposure,
                Units = units,
            });
        }

        public void CountReport(FixResult result)
        {
            Reports++;
            if (result == FixResult.Rejected) RejectedReports++;
        }

        public void CountCapture() => Captures++;

        public void Log(StageEventType type, double time, Vec2 pos, string text)
        {
            // Footsteps are too frequent to be interesting in the log.
            if (type == StageEventType.Footstep) return;
            EventLog.Add((time, type, pos, text));
        }

        public double AverageExposure
        {
            get
            {
                if (Samples.Count == 0) return 0;
                double s = 0;
                foreach (var x in Samples) s += x.Exposure;
                return s / Samples.Count;
            }
        }

        public string SamplesCsv()
        {
            var sb = new StringBuilder("t,px,py,hidden,has_estimate,gx,gy,spread,exposure\n");
            var c = CultureInfo.InvariantCulture;
            foreach (var s in Samples)
            {
                sb.Append(string.Format(c, "{0:0.00},{1:0.000},{2:0.000},{3},{4},{5:0.000},{6:0.000},{7:0.000},{8:0.0000}\n",
                    s.Time, s.Player.X, s.Player.Y, s.Hidden ? 1 : 0, s.HasEstimate ? 1 : 0, s.BestGuess.X, s.BestGuess.Y,
                    double.IsInfinity(s.Spread) ? -1 : s.Spread, s.Exposure));
            }
            return sb.ToString();
        }
    }

    /// <summary>The results screen for one level.</summary>
    public sealed class StageResult
    {
        public string StageId;
        public bool Escaped;
        public double Time;
        public int Captures;
        public int Detections;
        public double AverageExposure;
        public readonly List<ExploitId> Exploits = new List<ExploitId>();
        public readonly List<int> NotesRead = new List<int>();
        public readonly List<ToolKind> ToolsFound = new List<ToolKind>();

        /// <summary>No sensor ever detected you (hearing doesn't count).</summary>
        public bool Undetected => Detections == 0;

        /// <summary>Never caught.</summary>
        public bool Untouched => Captures == 0;

        public static StageResult From(Stage stage)
        {
            var r = new StageResult
            {
                StageId = stage.Definition.Id,
                Escaped = stage.Outcome == StageOutcome.Escaped,
                Time = stage.Time,
                Captures = stage.Captures,
                Detections = stage.Detections,
                AverageExposure = stage.Recorder.AverageExposure,
            };
            r.Exploits.AddRange(stage.Exploits.Found);
            r.Exploits.Sort();
            foreach (var n in stage.Notes) if (n.Read) r.NotesRead.Add(n.Index);
            r.ToolsFound.AddRange(stage.ToolsFoundHere);
            return r;
        }
    }
}
