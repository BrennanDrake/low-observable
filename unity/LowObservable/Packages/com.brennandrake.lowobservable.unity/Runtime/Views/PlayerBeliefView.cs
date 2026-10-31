using System.Collections.Generic;
using LowObservable.Core.Simulation;
using UnityEngine;

namespace LowObservable.Unity.Views
{
    /// <summary>
    /// The player's own belief: the drone drawn at the EKF mean, its 1-sigma and 2-sigma position ellipses,
    /// and a heading-uncertainty fan. This is the "you are probably here" the whole game is played through.
    /// Also flashes the landmarks that measurements were matched to, so association is visible.
    /// </summary>
    public sealed class PlayerBeliefView : MonoBehaviour
    {
        public EpisodeRunner Runner;
        public Color BeliefColor = new Color(0.35f, 0.70f, 1.00f, 1f);
        public Color WrongMatchHint = new Color(1f, 0.4f, 0.3f, 1f);
        public float DroneSize = 0.55f;
        [Tooltip("Ellipse radii to draw, in sigmas. 2 sigma holds about 86% of the probability in 2D.")]
        public float[] Sigmas = { 1f, 2f };
        public float MatchFlashSeconds = 0.35f;

        private GameObject _drone;
        private LineRenderer[] _ellipses;
        private LineRenderer _headingFan;
        private readonly List<Vector3> _buffer = new List<Vector3>();
        private readonly List<(LineRenderer line, float until)> _flashes = new List<(LineRenderer, float)>();

        private void Start()
        {
            if (Runner == null) Runner = FindAnyObjectByType<EpisodeRunner>();
            _drone = ViewKit.CreateShape(transform, "Drone (belief)", BeliefColor, DroneSize, Layers.Drone, triangle: true);
            _ellipses = new LineRenderer[Sigmas.Length];
            for (int k = 0; k < Sigmas.Length; k++)
            {
                var c = BeliefColor;
                c.a = k == 0 ? 0.9f : 0.45f;
                _ellipses[k] = ViewKit.CreateLine(transform, $"Belief {Sigmas[k]}σ", c, 0.05f, Layers.BeliefEllipse, loop: true);
            }
            var fan = BeliefColor;
            fan.a = 0.5f;
            _headingFan = ViewKit.CreateLine(transform, "Heading uncertainty", fan, 0.03f, Layers.BeliefEllipse);
            Runner.EventRaised += OnEvent;
        }

        private void OnDestroy()
        {
            if (Runner != null) Runner.EventRaised -= OnEvent;
        }

        private void LateUpdate()
        {
            var ep = Runner.Episode;
            var mean = ep.Filter.Mean;
            _drone.transform.position = mean.Position.ToVector3();
            _drone.transform.rotation = UnityConvert.HeadingRotation(mean.Theta);

            var ellipse = ep.Filter.PositionEllipse();
            for (int k = 0; k < _ellipses.Length; k++) ViewKit.SetEllipse(_ellipses[k], _buffer, ellipse, Sigmas[k], 0f);

            // Heading fan: two rays at +/- 2 sigma of heading.
            double h2 = 2.0 * ep.Filter.HeadingSigma;
            var origin = mean.Position;
            var left = origin + Core.Maths.Vec2.FromAngle(mean.Theta + h2, 1.2);
            var right = origin + Core.Maths.Vec2.FromAngle(mean.Theta - h2, 1.2);
            _headingFan.positionCount = 3;
            _headingFan.SetPosition(0, left.ToVector3());
            _headingFan.SetPosition(1, origin.ToVector3());
            _headingFan.SetPosition(2, right.ToVector3());

            for (int k = _flashes.Count - 1; k >= 0; k--)
            {
                if (Time.time < _flashes[k].until) continue;
                Destroy(_flashes[k].line.gameObject);
                _flashes.RemoveAt(k);
            }
        }

        private void OnEvent(EpisodeEvent e)
        {
            if (e.Type != EpisodeEventType.ObservationApplied) return;
            // Draw a line from the believed drone to the landmark it was matched with. A wrong match is only
            // tinted when the truth overlay is on, because during play the player cannot know it was wrong.
            var color = BeliefColor;
            if (Runner.ShowTruth && !e.AssociationCorrect) color = WrongMatchHint;
            color.a = 0.8f;
            var line = ViewKit.CreateLine(transform, "Match", color, 0.03f, Layers.BeliefEllipse);
            line.positionCount = 2;
            line.SetPosition(0, Runner.Episode.Filter.Mean.Position.ToVector3());
            line.SetPosition(1, e.Position.ToVector3());
            _flashes.Add((line, Time.time + MatchFlashSeconds));
        }
    }
}
