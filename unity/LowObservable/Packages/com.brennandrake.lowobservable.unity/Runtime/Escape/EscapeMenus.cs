using System.Collections.Generic;
using LowObservable.Core.Campaign;
using LowObservable.Core.Stages;
using LowObservable.Core.Tracking;
using UnityEngine;

namespace LowObservable.Unity.Escape
{
    /// <summary>
    /// Everything outside a level (IMGUI placeholder): the campaign menu with level select, the exploit
    /// journal and the WARDEN codex; the pre-level briefing (the algorithm's spec sheet); pause; and the
    /// results screen with marks and new journal entries.
    /// </summary>
    public sealed class EscapeMenus : MonoBehaviour
    {
        public EscapeGame Game;

        private enum Tab { Levels, Journal, Codex, Settings }

        private readonly EscapeStyles _s = new EscapeStyles();
        private Tab _tab = Tab.Levels;
        private Vector2 _scroll;
        private bool _confirmReset;

        private void Start()
        {
            if (Game == null) Game = FindAnyObjectByType<EscapeGame>();
        }

        private void OnGUI()
        {
            _s.Ensure();
            switch (Game.Screen)
            {
                case EscapeScreen.Menu: DrawMenu(); break;
                case EscapeScreen.Briefing: DrawBriefing(); break;
                case EscapeScreen.Paused: DrawPause(); break;
                case EscapeScreen.Results: DrawResults(); break;
            }
        }

        private Rect CenterRect(float w, float h)
        {
            w = Mathf.Min(_s.Px(w), Screen.width - _s.Px(24));
            h = Mathf.Min(_s.Px(h), Screen.height - _s.Px(24));
            return new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        }

        // ---------------------------------------------------------------- campaign menu

        private void DrawMenu()
        {
            var area = CenterRect(860, 820);
            GUILayout.BeginArea(area);
            GUILayout.BeginVertical(_s.Panel);
            GUILayout.Label("LOW OBSERVABLE", _s.Title);
            GUILayout.Label("<color=#999>Halvorsen Correctional & Research. You always know where you are. WARDEN never does.</color>", _s.Center);
            GUILayout.Space(_s.Px(8));
            GUILayout.BeginHorizontal();
            TabButton(Tab.Levels, "Escape");
            TabButton(Tab.Journal, $"Exploit journal ({Game.Progress.Exploits.Count}/{Exploits.All.Length})");
            TabButton(Tab.Codex, "WARDEN codex");
            TabButton(Tab.Settings, "Save");
            GUILayout.EndHorizontal();
            GUILayout.Space(_s.Px(6));
            _scroll = GUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Levels: DrawLevels(); break;
                case Tab.Journal: DrawJournal(); break;
                case Tab.Codex: DrawCodex(); break;
                case Tab.Settings: DrawSettings(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void TabButton(Tab tab, string label)
        {
            if (GUILayout.Button(_tab == tab ? $"<b>{label}</b>" : $"<color=#AAA>{label}</color>", _tab == tab ? _s.Selected : _s.Button))
            {
                _tab = tab;
                _scroll = Vector2.zero;
            }
        }

        private void DrawLevels()
        {
            var progress = Game.Progress;
            string zone = null;
            foreach (var level in Game.Campaign.Levels)
            {
                if (level.Zone != zone)
                {
                    zone = level.Zone;
                    GUILayout.Space(_s.Px(6));
                    GUILayout.Label($"<b>{zone.ToUpperInvariant()}</b>", _s.Heading);
                }
                bool unlocked = Game.IsUnlocked(level.Id);
                var rec = progress.Record(level.Id);
                string tracker = progress.TrackersMet.Contains(level.Tracker) || unlocked ? EscapeStyles.TrackerShort(level.Tracker) : "???";
                string marks = rec != null ? EscapeStyles.Marks(rec.Escaped, rec.Undetected, rec.Untouched) + $"  <color=#888>best {EscapeStyles.FormatTime(rec.BestTime)}</color>" : "";
                GUILayout.BeginHorizontal();
                GUI.enabled = unlocked;
                string label = unlocked
                    ? $"<b>{level.Number}. {level.Title}</b>   <color=#FF7080>{tracker}</color>\n<size={_s.Px(12)}>{marks}</size>"
                    : $"<color=#666>{level.Number}. Locked</color>";
                if (GUILayout.Button(label, _s.Button, GUILayout.MinHeight(_s.Px(48)))) Game.Begin(level.Id);
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                // Sandbox: replay a cleared level against any algorithm you have met. Same room, different maths.
                if (rec != null && rec.Escaped && progress.TrackersMet.Count > 1)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("<color=#888>   Replay against:</color>", _s.Small, GUILayout.Width(_s.Px(130)));
                    foreach (TrackerKind kind in System.Enum.GetValues(typeof(TrackerKind)))
                    {
                        if (kind == level.Tracker || !progress.TrackersMet.Contains(kind)) continue;
                        if (GUILayout.Button($"<size={_s.Px(12)}>{EscapeStyles.TrackerShort(kind)}</size>", _s.Button, GUILayout.ExpandWidth(false))) Game.Begin(level.Id, kind);
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.Space(_s.Px(10));
            GUILayout.Label("<color=#888>Planned after the labs: the server hall, where WARDEN runs ATLAS (SLAM) and maps the building while it hunts you.</color>", _s.Small);
        }

        private void DrawJournal()
        {
            GUILayout.Label("Every algorithm has assumptions. Each entry is one you broke, and the name the textbooks give it.", _s.Small);
            foreach (TrackerKind kind in System.Enum.GetValues(typeof(TrackerKind)))
            {
                GUILayout.Space(_s.Px(8));
                bool met = Game.Progress.TrackersMet.Contains(kind);
                GUILayout.Label(met ? $"<color=#FF7080><b>{EscapeStyles.TrackerShort(kind)}</b></color>  <color=#888>{TrackerSpecs.For(kind).AlgorithmName}</color>" : "<color=#666><b>Unknown model</b></color>", _s.Heading);
                foreach (var info in Exploits.ForTracker(kind)) JournalEntry(info);
            }
            var general = new List<ExploitInfo>();
            foreach (var info in Exploits.All) if (!info.Tracker.HasValue) general.Add(info);
            if (general.Count > 0)
            {
                GUILayout.Space(_s.Px(8));
                GUILayout.Label("<b>Any model</b>", _s.Heading);
                foreach (var info in general) JournalEntry(info);
            }
        }

        private void JournalEntry(ExploitInfo info)
        {
            bool known = Game.Progress.Exploits.Contains(info.Id);
            GUILayout.BeginVertical(_s.Box);
            if (known)
            {
                GUILayout.Label($"<color=#7CFFA0><b>{info.Title}</b></color>   <i>{info.Term}</i>", _s.Label);
                GUILayout.Label(info.WhatHappened, _s.Small);
                GUILayout.Label(info.WhyItWorks, _s.Small);
            }
            else
            {
                GUILayout.Label("<color=#666>?  Undiscovered</color>", _s.Label);
            }
            GUILayout.EndVertical();
        }

        private void DrawCodex()
        {
            GUILayout.Label("Halvorsen's product sheets for each WARDEN model you have faced. Read them for the weak spots.", _s.Small);
            foreach (TrackerKind kind in System.Enum.GetValues(typeof(TrackerKind)))
            {
                GUILayout.Space(_s.Px(8));
                GUILayout.BeginVertical(_s.Box);
                if (Game.Progress.TrackersMet.Contains(kind)) EscapeHud.DrawSpecBody(_s, TrackerSpecs.For(kind), Game);
                else GUILayout.Label("<color=#666><b>Classified.</b> You haven't faced this model yet.</color>", _s.Label);
                GUILayout.EndVertical();
            }
        }

        private void DrawSettings()
        {
            var p = Game.Progress;
            int cleared = 0;
            foreach (var l in Game.Campaign.Levels) if (p.IsCleared(l.Id)) cleared++;
            GUILayout.Label($"Levels escaped: {cleared} of {Game.Campaign.Levels.Count}", _s.Label);
            GUILayout.Label($"Tools: {(p.Tools.Count == 0 ? "none yet" : string.Join(", ", ToolNames(p)))}", _s.Label);
            GUILayout.Label($"Notes read: {p.Notes.Count}", _s.Label);
            GUILayout.Space(_s.Px(10));
            if (!_confirmReset)
            {
                if (GUILayout.Button("Erase save", _s.Button, GUILayout.Width(_s.Px(200)))) _confirmReset = true;
            }
            else
            {
                GUILayout.Label("<color=#FF8080>Erase all progress? This can't be undone.</color>", _s.Label);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Erase", _s.Button, GUILayout.Width(_s.Px(140)))) { Game.ResetProgress(); _confirmReset = false; }
                if (GUILayout.Button("Keep it", _s.Button, GUILayout.Width(_s.Px(140)))) _confirmReset = false;
                GUILayout.EndHorizontal();
            }
        }

        private static IEnumerable<string> ToolNames(CampaignProgress p)
        {
            foreach (var t in ToolInfo.All) if (p.Tools.Contains(t)) yield return ToolInfo.Name(t);
        }

        // ---------------------------------------------------------------- briefing

        private void DrawBriefing()
        {
            var def = Game.Definition;
            if (def == null) return;
            var level = Game.Campaign.Get(def.Id);
            var spec = TrackerSpecs.For(Game.ActiveTracker);
            bool firstTime = !Game.Progress.TrackersMet.Contains(Game.ActiveTracker);

            GUILayout.BeginArea(CenterRect(720, 800));
            GUILayout.BeginVertical(_s.Panel);
            GUILayout.Label($"<color=#888>{def.Zone.ToUpperInvariant()}</color>", _s.Center);
            GUILayout.Label($"{(level != null ? level.Number + ". " : "")}{def.Title}", _s.Title);
            if (!string.IsNullOrEmpty(def.Banner)) GUILayout.Label($"<i>{def.Banner}</i>", _s.Center);
            GUILayout.Space(_s.Px(10));
            if (Game.IsSandboxRun) GUILayout.Label("<color=#FFD060><b>SANDBOX REPLAY.</b> Same room, different algorithm. Marks aren't recorded; new exploits still go in the journal.</color>", _s.Label);
            else if (firstTime) GUILayout.Label("<color=#FF7080><b>WARDEN HAS BEEN UPGRADED.</b> New model below. The tricks that worked before may not.</color>", _s.Label);
            GUILayout.BeginVertical(_s.Box);
            EscapeHud.DrawSpecBody(_s, spec, Game);
            GUILayout.EndVertical();
            GUILayout.Space(_s.Px(8));
            var tools = new List<string>();
            foreach (var t in ToolInfo.All) if (t != ToolKind.Throw && Game.Progress.Tools.Contains(t)) tools.Add(ToolInfo.Name(t));
            GUILayout.Label($"<b>You carry:</b> {(tools.Count == 0 ? "your hands" : string.Join(", ", tools))}.  <color=#888>Par {EscapeStyles.FormatTime(def.Par)}</color>", _s.Label);
            GUILayout.Label("<color=#999>WASD move, Shift run, Ctrl sneak, E interact, 1-5 tools, click to use, Tab spec sheet, M map, R restart, Esc pause.</color>", _s.Small);
            GUILayout.Space(_s.Px(8));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<b>Start</b>  (Enter)", _s.Button, GUILayout.MinHeight(_s.Px(40)))) Game.Play();
            if (GUILayout.Button("Back", _s.Button, GUILayout.MinHeight(_s.Px(40)), GUILayout.Width(_s.Px(140)))) Game.QuitToMenu();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- pause

        private void DrawPause()
        {
            GUILayout.BeginArea(CenterRect(360, 300));
            GUILayout.BeginVertical(_s.Panel);
            GUILayout.Label("PAUSED", _s.Title);
            GUILayout.Space(_s.Px(8));
            if (GUILayout.Button("Resume  (Esc)", _s.Button)) Game.TogglePause();
            if (GUILayout.Button("Restart level  (R)", _s.Button)) Game.Restart();
            if (GUILayout.Button("Quit to menu", _s.Button)) Game.QuitToMenu();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- results

        private void DrawResults()
        {
            var r = Game.LastResult;
            var def = Game.Definition;
            if (r == null || def == null) return;
            GUILayout.BeginArea(CenterRect(720, 820));
            GUILayout.BeginVertical(_s.Panel);
            GUILayout.Label("ESCAPED", _s.Title);
            GUILayout.Label($"{def.Title}  <color=#888>{EscapeStyles.FormatTime(r.Time)} (par {EscapeStyles.FormatTime(def.Par)})</color>", _s.Center);
            GUILayout.Space(_s.Px(6));
            GUILayout.Label(EscapeStyles.Marks(true, r.Undetected, r.Untouched), _s.Center);
            GUILayout.Label($"<color=#999>Seen {r.Detections}x, caught {r.Captures}x, average belief on you {r.AverageExposure:P0}. Notes read: {r.NotesRead.Count} of {Game.Stage.Notes.Count}.</color>", _s.Center);
            GUILayout.Space(_s.Px(10));
            if (Game.LastNewExploits.Count > 0)
            {
                GUILayout.Label("<b>New in your journal</b>", _s.Heading);
                foreach (var id in Game.LastNewExploits)
                {
                    var info = Exploits.Get(id);
                    GUILayout.BeginVertical(_s.Box);
                    GUILayout.Label($"<color=#7CFFA0><b>{info.Title}</b></color>   <i>{info.Term}</i>", _s.Label);
                    GUILayout.Label(info.WhatHappened, _s.Small);
                    GUILayout.Label(info.WhyItWorks, _s.Small);
                    GUILayout.EndVertical();
                }
            }
            else
            {
                int found = Game.Progress.ExploitsFound(Game.ActiveTracker), total = 0;
                foreach (var _ in Exploits.ForTracker(Game.ActiveTracker)) total++;
                if (found < total)
                    GUILayout.Label($"<color=#999>{EscapeStyles.TrackerShort(Game.ActiveTracker)} still has {total - found} weakness{(total - found == 1 ? "" : "es")} you haven't found. Its spec sheet is a good place to look.</color>", _s.Label);
            }
            if (r.ToolsFound.Count > 0)
            {
                var names = new List<string>();
                foreach (var t in r.ToolsFound) names.Add(ToolInfo.Name(t));
                GUILayout.Label($"<b>Tools kept:</b> {string.Join(", ", names)}", _s.Label);
            }
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            int index = Game.Campaign.IndexOf(def.Id);
            bool hasNext = index >= 0 && index + 1 < Game.Campaign.Levels.Count;
            if (GUILayout.Button(hasNext ? "<b>Next level</b>  (Enter)" : "<b>Back to menu</b>  (Enter)", _s.Button, GUILayout.MinHeight(_s.Px(40)))) Game.NextLevel();
            if (GUILayout.Button("Retry  (R)", _s.Button, GUILayout.MinHeight(_s.Px(40)), GUILayout.Width(_s.Px(140)))) Game.Restart();
            if (GUILayout.Button("Menu", _s.Button, GUILayout.MinHeight(_s.Px(40)), GUILayout.Width(_s.Px(120)))) Game.QuitToMenu();
            GUILayout.EndHorizontal();
            if (!hasNext)
            {
                GUILayout.Label("<color=#FFD060>That's the end of the MVP campaign. The server hall, and ATLAS, are next.</color>", _s.Center);
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
