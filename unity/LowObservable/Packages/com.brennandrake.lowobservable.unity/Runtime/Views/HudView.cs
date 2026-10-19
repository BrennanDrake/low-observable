using System.Collections.Generic;
using LowObservable.Core.Agents;
using LowObservable.Core.Maths;
using LowObservable.Core.Robot;
using LowObservable.Core.Simulation;
using UnityEngine;

namespace LowObservable.Unity.Views
{
    /// <summary>
    /// Placeholder HUD in IMGUI (no UI package or prefabs needed): mode, ping cooldown, decoys, how sure
    /// you are, how sure the Seeker is, a listen compass, a short event log, and the data association
    /// picker when the episode pauses on an ambiguous measurement. Replace with UI Toolkit when the art pass comes.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        public EpisodeRunner Runner;
        public int FontSize = 15;
        public int LogLines = 6;

        private readonly Queue<string> _log = new Queue<string>();
        private GUIStyle _style;
        private GUIStyle _box;
        private float _listenShownUntil;

        private void Start()
        {
            if (Runner == null) Runner = FindFirstObjectByType<EpisodeRunner>();
            Runner.EventRaised += OnEvent;
            Runner.EpisodeStarted += _ => _log.Clear();
        }

        private void OnDestroy()
        {
            if (Runner != null) Runner.EventRaised -= OnEvent;
        }

        private void OnEvent(EpisodeEvent e)
        {
            string line = Describe(e);
            if (line == null) return;
            _log.Enqueue($"{e.Time,6:0.0}s  {line}");
            while (_log.Count > LogLines) _log.Dequeue();
            if (e.Type == EpisodeEventType.ListenHeardSeeker || e.Type == EpisodeEventType.ListenHeardNothing)
                _listenShownUntil = Time.time + 3f;
        }

        // Only things the player could perceive are logged. Wrong associations, for example, are not.
        private static string Describe(EpisodeEvent e)
        {
            switch (e.Type)
            {
                case EpisodeEventType.PingFired: return $"Ping: {e.Value:0} returns. It heard that.";
                case EpisodeEventType.PingOnCooldown: return "Ping recharging";
                case EpisodeEventType.ObservationRejected: return "A return matched nothing on the blueprint";
                case EpisodeEventType.ObservationAmbiguous: return $"Return could be {e.Value:0} different landmarks";
                case EpisodeEventType.ListenHeardSeeker: return "Heard the Seeker";
                case EpisodeEventType.ListenHeardNothing: return "Silence";
                case EpisodeEventType.DecoyThrown: return $"Decoy out ({e.Value:0} left)";
                case EpisodeEventType.NoDecoysLeft: return "No decoys left";
                case EpisodeEventType.Bump: return "Bumped into something";
                case EpisodeEventType.DockFailed: return "Dock failed: you are not at the uplink";
                case EpisodeEventType.Docked: return "Uplink reached";
                case EpisodeEventType.SeekerModeChanged:
                    return (SeekerMode)(int)e.Value == SeekerMode.Hunt ? "The Seeker is closing in" : "The Seeker lost the trail";
                case EpisodeEventType.Captured: return "Captured";
                case EpisodeEventType.TimedOut: return "Out of time";
                default: return null;
            }
        }

        private void OnGUI()
        {
            var ep = Runner.Episode;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = FontSize, richText = true };
                _box = new GUIStyle(GUI.skin.box) { fontSize = FontSize, alignment = TextAnchor.UpperLeft, richText = true, padding = new RectOffset(10, 10, 8, 8) };
            }

            string mode = ep.CurrentMode == MovementMode.Sprint ? "<color=#ff9a6a>SPRINT</color>"
                : ep.CurrentMode == MovementMode.Creep ? "<color=#8fd18f>CREEP</color>" : "WALK";
            string ping = ep.PingCooldownRemaining > 0 ? $"{ep.PingCooldownRemaining:0.0}s" : "<color=#7fd0ff>ready</color>";
            double yourSpread = ep.Filter.PositionSpread;
            double seekerSpread = ep.Seeker.Belief.Spread();
            double heat = ep.Seeker.Belief.MassWithin(ep.Filter.Mean.Position, 3.0);

            string text =
                $"<b>LOW OBSERVABLE</b>   v{Application.version}   seed {Runner.Seed}\n" +
                $"Mode {mode}   Ping {ping}   Decoys {ep.DecoysLeft}\n" +
                $"Your uncertainty   <b>{yourSpread:0.00} m</b>\n" +
                $"Seeker's certainty <b>{(1.0 / (1.0 + seekerSpread)):P0}</b>   near you {heat:P0}   ({ep.Seeker.Mode})\n" +
                (ep.IsListening ? "<color=#ffd36a>Listening...</color>\n" : "");
            GUI.Box(new Rect(12, 12, 470, 112), text, _box);

            if (Time.time < _listenShownUntil) DrawListenCompass(ep);

            var log = string.Join("\n", _log);
            GUI.Box(new Rect(12, Screen.height - 24 - FontSize * 1.4f * LogLines, 470, FontSize * 1.4f * LogLines + 12), log, _box);

            GUI.Label(new Rect(Screen.width - 330, 12, 320, 200),
                "WASD drive · Shift sprint · Ctrl creep\nSpace ping · F listen · Q decoy (mouse)\nE dock · R restart · F1 truth (debug)", _style);

            if (ep.AwaitingAssociation) DrawAssociationPicker(ep);
        }

        private void DrawListenCompass(Episode ep)
        {
            var listen = ep.LastListen;
            var center = new Vector2(Screen.width - 90, Screen.height - 90);
            GUI.Box(new Rect(center.x - 70, center.y - 70, 140, 140), "", _box);
            if (!listen.Heard)
            {
                GUI.Label(new Rect(center.x - 30, center.y - 10, 80, 24), "silence", _style);
                return;
            }
            // Bearing is in the drone frame, so "up" on the compass is the drone's nose.
            double a = listen.Bearing + System.Math.PI / 2;
            var tip = center + new Vector2((float)System.Math.Cos(a), -(float)System.Math.Sin(a)) * 55f;
            GUI.Label(new Rect(tip.x - 8, tip.y - 12, 24, 24), "<color=#ff5040><b>●</b></color>", _style);
            GUI.Label(new Rect(center.x - 8, center.y - 12, 24, 24), "▲", _style);
            GUI.Label(new Rect(center.x - 60, center.y + 45, 140, 24), $"±{listen.BearingSigma * Angle.Rad2Deg:0}°", _style);
        }

        private void DrawAssociationPicker(Episode ep)
        {
            var pending = ep.CurrentPending;
            float w = 420, h = 70 + 26 * pending.Candidates.Count;
            var rect = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(rect, "", _box);
            GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 8, w - 24, h - 16));
            GUILayout.Label($"<b>Which landmark was that?</b>  ({pending.Observation.Kind}, {pending.Observation.Range:0.0} m)", _style);
            for (int k = 0; k < pending.Candidates.Count && k < 9; k++)
            {
                var c = pending.Candidates[k];
                string label = $"{k + 1}.  '{c.Landmark.Glyph}' #{c.Landmark.Id}   fit {System.Math.Exp(-0.5 * c.Mahalanobis2):P0}";
                if (GUILayout.Button(label)) ResolveFromGui(ep, c.Landmark.Id);
            }
            if (GUILayout.Button("0.  Ignore it")) ResolveFromGui(ep, -1);
            GUILayout.EndArea();
        }

        private void ResolveFromGui(Episode ep, int landmarkId)
        {
            ep.ResolvePending(landmarkId);
            foreach (var e in ep.TickEvents) OnEvent(e);
        }
    }
}
