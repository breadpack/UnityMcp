using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace BreadPack.Mcp.Unity.Input
{
    public enum MouseButton { Left, Right, Middle }

    public static class InputInjector
    {
        // ===== Mouse =====
        public static void MouseMove(Vector2 screenPos)
        {
            var mouse = VirtualInputDevices.Mouse;
            InputState.Change(mouse.position, screenPos);
            InputSystem.Update();
        }

        public static void MouseDown(MouseButton button) => SetButton(button, true);
        public static void MouseUp(MouseButton button) => SetButton(button, false);

        private static void SetButton(MouseButton button, bool isPressed)
        {
            var mouse = VirtualInputDevices.Mouse;
            ButtonControl ctrl = button switch
            {
                MouseButton.Left => mouse.leftButton,
                MouseButton.Right => mouse.rightButton,
                MouseButton.Middle => mouse.middleButton,
                _ => mouse.leftButton
            };
            InputState.Change(ctrl, isPressed ? 1f : 0f);
            InputSystem.Update();
        }

        // 스크롤 휠 입력. dx>0 = 오른쪽, dy>0 = 위로 스크롤.
        public static void MouseScroll(Vector2 delta)
        {
            var mouse = VirtualInputDevices.Mouse;
            InputState.Change(mouse.scroll, delta);
            InputSystem.Update();
        }

        // ===== Keyboard =====
        public static void KeyDown(Key key) => SetKey(key, true);
        public static void KeyUp(Key key) => SetKey(key, false);

        private static void SetKey(Key key, bool isPressed)
        {
            var keyboard = VirtualInputDevices.Keyboard;
            var control = keyboard[key];
            if (control == null)
                throw new System.Exception($"Keyboard에 키 {key}가 없습니다");
            InputState.Change(control, isPressed ? 1f : 0f);
            InputSystem.Update();
        }

        // ASCII/printable 한 문자를 InputField가 받을 수 있는 텍스트 이벤트로 송신.
        public static void SendText(char ch)
        {
            var keyboard = VirtualInputDevices.Keyboard;
            var ev = TextEvent.Create(keyboard.deviceId, ch);
            InputSystem.QueueEvent(ref ev);
            InputSystem.Update();
        }

        // ===== Touchscreen =====
        // touchIndex: Touchscreen.touches 슬롯 인덱스 (0/1 등)
        // touchId: 같은 finger의 시퀀스를 추적하는 ID (Began부터 Ended까지 동일 ID 유지)
        public static void TouchSet(int touchIndex, Vector2 pos, TouchPhase phase, int touchId)
        {
            var touchscreen = VirtualInputDevices.Touchscreen;
            if (touchIndex < 0 || touchIndex >= touchscreen.touches.Count)
                throw new System.Exception($"touchIndex {touchIndex}가 슬롯 범위 밖입니다 (0..{touchscreen.touches.Count - 1})");

            var state = new TouchState
            {
                touchId = touchId,
                position = pos,
                phase = phase,
                pressure = phase == TouchPhase.Ended || phase == TouchPhase.Canceled ? 0f : 1f
            };
            InputState.Change(touchscreen.touches[touchIndex], state);
            InputSystem.Update();
        }
    }
}
