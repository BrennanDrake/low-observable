using LowObservable.Core.Stages;
using LowObservable.Core.Tracking;
using UnityEngine;

namespace LowObservable.Unity.Escape
{
    /// <summary>
    /// Shared IMGUI styles for the placeholder HUD and menus. IMGUI needs no UI package, prefabs or fonts, so
    /// the MVP runs from an empty scene; replace with UI Toolkit in the art pass. Sizes scale with screen height.
    /// </summary>
    public sealed class EscapeStyles
    {
        public GUIStyle Label, Small, Title, Heading, Box, Panel, Button, Selected, Note, Center, Mono;
        public float Scale;
        private Texture2D _panelTex, _noteTex, _boxTex, _selTex, _btnTex, _btnHoverTex;
        private int _builtFor;

        public static readonly Color PanelColor = new Color(0.05f, 0.05f, 0.08f, 0.86f);
        public static readonly Color NoteColor = new Color(0.98f, 0.94f, 0.75f, 0.97f);

        public void Ensure()
        {
            if (_builtFor == Screen.height && Label != null) return;
            _builtFor = Screen.height;
            Scale = Mathf.Clamp(Screen.height / 900f, 0.75f, 2.5f);
            int fs = Mathf.RoundToInt(15 * Scale);

            _panelTex = _panelTex != null ? _panelTex : Solid(PanelColor);
            _noteTex = _noteTex != null ? _noteTex : Solid(NoteColor);
            _boxTex = _boxTex != null ? _boxTex : Solid(new Color(1f, 1f, 1f, 0.08f));
            _selTex = _selTex != null ? _selTex : Solid(new Color(0.12f, 0.45f, 0.25f, 0.85f));
            _btnTex = _btnTex != null ? _btnTex : Solid(new Color(1f, 1f, 1f, 0.12f));
            _btnHoverTex = _btnHoverTex != null ? _btnHoverTex : Solid(new Color(1f, 1f, 1f, 0.24f));

            Label = new GUIStyle(GUI.skin.label) { fontSize = fs, richText = true, wordWrap = true };
            Label.normal.textColor = new Color(0.92f, 0.92f, 0.95f);
            Small = new GUIStyle(Label) { fontSize = Mathf.RoundToInt(12 * Scale) };
            Small.normal.textColor = new Color(0.75f, 0.76f, 0.82f);
            Mono = new GUIStyle(Small) { fontStyle = FontStyle.Italic };
            Title = new GUIStyle(Label) { fontSize = Mathf.RoundToInt(42 * Scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            Heading = new GUIStyle(Label) { fontSize = Mathf.RoundToInt(20 * Scale), fontStyle = FontStyle.Bold };
            Center = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter };
            Panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(Px(12), Px(12), Px(10), Px(10)), richText = true };
            Panel.normal.background = _panelTex;
            Box = new GUIStyle(Panel) { padding = new RectOffset(Px(8), Px(8), Px(6), Px(6)) };
            Box.normal.background = _boxTex;
            // The default box text is mid grey, unreadable on the green "selected" tint.
            Box.normal.textColor = new Color(0.95f, 0.95f, 0.97f);
            Selected = new GUIStyle(Box);
            Selected.normal.background = _selTex;
            Button = new GUIStyle(GUI.skin.button) { fontSize = fs, richText = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(Px(10), Px(10), Px(6), Px(6)), wordWrap = true };
            Button.normal.background = _btnTex;
            Button.hover.background = _btnHoverTex;
            Button.active.background = _selTex;
            Button.normal.textColor = Button.hover.textColor = Button.active.textColor = new Color(0.95f, 0.95f, 0.97f);
            Note = new GUIStyle(Label) { fontSize = Mathf.RoundToInt(17 * Scale), padding = new RectOffset(Px(18), Px(18), Px(16), Px(16)), fontStyle = FontStyle.Normal };
            Note.normal.background = _noteTex;
            Note.normal.textColor = new Color(0.15f, 0.13f, 0.1f);
        }

        public int Px(float v) => Mathf.RoundToInt(v * Scale);

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        /// <summary>A horizontal bar (0..1) drawn with GUI boxes.</summary>
        public void Bar(Rect r, float value, Color color)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value), r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public static string TrackerShort(TrackerKind kind) => TrackerSpecs.For(kind).ProductName;

        public static string Marks(bool escaped, bool undetected, bool untouched)
        {
            string on = "<color=#7CFFA0>", off = "<color=#555A66>";
            return $"{(escaped ? on : off)}ESCAPED</color>  {(undetected ? on : off)}UNSEEN</color>  {(untouched ? on : off)}UNTOUCHED</color>";
        }

        public static string NoteHeader(NoteKind kind)
        {
            switch (kind)
            {
                case NoteKind.Sticky: return "STICKY NOTE";
                case NoteKind.Poster: return "POSTER";
                case NoteKind.Whiteboard: return "WHITEBOARD";
                case NoteKind.Graffiti: return "SCRATCHED INTO THE WALL";
                case NoteKind.Ticket: return "BUG TRACKER, PRINTED";
                case NoteKind.Screen: return "TERMINAL SCREEN";
                default: return "CHALK";
            }
        }

        public static string FormatTime(double seconds)
        {
            if (double.IsInfinity(seconds) || double.IsNaN(seconds)) return "--:--";
            int s = (int)System.Math.Round(seconds);
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
