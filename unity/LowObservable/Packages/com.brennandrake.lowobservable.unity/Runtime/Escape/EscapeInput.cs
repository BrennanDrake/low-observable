using LowObservable.Core.Maths;
using LowObservable.Core.Stages;
using LowObservable.Unity.Input;
using UnityEngine;

namespace LowObservable.Unity.Escape
{
    /// <summary>
    /// Keyboard and mouse for the escape game. Movement is read every physics tick; one-shot presses are
    /// latched in Update and consumed by the next tick, so quick taps are never lost.
    ///
    ///   WASD / arrows   move              Shift (hold)    run (uses stamina)
    ///   Ctrl or C       sneak             E               interact (hold at terminals)
    ///   1-5 / Q         pick tool         Left click      use tool at the cursor
    ///   Scroll or [ ]   rotate mirror     Right click     cycle tool
    ///   Tab (hold)      WARDEN spec sheet M               toggle map zoom
    ///   R               restart           Esc             pause / back
    ///   Enter / Space   confirm on briefing and results screens
    /// </summary>
    public sealed class EscapeInput : MonoBehaviour
    {
        public EscapeGame Game;
        [Tooltip("Camera used to place the cursor on the floor. Defaults to Camera.main.")]
        public Camera AimCamera;
        [Tooltip("Degrees per scroll notch when rotating the mirror preview.")]
        public float MirrorDegreesPerNotch = 15f;

        public ToolKind SelectedTool { get; private set; } = ToolKind.Throw;
        /// <summary>Heading of the mirror you are about to place, radians.</summary>
        public double MirrorAngle { get; private set; } = System.Math.PI / 4;
        public Vector3 CursorWorld { get; private set; }
        public bool SpecSheetHeld { get; private set; }
        public bool MapZoomed { get; private set; }

        private bool _interact;
        private bool _use;
        private bool _back;
        private bool _restart;
        private bool _confirm;
        private Vector3 _useAim;

        private void Update()
        {
            CursorWorld = InputShim.MouseWorld(AimCamera != null ? AimCamera : Camera.main);
            SpecSheetHeld = InputShim.Held(KeyCode.Tab);
            if (InputShim.Pressed(KeyCode.Escape)) _back = true;
            if (InputShim.Pressed(KeyCode.R)) _restart = true;
            if (InputShim.Pressed(KeyCode.Return) || InputShim.Pressed(KeyCode.KeypadEnter) || InputShim.Pressed(KeyCode.Space)) _confirm = true;
            if (InputShim.Pressed(KeyCode.M)) MapZoomed = !MapZoomed;

            if (Game == null || Game.Screen != EscapeScreen.Playing || Game.Stage == null) return;
            var stage = Game.Stage;

            if (InputShim.Pressed(KeyCode.E)) _interact = true;
            for (int k = 0; k < ToolInfo.All.Length; k++)
                if (InputShim.Pressed(KeyCode.Alpha1 + k)) Select(stage, ToolInfo.All[k]);
            if (InputShim.Pressed(KeyCode.Q) || InputShim.RightClick) Cycle(stage);

            float notches = InputShim.ScrollNotches;
            if (InputShim.Pressed(KeyCode.LeftBracket)) notches += 1;
            if (InputShim.Pressed(KeyCode.RightBracket)) notches -= 1;
            if (Mathf.Abs(notches) > 0.01f) MirrorAngle = Angle.Wrap(MirrorAngle + notches * MirrorDegreesPerNotch * Mathf.Deg2Rad);

            // Clicks that land on IMGUI (menus, note popup) are not tool uses.
            if (InputShim.LeftClick && GUIUtility.hotControl == 0)
            {
                _use = true;
                _useAim = CursorWorld;
            }
        }

        private void Select(Stage stage, ToolKind tool)
        {
            if (stage.HasTool(tool)) SelectedTool = tool;
        }

        private void Cycle(Stage stage)
        {
            int start = System.Array.IndexOf(ToolInfo.All, SelectedTool);
            for (int k = 1; k <= ToolInfo.All.Length; k++)
            {
                var t = ToolInfo.All[(start + k) % ToolInfo.All.Length];
                if (stage.HasTool(t)) { SelectedTool = t; return; }
            }
        }

        /// <summary>Builds this tick's input. Called by <see cref="EscapeGame"/> from FixedUpdate.</summary>
        public StageInput Read(Stage stage)
        {
            if (!stage.HasTool(SelectedTool)) SelectedTool = ToolKind.Throw;
            var move = Vector2.zero;
            if (InputShim.Held(KeyCode.W) || InputShim.Held(KeyCode.UpArrow)) move.y += 1;
            if (InputShim.Held(KeyCode.S) || InputShim.Held(KeyCode.DownArrow)) move.y -= 1;
            if (InputShim.Held(KeyCode.A) || InputShim.Held(KeyCode.LeftArrow)) move.x -= 1;
            if (InputShim.Held(KeyCode.D) || InputShim.Held(KeyCode.RightArrow)) move.x += 1;
            if (move.sqrMagnitude > 1f) move.Normalize();

            var mode = MoveMode.Walk;
            if (InputShim.Held(KeyCode.LeftShift) || InputShim.Held(KeyCode.RightShift)) mode = MoveMode.Run;
            if (InputShim.Held(KeyCode.LeftControl) || InputShim.Held(KeyCode.RightControl) || InputShim.Held(KeyCode.C)) mode = MoveMode.Sneak;

            var aim = _use ? _useAim : CursorWorld;
            var input = new StageInput
            {
                Move = new Vec2(move.x, move.y),
                Mode = mode,
                InteractPressed = _interact,
                InteractHeld = InputShim.Held(KeyCode.E),
                UsePressed = _use,
                Tool = SelectedTool,
                Aim = new Vec2(aim.x, aim.y),
                MirrorAngle = MirrorAngle,
            };
            _interact = false;
            _use = false;
            return input;
        }

        public bool ConsumeBack() { bool b = _back; _back = false; return b; }
        public bool ConsumeRestart() { bool b = _restart; _restart = false; return b; }
        public bool ConsumeConfirm() { bool b = _confirm; _confirm = false; return b; }
    }
}
