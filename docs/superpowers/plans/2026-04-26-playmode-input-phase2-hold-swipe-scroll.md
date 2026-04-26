# Play Mode Input Simulation — Phase 2 Implementation Plan (Hold + Swipe + Scroll)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Phase 1에서 만든 가상 Mouse 디바이스 + 인프라(`TargetResolver`, `InputInjector`, `WaitConditions`, `ResultSnapshot`)를 재사용해 3개의 입력 도구 — `unity_input_hold`, `unity_input_swipe`, `unity_input_scroll`를 추가한다.

**Architecture:** 기존 Phase 1 인프라 그대로 재사용. `InputInjector`에 `MouseScroll(Vector2)` primitive 한 개만 추가. 핸들러 3개 + Bridge 도구 3개 추가.

**Tech Stack:** Phase 1과 동일 — 기존 `BreadPack.Mcp.Unity.Input` asmdef에 코드를 추가만 한다.

**Spec:** `docs/superpowers/specs/2026-04-26-playmode-input-simulation-design.md` §5.2, §6.3

**Out of scope (deferred to Phase 3 plan):**
- `unity_input_key`, `unity_input_type_text`, `unity_input_pinch` (Keyboard + Touchscreen 가상 디바이스 추가 필요)

---

## File Structure

**Modified**
- `UnityMcpEditor/Editor/Input/Core/InputInjector.cs` — `MouseScroll(Vector2)` 메서드 추가
- `plugins/unity-mcp/.claude-plugin/plugin.json` — 0.4.0 → 0.5.0

**New (Editor handlers)**
- `UnityMcpEditor/Editor/Input/Handlers/HoldHandler.cs`
- `UnityMcpEditor/Editor/Input/Handlers/SwipeHandler.cs`
- `UnityMcpEditor/Editor/Input/Handlers/ScrollHandler.cs`

**New (Bridge tools)**
- `UnityMcpBridge/Tools/Input/HoldTool.cs`
- `UnityMcpBridge/Tools/Input/SwipeTool.cs`
- `UnityMcpBridge/Tools/Input/ScrollTool.cs`

`McpServerBootstrap`는 reflection 자동 등록이므로 핸들러 등록 코드 변경 없음.

---

## Task 1: InputInjector — `MouseScroll(Vector2)` 추가

**Files:**
- Modify: `UnityMcpEditor/Editor/Input/Core/InputInjector.cs`

`Mouse.scroll`은 `Vector2Control`이며 `InputState.Change`로 변경 가능.

- [ ] **Step 1: `MouseScroll` 메서드 추가**

기존 `InputInjector` 클래스 끝(닫는 `}` 직전)에 다음 메서드 추가:

```csharp
        // 스크롤 휠 입력. dx>0 = 오른쪽, dy>0 = 위로 스크롤.
        public static void MouseScroll(Vector2 delta)
        {
            var mouse = VirtualInputDevices.Mouse;
            InputState.Change(mouse.scroll, delta);
            InputSystem.Update();
        }
```

추가 후 파일 전체는:

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

        // 스크롤 휠 입력. dx>0 = 오른쪽, dy>0 = 위로 스크롤.
        public static void MouseScroll(Vector2 delta)
        {
            var mouse = VirtualInputDevices.Mouse;
            InputState.Change(mouse.scroll, delta);
            InputSystem.Update();
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Core/InputInjector.cs
git commit -m "feat(input): add MouseScroll primitive to InputInjector"
```

---

## Task 2: HoldHandler

**Files:**
- Create: `UnityMcpEditor/Editor/Input/Handlers/HoldHandler.cs`

**Spec §5.2:** `MouseMove → MouseDown → Delay(holdMs) → MouseUp`

**Important:** 가드 순서는 Phase 1 fix와 동일 — `EnsurePlayMode()` → `Resolve` → `EnsureReady(kind)`.

- [ ] **Step 1: 코드 작성**

```csharp
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity.Input
{
    public class HoldHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_hold";

        public async Task<object> HandleAsync(JObject @params)
        {
            var ts = TargetSpec.Parse(@params);
            var opts = CommonOptions.Parse(@params);

            var holdMs = @params["holdMs"]?.Value<int?>() ?? 500;
            var buttonStr = @params["button"]?.Value<string>() ?? "left";
            var button = buttonStr switch
            {
                "right" => MouseButton.Right,
                "middle" => MouseButton.Middle,
                _ => MouseButton.Left
            };

            InputSystemGuard.EnsurePlayMode();
            var resolved = TargetResolver.Resolve(ts);
            InputSystemGuard.EnsureReady(resolved.Kind);
            VirtualInputDevices.EnsureRegistered();

            // 시퀀스: Move → Down → 시간 경과 → Up
            InputInjector.MouseMove(resolved.ScreenPoint);
            await MainThreadDispatcher.DelayFrames(1);
            InputInjector.MouseDown(button);

            // holdMs 동안 대기. DelayFrames(1)을 반복해 정확한 시간 흐름 보장.
            // 60fps 기준 약 16ms/frame.
            int frames = Mathf.Max(1, holdMs / 16);
            await MainThreadDispatcher.DelayFrames(frames);

            InputInjector.MouseUp(button);

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                var json = ClickHandler.BuildResolvedJson(resolved);
                json["holdMs"] = holdMs;
                return json;
            });
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Handlers/HoldHandler.cs
git commit -m "feat(input): add HoldHandler for unity_input_hold"
```

---

## Task 3: HoldTool (Bridge)

**Files:**
- Create: `UnityMcpBridge/Tools/Input/HoldTool.cs`

- [ ] **Step 1: 코드 작성**

```csharp
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools.Input;

[McpServerToolType]
public static class HoldTool
{
    [McpServerTool(Name = "unity_input_hold"), Description("Play Mode에서 GameView UI/3D 오브젝트를 길게 누릅니다 (Down → 시간 경과 → Up). 길게 누르기 UI 검증용.")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("타겟 path 또는 JSON 객체")] string? target = null,
        [Description("스크린 좌표 JSON")] string? position = null,
        [Description("3D 월드 좌표 JSON")] string? worldPoint = null,
        [Description("누르고 있는 시간 (ms)")] int holdMs = 500,
        [Description("마우스 버튼")] string button = "left",
        [Description("입력 후 대기 프레임 수")] int waitFrames = 1,
        [Description("대기 조건 JSON")] string? waitFor = null,
        [Description("스크린샷+로그 캡처")] bool captureResult = false,
        CancellationToken ct = default)
    {
        var paramsObj = new Dictionary<string, object?>
        {
            ["holdMs"] = holdMs,
            ["button"] = button,
            ["waitFrames"] = waitFrames,
            ["captureResult"] = captureResult
        };
        if (!string.IsNullOrEmpty(target)) paramsObj["target"] = ClickTool.TryParseOrString(target);
        if (!string.IsNullOrEmpty(position)) paramsObj["position"] = JsonDocument.Parse(position).RootElement;
        if (!string.IsNullOrEmpty(worldPoint)) paramsObj["worldPoint"] = JsonDocument.Parse(worldPoint).RootElement;
        if (!string.IsNullOrEmpty(waitFor)) paramsObj["waitFor"] = JsonDocument.Parse(waitFor).RootElement;

        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_input_hold", paramsJson.RootElement, ct);
        return ClickTool.BuildResult(result.RootElement);
    }
}
```

- [ ] **Step 2: 빌드 확인**

```bash
dotnet build UnityMcpBridge/UnityMcpBridge.csproj
```

- [ ] **Step 3: Commit**

```bash
git add UnityMcpBridge/Tools/Input/HoldTool.cs
git commit -m "feat(bridge): add HoldTool MCP tool surface"
```

---

## Task 4: SwipeHandler

**Files:**
- Create: `UnityMcpEditor/Editor/Input/Handlers/SwipeHandler.cs`

**Spec §5.2:** `direction`/`distance`로 `to` 계산 후 Drag와 동일한 시퀀스. 즉 Drag의 from + 방향 단축형.

- [ ] **Step 1: 코드 작성**

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity.Input
{
    public class SwipeHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_swipe";

        public async Task<object> HandleAsync(JObject @params)
        {
            var fromObj = @params["from"] as JObject ?? throw new System.ArgumentException("'from' 필요");
            var fromSpec = TargetSpec.Parse(fromObj);

            var direction = @params["direction"]?.Value<string>()?.ToLowerInvariant() ?? "right";
            var distance = @params["distance"]?.Value<float?>() ?? 200f;
            var durationMs = @params["durationMs"]?.Value<int?>() ?? 150;
            var opts = CommonOptions.Parse(@params);

            Vector2 dirVec = direction switch
            {
                "up" => Vector2.up,
                "down" => Vector2.down,
                "left" => Vector2.left,
                "right" => Vector2.right,
                _ => throw new System.ArgumentException($"알 수 없는 direction: {direction}. up/down/left/right 중 하나여야 합니다.")
            };

            InputSystemGuard.EnsurePlayMode();
            var fromR = TargetResolver.Resolve(fromSpec);
            InputSystemGuard.EnsureReady(fromR.Kind);
            VirtualInputDevices.EnsureRegistered();

            var toPoint = fromR.ScreenPoint + dirVec * distance;

            // 분할 수: max(2, durationMs / 16ms)
            int steps = Mathf.Max(2, durationMs / 16);
            var path = new List<Vector2> { fromR.ScreenPoint };
            for (int i = 1; i < steps; i++)
            {
                var t = (float)i / steps;
                path.Add(Vector2.Lerp(fromR.ScreenPoint, toPoint, t));
            }
            path.Add(toPoint);

            // 시퀀스: 첫 위치로 이동 → Down → 경유점 이동 → Up
            InputInjector.MouseMove(path[0]);
            await MainThreadDispatcher.DelayFrames(1);
            InputInjector.MouseDown(MouseButton.Left);
            await MainThreadDispatcher.DelayFrames(1);

            for (int i = 1; i < path.Count; i++)
            {
                InputInjector.MouseMove(path[i]);
                await MainThreadDispatcher.DelayFrames(1);
            }

            InputInjector.MouseUp(MouseButton.Left);

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                return new JObject
                {
                    ["type"] = "swipe",
                    ["from"] = ClickHandler.BuildResolvedJson(fromR),
                    ["direction"] = direction,
                    ["distance"] = distance,
                    ["to"] = new JObject { ["x"] = toPoint.x, ["y"] = toPoint.y },
                    ["pathLength"] = path.Count
                };
            });
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Handlers/SwipeHandler.cs
git commit -m "feat(input): add SwipeHandler for unity_input_swipe"
```

---

## Task 5: SwipeTool (Bridge)

**Files:**
- Create: `UnityMcpBridge/Tools/Input/SwipeTool.cs`

- [ ] **Step 1: 코드 작성**

```csharp
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools.Input;

[McpServerToolType]
public static class SwipeTool
{
    [McpServerTool(Name = "unity_input_swipe"), Description("Play Mode에서 from 좌표/오브젝트로부터 방향+거리만큼 스와이프합니다. 모바일 패턴(스크롤뷰 플리킹 등) 검증용.")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("시작 타겟 JSON (예: {\"target\":\"Canvas/ScrollView\"} 또는 {\"position\":{\"x\":100,\"y\":200}})")] string from,
        [Description("스와이프 방향 (\"up\"|\"down\"|\"left\"|\"right\")")] string direction = "right",
        [Description("스와이프 거리 (픽셀)")] float distance = 200,
        [Description("스와이프 지속 시간 (ms)")] int durationMs = 150,
        [Description("입력 후 대기 프레임 수")] int waitFrames = 1,
        [Description("대기 조건 JSON")] string? waitFor = null,
        [Description("스크린샷+로그 캡처")] bool captureResult = false,
        CancellationToken ct = default)
    {
        var paramsObj = new Dictionary<string, object?>
        {
            ["from"] = JsonDocument.Parse(from).RootElement,
            ["direction"] = direction,
            ["distance"] = distance,
            ["durationMs"] = durationMs,
            ["waitFrames"] = waitFrames,
            ["captureResult"] = captureResult
        };
        if (!string.IsNullOrEmpty(waitFor)) paramsObj["waitFor"] = JsonDocument.Parse(waitFor).RootElement;

        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_input_swipe", paramsJson.RootElement, ct);
        return ClickTool.BuildResult(result.RootElement);
    }
}
```

- [ ] **Step 2: 빌드 확인**

```bash
dotnet build UnityMcpBridge/UnityMcpBridge.csproj
```

- [ ] **Step 3: Commit**

```bash
git add UnityMcpBridge/Tools/Input/SwipeTool.cs
git commit -m "feat(bridge): add SwipeTool MCP tool surface"
```

---

## Task 6: ScrollHandler

**Files:**
- Create: `UnityMcpEditor/Editor/Input/Handlers/ScrollHandler.cs`

**Spec §5.2:** `Mouse.current.scroll` 컨트롤에 `Vector2(dx, dy)` 변경 이벤트.

스크롤은 마우스 위치에서 발생 — 먼저 `MouseMove(target)` 후 `MouseScroll(dx, dy)`. 누적 스크롤 이벤트 후 영(zero) 벡터로 리셋해야 다음 스크롤이 정상 동작.

- [ ] **Step 1: 코드 작성**

```csharp
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity.Input
{
    public class ScrollHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_scroll";

        public async Task<object> HandleAsync(JObject @params)
        {
            var ts = TargetSpec.Parse(@params);
            var opts = CommonOptions.Parse(@params);

            var dx = @params["dx"]?.Value<float?>() ?? 0f;
            var dy = @params["dy"]?.Value<float?>() ?? 100f;

            InputSystemGuard.EnsurePlayMode();
            var resolved = TargetResolver.Resolve(ts);
            InputSystemGuard.EnsureReady(resolved.Kind);
            VirtualInputDevices.EnsureRegistered();

            // 시퀀스: Move → Scroll(delta) → 다음 프레임 → Scroll(zero)로 리셋
            InputInjector.MouseMove(resolved.ScreenPoint);
            await MainThreadDispatcher.DelayFrames(1);
            InputInjector.MouseScroll(new Vector2(dx, dy));
            await MainThreadDispatcher.DelayFrames(1);
            // 스크롤은 단발 이벤트 — 0으로 리셋해 다음 호출이 정상 동작하게 함
            InputInjector.MouseScroll(Vector2.zero);

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                var json = ClickHandler.BuildResolvedJson(resolved);
                json["dx"] = dx;
                json["dy"] = dy;
                return json;
            });
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Handlers/ScrollHandler.cs
git commit -m "feat(input): add ScrollHandler for unity_input_scroll"
```

---

## Task 7: ScrollTool (Bridge)

**Files:**
- Create: `UnityMcpBridge/Tools/Input/ScrollTool.cs`

- [ ] **Step 1: 코드 작성**

```csharp
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools.Input;

[McpServerToolType]
public static class ScrollTool
{
    [McpServerTool(Name = "unity_input_scroll"), Description("Play Mode에서 GameView 좌표 위치에서 마우스 휠 스크롤을 발생시킵니다. ScrollRect 검증용.")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("타겟 path 또는 JSON 객체")] string? target = null,
        [Description("스크린 좌표 JSON")] string? position = null,
        [Description("3D 월드 좌표 JSON")] string? worldPoint = null,
        [Description("수평 스크롤 (양수=오른쪽)")] float dx = 0,
        [Description("수직 스크롤 (양수=위로)")] float dy = 100,
        [Description("입력 후 대기 프레임 수")] int waitFrames = 1,
        [Description("대기 조건 JSON")] string? waitFor = null,
        [Description("스크린샷+로그 캡처")] bool captureResult = false,
        CancellationToken ct = default)
    {
        var paramsObj = new Dictionary<string, object?>
        {
            ["dx"] = dx,
            ["dy"] = dy,
            ["waitFrames"] = waitFrames,
            ["captureResult"] = captureResult
        };
        if (!string.IsNullOrEmpty(target)) paramsObj["target"] = ClickTool.TryParseOrString(target);
        if (!string.IsNullOrEmpty(position)) paramsObj["position"] = JsonDocument.Parse(position).RootElement;
        if (!string.IsNullOrEmpty(worldPoint)) paramsObj["worldPoint"] = JsonDocument.Parse(worldPoint).RootElement;
        if (!string.IsNullOrEmpty(waitFor)) paramsObj["waitFor"] = JsonDocument.Parse(waitFor).RootElement;

        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_input_scroll", paramsJson.RootElement, ct);
        return ClickTool.BuildResult(result.RootElement);
    }
}
```

- [ ] **Step 2: 빌드 확인**

```bash
dotnet build UnityMcpBridge/UnityMcpBridge.csproj
```

- [ ] **Step 3: Commit**

```bash
git add UnityMcpBridge/Tools/Input/ScrollTool.cs
git commit -m "feat(bridge): add ScrollTool MCP tool surface"
```

---

## Task 8: 플러그인 매니페스트 버전 bump

**Files:**
- Modify: `plugins/unity-mcp/.claude-plugin/plugin.json`

- [ ] **Step 1: 버전 변경**

`"version": "0.4.0"` → `"version": "0.5.0"`

- [ ] **Step 2: Commit**

```bash
git add plugins/unity-mcp/.claude-plugin/plugin.json
git commit -m "chore: bump plugin to 0.5.0 for Phase 2 input tools (hold/swipe/scroll)"
```

---

## Task 9: PR + 머지

- [ ] **Step 1: 빌드 검증**

```bash
dotnet build UnityMcpBridge/UnityMcpBridge.csproj
```

기대: 0 errors, 0 warnings.

- [ ] **Step 2: Push**

```bash
git push -u origin feat/playmode-input-phase2
```

- [ ] **Step 3: PR 생성**

```bash
gh pr create --title "feat: Play Mode input simulation Phase 2 (hold + swipe + scroll)" --body "..."
```

본문에 다음 포함:
- Summary: 3개 새 도구, 기존 인프라 재사용, plugin 0.4.0 → 0.5.0
- Spec/Plan 링크
- Test plan: 3개 도구 각각 Play Mode 검증 시나리오 (toggle button hold, ScrollRect swipe, ScrollRect wheel scroll)

- [ ] **Step 4: 코드 리뷰어 dispatch**

전체 변경(7개 신규 파일 + 2개 modified)에 대해 `superpowers:code-reviewer` subagent dispatch. Critical/Important 이슈 발견 시 fix 후 force push.

- [ ] **Step 5: Squash 머지**

```bash
gh pr merge <PR#> --squash --delete-branch
```

- [ ] **Step 6: 로컬 main 동기화**

```bash
git checkout main
git fetch --prune origin
git reset --hard origin/main
git branch -D feat/playmode-input-phase2
```

---

## Self-Review Checklist (반영 결과)

**Spec coverage:**
- §5.2 Hold (Move→Down→Delay→Up): Task 2 ✅
- §5.2 Swipe (direction/distance → Drag): Task 4 ✅
- §5.2 Scroll (Mouse.scroll Vector2): Task 1 + Task 6 ✅
- §6.3 Hold/Swipe/Scroll 시그니처: Task 3/5/7 ✅

**Phase 1과 일관성:**
- 가드 순서 (`EnsurePlayMode → Resolve → EnsureReady`): 모든 핸들러 동일 패턴
- 응답 페이로드: `ClickHandler.BuildResolvedJson` 재사용
- Bridge 도구 패턴: `ClickTool.TryParseOrString`, `ClickTool.BuildResult` 재사용

**Placeholder scan:** 모든 step에 실제 코드/명령. "TBD"/"TODO" 없음.

**Type consistency:**
- `MouseButton` (Phase 1) ↔ Hold/Swipe에서 사용 ✅
- `TargetSpec.Parse` ↔ 모든 핸들러에서 사용 ✅
- `MouseScroll(Vector2)` (Task 1) ↔ Scroll handler에서 사용 (Task 6) ✅
