// A minimal stand-in for the parts of the Input System package (com.unity.inputsystem) that the Unity
// scripts use, so `make unity-check` can also compile the ENABLE_INPUT_SYSTEM branches. Member names and
// the order of Key's letters and digits match the real package (A..Z contiguous, Digit1..Digit9 then
// Digit0), because the scripts do arithmetic on them. Only compiled into the type-check project.
using UnityEngine;

namespace UnityEngine.InputSystem
{
    public enum Key
    {
        None, Space, Enter, Tab, Backquote, Quote, Semicolon, Comma, Period, Slash, Backslash,
        LeftBracket, RightBracket, Minus, Equals,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, Digit0,
        LeftShift, RightShift, LeftAlt, RightAlt, LeftCtrl, RightCtrl, LeftMeta, RightMeta,
        ContextMenu, Escape, LeftArrow, RightArrow, UpArrow, DownArrow, Backspace,
        PageDown, PageUp, Home, End, Insert, Delete, CapsLock, NumLock, PrintScreen, ScrollLock, Pause,
        NumpadEnter, NumpadDivide, NumpadMultiply, NumpadPlus, NumpadMinus, NumpadPeriod, NumpadEquals,
        Numpad0, Numpad1, Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    }

    public class ButtonControl
    {
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
        public bool wasReleasedThisFrame => false;
    }

    public sealed class KeyControl : ButtonControl { }

    public sealed class Vector2Control
    {
        public Vector2 ReadValue() => Vector2.zero;
    }

    public sealed class Keyboard
    {
        public static Keyboard current => null;
        public KeyControl this[Key key] => null;
    }

    public sealed class Mouse
    {
        public static Mouse current => null;
        public Vector2Control position => null;
        public Vector2Control scroll => null;
        public ButtonControl leftButton => null;
        public ButtonControl rightButton => null;
    }
}
