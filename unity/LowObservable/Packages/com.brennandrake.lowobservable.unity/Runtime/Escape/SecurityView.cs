using System.Collections.Generic;
using LowObservable.Core.Maths;
using LowObservable.Core.Stages;
using LowObservable.Unity.Views;
using UnityEngine;

namespace LowObservable.Unity.Escape
{
    /// <summary>
    /// The truth about WARDEN's hardware, which the player always sees: camera and guard vision cones clipped by
    /// walls, microphones, tripwires, roombas, and where each guard is heading. Plus the player's gadgets
    /// (mirrors, jammer fields, noisemakers, things in flight) and sound rings, so the player can see how far a
    /// noise carries. Jammed sensors are drawn grey.
    /// </summary>
    public sealed class SecurityView : MonoBehaviour
    {
        public EscapeGame Game;
        public int ConeRays = 28;
        public float SoundRingSeconds = 0.7f;

        private Stage _built;
        private readonly List<Mesh> _coneMeshes = new List<Mesh>();
        private readonly List<GameObject> _coneObjects = new List<GameObject>();
        private readonly List<Vector3> _fan = new List<Vector3>();
        private readonly List<int> _tris = new List<int>();
        private readonly List<Color> _colors = new List<Color>();

        private ShapePool _bodies;
        private ShapePool _heads;
        private ShapePool _small;
        private LinePool _lines;
        private LinePool _rings;
        private LinePool _mirrors;
        private readonly List<(Vector3 pos, float radius, float until, Color color)> _sounds = new List<(Vector3, float, float, Color)>();

        private void Start()
        {
            if (Game == null) Game = FindFirstObjectByType<EscapeGame>();
            _bodies = new ShapePool(transform, "Unit body", 0.7f, EscapeLayers.Units);
            _heads = new ShapePool(transform, "Unit heading", 0.8f, EscapeLayers.Units + 1, triangle: true);
            _small = new ShapePool(transform, "Gadget", 0.3f, EscapeLayers.Gadgets);
            _lines = new LinePool(transform, "Security line", 0.06f, EscapeLayers.Gadgets);
            _rings = new LinePool(transform, "Sound ring", 0.04f, EscapeLayers.Sound);
            _mirrors = new LinePool(transform, "Mirror", 0.15f, EscapeLayers.Gadgets + 1);
            Game.EventRaised += OnEvent;
        }

        private void OnDestroy()
        {
            if (Game != null) Game.EventRaised -= OnEvent;
        }

        private void OnEvent(StageEvent e)
        {
            switch (e.Type)
            {
                case StageEventType.Footstep:
                case StageEventType.Noise:
                case StageEventType.ThrowLanded:
                case StageEventType.DoorOpened:
                case StageEventType.LightsToggled:
                case StageEventType.TerminalProgress:
                    if (e.Radius > 0.05) _sounds.Add((e.Position.ToVector3(), (float)e.Radius, Time.time + SoundRingSeconds, EscapePalette.Sound));
                    break;
                case StageEventType.Bump:
                    _sounds.Add((e.Position.ToVector3(), 1.5f, Time.time + SoundRingSeconds, EscapePalette.Roomba));
                    break;
                case StageEventType.Captured:
                    _sounds.Add((e.Position.ToVector3(), 3f, Time.time + 1.2f, EscapePalette.Alarm));
                    break;
            }
        }

        private void LateUpdate()
        {
            var stage = Game.Stage;
            if (stage != _built)
            {
                _built = stage;
                _sounds.Clear();
            }
            if (stage == null) return;

            int cone = 0;
            _bodies.Begin();
            _heads.Begin();
            _small.Begin();
            _lines.Begin();
            _rings.Begin();
            _mirrors.Begin();

            var t = stage.Tuning;
            foreach (var cam in stage.Cameras)
            {
                var color = cam.Jammed ? EscapePalette.Jammed : cam.SinceAlarm < 1.0 ? EscapePalette.Alarm : EscapePalette.CameraCone;
                DrawCone(ref cone, stage, cam.Position, cam.Heading, cam.FieldOfView, cam.Range, color);
                _heads.Next(cam.Jammed ? EscapePalette.Jammed : EscapePalette.Camera, cam.Position.ToVector3(), Deg(cam.Heading), 0.7f);
            }
            foreach (var u in stage.Units)
            {
                var body = ModeColor(u.Mode);
                if (u.Jammed) body = Color.Lerp(body, EscapePalette.Jammed, 0.5f);
                var coneColor = body;
                coneColor.a = u.Mode == GuardMode.Chase ? 0.28f : 0.14f;
                DrawCone(ref cone, stage, u.Pose.Position, u.LookHeading, t.GuardFieldOfView, t.GuardVisionRange, coneColor);
                _bodies.Next(body, u.Pose.Position.ToVector3(), Deg(u.Pose.Theta) + 45f, 1f);
                _heads.Next(body, u.Pose.Position.ToVector3(), Deg(u.LookHeading), 0.8f);
                // Where WARDEN sent it: the consequence of its belief, drawn as a thin line.
                if (u.Mode == GuardMode.Investigate || u.Mode == GuardMode.Search || u.Mode == GuardMode.CheckLocker)
                {
                    var target = u.Mode == GuardMode.Search ? u.SearchCenter : u.Mode == GuardMode.CheckLocker ? u.LockerTarget : u.Goal;
                    var c = body;
                    c.a = 0.45f;
                    EscapeDraw.Segment(_lines.Next(c), u.Pose.Position.ToVector3(), target.ToVector3());
                }
            }
            foreach (var r in stage.Roombas)
            {
                var c = r.Jammed ? EscapePalette.Jammed : EscapePalette.Roomba;
                EscapeDraw.Circle(_lines.Next(c), r.Position.ToVector3(), 0.32f, 18);
                if (!string.IsNullOrEmpty(r.Hat))
                    _heads.Next(EscapePalette.Note, (r.Position + new Vec2(0, 0.28)).ToVector3(), 90f, 0.35f);
            }
            foreach (var m in stage.Microphones)
            {
                var c = m.Jammed ? EscapePalette.Jammed : m.SinceHeard < 0.6 ? EscapePalette.Alarm : EscapePalette.Mic;
                _small.Next(c, m.Position.ToVector3(), 45f, 1f);
                EscapeDraw.Circle(_lines.Next(new Color(c.r, c.g, c.b, 0.35f)), m.Position.ToVector3(), (float)(t.MicRadius * m.Sensitivity), 28);
            }
            foreach (var l in stage.Lasers)
            {
                var c = l.Jammed ? EscapePalette.Jammed : EscapePalette.Laser;
                if (l.SinceTripped < 0.5) c = Color.white;
                EscapeDraw.Segment(_lines.Next(c), l.A.ToVector3(), l.B.ToVector3());
            }
            foreach (var m in stage.Mirrors)
            {
                EscapeDraw.Segment(_mirrors.Next(EscapePalette.Mirror), m.A.ToVector3(), m.B.ToVector3());
            }
            foreach (var j in stage.Jammers)
            {
                if (!j.Active) continue;
                var c = EscapePalette.JammerField;
                c.a = 0.2f + 0.4f * Mathf.Clamp01((float)(j.TimeLeft / t.JammerDuration));
                EscapeDraw.Circle(_lines.Next(c), j.Position.ToVector3(), (float)j.Radius, 48);
                _small.Next(EscapePalette.JammerField, j.Position.ToVector3(), 0f, 1f);
            }
            foreach (var n in stage.Noisemakers)
            {
                if (!n.Active) continue;
                var pos = n.Roomba >= 0 && n.Roomba < stage.Roombas.Count ? stage.Roombas[n.Roomba].Position : n.Position;
                _small.Next(EscapePalette.Keycard, pos.ToVector3(), 0f, 0.8f);
            }
            foreach (var p in stage.Projectiles)
                _small.Next(p.IsNoisemaker ? EscapePalette.Keycard : EscapePalette.Throwable, p.Position.ToVector3(), 0f, 0.6f);

            float now = Time.time;
            for (int k = _sounds.Count - 1; k >= 0; k--)
            {
                var s = _sounds[k];
                if (now >= s.until) { _sounds.RemoveAt(k); continue; }
                float life = s.color == EscapePalette.Alarm ? 1.2f : SoundRingSeconds;
                float age = 1f - (s.until - now) / life;
                var c = s.color;
                c.a *= 1f - age;
                EscapeDraw.Circle(_rings.Next(c), s.pos, Mathf.Max(0.1f, s.radius * Mathf.Sqrt(age)), 40);
            }

            for (int k = cone; k < _coneObjects.Count; k++) _coneObjects[k].SetActive(false);
            _bodies.End();
            _heads.End();
            _small.End();
            _lines.End();
            _rings.End();
            _mirrors.End();
        }

        private void DrawCone(ref int index, Stage stage, Vec2 origin, double heading, double fov, double range, Color color)
        {
            if (index == _coneMeshes.Count)
            {
                _coneObjects.Add(EscapeDraw.CreateMeshObject(transform, $"Cone {index}", EscapeLayers.Cones, out var mesh));
                _coneMeshes.Add(mesh);
            }
            _coneObjects[index].SetActive(true);
            EscapeDraw.Fan(stage.Plan, origin, heading, fov, range, ConeRays, _fan);
            EscapeDraw.FillFan(_coneMeshes[index], _fan, color, _tris, _colors);
            index++;
        }

        private static float Deg(double radians) => (float)(radians * Angle.Rad2Deg);

        public static Color ModeColor(GuardMode mode)
        {
            switch (mode)
            {
                case GuardMode.Investigate: return new Color(1f, 0.85f, 0.2f);
                case GuardMode.Search: return new Color(1f, 0.7f, 0.2f);
                case GuardMode.Chase: return new Color(1f, 0.2f, 0.15f);
                case GuardMode.CheckLocker: return new Color(0.9f, 0.4f, 1f);
                default: return EscapePalette.Unit;
            }
        }
    }
}
