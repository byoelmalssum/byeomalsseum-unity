using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Byeolmalsseum
{
    public enum BmKey { W, A, S, D, Q, E, Shift, Tab, Up, Down, Left, Right, Space, Ctrl, Esc }

    /// <summary>새 Input System / 예전 Input 어느 쪽이 켜져 있어도 동작하도록 감싼 입력</summary>
    public static class BmInput
    {
        public static Vector2 MousePosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width, Screen.height) * .5f;
#else
                return Input.mousePosition;
#endif
            }
        }

        /// <summary>이번 프레임 마우스 이동량 (픽셀)</summary>
        public static Vector2 MouseDelta
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
#endif
            }
        }

        public static bool RightMouseHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
                return Input.GetMouseButton(1);
#endif
            }
        }

        public static bool Held(BmKey k)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (k)
            {
                case BmKey.W: return kb.wKey.isPressed;
                case BmKey.A: return kb.aKey.isPressed;
                case BmKey.S: return kb.sKey.isPressed;
                case BmKey.D: return kb.dKey.isPressed;
                case BmKey.Q: return kb.qKey.isPressed;
                case BmKey.E: return kb.eKey.isPressed;
                case BmKey.Shift: return kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                case BmKey.Tab: return kb.tabKey.isPressed;
                case BmKey.Up: return kb.upArrowKey.isPressed;
                case BmKey.Down: return kb.downArrowKey.isPressed;
                case BmKey.Left: return kb.leftArrowKey.isPressed;
                case BmKey.Right: return kb.rightArrowKey.isPressed;
                case BmKey.Space: return kb.spaceKey.isPressed;
                case BmKey.Ctrl: return kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
                case BmKey.Esc: return kb.escapeKey.isPressed;
            }
            return false;
#else
            return Input.GetKey(ToKeyCode(k))
                || (k == BmKey.Shift && Input.GetKey(KeyCode.RightShift))
                || (k == BmKey.Ctrl && Input.GetKey(KeyCode.RightControl));
#endif
        }

        public static bool Down(BmKey k)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (k)
            {
                case BmKey.W: return kb.wKey.wasPressedThisFrame;
                case BmKey.A: return kb.aKey.wasPressedThisFrame;
                case BmKey.S: return kb.sKey.wasPressedThisFrame;
                case BmKey.D: return kb.dKey.wasPressedThisFrame;
                case BmKey.Q: return kb.qKey.wasPressedThisFrame;
                case BmKey.E: return kb.eKey.wasPressedThisFrame;
                case BmKey.Shift: return kb.leftShiftKey.wasPressedThisFrame;
                case BmKey.Tab: return kb.tabKey.wasPressedThisFrame;
                case BmKey.Up: return kb.upArrowKey.wasPressedThisFrame;
                case BmKey.Down: return kb.downArrowKey.wasPressedThisFrame;
                case BmKey.Left: return kb.leftArrowKey.wasPressedThisFrame;
                case BmKey.Right: return kb.rightArrowKey.wasPressedThisFrame;
                case BmKey.Space: return kb.spaceKey.wasPressedThisFrame;
                case BmKey.Ctrl: return kb.leftCtrlKey.wasPressedThisFrame;
                case BmKey.Esc: return kb.escapeKey.wasPressedThisFrame;
            }
            return false;
#else
            return Input.GetKeyDown(ToKeyCode(k));
#endif
        }

#if !ENABLE_INPUT_SYSTEM
        static KeyCode ToKeyCode(BmKey k)
        {
            switch (k)
            {
                case BmKey.W: return KeyCode.W;
                case BmKey.A: return KeyCode.A;
                case BmKey.S: return KeyCode.S;
                case BmKey.D: return KeyCode.D;
                case BmKey.Q: return KeyCode.Q;
                case BmKey.E: return KeyCode.E;
                case BmKey.Shift: return KeyCode.LeftShift;
                case BmKey.Up: return KeyCode.UpArrow;
                case BmKey.Down: return KeyCode.DownArrow;
                case BmKey.Left: return KeyCode.LeftArrow;
                case BmKey.Right: return KeyCode.RightArrow;
                case BmKey.Space: return KeyCode.Space;
                case BmKey.Ctrl: return KeyCode.LeftControl;
                case BmKey.Esc: return KeyCode.Escape;
                default: return KeyCode.Tab;
            }
        }
#endif
    }
}
