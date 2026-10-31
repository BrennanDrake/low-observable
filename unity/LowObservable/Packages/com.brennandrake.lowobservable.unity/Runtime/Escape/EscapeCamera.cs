using UnityEngine;

namespace LowObservable.Unity.Escape
{
    /// <summary>
    /// Orthographic camera that follows the player (the truth: in this game you always know where you are),
    /// leaning a little toward the cursor so you can see where you are aiming. M frames the whole level;
    /// so do the menu and the results screen.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class EscapeCamera : MonoBehaviour
    {
        public EscapeGame Game;
        public float PlaySize = 8f;
        public float FollowSharpness = 6f;
        [Range(0f, 0.5f)] public float CursorLean = 0.2f;
        public Color Background = new Color(0.04f, 0.04f, 0.06f);

        private Camera _camera;

        private void Start()
        {
            if (Game == null) Game = FindAnyObjectByType<EscapeGame>();
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = PlaySize;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Background;
            transform.position = new Vector3(0, 0, -10);
        }

        private void LateUpdate()
        {
            var stage = Game.Stage;
            if (stage == null) return;
            var world = stage.Plan.WorldSize;
            var center = new Vector3((float)world.X * 0.5f, (float)world.Y * 0.5f, -10f);
            float whole = Mathf.Max((float)world.Y * 0.55f, (float)world.X * 0.55f / Mathf.Max(_camera.aspect, 0.1f));
            bool overview = Game.Screen != EscapeScreen.Playing && Game.Screen != EscapeScreen.Paused || (Game.Input != null && Game.Input.MapZoomed);

            Vector3 target;
            float size;
            if (overview)
            {
                target = center;
                size = whole;
            }
            else
            {
                var p = stage.Player.ToVector3(-10f);
                var cursor = Game.Input != null ? Game.Input.CursorWorld : p;
                var lean = new Vector3(cursor.x - p.x, cursor.y - p.y, 0f) * CursorLean;
                lean = Vector3.ClampMagnitude(lean, PlaySize * 0.5f);
                target = p + lean;
                size = Mathf.Min(PlaySize, whole);
            }
            float k = 1f - Mathf.Exp(-FollowSharpness * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, target, k);
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, size, k);
        }
    }
}
