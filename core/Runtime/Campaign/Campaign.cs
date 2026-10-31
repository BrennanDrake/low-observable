using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LowObservable.Core.Stages;
using LowObservable.Core.Tracking;

namespace LowObservable.Core.Campaign
{
    /// <summary>One level in the campaign's running order.</summary>
    public sealed class CampaignLevel
    {
        public string Id;
        public string Title;
        public string Zone;
        public TrackerKind Tracker;
        public int Number;
    }

    /// <summary>
    /// The campaign: levels in order, grouped by zone, each against a WARDEN tracker. The list is built from
    /// the stage files themselves so titles and trackers can't drift out of sync.
    /// </summary>
    public sealed class CampaignDefinition
    {
        public readonly List<CampaignLevel> Levels = new List<CampaignLevel>();

        public static CampaignDefinition FromStages(IEnumerable<StageDefinition> stagesInOrder)
        {
            var c = new CampaignDefinition();
            foreach (var s in stagesInOrder)
                c.Levels.Add(new CampaignLevel { Id = s.Id, Title = s.Title, Zone = s.Zone, Tracker = s.Tracker, Number = c.Levels.Count + 1 });
            return c;
        }

        public CampaignLevel Get(string id) => Levels.Find(l => l.Id == id);

        public int IndexOf(string id) => Levels.FindIndex(l => l.Id == id);
    }

    /// <summary>Best marks for one level.</summary>
    public sealed class LevelRecord
    {
        public bool Escaped;
        public bool Undetected;
        public bool Untouched;
        public double BestTime = double.PositiveInfinity;

        public int Marks => (Escaped ? 1 : 0) + (Undetected ? 1 : 0) + (Untouched ? 1 : 0);
    }

    /// <summary>
    /// What the player has achieved: levels cleared and their best marks, tools found, exploits discovered
    /// (the exploit journal), story notes read. Saved as plain "key=value" lines so it needs no JSON library
    /// in the core and diffs nicely.
    /// </summary>
    public sealed class CampaignProgress
    {
        public readonly Dictionary<string, LevelRecord> Levels = new Dictionary<string, LevelRecord>();
        public readonly HashSet<ToolKind> Tools = new HashSet<ToolKind>();
        public readonly HashSet<ExploitId> Exploits = new HashSet<ExploitId>();
        /// <summary>Notes read, as "stageId:noteIndex".</summary>
        public readonly HashSet<string> Notes = new HashSet<string>();
        /// <summary>Trackers whose spec sheet the player has seen.</summary>
        public readonly HashSet<TrackerKind> TrackersMet = new HashSet<TrackerKind>();

        public LevelRecord Record(string id) => Levels.TryGetValue(id, out var r) ? r : null;

        public bool IsCleared(string id) => Levels.TryGetValue(id, out var r) && r.Escaped;

        /// <summary>The first level is always open; each later level opens when the one before it is cleared.</summary>
        public bool IsUnlocked(CampaignDefinition campaign, string id)
        {
            int i = campaign.IndexOf(id);
            if (i < 0) return false;
            return i == 0 || IsCleared(campaign.Levels[i - 1].Id);
        }

        /// <summary>The level to offer on "Continue": the first one not cleared yet, or the last.</summary>
        public CampaignLevel NextLevel(CampaignDefinition campaign)
        {
            foreach (var l in campaign.Levels) if (!IsCleared(l.Id)) return l;
            return campaign.Levels.Count > 0 ? campaign.Levels[campaign.Levels.Count - 1] : null;
        }

        /// <summary>Folds a finished level into the progress. Returns the exploits that are new to the journal.</summary>
        public List<ExploitId> Apply(StageResult result, TrackerKind tracker)
        {
            var newExploits = new List<ExploitId>();
            TrackersMet.Add(tracker);
            foreach (var t in result.ToolsFound) Tools.Add(t);
            foreach (var e in result.Exploits) if (Exploits.Add(e)) newExploits.Add(e);
            foreach (var n in result.NotesRead) Notes.Add(result.StageId + ":" + n.ToString(CultureInfo.InvariantCulture));
            if (!result.Escaped) return newExploits;
            if (!Levels.TryGetValue(result.StageId, out var rec)) Levels[result.StageId] = rec = new LevelRecord();
            rec.Escaped = true;
            rec.Undetected |= result.Undetected;
            rec.Untouched |= result.Untouched;
            if (result.Time < rec.BestTime) rec.BestTime = result.Time;
            return newExploits;
        }

        public int ExploitsFound(TrackerKind kind)
        {
            int n = 0;
            foreach (var e in Tracking.ExploitsFor(kind)) if (Exploits.Contains(e)) n++;
            return n;
        }

        // ---------------------------------------------------------------- Save format

        public string Serialize()
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("version=1\n");
            var ids = new List<string>(Levels.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                var r = Levels[id];
                sb.Append("level=").Append(id).Append(',')
                  .Append(r.Escaped ? 1 : 0).Append(',')
                  .Append(r.Undetected ? 1 : 0).Append(',')
                  .Append(r.Untouched ? 1 : 0).Append(',')
                  .Append(double.IsInfinity(r.BestTime) ? "-" : r.BestTime.ToString("0.00", c)).Append('\n');
            }
            foreach (var t in Sorted(Tools)) sb.Append("tool=").Append(ToolInfo.Id(t)).Append('\n');
            foreach (var e in Sorted(Exploits)) sb.Append("exploit=").Append(e).Append('\n');
            foreach (var t in Sorted(TrackersMet)) sb.Append("tracker=").Append(t).Append('\n');
            var notes = new List<string>(Notes);
            notes.Sort(StringComparer.Ordinal);
            foreach (var n in notes) sb.Append("note=").Append(n).Append('\n');
            return sb.ToString();
        }

        public static CampaignProgress Deserialize(string text)
        {
            var p = new CampaignProgress();
            if (string.IsNullOrEmpty(text)) return p;
            var c = CultureInfo.InvariantCulture;
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                string key = raw.Substring(0, eq), value = raw.Substring(eq + 1);
                switch (key)
                {
                    case "level":
                        var parts = value.Split(',');
                        if (parts.Length < 5) break;
                        p.Levels[parts[0]] = new LevelRecord
                        {
                            Escaped = parts[1] == "1",
                            Undetected = parts[2] == "1",
                            Untouched = parts[3] == "1",
                            BestTime = parts[4] == "-" ? double.PositiveInfinity : double.Parse(parts[4], c),
                        };
                        break;
                    case "tool":
                        if (ToolInfo.TryParse(value, out var tool)) p.Tools.Add(tool);
                        break;
                    case "exploit":
                        if (Enum.TryParse(value, out ExploitId e)) p.Exploits.Add(e);
                        break;
                    case "tracker":
                        if (Enum.TryParse(value, out TrackerKind k)) p.TrackersMet.Add(k);
                        break;
                    case "note":
                        p.Notes.Add(value);
                        break;
                }
            }
            return p;
        }

        private static List<T> Sorted<T>(IEnumerable<T> items)
        {
            var l = new List<T>(items);
            l.Sort();
            return l;
        }

        private static class Tracking
        {
            public static IEnumerable<ExploitId> ExploitsFor(TrackerKind kind)
            {
                foreach (var info in Stages.Exploits.ForTracker(kind)) yield return info.Id;
            }
        }
    }
}
