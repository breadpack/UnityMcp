using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using InputSystemTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace BreadPack.Mcp.Unity.Input
{
    public static class InputInjector
    {
        public static void MouseMove(Vector2 screenPosition)
        {
            var mouse = VirtualInputDevices.Mouse;
            InputState.Change(mouse.position, screenPosition);
            InputSystem.Update();
        }

        public static void MouseDown(McpMouseButton button) => SetButton(button, true);
        public static void MouseUp(McpMouseButton button) => SetButton(button, false);

        private static void SetButton(McpMouseButton button, bool isPressed)
        {
            var mouse = VirtualInputDevices.Mouse;
            ButtonControl control = button switch
            {
                McpMouseButton.Left => mouse.leftButton,
                McpMouseButton.Right => mouse.rightButton,
                McpMouseButton.Middle => mouse.middleButton,
                _ => mouse.leftButton
            };
            InputState.Change(control, isPressed ? 1f : 0f);
            InputSystem.Update();
        }

        public static void MouseScroll(Vector2 delta)
        {
            var mouse = VirtualInputDevices.Mouse;
            InputState.Change(mouse.scroll, delta);
            InputSystem.Update();
        }

        public static void KeyDown(string key) => SetKey(ParseKey(key), true);
        public static void KeyUp(string key) => SetKey(ParseKey(key), false);

        private static void SetKey(Key key, bool isPressed)
        {
            var keyboard = VirtualInputDevices.Keyboard;
            var control = keyboard[key];
            if (control == null)
                throw new InvalidOperationException($"Keyboard에 키 {key}가 없습니다.");
            InputState.Change(control, isPressed ? 1f : 0f);
            InputSystem.Update();
        }

        public static void SendText(char character)
        {
            var keyboard = VirtualInputDevices.Keyboard;
            var inputEvent = TextEvent.Create(keyboard.deviceId, character);
            InputSystem.QueueEvent(ref inputEvent);
            InputSystem.Update();
        }

        public static void TouchSet(int touchIndex, Vector2 position, McpTouchPhase phase, int touchId)
        {
            var touchscreen = VirtualInputDevices.Touchscreen;
            if (touchIndex < 0 || touchIndex >= touchscreen.touches.Count)
                throw new ArgumentOutOfRangeException(nameof(touchIndex),
                    $"touchIndex {touchIndex}가 슬롯 범위 밖입니다 (0..{touchscreen.touches.Count - 1}).");

            var inputPhase = ConvertPhase(phase);
            var state = new TouchState
            {
                touchId = touchId,
                position = position,
                phase = inputPhase,
                pressure = inputPhase == InputSystemTouchPhase.Ended || inputPhase == InputSystemTouchPhase.Canceled ? 0f : 1f
            };
            InputState.Change(touchscreen.touches[touchIndex], state);
            InputSystem.Update();
        }

        private static Key ParseKey(string key)
        {
            if (!Enum.TryParse(key, true, out Key parsed))
                throw new ArgumentException(
                    $"알 수 없는 key: {key}. UnityEngine.InputSystem.Key 열거자 이름을 사용하세요 (예: Enter, Escape, A, Digit1).");
            return parsed;
        }

        private static InputSystemTouchPhase ConvertPhase(McpTouchPhase phase)
        {
            return phase switch
            {
                McpTouchPhase.Began => InputSystemTouchPhase.Began,
                McpTouchPhase.Moved => InputSystemTouchPhase.Moved,
                McpTouchPhase.Stationary => InputSystemTouchPhase.Stationary,
                McpTouchPhase.Ended => InputSystemTouchPhase.Ended,
                McpTouchPhase.Canceled => InputSystemTouchPhase.Canceled,
                _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
            };
        }
    }
}
