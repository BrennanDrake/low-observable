using System.Collections.Generic;
using LowObservable.Core.Stages;
using LowObservable.Core.Tracking;
using LowObservable.Unity.Views;
using UnityEngine;

namespace LowObservable.Unity.Escape
{
    /// <summary>
    /// WARDEN's mind, drawn in red over the map. Each tracker gets the picture that matches its maths, so the
    /// player learns what each algorithm can and can't represent by looking at it:
    ///   SIGHTLINE (last known)  a single X. One point, no uncertainty, no motion.
    ///   HEATMAP (grid)          a heat map, one cell per square. Can hold any shape, blurs with time.
    ///   KESTREL (Kalman)        one ellipse (1 and 2 sigma) and a velocity arrow. One blob, always.
    ///                           Reports it gated out flash grey where they landed.
    ///   SWARM (particles)       a cloud of dots. Can split, can starve.
    /// </summary>
    public sealed class BeliefView : MonoBehaviour
    {
        public EscapeGame Game;
        [Tooltip("Heat map opacity at the hottest cell.")]
        [Range(0f, 1f)] public float HeatAlpha = 0.75f;
        public float ParticleSize = 0.13f;
        [Tooltip("Seconds a gated-out report stays visible.")]
        public float RejectedShowSeconds = 1.5f;

        private Stage _built;
        // Last known
        private LineRenderer _xA, _xB, _lkRing;
        // Grid
        private GameObject _heatQuad;
        private Texture2D _heat;
        private Color32[] _heatPixels;
        private Material _heatMaterial;
        // Kalman
        private LineRenderer _ellipse1, _ellipse2, _velocity;
        private readonly List<Vector3> _ellipseBuffer = new List<Vector3>();
        private int _lastRejectedCount;
        private float _rejectedShownUntil;
        private LineRenderer _rejA, _rejB;
        // Particles
        private ParticleSystem _cloud;
        private ParticleSystem.Particle[] _particles;
        // Report flashes (any tracker): where the latest report was placed.
        private readonly List<(Vector3 pos, float until, bool rejected)> _reports = new List<(Vector3, float, bool)>();
        private LinePool _reportRings;

        private void Start()
        {
            if (Game == null) Game = FindFirstObjectByType<EscapeGame>();
            var c = EscapePalette.Belief;
            _xA = ViewKit.CreateLine(transform, "Last known A", c, 0.12f, EscapeLayers.BeliefMarks);
            _xB = ViewKit.CreateLine(transform, "Last known B", c, 0.12f, EscapeLayers.BeliefMarks);
            _lkRing = ViewKit.CreateLine(transform, "Last known ring", EscapePalette.BeliefSoft, 0.05f, EscapeLayers.BeliefMarks, true);
            _ellipse1 = ViewKit.CreateLine(transform, "Kalman 1 sigma", c, 0.08f, EscapeLayers.BeliefMarks, true);
            _ellipse2 = ViewKit.CreateLine(transform, "Kalman 2 sigma", EscapePalette.BeliefSoft, 0.05f, EscapeLayers.BeliefMarks, true);
            _velocity = ViewKit.CreateLine(transform, "Kalman velocity", c, 0.08f, EscapeLayers.BeliefMarks);
            _rejA = ViewKit.CreateLine(transform, "Gated A", EscapePalette.Rejected, 0.08f, EscapeLayers.BeliefMarks);
            _rejB = ViewKit.CreateLine(transform, "Gated B", EscapePalette.Rejected, 0.08f, EscapeLayers.BeliefMarks);
            _reportRings = new LinePool(transform, "Report", 0.05f, EscapeLayers.BeliefMarks);
            _cloud = CreateCloud();
            Game.EventRaised += OnEvent;
            Game.StageStarted += _ => _reports.Clear();
        }

        private void OnDestroy()
        {
            if (Game != null) Game.EventRaised -= OnEvent;
            if (_heat != null) Destroy(_heat);
            if (_heatMaterial != null) Destroy(_heatMaterial);
        }

        private void OnEvent(StageEvent e)
        {
            // Every report WARDEN takes in flashes a ring where it was placed, so the player sees a mirror or a
            // noisemaker "becoming" a sighting.
            if (e.Type == StageEventType.Sighted || e.Type == StageEventType.Heard || e.Type == StageEventType.LaserTripped || e.Type == StageEventType.Bump || e.Type == StageEventType.Forged)
                _reports.Add((e.Position.ToVector3(), Time.time + 0.8f, false));
            else if (e.Type == StageEventType.ReportRejected)
                _reports.Add((e.Position.ToVector3(), Time.time + RejectedShowSeconds, true));
        }

        private ParticleSystem CreateCloud()
        {
            var go = new GameObject("Swarm particles");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 10000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1e6f;
            main.startSpeed = 0f;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ViewKit.Unlit;
            r.sortingOrder = EscapeLayers.Belief;
            ps.Play();
            return ps;
        }

        private void LateUpdate()
        {
            var stage = Game.Stage;
            var tracker = stage?.Tracker;
            if (stage != _built)
            {
                _built = stage;
                _lastRejectedCount = 0;
                if (tracker is OccupancyGridTracker g) BuildHeat(g, stage);
            }
            var kind = tracker?.Kind;
            DrawLastKnown(kind == TrackerKind.LastKnown ? (LastKnownTracker)tracker : null);
            DrawHeat(kind == TrackerKind.OccupancyGrid ? (OccupancyGridTracker)tracker : null);
            DrawKalman(kind == TrackerKind.Kalman ? (KalmanTracker)tracker : null);
            DrawParticles(kind == TrackerKind.Particle ? (ParticleTracker)tracker : null);
            DrawReports();
        }

        private void DrawLastKnown(LastKnownTracker t)
        {
            bool on = t != null && t.HasEstimate;
            _xA.enabled = _xB.enabled = _lkRing.enabled = on;
            if (!on) return;
            var p = t.BestGuess.ToVector3();
            const float s = 0.35f;
            EscapeDraw.Segment(_xA, p + new Vector3(-s, -s), p + new Vector3(s, s));
            EscapeDraw.Segment(_xB, p + new Vector3(-s, s), p + new Vector3(s, -s));
            // The ring grows with the age of the sighting: a hint that the point is getting stale, which
            // SIGHTLINE itself does not know.
            EscapeDraw.Circle(_lkRing, p, 0.6f + Mathf.Min((float)t.Age, 10f) * 0.15f, 32);
        }

        private void BuildHeat(OccupancyGridTracker g, Stage stage)
        {
            if (_heat != null) Destroy(_heat);
            _heat = new Texture2D(g.Width, g.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _heatPixels = new Color32[g.Width * g.Height];
            if (_heatQuad == null)
            {
                // A unit quad with UVs and white vertex colours, so the unlit sprite shader shows the texture as is.
                _heatQuad = EscapeDraw.CreateMeshObject(transform, "Heat map", EscapeLayers.Belief, out var mesh);
                mesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
                mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateBounds();
                _heatMaterial = new Material(ViewKit.Unlit) { name = "Heat map (generated)" };
                _heatQuad.GetComponent<MeshRenderer>().sharedMaterial = _heatMaterial;
            }
            _heatMaterial.mainTexture = _heat;
            var size = stage.Plan.WorldSize;
            _heatQuad.transform.position = new Vector3((float)size.X * 0.5f, (float)size.Y * 0.5f, 0f);
            _heatQuad.transform.localScale = new Vector3((float)size.X, (float)size.Y, 1f);
        }

        private void DrawHeat(OccupancyGridTracker g)
        {
            if (_heatQuad != null) _heatQuad.SetActive(g != null);
            if (g == null || _heat == null) return;
            double max = System.Math.Max(g.MaxProbability, 1e-9);
            var c = (Color32)EscapePalette.Belief;
            for (int j = 0; j < g.Height; j++)
            {
                for (int i = 0; i < g.Width; i++)
                {
                    // Square root so faint, wide belief stays visible next to a sharp peak.
                    double v = System.Math.Sqrt(g.Probability(i, j) / max);
                    c.a = (byte)(255 * HeatAlpha * v);
                    _heatPixels[j * g.Width + i] = c;
                }
            }
            _heat.SetPixels32(_heatPixels);
            _heat.Apply(false);
        }

        private void DrawKalman(KalmanTracker k)
        {
            bool on = k != null;
            _ellipse1.enabled = _ellipse2.enabled = _velocity.enabled = on;
            if (!on)
            {
                _rejA.enabled = _rejB.enabled = false;
                return;
            }
            var e = k.Ellipse;
            ViewKit.SetEllipse(_ellipse1, _ellipseBuffer, e, 1.0, 0f);
            ViewKit.SetEllipse(_ellipse2, _ellipseBuffer, e, 2.0, 0f);
            var m = k.Mean.ToVector3();
            EscapeDraw.Segment(_velocity, m, m + k.Velocity.ToVector3());

            if (k.RejectedCount != _lastRejectedCount)
            {
                _lastRejectedCount = k.RejectedCount;
                _rejectedShownUntil = Time.time + RejectedShowSeconds;
            }
            bool showRej = Time.time < _rejectedShownUntil;
            _rejA.enabled = _rejB.enabled = showRej;
            if (showRej)
            {
                var p = k.LastRejected.ToVector3();
                const float s = 0.3f;
                EscapeDraw.Segment(_rejA, p + new Vector3(-s, -s), p + new Vector3(s, s));
                EscapeDraw.Segment(_rejB, p + new Vector3(-s, s), p + new Vector3(s, -s));
            }
        }

        private void DrawParticles(ParticleTracker t)
        {
            if (t == null)
            {
                _cloud.Clear();
                return;
            }
            var pf = t.Filter;
            int n = pf.Count;
            if (_particles == null || _particles.Length != n) _particles = new ParticleSystem.Particle[n];
            double uniform = 1.0 / n;
            var c = EscapePalette.Belief;
            c.a = 0.7f;
            for (int k = 0; k < n; k++)
            {
                var p = pf.Positions[k];
                float scale = Mathf.Clamp((float)System.Math.Sqrt(pf.Weights[k] / uniform), 0.6f, 3f);
                _particles[k].position = new Vector3((float)p.X, (float)p.Y, 0f);
                _particles[k].startSize = ParticleSize * scale;
                _particles[k].startColor = c;
                _particles[k].remainingLifetime = 1e6f;
                _particles[k].startLifetime = 1e6f;
            }
            _cloud.SetParticles(_particles, n);
        }

        private void DrawReports()
        {
            _reportRings.Begin();
            float now = Time.time;
            for (int k = _reports.Count - 1; k >= 0; k--)
            {
                var r = _reports[k];
                if (now >= r.until) { _reports.RemoveAt(k); continue; }
                float life = r.rejected ? RejectedShowSeconds : 0.8f;
                float age = 1f - (r.until - now) / life;
                var c = r.rejected ? EscapePalette.Rejected : EscapePalette.Belief;
                c.a = 1f - age;
                EscapeDraw.Circle(_reportRings.Next(c), r.pos, 0.3f + age * 0.9f, 24);
            }
            _reportRings.End();
        }
    }
}
