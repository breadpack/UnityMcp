using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BreadPack.Mcp.Unity.Input
{
    // Legacy Input의 raw 상태는 공개 API로 변경할 수 없으므로 uGUI 의미 이벤트를 직접 전달한다.
    public sealed class LegacyUiInputBackend : IInputBackend
    {
        private PointerEventData _pointer;
        private Vector2 _lastPosition;
        private float _lastClickTime;
        private int _clickCount;

        public string Name => "legacy-ui-events";
        public string Delivery => "semantic";
        public int Priority => 200;

        public bool Supports(InputCapabilities capability, ResolvedTarget target = null)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || !(eventSystem.currentInputModule is StandaloneInputModule))
                return false;

            if (capability == InputCapabilities.Pointer)
                return target != null && target.Kind == TargetKind.UGui;
            if (capability == InputCapabilities.Keyboard)
                return true;
            if (capability == InputCapabilities.Text)
                return eventSystem.currentSelectedGameObject != null;
            return false;
        }

        public void EnsureReady(InputCapabilities capability, ResolvedTarget target = null)
        {
            if (!Supports(capability, target))
                throw new NotSupportedException($"Legacy UI backend는 이 {capability} 요청을 지원하지 않습니다.");
        }

        public void PointerMove(Vector2 screenPosition)
        {
            var data = Pointer;
            data.delta = screenPosition - _lastPosition;
            data.position = screenPosition;
            _lastPosition = screenPosition;
            UpdateRaycast(data);

            if (data.pointerDrag != null && data.eligibleForClick)
            {
                ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.beginDragHandler);
                data.dragging = true;
                data.eligibleForClick = false;
            }
            if (data.dragging && data.pointerDrag != null)
                ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.dragHandler);
        }

        public void PointerDown(McpMouseButton button)
        {
            var data = Pointer;
            data.button = ConvertButton(button);
            UpdateRaycast(data);
            data.pressPosition = data.position;
            data.pointerPressRaycast = data.pointerCurrentRaycast;
            var now = Time.unscaledTime;
            _clickCount = now - _lastClickTime <= 0.3f ? _clickCount + 1 : 1;
            _lastClickTime = now;
            data.clickTime = now;
            data.clickCount = _clickCount;
            data.eligibleForClick = true;
            data.dragging = false;
            data.useDragThreshold = true;

            var current = data.pointerCurrentRaycast.gameObject;
            if (current == null)
                throw new InvalidOperationException(
                    $"스크린 좌표 {data.position}에서 Legacy uGUI Raycast 타깃을 찾지 못했습니다.");

            var pressed = ExecuteEvents.ExecuteHierarchy(current, data, ExecuteEvents.pointerDownHandler)
                          ?? ExecuteEvents.GetEventHandler<IPointerClickHandler>(current);
            data.pointerPress = pressed;
            data.rawPointerPress = current;
            data.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(current);
            if (data.pointerDrag != null)
                ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.initializePotentialDrag);
        }

        public void PointerUp(McpMouseButton button)
        {
            var data = Pointer;
            data.button = ConvertButton(button);
            UpdateRaycast(data);

            if (data.pointerPress != null)
                ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);

            var current = data.pointerCurrentRaycast.gameObject;
            var clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(current);
            if (data.eligibleForClick && data.pointerPress == clickHandler)
                ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerClickHandler);
            else if (data.dragging && data.pointerDrag != null)
                ExecuteEvents.ExecuteHierarchy(current, data, ExecuteEvents.dropHandler);

            if (data.dragging && data.pointerDrag != null)
                ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.endDragHandler);

            data.eligibleForClick = false;
            data.dragging = false;
            data.pointerPress = null;
            data.rawPointerPress = null;
            data.pointerDrag = null;
        }

        public void Scroll(Vector2 delta)
        {
            var data = Pointer;
            data.scrollDelta = delta;
            UpdateRaycast(data);
            var current = data.pointerCurrentRaycast.gameObject;
            if (current == null)
                throw new InvalidOperationException(
                    $"스크린 좌표 {data.position}에서 Legacy uGUI 스크롤 타깃을 찾지 못했습니다.");
            ExecuteEvents.ExecuteHierarchy(current, data, ExecuteEvents.scrollHandler);
        }

        public void KeyDown(string key)
        {
            ValidateSemanticKey(key);
        }

        public void KeyUp(string key)
        {
            ValidateSemanticKey(key);
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null)
                throw new InvalidOperationException("Legacy 키 이벤트를 받을 선택된 UI 오브젝트가 없습니다.");

            var data = new BaseEventData(EventSystem.current);
            if (IsEnter(key))
                ExecuteEvents.ExecuteHierarchy(selected, data, ExecuteEvents.submitHandler);
            else
                ExecuteEvents.ExecuteHierarchy(selected, data, ExecuteEvents.cancelHandler);
        }

        public void SendText(char character)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null)
                throw new InvalidOperationException("텍스트를 받을 선택된 UI 오브젝트가 없습니다.");

            var inputField = selected.GetComponent<InputField>();
            if (inputField != null)
            {
                inputField.text = ApplyCharacter(inputField.text, character);
                inputField.caretPosition = inputField.text.Length;
                return;
            }

            foreach (var component in selected.GetComponents<Component>())
            {
                if (component == null || component.GetType().FullName != "TMPro.TMP_InputField") continue;
                var textProperty = component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                if (textProperty == null || !textProperty.CanWrite) break;
                var current = textProperty.GetValue(component) as string ?? string.Empty;
                textProperty.SetValue(component, ApplyCharacter(current, character));
                return;
            }

            throw new NotSupportedException("선택된 오브젝트가 Legacy InputField 또는 TMP_InputField가 아닙니다.");
        }

        public void TouchSet(int touchIndex, Vector2 position, McpTouchPhase phase, int touchId)
        {
            throw new NotSupportedException("Legacy Input.touches는 공개 API로 주입할 수 없습니다. New Input System을 사용하세요.");
        }

        private PointerEventData Pointer => _pointer ??= new PointerEventData(EventSystem.current);

        private static void UpdateRaycast(PointerEventData data)
        {
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);
            data.pointerCurrentRaycast = results.Count > 0 ? results[0] : default;
        }

        private static PointerEventData.InputButton ConvertButton(McpMouseButton button)
        {
            return button switch
            {
                McpMouseButton.Right => PointerEventData.InputButton.Right,
                McpMouseButton.Middle => PointerEventData.InputButton.Middle,
                _ => PointerEventData.InputButton.Left
            };
        }

        private static void ValidateSemanticKey(string key)
        {
            if (!IsEnter(key) && !string.Equals(key, "Escape", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(
                    $"Legacy UI backend는 Enter와 Escape만 의미 이벤트로 지원합니다. '{key}' raw 키 입력에는 New Input System이 필요합니다.");
        }

        private static bool IsEnter(string key)
        {
            return string.Equals(key, "Enter", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(key, "NumpadEnter", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(key, "Return", StringComparison.OrdinalIgnoreCase);
        }

        private static string ApplyCharacter(string current, char character)
        {
            current ??= string.Empty;
            if (character == '\b')
                return current.Length == 0 ? current : current.Substring(0, current.Length - 1);
            if (character == '\r') return current;
            return current + character;
        }
    }
}
