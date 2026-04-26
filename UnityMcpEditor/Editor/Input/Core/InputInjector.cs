using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace BreadPack.Mcp.Unity.Input
{
    public enum MouseButton { Left, Right, Middle }

    public static class InputInjector
    {
        public static void MouseMove(Vector2 screenPos)
        {
            var mouse = VirtualInputDevices.Mouse;
            InputState.Change(mouse.position, screenPos);
            InputSystem.Update();
        }

        public static void MouseDown(MouseButton button)
        {
            SetButton(button, isPressed: true);
        }

        public static void MouseUp(MouseButton button)
        {
            SetButton(button, isPressed: false);
        }

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
    }
}
