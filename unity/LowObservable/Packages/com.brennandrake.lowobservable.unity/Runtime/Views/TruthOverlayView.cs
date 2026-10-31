using UnityEngine;

namespace LowObservable.Unity.Views
{
    /// <summary>
    /// Debug overlay (F1): the drone's true pose and the error line from belief to truth. Never on during
    /// real play; it exists so you can see the filter working while tuning, and for screenshots in docs.
    /// </summary>
    public sealed class TruthOverlayView : MonoBehaviour
    {
        public EpisodeRunner Runner;
        public Color TruthColor = new Color(1f, 1f, 1f, 0.9f);

        private GameObject _truth;
        private LineRenderer _error;

        private void Start()
        {
            if (Runner == null) Runner = FindAnyObjectByType<EpisodeRunner>();
            _truth = ViewKit.CreateShape(transform, "Drone (truth)", TruthColor, 0.4f, Layers.Truth, triangle: true);
            _error = ViewKit.CreateLine(transform, "Estimation error", TruthColor, 0.03f, Layers.Truth);
        }

        private void LateUpdate()
        {
            bool on = Runner.ShowTruth;
            _truth.SetActive(on);
            _error.enabled = on;
            if (!on) return;
            var ep = Runner.Episode;
            _truth.transform.position = ep.TruePose.Position.ToVector3();
            _truth.transform.rotation = UnityConvert.HeadingRotation(ep.TruePose.Theta);
            _error.positionCount = 2;
            _error.SetPosition(0, ep.Filter.Mean.Position.ToVector3());
            _error.SetPosition(1, ep.TruePose.Position.ToVector3());
        }
    }
}
