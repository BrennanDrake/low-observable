using System.Collections.Generic;
using LowObservable.Core.Maths;
using LowObservable.Core.Stages;
using LowObservable.Core.Tracking;
using UnityEngine;

namespace LowObservable.Unity.Escape
{
    /// <summary>
    /// The in-level HUD (IMGUI placeholder):
    ///   top left    level, WARDEN's current algorithm, what it believes about you right now (exposure)
    ///   top right   event feed in plain words
    ///   bottom      interact prompt, tool bar with charges, why the selected tool can't be used
    ///   centre      toasts (new exploit in the journal, tool found), the note you're reading
    ///   Tab         the algorithm's spec sheet, and which of its weaknesses you have found
    ///   in world    unit barks and roomba apologies above their heads
    /// </summary>
    public sealed class EscapeHud : MonoBehaviour
    {
        public EscapeGame Game;
        public int FeedLines = 6;
        public float ToastSeconds = 5f;
        public float BarkSeconds = 2.2f;

        private readonly EscapeStyles _s = new EscapeStyles();
        private readonly Queue<string> _feed = new Queue<string>();
        private readonly List<(string title, string body, float until)> _toasts = new List<(string, string, float)>();
        private readonly List<(Vector3 pos, string text, float until, int unit, bool roomba)> _barks = new List<(Vector3, string, float, int, bool)>();

        private void Start()
        {
            if (Game == null) Game = FindFirstObjectByType<EscapeGame>();
            Game.EventRaised += OnEvent;
            Game.StageStarted += _ =>
            {
                _feed.Clear();
                _toasts.Clear();
                _barks.Clear();
            };
        }

        private void OnDestroy()
        {
            if (Game != null) Game.EventRaised -= OnEvent;
        }

        private void OnEvent(StageEvent e)
        {
            switch (e.Type)
            {
                case StageEventType.Exploit:
                    if (System.Enum.TryParse(e.Tag, out ExploitId id))
                    {
                        var info = Exploits.Get(id);
                        bool fresh = !Game.Progress.Exploits.Contains(id);
                        Toast((fresh ? "NEW IN YOUR JOURNAL: " : "EXPLOIT: ") + info.Title.ToUpperInvariant(), $"{info.WhatHappened}\n<i>{info.Term}</i>");
                    }
                    break;
                case StageEventType.ToolUnlocked:
                    if (ToolInfo.TryParse(e.Tag, out var tool)) Toast("FOUND: " + ToolInfo.Name(tool).ToUpperInvariant(), $"{ToolInfo.Description(tool)}\nPress {System.Array.IndexOf(ToolInfo.All, tool) + 1} to select it, left click to use.");
                    break;
                case StageEventType.GuardBark:
                    _barks.Add((e.Position.ToVector3(), e.Text, Time.time + BarkSeconds, e.Unit, false));
                    break;
                case StageEventType.Bump:
                    _barks.Add((e.Position.ToVector3(), e.Text, Time.time + BarkSeconds, e.Unit, true));
                    break;
                case StageEventType.Captured:
                    _barks.Add((e.Position.ToVector3(), e.Text, Time.time + BarkSeconds, e.Unit, false));
                    break;
            }
            string line = Describe(e);
            if (line == null) return;
            _feed.Enqueue(line);
            while (_feed.Count > FeedLines) _feed.Dequeue();
        }

        private void Toast(string title, string body) => _toasts.Add((title, body, Time.time + ToastSeconds));

        /// <summary>Feed text for events the player would notice. Sensor internals stay out of it.</summary>
        private static string Describe(StageEvent e)
        {
            switch (e.Type)
            {
                case StageEventType.Sighted: return e.Tag == "Mirror" ? "<color=#BFEAFF>A camera saw your reflection</color>" : "<color=#FF6060>Spotted</color>";
                case StageEventType.Heard: return e.Tag == "Player" ? "<color=#FFB060>Something heard you</color>" : "Something heard a noise";
                case StageEventType.LaserTripped: return "<color=#FF6060>Tripwire</color>";
                case StageEventType.Bump: return "A roomba reported an obstruction";
                case StageEventType.ReportRejected: return "<color=#B0B0B8>WARDEN threw a report away as impossible</color>";
                case StageEventType.Captured: return "<color=#FF4040>Detained. Back to the checkpoint.</color>";
                case StageEventType.CheckpointReached: return "<color=#80A8FF>Checkpoint</color>";
                case StageEventType.KeycardTaken: return $"Keycard: {e.Text}";
                case StageEventType.DoorOpened: return "Door open";
                case StageEventType.TerminalDone: return "Terminal done";
                case StageEventType.LightsToggled: return "Lights flipped";
                case StageEventType.Hid: return "Hidden";
                case StageEventType.NoisemakerAttached: return "<color=#FFE070>Noisemaker stuck to a roomba</color>";
                case StageEventType.JammerPlaced: return "Jammer on";
                case StageEventType.MirrorPlaced: return "Mirror placed";
                case StageEventType.MirrorTaken: return "Mirror picked up";
                case StageEventType.Forged: return $"<color=#FF70A0>{e.Text}</color>";
                case StageEventType.Denied: return $"<color=#A0A0A8>{e.Text}</color>";
                default: return null;
            }
        }

        private void OnGUI()
        {
            var stage = Game.Stage;
            if (stage == null || (Game.Screen != EscapeScreen.Playing && Game.Screen != EscapeScreen.Paused)) return;
            _s.Ensure();
            DrawBarks(stage);
            DrawStatus(stage);
            DrawFeed();
            DrawToolbar(stage);
            DrawToasts();
            if (Game.OpenNote != null) DrawNote(Game.OpenNote);
            if (Game.Input != null && Game.Input.SpecSheetHeld) DrawSpecSheet(stage);
        }

        private void DrawStatus(Stage stage)
        {
            var spec = TrackerSpecs.For(stage.Tracker.Kind);
            float w = _s.Px(330);
            GUILayout.BeginArea(new Rect(_s.Px(12), _s.Px(12), w, _s.Px(260)));
            GUILayout.BeginVertical(_s.Panel);
            var lvl = Game.Campaign.Get(stage.Definition.Id);
            string number = lvl != null ? $"{lvl.Number}. " : "";
            GUILayout.Label($"<b>{number}{stage.Definition.Title}</b>  <color=#888>{stage.Definition.Zone}  {EscapeStyles.FormatTime(stage.Time)}</color>", _s.Label);
            string sandbox = Game.IsSandboxRun ? "  <color=#FFD060>(sandbox)</color>" : "";
            GUILayout.Label($"WARDEN runs <color=#FF6070><b>{spec.ProductName}</b></color>{sandbox}", _s.Label);

            double exposure = stage.Exposure;
            string state; Color color;
            if (!stage.Tracker.HasEstimate) { state = "IT HAS LOST YOU"; color = new Color(0.5f, 1f, 0.6f); }
            else if (exposure > 0.5) { state = "IT KNOWS WHERE YOU ARE"; color = new Color(1f, 0.3f, 0.3f); }
            else if (exposure > 0.15) { state = "IT SUSPECTS YOU'RE CLOSE"; color = new Color(1f, 0.7f, 0.3f); }
            else { state = "IT'S LOOKING ELSEWHERE"; color = new Color(0.8f, 0.85f, 0.9f); }
            GUILayout.Label($"<color=#{EscapeStyles.Hex(color)}><b>{state}</b></color>", _s.Label);
            var r = GUILayoutUtility.GetRect(w - _s.Px(30), _s.Px(10));
            _s.Bar(r, (float)exposure, color);
            GUILayout.Label($"<color=#888>Belief on you {exposure:P0}.  Seen {stage.Detections}x, caught {stage.Captures}x.  Tab: spec sheet</color>", _s.Small);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawFeed()
        {
            if (_feed.Count == 0) return;
            float w = _s.Px(330);
            GUILayout.BeginArea(new Rect(Screen.width - w - _s.Px(12), _s.Px(12), w, _s.Px(220)));
            GUILayout.BeginVertical(_s.Panel);
            foreach (var line in _feed) GUILayout.Label(line, _s.Small);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawToolbar(Stage stage)
        {
            var input = Game.Input;
            float slot = _s.Px(118), h = _s.Px(54), gap = _s.Px(6);
            var tools = new List<ToolKind>();
            foreach (var t in ToolInfo.All) if (stage.HasTool(t)) tools.Add(t);
            float total = tools.Count * slot + (tools.Count - 1) * gap;
            float x = (Screen.width - total) * 0.5f, y = Screen.height - h - _s.Px(14);
            foreach (var t in tools)
            {
                bool sel = input != null && input.SelectedTool == t;
                int n = System.Array.IndexOf(ToolInfo.All, t) + 1;
                int charges = stage.Charges(t);
                string count = t == ToolKind.Throw ? $"carrying {charges}" : $"x{charges}";
                string dim = charges > 0 ? "" : "<color=#777>";
                string undim = charges > 0 ? "" : "</color>";
                GUI.Box(new Rect(x, y, slot, h), $"{dim}<b>{n}  {ToolInfo.Name(t)}</b>\n{count}{undim}", sel ? _s.Selected : _s.Box);
                x += slot + gap;
            }

            string prompt = stage.InteractPrompt();
            float py = y - _s.Px(34);
            if (prompt != null) GUI.Label(new Rect(0, py, Screen.width, _s.Px(28)), $"<b>[E]</b> {prompt}", _s.Center);
            if (input != null && stage.HasTool(input.SelectedTool) && !stage.Hidden)
            {
                var aim = new Vec2(input.CursorWorld.x, input.CursorWorld.y);
                string reason = stage.ToolBlockedReason(input.SelectedTool, aim);
                string extra = input.SelectedTool == ToolKind.Mirror ? "  (scroll to rotate)" : "";
                if (reason != null && !(input.SelectedTool == ToolKind.Throw && stage.Carried == 0))
                    GUI.Label(new Rect(0, py - _s.Px(24), Screen.width, _s.Px(24)), $"<color=#999>{ToolInfo.Name(input.SelectedTool)}: {reason}{extra}</color>", _s.Center);
                else if (reason == null && extra.Length > 0)
                    GUI.Label(new Rect(0, py - _s.Px(24), Screen.width, _s.Px(24)), $"<color=#999>Mirror{extra}</color>", _s.Center);
            }
            if (stage.Mode == MoveMode.Run && stage.Stamina <= 0.01)
                GUI.Label(new Rect(0, py - _s.Px(48), Screen.width, _s.Px(24)), "<color=#FF8080>Out of breath</color>", _s.Center);
        }

        private void DrawToasts()
        {
            float now = Time.time;
            _toasts.RemoveAll(t => now >= t.until);
            float w = _s.Px(520), y = _s.Px(90);
            foreach (var t in _toasts)
            {
                float alpha = Mathf.Clamp01((t.until - now) / 0.6f);
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUILayout.BeginArea(new Rect((Screen.width - w) * 0.5f, y, w, _s.Px(140)));
                GUILayout.BeginVertical(_s.Panel);
                GUILayout.Label($"<color=#7CFFA0><b>{t.title}</b></color>", _s.Label);
                GUILayout.Label(t.body, _s.Small);
                GUILayout.EndVertical();
                GUILayout.EndArea();
                GUI.color = old;
                y += _s.Px(146);
            }
        }

        private void DrawNote(Note note)
        {
            float w = Mathf.Min(_s.Px(560), Screen.width * 0.8f);
            var content = new GUIContent($"<size={_s.Px(11)}><b>{EscapeStyles.NoteHeader(note.Kind)}</b></size>\n\n{note.Text}\n\n<size={_s.Px(11)}><color=#7A7060>E / Enter to close. WARDEN is still running.</color></size>");
            float h = _s.Note.CalcHeight(content, w);
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.5f - h * 0.5f + _s.Px(60), w, h);
            if (GUI.Button(rect, content, _s.Note)) Game.CloseNote();
        }

        private void DrawSpecSheet(Stage stage)
        {
            var kind = stage.Tracker.Kind;
            var spec = TrackerSpecs.For(kind);
            float w = Mathf.Min(_s.Px(640), Screen.width * 0.9f);
            GUILayout.BeginArea(new Rect((Screen.width - w) * 0.5f, _s.Px(70), w, Screen.height - _s.Px(140)));
            GUILayout.BeginVertical(_s.Panel);
            DrawSpecBody(_s, spec, Game);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        /// <summary>Spec sheet body, shared with the briefing and the codex.</summary>
        public static void DrawSpecBody(EscapeStyles s, TrackerSpec spec, EscapeGame game)
        {
            GUILayout.Label($"<color=#FF6070><b>{spec.ProductName}</b></color>  <color=#888>Halvorsen Applied Autonomy</color>", s.Heading);
            GUILayout.Label($"<i>{spec.Tagline}</i>", s.Label);
            GUILayout.Space(s.Px(6));
            GUILayout.Label("<b>How it works</b>", s.Label);
            foreach (var line in spec.HowItWorks) GUILayout.Label("•  " + line, s.Label);
            GUILayout.Space(s.Px(6));
            GUILayout.Label($"<b>The textbook name:</b> {spec.AlgorithmName}", s.Label);
            GUILayout.Label(spec.Detail, s.Mono);
            GUILayout.Space(s.Px(8));
            int found = 0, total = 0;
            foreach (var info in Exploits.ForTracker(spec.Kind))
            {
                total++;
                bool known = game.Progress.Exploits.Contains(info.Id) || (game.Stage != null && IsFoundThisRun(game, info.Id));
                if (known) found++;
            }
            GUILayout.Label($"<b>Known weaknesses: {found} of {total}</b>", s.Label);
            foreach (var info in Exploits.ForTracker(spec.Kind))
            {
                bool known = game.Progress.Exploits.Contains(info.Id) || (game.Stage != null && IsFoundThisRun(game, info.Id));
                GUILayout.Label(known ? $"<color=#7CFFA0>✓ {info.Title}</color>  <color=#999>{info.Term}</color>" : "<color=#666>?  Undiscovered</color>", s.Small);
            }
        }

        private static bool IsFoundThisRun(EscapeGame game, ExploitId id)
        {
            foreach (var e in game.Stage.Exploits.Found) if (e == id) return true;
            return false;
        }

        private void DrawBarks(Stage stage)
        {
            var cam = Camera.main;
            if (cam == null) return;
            float now = Time.time;
            _barks.RemoveAll(b => now >= b.until);
            foreach (var b in _barks)
            {
                // Follow the speaker if it is still around.
                var pos = b.pos;
                if (b.roomba && b.unit >= 0 && b.unit < stage.Roombas.Count) pos = stage.Roombas[b.unit].Position.ToVector3();
                else if (!b.roomba && b.unit >= 0 && b.unit < stage.Units.Count) pos = stage.Units[b.unit].Pose.Position.ToVector3();
                var sp = cam.WorldToScreenPoint(pos + new Vector3(0f, 0.8f, 0f));
                if (sp.z < 0) continue;
                var content = new GUIContent(b.roomba ? $"<color=#9FF0FF>{b.text}</color>" : $"<color=#FFC070><b>{b.text}</b></color>");
                var size = _s.Small.CalcSize(content);
                var rect = new Rect(sp.x - size.x * 0.5f - _s.Px(4), Screen.height - sp.y - size.y, size.x + _s.Px(8), size.y);
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((b.until - now) / 0.4f));
                GUI.Box(rect, GUIContent.none, _s.Panel);
                GUI.Label(new Rect(rect.x + _s.Px(4), rect.y, size.x, size.y), content, _s.Small);
                GUI.color = old;
            }
        }
    }
}
