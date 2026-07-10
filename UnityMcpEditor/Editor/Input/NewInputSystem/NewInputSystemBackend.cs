using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace BreadPack.Mcp.Unity.Input
{
    public sealed class NewInputSystemBackend : IInputBackend
    {
        public string Name => "new-input-system";
        public string Delivery => "raw-device";
        public int Priority => 100;

        public bool Supports(InputCapabilities capability, ResolvedTarget target = null)
        {
#if ENABLE_INPUT_SYSTEM
            return capability == InputCapabilities.Pointer
                   || capability == InputCapabilities.Keyboard
                   || capability == InputCapabilities.Text
                   || capability == InputCapabilities.Touch;
#else
            return false;
#endif
        }

        public void EnsureReady(InputCapabilities capability, ResolvedTarget target = null)
        {
            if (!Supports(capability, target))
                throw new NotSupportedException("Player Settings의 Active Input Handling에서 Input System이 활성화되어 있지 않습니다.");

            if (target != null && target.Kind == TargetKind.UGui)
            {
                var eventSystem = EventSystem.current;
                if (eventSystem == null || !(eventSystem.currentInputModule is InputSystemUIInputModule))
                    throw new InvalidOperationException(
                        "New Input System으로 uGUI 입력을 전달하려면 EventSystem에 InputSystemUIInputModule이 필요합니다.");
            }

            VirtualInputDevices.EnsureRegistered();
        }

        public void PointerMove(Vector2 screenPosition) => InputInjector.MouseMove(screenPosition);
        public void PointerDown(McpMouseButton button) => InputInjector.MouseDown(button);
        public void PointerUp(McpMouseButton button) => InputInjector.MouseUp(button);
        public void Scroll(Vector2 delta) => InputInjector.MouseScroll(delta);
        public void KeyDown(string key) => InputInjector.KeyDown(key);
        public void KeyUp(string key) => InputInjector.KeyUp(key);
        public void SendText(char character) => InputInjector.SendText(character);

        public void TouchSet(int touchIndex, Vector2 position, McpTouchPhase phase, int touchId)
            => InputInjector.TouchSet(touchIndex, position, phase, touchId);
    }
}
