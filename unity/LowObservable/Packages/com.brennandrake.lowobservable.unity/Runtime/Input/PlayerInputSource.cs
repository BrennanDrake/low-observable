using LowObservable.Core.Maths;
using LowObservable.Core.Robot;
using LowObservable.Core.Simulation;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LowObservable.Unity.Input
{
    /// <summary>
    /// Anything that can drive the drone: keyboard, gamepad, the autopilot, a replay. The runner polls
    /// <see cref="ReadCommand"/> once per FixedUpdate.
    /// </summary>
    public abstract class PlayerInputSource : MonoBehaviour
    {
        public abstract PlayerCommand ReadCommand(Episode episode);

        /// <summary>
        /// Index of the association candidate the player picked this frame (0-based), -1 to discard
        /// the measurement, or null for no choice yet. Polled only while the episode is paused for association.
        /// </summary>
        public virtual int? ReadAssociationChoice() => null;

        public virtual bool RestartRequested() => false;
        public virtual bool TruthToggleRequested() => false;
    }

    /// <summary>
    /// Keyboard and mouse controls. Works with the Input System package (preferred) or the legacy Input
    /// Manager, whichever the project's Active Input Handling enables.
    ///
    ///   W/S or Up/Down  drive          A/D or Left/Right  turn
    ///   Shift (hold)    sprint         Ctrl or C (hold)   creep
    ///   Space           ping           F                  listen
    ///   Q               throw decoy at the mouse cursor
    ///   E               dock           R                  restart
    ///   1-9             pick a landmark when asked, 0 = discard     F1  toggle truth overlay (debug)
    ///
    /// One-shot actions are latched in Update and consumed in the next FixedUpdate, so a quick tap is
    /// never lost between physics ticks.
    /// </summary>
    public sealed class KeyboardMouseInput : PlayerInputSource
    {
        [Tooltip("Camera used to turn the mouse position into a decoy target. Defaults to Camera.main.")]
        public Camera AimCamera;

        private PlayerAction _latched = PlayerAction.None;
        private Vector3 _latchedAim;
        private int? _associationChoice;
        private bool _restart;
        private bool _truthToggle;

        private void Update()
        {
            if (Pressed(KeyCode.Space, KeyId.Space)) _latched = PlayerAction.Ping;
            else if (Pressed(KeyCode.F, KeyId.F)) _latched = PlayerAction.Listen;
            else if (Pressed(KeyCode.E, KeyId.E)) _latched = PlayerAction.Dock;
            else if (Pressed(KeyCode.Q, KeyId.Q))
            {
                _latched = PlayerAction.ThrowDecoy;
                _latchedAim = MouseWorld();
            }

            if (Pressed(KeyCode.Alpha0, KeyId.Digit0)) _associationChoice = -1;
            for (int k = 1; k <= 9; k++)
                if (Pressed(KeyCode.Alpha0 + k, KeyId.Digit0 + k)) _associationChoice = k - 1;

            if (Pressed(KeyCode.R, KeyId.R)) _restart = true;
            if (Pressed(KeyCode.F1, KeyId.F1)) _truthToggle = true;
        }

        public override PlayerCommand ReadCommand(Episode episode)
        {
            double forward = 0, turn = 0;
            if (Held(KeyCode.W, KeyId.W) || Held(KeyCode.UpArrow, KeyId.UpArrow)) forward += 1;
            if (Held(KeyCode.S, KeyId.S) || Held(KeyCode.DownArrow, KeyId.DownArrow)) forward -= 1;
            if (Held(KeyCode.A, KeyId.A) || Held(KeyCode.LeftArrow, KeyId.LeftArrow)) turn += 1;
            if (Held(KeyCode.D, KeyId.D) || Held(KeyCode.RightArrow, KeyId.RightArrow)) turn -= 1;

            var mode = MovementMode.Walk;
            if (Held(KeyCode.LeftShift, KeyId.LeftShift) || Held(KeyCode.RightShift, KeyId.RightShift)) mode = MovementMode.Sprint;
            if (Held(KeyCode.LeftControl, KeyId.LeftCtrl) || Held(KeyCode.C, KeyId.C)) mode = MovementMode.Creep;

            var command = PlayerCommand.Drive(forward, turn, mode);
            command.Action = _latched;
            if (_latched == PlayerAction.ThrowDecoy)
            {
                // Aim relative to where you *believe* you are; a lost drone throws decoys in the wrong place.
                command.DecoyTargetLocal = episode.Filter.Mean.WorldToLocal(new Vec2(_latchedAim.x, _latchedAim.y));
            }
            _latched = PlayerAction.None;
            return command;
        }

        public override int? ReadAssociationChoice()
        {
            var c = _associationChoice;
            _associationChoice = null;
            return c;
        }

        public override bool RestartRequested()
        {
            bool r = _restart;
            _restart = false;
            return r;
        }

        public override bool TruthToggleRequested()
        {
            bool t = _truthToggle;
            _truthToggle = false;
            return t;
        }

        private Vector3 MouseWorld()
        {
            var cam = AimCamera != null ? AimCamera : Camera.main;
            if (cam == null) return Vector3.zero;
            Vector3 screen;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            screen = mouse != null ? (Vector3)mouse.position.ReadValue() : Vector3.zero;
#else
            screen = UnityEngine.Input.mousePosition;
#endif
            screen.z = -cam.transform.position.z;
            return cam.ScreenToWorldPoint(screen);
        }

        // ---------------------------------------------------------------- input backend shim

        /// <summary>Key ids for the Input System path; values mirror UnityEngine.InputSystem.Key where used.</summary>
        private enum KeyId
        {
            Space, F, E, Q, R, F1, W, A, S, D, C,
            UpArrow, DownArrow, LeftArrow, RightArrow, LeftShift, RightShift, LeftCtrl,
            Digit0, Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9,
        }

#if ENABLE_INPUT_SYSTEM
        private static Key ToKey(KeyId id)
        {
            switch (id)
            {
                case KeyId.Space: return Key.Space;
                case KeyId.F: return Key.F;
                case KeyId.E: return Key.E;
                case KeyId.Q: return Key.Q;
                case KeyId.R: return Key.R;
                case KeyId.F1: return Key.F1;
                case KeyId.W: return Key.W;
                case KeyId.A: return Key.A;
                case KeyId.S: return Key.S;
                case KeyId.D: return Key.D;
                case KeyId.C: return Key.C;
                case KeyId.UpArrow: return Key.UpArrow;
                case KeyId.DownArrow: return Key.DownArrow;
                case KeyId.LeftArrow: return Key.LeftArrow;
                case KeyId.RightArrow: return Key.RightArrow;
                case KeyId.LeftShift: return Key.LeftShift;
                case KeyId.RightShift: return Key.RightShift;
                case KeyId.LeftCtrl: return Key.LeftCtrl;
                default: return Key.Digit0 + (id - KeyId.Digit0);
            }
        }
#endif

        private static bool Held(KeyCode legacy, KeyId id)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb[ToKey(id)].isPressed;
#else
            return UnityEngine.Input.GetKey(legacy);
#endif
        }

        private static bool Pressed(KeyCode legacy, KeyId id)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb[ToKey(id)].wasPressedThisFrame;
#else
            return UnityEngine.Input.GetKeyDown(legacy);
#endif
        }
    }

    /// <summary>Lets the core autopilot drive the drone in Unity: attract mode, demos, or watching a policy play.</summary>
    public sealed class AutopilotInput : PlayerInputSource
    {
        [Tooltip("reckless, cautious, ghost or passive")]
        public string Profile = "cautious";

        private Autopilot _pilot;
        private Episode _episode;

        public override PlayerCommand ReadCommand(Episode episode)
        {
            if (_pilot == null || _episode != episode)
            {
                _episode = episode;
                _pilot = new Autopilot(episode.Plan, AutopilotProfile.ByName(Profile));
            }
            return _pilot.Decide(episode);
        }

        public override int? ReadAssociationChoice() => 0;
    }
}
