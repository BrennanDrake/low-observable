using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LowObservable.Unity.Input
{
    /// <summary>
    /// One place that hides which input backend the project uses (the Input System package or the legacy
    /// Input Manager, per Active Input Handling). Callers name keys with the legacy <see cref="KeyCode"/>
    /// enum; the Input System path maps them. Only the keys the game uses are mapped.
    /// </summary>
    public static class InputShim
    {
        public static bool Held(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && TryMap(key, out var k) && kb[k].isPressed;
#else
            return UnityEngine.Input.GetKey(key);
#endif
        }

        public static bool Pressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && TryMap(key, out var k) && kb[k].wasPressedThisFrame;
#else
            return UnityEngine.Input.GetKeyDown(key);
#endif
        }

        public static Vector2 MousePosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
                return UnityEngine.Input.mousePosition;
#endif
            }
        }

        public static bool LeftClick
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
                return UnityEngine.Input.GetMouseButtonDown(0);
#endif
            }
        }

        public static bool RightClick
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null && mouse.rightButton.wasPressedThisFrame;
#else
                return UnityEngine.Input.GetMouseButtonDown(1);
#endif
            }
        }

        /// <summary>Scroll wheel this frame in notches (positive = away from the player).</summary>
        public static float ScrollNotches
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                // The Input System reports raw deltas (often 120 per notch on Windows, 1 elsewhere).
                float y = mouse != null ? mouse.scroll.ReadValue().y : 0f;
                return Mathf.Abs(y) >= 10f ? y / 120f : y;
#else
                return UnityEngine.Input.mouseScrollDelta.y;
#endif
            }
        }

        /// <summary>Mouse position on the z = 0 game plane.</summary>
        public static Vector3 MouseWorld(Camera cam)
        {
            if (cam == null) return Vector3.zero;
            Vector3 screen = MousePosition;
            screen.z = -cam.transform.position.z;
            var w = cam.ScreenToWorldPoint(screen);
            w.z = 0f;
            return w;
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryMap(KeyCode code, out Key key)
        {
            if (code >= KeyCode.A && code <= KeyCode.Z) { key = Key.A + (code - KeyCode.A); return true; }
            if (code >= KeyCode.Alpha1 && code <= KeyCode.Alpha9) { key = Key.Digit1 + (code - KeyCode.Alpha1); return true; }
            if (code >= KeyCode.F1 && code <= KeyCode.F12) { key = Key.F1 + (code - KeyCode.F1); return true; }
            switch (code)
            {
                case KeyCode.Alpha0: key = Key.Digit0; return true;
                case KeyCode.Space: key = Key.Space; return true;
                case KeyCode.Return: key = Key.Enter; return true;
                case KeyCode.KeypadEnter: key = Key.NumpadEnter; return true;
                case KeyCode.Escape: key = Key.Escape; return true;
                case KeyCode.Tab: key = Key.Tab; return true;
                case KeyCode.Backspace: key = Key.Backspace; return true;
                case KeyCode.LeftShift: key = Key.LeftShift; return true;
                case KeyCode.RightShift: key = Key.RightShift; return true;
                case KeyCode.LeftControl: key = Key.LeftCtrl; return true;
                case KeyCode.RightControl: key = Key.RightCtrl; return true;
                case KeyCode.LeftAlt: key = Key.LeftAlt; return true;
                case KeyCode.UpArrow: key = Key.UpArrow; return true;
                case KeyCode.DownArrow: key = Key.DownArrow; return true;
                case KeyCode.LeftArrow: key = Key.LeftArrow; return true;
                case KeyCode.RightArrow: key = Key.RightArrow; return true;
                case KeyCode.LeftBracket: key = Key.LeftBracket; return true;
                case KeyCode.RightBracket: key = Key.RightBracket; return true;
                default: key = Key.None; return false;
            }
        }
#endif
    }
}
