# Play Mode Input Simulation — Phase 3 Implementation Plan (Key + TypeText + Pinch)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 마지막 3개 입력 도구 — `unity_input_key`, `unity_input_type_text`, `unity_input_pinch` — 추가. 이를 위해 `VirtualInputDevices`를 확장해 가상 `Keyboard`와 `Touchscreen` 디바이스를 추가하고, 텍스트는 `TextEvent.Create` + `InputSystem.QueueEvent`로, 멀티터치 핀치는 `InputState.Change(touchControl, TouchState)`로 구현한다.

**Architecture:** 새 가상 디바이스 2종을 lazy 등록하는 패턴은 Phase 1의 Mouse 등록과 동일. 핸들러는 기존 가드/리졸버/스냅샷 인프라를 그대로 재사용.

**Tech Stack:** Phase 1/2와 동일. New Input System의 `Keyboard` (`Key` enum, `TextEvent`), `Touchscreen` (`TouchState`).

**Spec:** `docs/superpowers/specs/2026-04-26-playmode-input-simulation-design.md` §5.2, §6.3, §10

**Known risks (from spec §10):**
- TypeText는 ASCII 우선. IME/한글 입력은 후속 이슈로 이연.
- 멀티터치 동작은 Unity 버전별로 미묘한 차이가 있을 수 있음 — 핀치 검증 시 ScrollView 등 실제 멀티터치 콜백 컴포넌트로 동작 확인 필요.

---

## File Structure

**Modified**
- `UnityMcpEditor/Editor/Input/Core/VirtualInputDevices.cs` — `Keyboard`, `Touchscreen` 디바이스 추가
- `UnityMcpEditor/Editor/Input/Core/InputInjector.cs` — `KeyDown/KeyUp`, `SendText`, `Touch(index, ...)` primitives 추가
- `plugins/unity-mcp/.claude-plugin/plugin.json` — 0.5.0 → 0.6.0

**New (Editor handlers)**
- `UnityMcpEditor/Editor/Input/Handlers/KeyHandler.cs`
- `UnityMcpEditor/Editor/Input/Handlers/TypeTextHandler.cs`
- `UnityMcpEditor/Editor/Input/Handlers/PinchHandler.cs`

**New (Bridge tools)**
- `UnityMcpBridge/Tools/Input/KeyTool.cs`
- `UnityMcpBridge/Tools/Input/TypeTextTool.cs`
- `UnityMcpBridge/Tools/Input/PinchTool.cs`

---

## Task 1: VirtualInputDevices — Keyboard + Touchscreen 추가

**Files:**
- Modify: `UnityMcpEditor/Editor/Input/Core/VirtualInputDevices.cs`

기존 Mouse 패턴 동일. 새 두 디바이스를 추가하고 Reset/EnsureRegistered를 확장.

- [ ] **Step 1: 코드 교체 (전체 파일)**

```csharp
using UnityEditor;
using UnityEngine.InputSystem;

namespace BreadPack.Mcp.Unity.Input
{
    [InitializeOnLoad]
    public static class VirtualInputDevices
    {
        private const string McpMouseName = "McpVirtualMouse";
        private const string McpKeyboardName = "McpVirtualKeyboard";
        private const string McpTouchscreenName = "McpVirtualTouchscreen";

        private static Mouse _mouse;
        private static Keyboard _keyboard;
        private static Touchscreen _touchscreen;

        static VirtualInputDevices()
        {
            // 도메인 리로드 후 / Play Mode 전환 시 디바이스 참조가 사라지므로 플래그 리셋
            AssemblyReloadEvents.afterAssemblyReload += Reset;
            EditorApplication.playModeStateChanged += _ => Reset();
        }

        public static Mouse Mouse
        {
            get
            {
                EnsureRegistered();
                return _mouse;
            }
        }

        public static Keyboard Keyboard
        {
            get
            {
                EnsureRegistered();
                return _keyboard;
            }
        }

        public static Touchscreen Touchscreen
        {
            get
            {
                EnsureRegistered();
                return _touchscreen;
            }
        }

        public static void EnsureRegistered()
        {
            _mouse = EnsureDevice(_mouse, "Mouse", McpMouseName, d => (Mouse)d);
            _keyboard = EnsureDevice(_keyboard, "Keyboard", McpKeyboardName, d => (Keyboard)d);
            _touchscreen = EnsureDevice(_touchscreen, "Touchscreen", McpTouchscreenName, d => (Touchscreen)d);
        }

        private static T EnsureDevice<T>(T cached, string layoutName, string deviceName, System.Func<InputDevice, T> cast)
            where T : InputDevice
        {
            if (cached != null && cached.added) return cached;

            // 이미 같은 이름의 디바이스가 등록되어 있으면 재사용
            foreach (var device in InputSystem.devices)
            {
                if (device is T t && device.name == deviceName)
                    return t;
            }

            return cast(InputSystem.AddDevice(layoutName, deviceName));
        }

        private static void Reset()
        {
            _mouse = null;
            _keyboard = null;
            _touchscreen = null;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Core/VirtualInputDevices.cs
git commit -m "feat(input): add virtual Keyboard and Touchscreen devices"
```

---

## Task 2: InputInjector — Keyboard / Text / Touch primitives

**Files:**
- Modify: `UnityMcpEditor/Editor/Input/Core/InputInjector.cs`

기존 Mouse 메서드는 그대로. 추가 메서드 4개:
- `KeyDown(Key key)` — 단일 키 누름
- `KeyUp(Key key)` — 단일 키 떼기
- `SendText(char ch)` — `TextEvent` 큐잉 (uGUI InputField가 받음)
- `TouchSet(int touchIndex, Vector2 pos, TouchPhase phase, int touchId)` — 단일 터치 슬롯 상태 변경

`TouchState`는 `using UnityEngine.InputSystem.LowLevel`에서 import. `TextEvent.Create(deviceId, ch)`도 동일.

- [ ] **Step 1: 코드 교체 (전체 파일)**

```csharp
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
```

- [ ] **Step 2: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Core/InputInjector.cs
git commit -m "feat(input): add Keyboard/Text/Touch primitives to InputInjector"
```

---

## Task 3: KeyHandler

**Files:**
- Create: `UnityMcpEditor/Editor/Input/Handlers/KeyHandler.cs`

**Spec §6.3:** `key` (UnityEngine.InputSystem.Key 열거자), `modifiers` (["Ctrl","Shift","Alt"]), `action` ("press"|"down"|"up"). 키 입력은 화면 어느 곳도 가리키지 않으므로 가드는 단순(Play Mode만 검증).

- [ ] **Step 1: 코드 작성**

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine.InputSystem;

namespace BreadPack.Mcp.Unity.Input
{
    public class KeyHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_key";

        public async Task<object> HandleAsync(JObject @params)
        {
            var keyStr = @params["key"]?.Value<string>() ?? throw new System.ArgumentException("'key' 필요");
            var action = @params["action"]?.Value<string>() ?? "press";
            var opts = CommonOptions.Parse(@params);

            if (!System.Enum.TryParse<Key>(keyStr, ignoreCase: true, out var key))
                throw new System.ArgumentException($"알 수 없는 key: {keyStr}. UnityEngine.InputSystem.Key 열거자 이름을 사용하세요 (예: Enter, Escape, A, Digit1).");

            var modifiers = ParseModifiers(@params["modifiers"] as JArray);

            InputSystemGuard.EnsurePlayMode();
            VirtualInputDevices.EnsureRegistered();

            // 시퀀스: modifier down(들) → 본 키 (action별) → modifier up(들 — press일 때만)
            if (action == "press" || action == "down")
            {
                foreach (var m in modifiers) InputInjector.KeyDown(m);
                InputInjector.KeyDown(key);
            }
            if (action == "press")
            {
                await MainThreadDispatcher.DelayFrames(1);
            }
            if (action == "press" || action == "up")
            {
                InputInjector.KeyUp(key);
                // 'up'만 호출된 경우 modifiers는 caller가 별도 호출로 관리한다고 가정 — 여기서는 누른 modifier가 없으므로 떼기도 없음.
                if (action == "press")
                {
                    foreach (var m in modifiers) InputInjector.KeyUp(m);
                }
            }

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                return new JObject
                {
                    ["type"] = "key",
                    ["key"] = key.ToString(),
                    ["action"] = action,
                    ["modifiers"] = new JArray(System.Linq.Enumerable.Select(modifiers, m => (object)m.ToString()))
                };
            });
        }

        private static List<Key> ParseModifiers(JArray arr)
        {
            var list = new List<Key>();
            if (arr == null) return list;
            foreach (var item in arr)
            {
                var s = item.Value<string>();
                if (string.IsNullOrEmpty(s)) continue;
                Key m = s.ToLowerInvariant() switch
                {
                    "ctrl" or "control" => Key.LeftCtrl,
                    "shift" => Key.LeftShift,
                    "alt" => Key.LeftAlt,
                    "cmd" or "meta" or "win" => Key.LeftMeta,
                    _ => throw new System.ArgumentException($"알 수 없는 modifier: {s}. Ctrl/Shift/Alt/Cmd 중 하나여야 합니다.")
                };
                list.Add(m);
            }
            return list;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Handlers/KeyHandler.cs
git commit -m "feat(input): add KeyHandler for unity_input_key"
```

---

## Task 4: KeyTool (Bridge)

**Files:**
- Create: `UnityMcpBridge/Tools/Input/KeyTool.cs`

- [ ] **Step 1: 코드 작성**

```csharp
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools.Input;

[McpServerToolType]
public static class KeyTool
{
    [McpServerTool(Name = "unity_input_key"), Description("Play Mode에서 가상 키보드 키를 누릅니다. 단축키, ESC, Enter 등 단일 키 입력용. 텍스트 입력은 unity_input_type_text를 사용하세요.")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("키 이름 (UnityEngine.InputSystem.Key 열거자, 예: \"Enter\", \"Escape\", \"A\", \"Digit1\")")] string key,
        [Description("modifier 키 배열 JSON (예: [\"Ctrl\",\"Shift\"]). \"Ctrl\"|\"Shift\"|\"Alt\"|\"Cmd\".")] string? modifiers = null,
        [Description("\"press\"(누름+뗌) | \"down\"(누름만) | \"up\"(뗌만)")] string action = "press",
        [Description("입력 후 대기 프레임 수")] int waitFrames = 1,
        [Description("대기 조건 JSON")] string? waitFor = null,
        [Description("스크린샷+로그 캡처")] bool captureResult = false,
        CancellationToken ct = default)
    {
        var paramsObj = new Dictionary<string, object?>
        {
            ["key"] = key,
            ["action"] = action,
            ["waitFrames"] = waitFrames,
            ["captureResult"] = captureResult
        };
        if (!string.IsNullOrEmpty(modifiers)) paramsObj["modifiers"] = JsonDocument.Parse(modifiers).RootElement;
        if (!string.IsNullOrEmpty(waitFor)) paramsObj["waitFor"] = JsonDocument.Parse(waitFor).RootElement;

        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_input_key", paramsJson.RootElement, ct);
        return ClickTool.BuildResult(result.RootElement);
    }
}
```

- [ ] **Step 2: 빌드 확인 및 Commit**

```bash
dotnet build UnityMcpBridge/UnityMcpBridge.csproj
git add UnityMcpBridge/Tools/Input/KeyTool.cs
git commit -m "feat(bridge): add KeyTool MCP tool surface"
```

---

## Task 5: TypeTextHandler

**Files:**
- Create: `UnityMcpEditor/Editor/Input/Handlers/TypeTextHandler.cs`

**Spec §5.2:** 각 문자에 대해 (1) `SendText(ch)` 송신, (2) `intervalMs` 간격 대기. uGUI `InputField`/`TMP_InputField`는 `TextEvent`를 받으면 자동으로 캐릭터를 추가한다. ASCII 우선.

- [ ] **Step 1: 코드 작성**

```csharp
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity.Input
{
    public class TypeTextHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_type_text";

        public async Task<object> HandleAsync(JObject @params)
        {
            var text = @params["text"]?.Value<string>() ?? throw new System.ArgumentException("'text' 필요");
            var intervalMs = @params["intervalMs"]?.Value<int?>() ?? 20;
            var opts = CommonOptions.Parse(@params);

            InputSystemGuard.EnsurePlayMode();
            VirtualInputDevices.EnsureRegistered();

            int frameInterval = Mathf.Max(1, intervalMs / 16);

            foreach (var ch in text)
            {
                InputInjector.SendText(ch);
                await MainThreadDispatcher.DelayFrames(frameInterval);
            }

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                return new JObject
                {
                    ["type"] = "type_text",
                    ["length"] = text.Length,
                    ["intervalMs"] = intervalMs
                };
            });
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Handlers/TypeTextHandler.cs
git commit -m "feat(input): add TypeTextHandler for unity_input_type_text"
```

---

## Task 6: TypeTextTool (Bridge)

**Files:**
- Create: `UnityMcpBridge/Tools/Input/TypeTextTool.cs`

- [ ] **Step 1: 코드 작성**

```csharp
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools.Input;

[McpServerToolType]
public static class TypeTextTool
{
    [McpServerTool(Name = "unity_input_type_text"), Description("Play Mode에서 가상 키보드로 텍스트를 입력합니다. uGUI InputField/TMP_InputField가 포커스되어 있어야 합니다. ASCII 우선 — 한글/IME 입력은 미지원.")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("입력할 텍스트")] string text,
        [Description("문자 간 입력 간격 (ms). 자연스러운 타이핑 시뮬레이션.")] int intervalMs = 20,
        [Description("입력 후 대기 프레임 수")] int waitFrames = 1,
        [Description("대기 조건 JSON")] string? waitFor = null,
        [Description("스크린샷+로그 캡처")] bool captureResult = false,
        CancellationToken ct = default)
    {
        var paramsObj = new Dictionary<string, object?>
        {
            ["text"] = text,
            ["intervalMs"] = intervalMs,
            ["waitFrames"] = waitFrames,
            ["captureResult"] = captureResult
        };
        if (!string.IsNullOrEmpty(waitFor)) paramsObj["waitFor"] = JsonDocument.Parse(waitFor).RootElement;

        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_input_type_text", paramsJson.RootElement, ct);
        return ClickTool.BuildResult(result.RootElement);
    }
}
```

- [ ] **Step 2: 빌드 확인 및 Commit**

```bash
dotnet build UnityMcpBridge/UnityMcpBridge.csproj
git add UnityMcpBridge/Tools/Input/TypeTextTool.cs
git commit -m "feat(bridge): add TypeTextTool MCP tool surface"
```

---

## Task 7: PinchHandler

**Files:**
- Create: `UnityMcpEditor/Editor/Input/Handlers/PinchHandler.cs`

**Spec §5.2/§6.3:** `center` 타겟, `startSpread`/`endSpread` 픽셀 거리, `durationMs`. 두 손가락이 center를 기준으로 좌우 대칭으로 위치, 각 프레임마다 spread를 보간하며 이동. 두 finger의 touchId는 1, 2 사용 (구분만 되면 됨).

가이드 축: 수평(좌우). 더 복잡한 핀치(회전, 대각선)는 후속 이슈.

- [ ] **Step 1: 코드 작성**

```csharp
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace BreadPack.Mcp.Unity.Input
{
    public class PinchHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_pinch";

        public async Task<object> HandleAsync(JObject @params)
        {
            var centerObj = @params["center"] as JObject ?? throw new System.ArgumentException("'center' 필요");
            var centerSpec = TargetSpec.Parse(centerObj);

            var startSpread = @params["startSpread"]?.Value<float?>() ?? 100f;
            var endSpread = @params["endSpread"]?.Value<float?>() ?? 300f;
            var durationMs = @params["durationMs"]?.Value<int?>() ?? 300;
            var opts = CommonOptions.Parse(@params);

            InputSystemGuard.EnsurePlayMode();
            var center = TargetResolver.Resolve(centerSpec);
            InputSystemGuard.EnsureReady(center.Kind);
            VirtualInputDevices.EnsureRegistered();

            int steps = Mathf.Max(2, durationMs / 16);
            const int touchId0 = 1;
            const int touchId1 = 2;

            // 시작 위치: center를 기준으로 좌우 대칭
            Vector2 finger0Start = center.ScreenPoint + Vector2.left * (startSpread / 2f);
            Vector2 finger1Start = center.ScreenPoint + Vector2.right * (startSpread / 2f);

            // 1. 두 손가락 동시 Began
            InputInjector.TouchSet(0, finger0Start, TouchPhase.Began, touchId0);
            InputInjector.TouchSet(1, finger1Start, TouchPhase.Began, touchId1);
            await MainThreadDispatcher.DelayFrames(1);

            // 2. spread 보간하며 Moved
            for (int i = 1; i < steps; i++)
            {
                float t = (float)i / steps;
                float spread = Mathf.Lerp(startSpread, endSpread, t);
                Vector2 finger0 = center.ScreenPoint + Vector2.left * (spread / 2f);
                Vector2 finger1 = center.ScreenPoint + Vector2.right * (spread / 2f);

                InputInjector.TouchSet(0, finger0, TouchPhase.Moved, touchId0);
                InputInjector.TouchSet(1, finger1, TouchPhase.Moved, touchId1);
                await MainThreadDispatcher.DelayFrames(1);
            }

            // 3. 최종 위치 + Ended
            Vector2 finger0End = center.ScreenPoint + Vector2.left * (endSpread / 2f);
            Vector2 finger1End = center.ScreenPoint + Vector2.right * (endSpread / 2f);
            InputInjector.TouchSet(0, finger0End, TouchPhase.Ended, touchId0);
            InputInjector.TouchSet(1, finger1End, TouchPhase.Ended, touchId1);

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                var json = ClickHandler.BuildResolvedJson(center);
                json["type"] = "pinch";
                json["startSpread"] = startSpread;
                json["endSpread"] = endSpread;
                json["steps"] = steps;
                return json;
            });
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Handlers/PinchHandler.cs
git commit -m "feat(input): add PinchHandler for unity_input_pinch"
```

---

## Task 8: PinchTool (Bridge)

**Files:**
- Create: `UnityMcpBridge/Tools/Input/PinchTool.cs`

- [ ] **Step 1: 코드 작성**

```csharp
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools.Input;

[McpServerToolType]
public static class PinchTool
{
    [McpServerTool(Name = "unity_input_pinch"), Description("Play Mode에서 두 손가락 핀치 제스처를 시뮬레이션합니다. center를 기준으로 두 손가락이 startSpread → endSpread로 변화. 줌/회전 검증용 (수평 축).")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("중심 타겟 JSON (예: {\"target\":\"Canvas/Photo\"} 또는 {\"position\":{\"x\":480,\"y\":320}})")] string center,
        [Description("초기 두 손가락 거리 (픽셀)")] float startSpread = 100,
        [Description("최종 두 손가락 거리 (픽셀). startSpread보다 크면 zoom-in, 작으면 zoom-out.")] float endSpread = 300,
        [Description("핀치 지속 시간 (ms)")] int durationMs = 300,
        [Description("입력 후 대기 프레임 수")] int waitFrames = 1,
        [Description("대기 조건 JSON")] string? waitFor = null,
        [Description("스크린샷+로그 캡처")] bool captureResult = false,
        CancellationToken ct = default)
    {
        var paramsObj = new Dictionary<string, object?>
        {
            ["center"] = JsonDocument.Parse(center).RootElement,
            ["startSpread"] = startSpread,
            ["endSpread"] = endSpread,
            ["durationMs"] = durationMs,
            ["waitFrames"] = waitFrames,
            ["captureResult"] = captureResult
        };
        if (!string.IsNullOrEmpty(waitFor)) paramsObj["waitFor"] = JsonDocument.Parse(waitFor).RootElement;

        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_input_pinch", paramsJson.RootElement, ct);
        return ClickTool.BuildResult(result.RootElement);
    }
}
```

- [ ] **Step 2: 빌드 확인 및 Commit**

```bash
dotnet build UnityMcpBridge/UnityMcpBridge.csproj
git add UnityMcpBridge/Tools/Input/PinchTool.cs
git commit -m "feat(bridge): add PinchTool MCP tool surface"
```

---

## Task 9: 플러그인 매니페스트 버전 bump

- [ ] **Step 1**: `plugins/unity-mcp/.claude-plugin/plugin.json`에서 `"version": "0.5.0"` → `"version": "0.6.0"`. 다른 필드 변경 없음.

- [ ] **Step 2**: Commit
```bash
git add plugins/unity-mcp/.claude-plugin/plugin.json
git commit -m "chore: bump plugin to 0.6.0 for Phase 3 input tools (key/type-text/pinch)"
```

---

## Task 10: PR + 머지

- [ ] **Step 1**: 빌드 검증
```bash
dotnet build UnityMcpBridge/UnityMcpBridge.csproj
```

- [ ] **Step 2**: Push + PR 생성. 본문에 모든 Phase 3 도구의 manual test plan 항목 포함:
- 키: 단축키(Ctrl+Z 등)로 Undo 동작 확인
- TypeText: TMP_InputField에 ASCII 입력 후 `text` 값 확인
- Pinch: 두 손가락 핀치 인식하는 컴포넌트(예: `IDragHandler` + 두 finger 추적 스크립트)로 검증

- [ ] **Step 3**: 코드 리뷰 dispatch + Critical/Important 이슈 fix

- [ ] **Step 4**: Squash 머지 + 로컬 main 동기화

---

## Self-Review Checklist (반영 결과)

**Spec coverage:**
- §5.2 Key (down/up + modifiers): Task 3 ✅
- §5.2 TypeText (TextEvent per char + intervalMs): Task 5 ✅ (ASCII 우선, 한글 미지원 — spec §10에 이미 기록)
- §5.2 Pinch (Touchscreen 두 finger): Task 7 ✅
- §6.3 시그니처: Task 4/6/8 ✅
- §10 known risks: TypeText 한글 미지원, 멀티터치 검증 권고 — README/PR test plan에 명시

**Pattern consistency:**
- 가드 순서 (`EnsurePlayMode → Resolve → EnsureReady`): Pinch handler에 적용 ✅
- Key/TypeText는 화면 좌표 무관 → `EnsurePlayMode`만 호출 + `EnsureRegistered` ✅
- Bridge 도구는 `ClickTool.BuildResult` 재사용 ✅

**Type consistency:**
- `Key` enum (Task 3) — 동일 방식으로 InputInjector에서 사용 (Task 2) ✅
- `TouchPhase` enum (Task 7) ↔ `InputInjector.TouchSet` (Task 2) ✅

**Placeholder scan:** 모든 step에 실제 코드/명령. "TBD"/"TODO" 없음.
