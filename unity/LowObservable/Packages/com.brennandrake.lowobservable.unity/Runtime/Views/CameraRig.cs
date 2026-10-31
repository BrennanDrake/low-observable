using UnityEngine;

namespace LowObservable.Unity.Views
{
    /// <summary>
    /// Orthographic camera that follows the drone's <i>believed</i> position (never the truth), and zooms
    /// out to frame the whole floor on the reveal screen.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        public EpisodeRunner Runner;
        public float PlaySize = 7f;
        public float FollowSharpness = 6f;
        public Color Background = new Color(0.05f, 0.05f, 0.07f);

        private Camera _camera;

        private void Start()
        {
            if (Runner == null) Runner = FindAnyObjectByType<EpisodeRunner>();
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = PlaySize;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Background;
            transform.position = new Vector3(0, 0, -10);
        }

        private void LateUpdate()
        {
            var ep = Runner.Episode;
            Vector3 target;
            float size;
            if (ep.Outcome == Core.Simulation.EpisodeOutcome.Running)
            {
                target = ep.Filter.Mean.Position.ToVector3(-10f);
                size = PlaySize;
            }
            else
            {
                var world = Runner.Plan.WorldSize;
                target = new Vector3((float)world.X * 0.5f, (float)world.Y * 0.5f, -10f);
                size = Mathf.Max((float)world.Y * 0.55f, (float)world.X * 0.55f / Mathf.Max(_camera.aspect, 0.1f));
            }
            float k = 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, target, k);
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, size, k);
        }
    }
}
