using System.Collections.Generic;
using LowObservable.Core.Maths;
using LowObservable.Core.Simulation;
using UnityEngine;

namespace LowObservable.Unity.Views
{
    /// <summary>
    /// The end-of-floor reveal: where you really went (white) against where you thought you were (blue),
    /// the Seeker's real path (red), and a scrubber that replays the belief ellipse and the Seeker's guess
    /// over time. This screen is where the learning lands, so it is deliberately the first thing to polish.
    /// </summary>
    public sealed class RevealView : MonoBehaviour
    {
        public EpisodeRunner Runner;
        public Color TruthColor = new Color(1f, 1f, 1f, 0.95f);
        public Color BeliefColor = new Color(0.35f, 0.70f, 1f, 0.95f);
        public Color SeekerColor = new Color(1f, 0.3f, 0.25f, 0.8f);
        public Color SeekerGuessColor = new Color(1f, 0.6f, 0.5f, 0.8f);

        private LineRenderer _truthPath, _beliefPath, _seekerPath, _scrubEllipse, _scrubLink;
        private GameObject _scrubTruth, _scrubBelief, _scrubGuess;
        private readonly List<Vector3> _buffer = new List<Vector3>();
        private bool _showing;
        private float _scrub = 1f;
        private GUIStyle _box;

        private void Start()
        {
            if (Runner == null) Runner = FindAnyObjectByType<EpisodeRunner>();
            _truthPath = ViewKit.CreateLine(transform, "Reveal truth path", TruthColor, 0.06f, Layers.Reveal);
            _beliefPath = ViewKit.CreateLine(transform, "Reveal belief path", BeliefColor, 0.06f, Layers.Reveal);
            _seekerPath = ViewKit.CreateLine(transform, "Reveal seeker path", SeekerColor, 0.05f, Layers.Reveal);
            _scrubEllipse = ViewKit.CreateLine(transform, "Reveal ellipse", BeliefColor, 0.04f, Layers.Reveal, loop: true);
            _scrubLink = ViewKit.CreateLine(transform, "Reveal error", TruthColor, 0.02f, Layers.Reveal);
            _scrubTruth = ViewKit.CreateShape(transform, "Reveal truth", TruthColor, 0.4f, Layers.Reveal + 1, triangle: true);
            _scrubBelief = ViewKit.CreateShape(transform, "Reveal belief", BeliefColor, 0.4f, Layers.Reveal + 1, triangle: true);
            _scrubGuess = ViewKit.CreateShape(transform, "Reveal seeker guess", SeekerGuessColor, 0.3f, Layers.Reveal + 1);
            Runner.EpisodeEnded += Show;
            Runner.EpisodeStarted += _ => Hide();
            Hide();
        }

        private void OnDestroy()
        {
            if (Runner == null) return;
            Runner.EpisodeEnded -= Show;
        }

        private void Show(Episode ep)
        {
            var samples = ep.Recorder.Samples;
            SetPath(_truthPath, samples, s => new Vector3((float)s.TrueX, (float)s.TrueY));
            SetPath(_beliefPath, samples, s => new Vector3((float)s.EstX, (float)s.EstY));
            SetPath(_seekerPath, samples, s => new Vector3((float)s.SeekerX, (float)s.SeekerY));
            _scrub = 1f;
            SetVisible(true);
        }

        private void Hide() => SetVisible(false);

        private void SetVisible(bool on)
        {
            _showing = on;
            foreach (var l in new[] { _truthPath, _beliefPath, _seekerPath, _scrubEllipse, _scrubLink }) l.enabled = on;
            foreach (var g in new[] { _scrubTruth, _scrubBelief, _scrubGuess }) g.SetActive(on);
        }

        private static void SetPath(LineRenderer line, IReadOnlyList<RunSample> samples, System.Func<RunSample, Vector3> f)
        {
            line.positionCount = samples.Count;
            for (int k = 0; k < samples.Count; k++) line.SetPosition(k, f(samples[k]));
        }

        private void LateUpdate()
        {
            if (!_showing) return;
            var samples = Runner.Episode.Recorder.Samples;
            if (samples.Count == 0) return;
            var s = samples[Mathf.Clamp(Mathf.RoundToInt(_scrub * (samples.Count - 1)), 0, samples.Count - 1)];

            _scrubTruth.transform.position = new Vector3((float)s.TrueX, (float)s.TrueY);
            _scrubTruth.transform.rotation = UnityConvert.HeadingRotation(s.TrueTheta);
            _scrubBelief.transform.position = new Vector3((float)s.EstX, (float)s.EstY);
            _scrubBelief.transform.rotation = UnityConvert.HeadingRotation(s.EstTheta);
            _scrubGuess.transform.position = new Vector3((float)s.SeekerBeliefX, (float)s.SeekerBeliefY);
            var e = CovarianceEllipse.FromComponents(new Vec2(s.EstX, s.EstY), s.CovXX, s.CovXY, s.CovYY);
            ViewKit.SetEllipse(_scrubEllipse, _buffer, e, 2.0, 0f);
            _scrubLink.positionCount = 2;
            _scrubLink.SetPosition(0, new Vector3((float)s.EstX, (float)s.EstY));
            _scrubLink.SetPosition(1, new Vector3((float)s.TrueX, (float)s.TrueY));
        }

        private void OnGUI()
        {
            if (!_showing) return;
            var ep = Runner.Episode;
            var sum = ep.Recorder.Summary;
            if (sum == null) return;
            if (_box == null) _box = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.UpperLeft, richText = true, padding = new RectOffset(14, 14, 10, 10) };

            string headline = sum.Outcome == EpisodeOutcome.Escaped ? "<color=#7dff9a><b>ESCAPED</b></color>"
                : sum.Outcome == EpisodeOutcome.Captured ? "<color=#ff6a5a><b>CAPTURED</b></color>" : "<b>OUT OF TIME</b>";
            string verdict = sum.FractionInsideEllipse >= 0.85 ? "Your belief was honest: the truth stayed inside your ellipse."
                : sum.FractionInsideEllipse >= 0.6 ? "Your belief was a little overconfident."
                : "You were confidently wrong for much of the run.";
            string text =
                $"{headline}   {sum.Duration:0.0}s\n\n" +
                $"<color=#ffffff>White</color>: where you really went.   <color=#5ab2ff>Blue</color>: where you thought you were.\n" +
                $"Average error {sum.AverageError:0.00} m, worst {sum.MaxError:0.00} m\n" +
                $"Truth inside your 95% ellipse {sum.FractionInsideEllipse:P0}   (a perfect filter scores about 95%)\n" +
                $"Landmark matches: {sum.CorrectAssociations} right, <b>{sum.WrongAssociations} wrong</b>\n" +
                $"Pings {sum.Pings}, decoys {sum.DecoysThrown}, seen by the Seeker {sum.TimesSeen}x, heard {sum.TimesHeard}x\n\n" +
                $"{verdict}\n\nDrag to replay.  R to try again.";
            float w = 560, h = 270;
            var rect = new Rect((Screen.width - w) / 2, 30, w, h);
            GUI.Box(rect, text, _box);
            _scrub = GUI.HorizontalSlider(new Rect(rect.x + 14, rect.yMax - 26, w - 28, 20), _scrub, 0f, 1f);
        }
    }
}
