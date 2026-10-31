using System.Collections.Generic;
using LowObservable.Core.Maths;
using UnityEngine;

namespace LowObservable.Unity.Views
{
    /// <summary>
    /// The hunter, as the player perceives it:
    ///   - The red particle cloud: the Seeker's belief about <i>you</i>. Shown openly in the MVP so the
    ///     player can learn how its estimate reacts to their noise.
    ///   - The Seeker itself, only while the drone's camera could actually see it. It is drawn relative to
    ///     the drone's believed pose (truth relative offset applied to the belief), so a lost player also
    ///     misplaces the Seeker on their map.
    ///   - Decoys you have thrown, also in your belief frame.
    /// </summary>
    public sealed class SeekerView : MonoBehaviour
    {
        public EpisodeRunner Runner;
        public Color CloudColor = new Color(1f, 0.25f, 0.25f, 0.55f);
        public Color SeekerColor = new Color(1f, 0.35f, 0.25f, 1f);
        public Color ConeColor = new Color(1f, 0.35f, 0.25f, 0.35f);
        public Color DecoyColor = new Color(1f, 0.85f, 0.3f, 1f);
        [Tooltip("Particle size at uniform weight; heavier particles draw larger.")]
        public float ParticleSize = 0.12f;
        [Tooltip("Show the Seeker even when not in view (debug).")]
        public bool AlwaysShowSeeker;

        private ParticleSystem _cloud;
        private ParticleSystem.Particle[] _particles;
        private GameObject _seeker;
        private LineRenderer _cone;
        private readonly List<GameObject> _decoys = new List<GameObject>();

        private void Start()
        {
            if (Runner == null) Runner = FindAnyObjectByType<EpisodeRunner>();
            _cloud = CreateCloud();
            _seeker = ViewKit.CreateShape(transform, "Seeker (seen)", SeekerColor, 0.7f, Layers.Seeker, triangle: true);
            _cone = ViewKit.CreateLine(transform, "Seeker view cone", ConeColor, 0.04f, Layers.Seeker);
        }

        private ParticleSystem CreateCloud()
        {
            var go = new GameObject("Seeker belief cloud");
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
            r.sortingOrder = Layers.SeekerCloud;
            ps.Play();
            return ps;
        }

        private void LateUpdate()
        {
            var ep = Runner.Episode;
            DrawCloud(ep);
            DrawSeeker(ep);
            DrawDecoys(ep);
        }

        private void DrawCloud(Core.Simulation.Episode ep)
        {
            var pf = ep.Seeker.Belief;
            int n = pf.Count;
            if (_particles == null || _particles.Length != n) _particles = new ParticleSystem.Particle[n];
            double uniform = 1.0 / n;
            for (int k = 0; k < n; k++)
            {
                var p = pf.Positions[k];
                float scale = Mathf.Clamp((float)System.Math.Sqrt(pf.Weights[k] / uniform), 0.6f, 3f);
                _particles[k].position = new Vector3((float)p.X, (float)p.Y, 0f);
                _particles[k].startSize = ParticleSize * scale;
                _particles[k].startColor = CloudColor;
                _particles[k].remainingLifetime = 1e6f;
                _particles[k].startLifetime = 1e6f;
            }
            _cloud.SetParticles(_particles, n);
        }

        private void DrawSeeker(Core.Simulation.Episode ep)
        {
            var truePose = ep.TruePose;
            var seekerTrue = ep.Seeker.Pose;
            bool visible = AlwaysShowSeeker || Runner.ShowTruth || PlayerCanSee(ep, seekerTrue.Position);
            _seeker.SetActive(visible);
            _cone.enabled = visible;
            if (!visible) return;

            // Re-express the true relative offset in the belief frame: what you see, placed where you think you are.
            var rel = truePose.WorldToLocal(seekerTrue.Position);
            var drawn = Runner.ShowTruth ? seekerTrue.Position : ep.Filter.Mean.LocalToWorld(rel);
            double look = Runner.ShowTruth ? ep.Seeker.LookHeading : ep.Seeker.LookHeading - truePose.Theta + ep.Filter.Mean.Theta;
            _seeker.transform.position = drawn.ToVector3();
            _seeker.transform.rotation = UnityConvert.HeadingRotation(look);

            double half = 0.5 * ep.Tuning.SeekerFieldOfView;
            double range = ep.Tuning.SeekerVisionRange;
            _cone.positionCount = 3;
            _cone.SetPosition(0, (drawn + Vec2.FromAngle(look + half, range)).ToVector3());
            _cone.SetPosition(1, drawn.ToVector3());
            _cone.SetPosition(2, (drawn + Vec2.FromAngle(look - half, range)).ToVector3());
        }

        private static bool PlayerCanSee(Core.Simulation.Episode ep, Vec2 point)
        {
            // Same rules as the drone camera: in range, in field of view, line of sight, lit.
            var t = ep.Tuning;
            var rel = point - ep.TruePose.Position;
            if (rel.Length > t.CameraRange) return false;
            if (System.Math.Abs(Angle.Difference(rel.Angle, ep.TruePose.Theta)) > 0.5 * t.CameraFieldOfView) return false;
            if (ep.Plan.IsDarkAt(point)) return false;
            return ep.Plan.LineOfSight(ep.TruePose.Position, point);
        }

        private void DrawDecoys(Core.Simulation.Episode ep)
        {
            while (_decoys.Count < ep.Decoys.Count)
                _decoys.Add(ViewKit.CreateShape(transform, "Decoy", DecoyColor, 0.3f, Layers.Decoys));
            for (int k = 0; k < _decoys.Count; k++)
            {
                bool active = k < ep.Decoys.Count;
                _decoys[k].SetActive(active);
                if (!active) continue;
                var p = ep.Decoys[k].Position;
                var drawn = Runner.ShowTruth ? p : ep.Filter.Mean.LocalToWorld(ep.TruePose.WorldToLocal(p));
                _decoys[k].transform.position = drawn.ToVector3();
            }
        }
    }
}
