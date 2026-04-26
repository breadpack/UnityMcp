# Play Mode Input Simulation — Phase 1 Implementation Plan (Click + Drag)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Unity Editor의 Play Mode 중 GameView에서 AI 에이전트가 `unity_input_click`, `unity_input_drag` 두 MCP 도구로 uGUI/UI Toolkit/3D 오브젝트를 클릭·드래그할 수 있게 한다. 이후 Phase에서 hold/swipe/key/type/pinch/scroll를 추가할 수 있도록 공통 인프라(가상 디바이스·타겟 해석·결과 스냅샷·대기 조건)를 함께 만든다.

**Architecture:** New Input System의 가상 `Mouse` 디바이스에 좌표/버튼 상태를 큐잉하는 단일 입력 경로. `InputSystemUIInputModule`이 자동으로 가상 입력을 수신하여 uGUI/UI Toolkit/3D 모두에 전달된다. 이름 기반 타겟은 `RectTransform`/`Camera.WorldToScreenPoint`로 스크린 좌표로 변환된 후 가상 마우스에 주입된다.

**Tech Stack:** Unity 6000.0+, .NET 9 (Bridge), `com.unity.inputsystem` 1.x, Newtonsoft.Json. 새 Editor 어셈블리 `BreadPack.Mcp.Unity.Input` (조건부 컴파일).

**Spec:** `docs/superpowers/specs/2026-04-26-playmode-input-simulation-design.md`

**Out of scope (deferred to later plans):**
- Phase 2: `unity_input_hold`, `unity_input_swipe`, `unity_input_scroll`
- Phase 3: `unity_input_key`, `unity_input_type_text`, `unity_input_pinch`

---

## File Structure

**New Editor asmdef** (`UnityMcpEditor/Editor/Input/`)
- `BreadPack.Mcp.Unity.Input.asmdef` — 조건부 컴파일 (`com.unity.inputsystem` 미설치 시 스킵)
- `Core/InputSystemGuard.cs` — 환경 검증
- `Core/VirtualInputDevices.cs` — 가상 Mouse 등록·생명주기
- `Core/TargetResolver.cs` — 이름/path → 스크린 좌표
- `Core/InputInjector.cs` — `InputState.Change` 큐잉 (마우스 primitives)
- `Core/WaitConditions.cs` — 5종 predicate 평가
- `Core/ResultSnapshot.cs` — 프레임 진행 + 결과 묶음
- `Core/InputDtos.cs` — Target/Position/공통 옵션 파싱 DTO
- `Handlers/ClickHandler.cs`
- `Handlers/DragHandler.cs`

**Bridge** (`UnityMcpBridge/Tools/Input/`)
- `ClickTool.cs`
- `DragTool.cs`

**Modified**
- `UnityMcpEditor/Editor/BreadPack.Mcp.Unity.asmdef` — `versionDefines` 추가 (메인 asmdef은 그대로지만 후속 phase에서 `InputSystemUIInputModule` 같은 타입을 메인 asmdef 코드에서 참조하지는 않으므로 변경 없음 — Input 서브 asmdef로 격리)
- `.claude-plugin/plugin.json` — 버전 0.3.1 → 0.4.0

`McpServerBootstrap`는 이미 reflection으로 `IRequestHandler`/`IAsyncRequestHandler`를 자동 등록하므로 핸들러 클래스를 추가하기만 하면 된다 (수동 등록 불필요).

---

## Task 1: 입력 전용 Editor 어셈블리 생성

**Files:**
- Create: `UnityMcpEditor/Editor/Input/BreadPack.Mcp.Unity.Input.asmdef`
- Create: `UnityMcpEditor/Editor/Input/.gitkeep` (folder placeholder)

**Why a separate asmdef:** Unity asmdef의 `references` 항목은 무조건 컴파일 시 해석되므로, `Unity.InputSystem`을 메인 asmdef에 직접 추가하면 패키지 미설치 환경에서 전체 어셈블리가 컴파일 실패한다. `defineConstraints` + `versionDefines` 조합으로 "패키지가 설치된 경우에만 컴파일되는 별도 asmdef"를 만든다.

- [ ] **Step 1: 새 폴더 생성**

```bash
mkdir -p UnityMcpEditor/Editor/Input/Core
mkdir -p UnityMcpEditor/Editor/Input/Handlers
```

- [ ] **Step 2: asmdef 작성**

`UnityMcpEditor/Editor/Input/BreadPack.Mcp.Unity.Input.asmdef`:

```json
{
    "name": "BreadPack.Mcp.Unity.Input",
    "rootNamespace": "BreadPack.Mcp.Unity.Input",
    "references": [
        "BreadPack.Mcp.Unity",
        "Unity.InputSystem"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["Newtonsoft.Json.dll"],
    "autoReferenced": true,
    "defineConstraints": ["INPUT_SYSTEM_PRESENT"],
    "versionDefines": [
        {
            "name": "com.unity.inputsystem",
            "expression": "1.0.0",
            "define": "INPUT_SYSTEM_PRESENT"
        }
    ],
    "noEngineReferences": false
}
```

`autoReferenced: true`로 두어 메인 asmdef 등 다른 코드가 이 어셈블리의 publicly-visible 타입을 자유롭게 참조할 수 있도록 한다 (Phase 1 핸들러는 모두 이 어셈블리 안에 있으므로 사실상 무관하지만, 미래 확장 대비).

- [ ] **Step 3: 컴파일 확인**

Unity Editor를 열고 `Console`에 컴파일 에러가 없음을 확인. `Project > Packages` 창에서 `Input System` 패키지가 설치되어 있어야 한다 (없으면 `Window > Package Manager`에서 설치).

- [ ] **Step 4: Commit**

```bash
git add UnityMcpEditor/Editor/Input/BreadPack.Mcp.Unity.Input.asmdef
git commit -m "feat(input): add conditional Editor asmdef for InputSystem-dependent handlers"
```

---

## Task 2: InputSystemGuard

**Files:**
- Create: `UnityMcpEditor/Editor/Input/Core/InputSystemGuard.cs`

**Responsibility:** 매 핸들러 호출 첫 단계에서 환경(Play Mode, 컴파일, EventSystem)을 검증하고 친절한 에러로 실패. 타겟 종류별 분기 검증.

- [ ] **Step 1: 코드 작성**

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace BreadPack.Mcp.Unity.Input
{
    public enum TargetKind { UGui, UiToolkit, World, Screen }

    public static class InputSystemGuard
    {
        public static void EnsureReady(TargetKind kind)
        {
            if (!EditorApplication.isPlaying)
                throw new System.Exception("입력 시뮬레이션은 Play Mode에서만 가능합니다. 먼저 unity_play_mode로 진입하세요.");

            if (EditorApplication.isCompiling)
                throw new System.Exception("컴파일이 끝난 후 다시 시도하세요.");

            if (kind == TargetKind.UGui)
            {
                var es = EventSystem.current;
                if (es == null)
                    throw new System.Exception("씬에 EventSystem이 없습니다. uGUI 입력을 받으려면 EventSystem이 필요합니다.");

                if (!(es.currentInputModule is InputSystemUIInputModule))
                    throw new System.Exception(
                        "EventSystem이 InputSystemUIInputModule을 사용하지 않습니다. " +
                        "EventSystem GameObject에서 'Standalone Input Module'을 제거하고 'Input System UI Input Module'을 추가하세요.");
            }
            // UiToolkit: UIDocument 사용 시 PanelEventHandler가 자동 추가되므로 별도 검증 생략
            // World/Screen: 추가 검증 없음
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity Editor에서 컴파일 에러가 없음을 확인.

- [ ] **Step 3: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Core/InputSystemGuard.cs
git commit -m "feat(input): add InputSystemGuard for environment validation"
```

---

## Task 3: VirtualInputDevices (Mouse only — Phase 1)

**Files:**
- Create: `UnityMcpEditor/Editor/Input/Core/VirtualInputDevices.cs`

**Responsibility:** 가상 `Mouse` 디바이스를 lazy 등록하고, 도메인 리로드/Play Mode 전환 시 재등록되도록 한다. 정적 싱글턴.

- [ ] **Step 1: 코드 작성**

```csharp
using UnityEditor;
using UnityEngine.InputSystem;

namespace BreadPack.Mcp.Unity.Input
{
    [InitializeOnLoad]
    public static class VirtualInputDevices
    {
        private const string MouseLayoutName = "Mouse";
        private const string McpMouseName = "McpVirtualMouse";

        private static Mouse _mouse;

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

        public static void EnsureRegistered()
        {
            if (_mouse != null && _mouse.added) return;

            // 이미 같은 이름의 디바이스가 등록되어 있으면 재사용
            foreach (var device in InputSystem.devices)
            {
                if (device is Mouse m && device.name == McpMouseName)
                {
                    _mouse = m;
                    return;
                }
            }

            _mouse = (Mouse)InputSystem.AddDevice(MouseLayoutName, McpMouseName);
        }

        private static void Reset()
        {
            _mouse = null;
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity Editor에서 컴파일 에러가 없음을 확인.

- [ ] **Step 3: 수동 동작 확인**

`unity_execute_code` 도구로 다음 코드를 실행하여 가상 마우스가 등록되는지 확인:

```csharp
using BreadPack.Mcp.Unity.Input;
using UnityEngine.InputSystem;

VirtualInputDevices.EnsureRegistered();
return $"Devices: {string.Join(\",\", System.Array.ConvertAll(InputSystem.devices.ToArray(), d => d.name))}";
```

기대 결과: 디바이스 목록에 `McpVirtualMouse`가 포함됨.

- [ ] **Step 4: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Core/VirtualInputDevices.cs
git commit -m "feat(input): add VirtualInputDevices with lazy mouse registration"
```

---

## Task 4: InputDtos (공통 파라미터 파싱)

**Files:**
- Create: `UnityMcpEditor/Editor/Input/Core/InputDtos.cs`

**Responsibility:** 모든 입력 핸들러가 받는 공통 파라미터(`target`/`position`/`worldPoint`, `waitFrames`/`waitFor`/`captureResult`)를 파싱하는 DTO와 헬퍼.

- [ ] **Step 1: 코드 작성**

```csharp
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity.Input
{
    public sealed class TargetSpec
    {
        public string Path;          // "Canvas/Panel/Button" or VisualElement name
        public int? Index;           // 동명 분별
        public int? InstanceId;
        public string VisualElement; // UI Toolkit
        public Vector2? Position;    // 직접 스크린 좌표
        public Vector3? WorldPoint;  // 3D 월드 좌표

        public static TargetSpec Parse(JObject root, string targetKey = "target")
        {
            var spec = new TargetSpec();

            // worldPoint
            if (root[$"worldPoint"] is JObject wp)
            {
                spec.WorldPoint = new Vector3(
                    wp["x"]?.Value<float>() ?? 0,
                    wp["y"]?.Value<float>() ?? 0,
                    wp["z"]?.Value<float>() ?? 0);
                return spec;
            }

            // position
            if (root["position"] is JObject pos)
            {
                spec.Position = new Vector2(
                    pos["x"]?.Value<float>() ?? 0,
                    pos["y"]?.Value<float>() ?? 0);
                return spec;
            }

            // target (string or object)
            var target = root[targetKey];
            if (target == null)
                throw new System.ArgumentException($"target/position/worldPoint 중 하나는 필수입니다");

            if (target.Type == JTokenType.String)
            {
                spec.Path = target.Value<string>();
                return spec;
            }

            if (target is JObject obj)
            {
                spec.Path = obj["path"]?.Value<string>();
                spec.Index = obj["index"]?.Value<int?>();
                spec.InstanceId = obj["instanceId"]?.Value<int?>();
                spec.VisualElement = obj["ve"]?.Value<string>();
                return spec;
            }

            throw new System.ArgumentException($"{targetKey}의 형식이 올바르지 않습니다");
        }
    }

    public sealed class CommonOptions
    {
        public int WaitFrames = 1;
        public JObject WaitFor;        // null이면 미사용
        public bool CaptureResult;

        public static CommonOptions Parse(JObject root)
        {
            return new CommonOptions
            {
                WaitFrames = root["waitFrames"]?.Value<int?>() ?? 1,
                WaitFor = root["waitFor"] as JObject,
                CaptureResult = root["captureResult"]?.Value<bool?>() ?? false
            };
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity Editor 컴파일 에러 없음 확인.

- [ ] **Step 3: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Core/InputDtos.cs
git commit -m "feat(input): add TargetSpec and CommonOptions DTOs"
```

---

## Task 5: TargetResolver

**Files:**
- Create: `UnityMcpEditor/Editor/Input/Core/TargetResolver.cs`

**Responsibility:** `TargetSpec` → `Vector2 screenPoint`. uGUI(Canvas 렌더모드 분기) + UI Toolkit + 3D + 직접 좌표 모두 처리.

- [ ] **Step 1: 코드 작성**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace BreadPack.Mcp.Unity.Input
{
    public sealed class ResolvedTarget
    {
        public TargetKind Kind;
        public Vector2 ScreenPoint;
        public string ResolvedPath; // 디버깅/응답용
        public GameObject GameObject; // null 가능 (직접 좌표인 경우)
    }

    public static class TargetResolver
    {
        public static ResolvedTarget Resolve(TargetSpec spec)
        {
            // 1. 직접 좌표
            if (spec.Position.HasValue)
                return new ResolvedTarget { Kind = TargetKind.Screen, ScreenPoint = spec.Position.Value, ResolvedPath = "(screen)" };

            if (spec.WorldPoint.HasValue)
            {
                var cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
                if (cam == null)
                    throw new System.Exception("월드 좌표 변환에 사용할 Camera가 없습니다");
                var sp = cam.WorldToScreenPoint(spec.WorldPoint.Value);
                if (sp.z < 0)
                    throw new System.Exception("월드 좌표가 카메라 뒤에 있습니다");
                return new ResolvedTarget { Kind = TargetKind.World, ScreenPoint = new Vector2(sp.x, sp.y), ResolvedPath = $"(world {spec.WorldPoint.Value})" };
            }

            // 2. UI Toolkit
            if (!string.IsNullOrEmpty(spec.VisualElement))
            {
                var ve = ResolveVisualElement(spec.VisualElement);
                var rect = ve.worldBound; // panel coords (already screen-aligned for Screen panels)
                var center = rect.center;
                // UI Toolkit panel coords: y는 위에서 0, Unity screen coords는 아래에서 0 → 변환
                center.y = Screen.height - center.y;
                return new ResolvedTarget { Kind = TargetKind.UiToolkit, ScreenPoint = center, ResolvedPath = $"VE:{spec.VisualElement}" };
            }

            // 3. uGUI / 3D Object (path or instanceId)
            var go = ResolveGameObject(spec);
            if (go == null)
                throw new System.Exception($"GameObject를 찾을 수 없습니다: {spec.Path ?? spec.InstanceId.ToString()}");

            // active 검증
            if (!go.activeInHierarchy)
                throw new System.Exception($"비활성 상태의 오브젝트는 입력을 받을 수 없습니다: {go.name}");

            // RectTransform이면 uGUI 경로
            if (go.GetComponent<RectTransform>() is RectTransform rt && rt.GetComponentInParent<Canvas>() != null)
            {
                var canvas = rt.GetComponentInParent<Canvas>();
                var worldPos = rt.position;
                Vector2 screen;

                switch (canvas.renderMode)
                {
                    case RenderMode.ScreenSpaceOverlay:
                        screen = new Vector2(worldPos.x, worldPos.y);
                        break;
                    case RenderMode.ScreenSpaceCamera:
                    case RenderMode.WorldSpace:
                        var cam = canvas.worldCamera ?? Camera.main;
                        if (cam == null)
                            throw new System.Exception($"Canvas '{canvas.name}'의 worldCamera가 없고 Camera.main도 없습니다");
                        var sp = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
                        screen = sp;
                        break;
                    default:
                        throw new System.Exception($"알 수 없는 Canvas RenderMode: {canvas.renderMode}");
                }

                return new ResolvedTarget { Kind = TargetKind.UGui, ScreenPoint = screen, ResolvedPath = GetHierarchyPath(go), GameObject = go };
            }

            // 4. 3D 오브젝트 (Renderer/Collider bounds 중심)
            {
                var cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
                if (cam == null)
                    throw new System.Exception("3D 오브젝트 변환에 사용할 Camera가 없습니다");

                Vector3 worldCenter;
                if (go.GetComponent<Renderer>() is Renderer r) worldCenter = r.bounds.center;
                else if (go.GetComponent<Collider>() is Collider c) worldCenter = c.bounds.center;
                else worldCenter = go.transform.position;

                var sp = cam.WorldToScreenPoint(worldCenter);
                if (sp.z < 0)
                    throw new System.Exception($"오브젝트 '{go.name}'가 카메라 뒤에 있습니다");

                return new ResolvedTarget { Kind = TargetKind.World, ScreenPoint = new Vector2(sp.x, sp.y), ResolvedPath = GetHierarchyPath(go), GameObject = go };
            }
        }

        private static GameObject ResolveGameObject(TargetSpec spec)
        {
            if (spec.InstanceId.HasValue)
            {
                var obj = UnityEditor.EditorUtility.InstanceIDToObject(spec.InstanceId.Value);
                return obj as GameObject;
            }

            if (string.IsNullOrEmpty(spec.Path))
                return null;

            // Path 기반: "/"로 분리. 첫 segment는 루트 GameObject 이름
            var segments = spec.Path.Split('/');
            var roots = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(g => g.transform.parent == null && g.name == segments[0])
                .ToList();

            var matches = new List<GameObject>();
            foreach (var root in roots)
            {
                var found = TraverseChildren(root.transform, segments, 1);
                if (found != null) matches.Add(found.gameObject);
            }

            if (matches.Count == 0) return null;
            if (matches.Count == 1) return matches[0];

            // 동명 다수
            if (spec.Index.HasValue && spec.Index.Value < matches.Count)
                return matches[spec.Index.Value];

            var paths = string.Join(", ", matches.Select((m, i) => $"#{i}:{GetHierarchyPath(m)}"));
            throw new System.Exception($"동명 후보 다수. 'index' 지정 필요: [{paths}]");
        }

        private static Transform TraverseChildren(Transform current, string[] segments, int idx)
        {
            if (idx >= segments.Length) return current;
            for (int i = 0; i < current.childCount; i++)
            {
                var child = current.GetChild(i);
                if (child.name == segments[idx])
                {
                    var found = TraverseChildren(child, segments, idx + 1);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var path = go.name;
            var t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }

        private static VisualElement ResolveVisualElement(string spec)
        {
            var doc = Object.FindFirstObjectByType<UIDocument>();
            if (doc == null || doc.rootVisualElement == null)
                throw new System.Exception("UIDocument를 찾을 수 없습니다");

            // "name" 또는 "parent/name" 형식
            var segments = spec.Split('/');
            VisualElement current = doc.rootVisualElement;
            foreach (var seg in segments)
            {
                if (string.IsNullOrEmpty(seg)) continue;
                var found = current.Q<VisualElement>(seg);
                if (found == null)
                    throw new System.Exception($"VisualElement '{seg}'를 찾을 수 없습니다 (in '{spec}')");
                current = found;
            }
            return current;
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity Editor 컴파일 에러 없음.

- [ ] **Step 3: 수동 동작 확인 (Edit Mode 가능)**

씬에 `Canvas > Panel > Button` 계층이 있다고 가정. `unity_execute_code`로:

```csharp
using BreadPack.Mcp.Unity.Input;
using Newtonsoft.Json.Linq;

var spec = TargetSpec.Parse(JObject.Parse(@"{""target"":""Canvas/Panel/Button""}"));
var resolved = TargetResolver.Resolve(spec);
return $"{resolved.Kind} @ ({resolved.ScreenPoint.x}, {resolved.ScreenPoint.y}) [{resolved.ResolvedPath}]";
```

기대: uGUI 경로로 해석되어 화면 좌표 출력.

- [ ] **Step 4: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Core/TargetResolver.cs
git commit -m "feat(input): add TargetResolver covering uGUI/UI Toolkit/3D/screen targets"
```

---

## Task 6: InputInjector (mouse primitives)

**Files:**
- Create: `UnityMcpEditor/Editor/Input/Core/InputInjector.cs`

**Responsibility:** 가상 Mouse 디바이스에 위치/버튼 상태 변경 이벤트를 큐잉하는 저수준 API. Phase 1은 마우스만.

- [ ] **Step 1: 코드 작성**

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
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
```

`InputSystem.Update()` 호출 이유: 가상 디바이스에 큐잉된 이벤트가 다음 InputSystem 업데이트 사이클에 처리되어야 UI Module이 PointerEvent를 디스패치한다. 명시적으로 호출하여 결정론적 동작 보장.

- [ ] **Step 2: 컴파일 확인**

Unity Editor 컴파일 에러 없음.

- [ ] **Step 3: 수동 동작 확인 (Play Mode 필요)**

씬에 `Button`이 있고 OnClick에 `Debug.Log("clicked from inject")`가 연결되어 있다고 가정. Play Mode 진입 후 `unity_execute_code`로:

```csharp
using BreadPack.Mcp.Unity.Input;
using UnityEngine;

InputInjector.MouseMove(new Vector2(Screen.width / 2, Screen.height / 2));
InputInjector.MouseDown(MouseButton.Left);
InputInjector.MouseUp(MouseButton.Left);
return "injected";
```

기대: 다음 프레임에 콘솔에 `clicked from inject` 로그 출력 (버튼이 화면 중앙에 있을 때).

- [ ] **Step 4: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Core/InputInjector.cs
git commit -m "feat(input): add InputInjector mouse primitives via InputState.Change"
```

---

## Task 7: WaitConditions

**Files:**
- Create: `UnityMcpEditor/Editor/Input/Core/WaitConditions.cs`

**Responsibility:** 5종 predicate(`objectActive`, `objectExists`, `consoleLogContains`, `sceneLoaded`, `frames`)를 매 프레임 평가하여 결과 반환. 타임아웃은 예외 X — `{satisfied, timedOut, elapsedMs}` 객체 반환.

`consoleLogContains`는 `ConsoleLogBuffer`에 의존. 기존 `GetConsoleLogsHandler` 코드를 보고 정확한 인터페이스 확인 필요(현재 `ConsoleLogBuffer`는 사적 등록 인스턴스). Phase 1에서 `consoleLogContains`는 **단순화 — 직접 `Application.logMessageReceived` 구독으로 한정 시간 누적**하여 패턴 매칭.

- [ ] **Step 1: 코드 작성**

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BreadPack.Mcp.Unity.Input
{
    public sealed class WaitResult
    {
        public string Kind;
        public bool Satisfied;
        public bool TimedOut;
        public long ElapsedMs;

        public JObject ToJson()
        {
            return new JObject
            {
                ["kind"] = Kind,
                ["satisfied"] = Satisfied,
                ["timedOut"] = TimedOut,
                ["elapsedMs"] = ElapsedMs
            };
        }
    }

    public static class WaitConditions
    {
        public static async Task<WaitResult> EvaluateAsync(JObject spec)
        {
            if (spec == null) return null;

            var kind = spec["kind"]?.Value<string>() ?? throw new ArgumentException("waitFor.kind 필요");
            var timeoutMs = spec["timeoutMs"]?.Value<int?>() ?? 3000;

            return kind switch
            {
                "objectActive" => await PollAsync(kind, timeoutMs, () => CheckObjectActive(spec)),
                "objectExists" => await PollAsync(kind, timeoutMs, () => CheckObjectExists(spec)),
                "consoleLogContains" => await CheckConsoleLogAsync(spec, timeoutMs),
                "sceneLoaded" => await PollAsync(kind, timeoutMs, () => CheckSceneLoaded(spec)),
                "frames" => await WaitFramesAsync(spec),
                _ => throw new ArgumentException($"알 수 없는 waitFor.kind: {kind}")
            };
        }

        private static async Task<WaitResult> PollAsync(string kind, int timeoutMs, Func<bool> predicate)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (predicate())
                    return new WaitResult { Kind = kind, Satisfied = true, ElapsedMs = sw.ElapsedMilliseconds };
                await MainThreadDispatcher.DelayFrames(1);
            }
            return new WaitResult { Kind = kind, Satisfied = predicate(), TimedOut = true, ElapsedMs = sw.ElapsedMilliseconds };
        }

        private static bool CheckObjectActive(JObject spec)
        {
            try
            {
                var ts = TargetSpec.Parse(spec);
                var resolved = TargetResolver.Resolve(ts);
                var expected = spec["expected"]?.Value<bool?>() ?? true;
                return (resolved.GameObject?.activeInHierarchy ?? false) == expected;
            }
            catch { return false; }
        }

        private static bool CheckObjectExists(JObject spec)
        {
            try
            {
                var ts = TargetSpec.Parse(spec);
                var go = ResolveOrNull(ts);
                var expected = spec["expected"]?.Value<bool?>() ?? true;
                return (go != null) == expected;
            }
            catch { return false; }
        }

        private static GameObject ResolveOrNull(TargetSpec ts)
        {
            try { return TargetResolver.Resolve(ts).GameObject; }
            catch { return null; }
        }

        private static bool CheckSceneLoaded(JObject spec)
        {
            var name = spec["name"]?.Value<string>() ?? "";
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.name == name && s.isLoaded) return true;
            }
            return false;
        }

        private static async Task<WaitResult> CheckConsoleLogAsync(JObject spec, int timeoutMs)
        {
            var pattern = spec["pattern"]?.Value<string>() ?? throw new ArgumentException("pattern 필요");
            var levelStr = spec["level"]?.Value<string>() ?? "Any";
            var regex = new Regex(pattern);
            var matched = false;

            void OnLog(string condition, string stack, LogType type)
            {
                if (matched) return;
                if (levelStr != "Any" && !MatchLevel(type, levelStr)) return;
                if (regex.IsMatch(condition)) matched = true;
            }

            Application.logMessageReceived += OnLog;
            var sw = Stopwatch.StartNew();
            try
            {
                while (sw.ElapsedMilliseconds < timeoutMs && !matched)
                {
                    await MainThreadDispatcher.DelayFrames(1);
                }
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
            return new WaitResult { Kind = "consoleLogContains", Satisfied = matched, TimedOut = !matched, ElapsedMs = sw.ElapsedMilliseconds };
        }

        private static bool MatchLevel(LogType type, string levelStr)
        {
            return levelStr switch
            {
                "Log" => type == LogType.Log,
                "Warning" => type == LogType.Warning,
                "Error" => type == LogType.Error || type == LogType.Exception || type == LogType.Assert,
                _ => true
            };
        }

        private static async Task<WaitResult> WaitFramesAsync(JObject spec)
        {
            var count = spec["count"]?.Value<int?>() ?? 1;
            var sw = Stopwatch.StartNew();
            await MainThreadDispatcher.DelayFrames(count);
            return new WaitResult { Kind = "frames", Satisfied = true, ElapsedMs = sw.ElapsedMilliseconds };
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity Editor 컴파일 에러 없음.

- [ ] **Step 3: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Core/WaitConditions.cs
git commit -m "feat(input): add WaitConditions with 5 predicates and timeout handling"
```

---

## Task 8: ResultSnapshot

**Files:**
- Create: `UnityMcpEditor/Editor/Input/Core/ResultSnapshot.cs`

**Responsibility:** 입력 후 처리 — `waitFrames` 진행, `waitFor` 평가, `captureResult` 시 스크린샷 + 콘솔 로그 delta 묶음. 응답 JObject 빌드.

콘솔 로그 delta는 `Application.logMessageReceived` 일시 구독으로 구현 (별도의 `ConsoleLogBuffer` 통합은 추후 리팩토링).

- [ ] **Step 1: 코드 작성**

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity.Input
{
    public static class ResultSnapshot
    {
        public static async Task<JObject> CaptureAsync(
            CommonOptions opts,
            Func<JObject> resolvedJsonProvider)
        {
            var response = new JObject
            {
                ["ok"] = true,
                ["resolved"] = resolvedJsonProvider()
            };

            // 콘솔 로그 캡처 시작 (waitFrames + waitFor 동안 기록)
            var logs = new List<JObject>();
            void OnLog(string condition, string stack, LogType type)
            {
                logs.Add(new JObject
                {
                    ["level"] = type.ToString(),
                    ["message"] = condition
                });
            }
            if (opts.CaptureResult) Application.logMessageReceived += OnLog;

            try
            {
                // 1. waitFrames 진행
                if (opts.WaitFrames > 0)
                    await MainThreadDispatcher.DelayFrames(opts.WaitFrames);

                // 2. waitFor 평가
                if (opts.WaitFor != null)
                {
                    var waitResult = await WaitConditions.EvaluateAsync(opts.WaitFor);
                    if (waitResult != null) response["waitFor"] = waitResult.ToJson();
                }

                // 3. captureResult: 스크린샷 + 로그
                if (opts.CaptureResult)
                {
                    var screenshot = await TakeScreenshotInline();
                    response["screenshotBase64"] = screenshot.base64;
                    response["mimeType"] = screenshot.mime;
                    response["width"] = screenshot.width;
                    response["height"] = screenshot.height;
                    response["consoleLogsDelta"] = JArray.FromObject(logs);
                }
            }
            finally
            {
                if (opts.CaptureResult) Application.logMessageReceived -= OnLog;
            }

            return response;
        }

        private static async Task<(string base64, string mime, int width, int height)> TakeScreenshotInline()
        {
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            await MainThreadDispatcher.DelayFrames(1);

            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null)
                return ("", "image/jpeg", 0, 0);

            try
            {
                var bytes = tex.EncodeToJPG(75);
                return (Convert.ToBase64String(bytes), "image/jpeg", tex.width, tex.height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity Editor 컴파일 에러 없음.

- [ ] **Step 3: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Core/ResultSnapshot.cs
git commit -m "feat(input): add ResultSnapshot for post-input wait + capture"
```

---

## Task 9: ClickHandler (Editor)

**Files:**
- Create: `UnityMcpEditor/Editor/Input/Handlers/ClickHandler.cs`

**Responsibility:** `unity_input_click` 도구의 처리. 가드 → 디바이스 보장 → 타겟 해석 → 입력 시퀀스 → 스냅샷.

- [ ] **Step 1: 코드 작성**

```csharp
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace BreadPack.Mcp.Unity.Input
{
    public class ClickHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_click";

        public async Task<object> HandleAsync(JObject @params)
        {
            var ts = TargetSpec.Parse(@params);
            var opts = CommonOptions.Parse(@params);

            var buttonStr = @params["button"]?.Value<string>() ?? "left";
            var count = @params["count"]?.Value<int?>() ?? 1;

            var button = buttonStr switch
            {
                "right" => MouseButton.Right,
                "middle" => MouseButton.Middle,
                _ => MouseButton.Left
            };

            var resolved = TargetResolver.Resolve(ts);
            InputSystemGuard.EnsureReady(resolved.Kind);
            VirtualInputDevices.EnsureRegistered();

            // 시퀀스: Move → (Down → Up) × count
            InputInjector.MouseMove(resolved.ScreenPoint);
            await MainThreadDispatcher.DelayFrames(1);
            for (int i = 0; i < count; i++)
            {
                InputInjector.MouseDown(button);
                await MainThreadDispatcher.DelayFrames(1);
                InputInjector.MouseUp(button);
                if (i < count - 1) await MainThreadDispatcher.DelayFrames(1);
            }

            return await ResultSnapshot.CaptureAsync(opts, () => BuildResolvedJson(resolved));
        }

        internal static JObject BuildResolvedJson(ResolvedTarget r)
        {
            return new JObject
            {
                ["type"] = r.Kind.ToString().ToLowerInvariant(),
                ["path"] = r.ResolvedPath,
                ["screen"] = new JObject
                {
                    ["x"] = r.ScreenPoint.x,
                    ["y"] = r.ScreenPoint.y
                }
            };
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity Editor 컴파일 에러 없음. `McpServerBootstrap`가 reflection 자동 등록하므로 추가 등록 불필요. 다음 도메인 리로드 후 핸들러가 활성화됨.

- [ ] **Step 3: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Handlers/ClickHandler.cs
git commit -m "feat(input): add ClickHandler for unity_input_click"
```

---

## Task 10: ClickTool (Bridge)

**Files:**
- Create: `UnityMcpBridge/Tools/Input/ClickTool.cs`

**Responsibility:** MCP 도구 표면. 파라미터를 JSON으로 직렬화하여 Editor에 전달, 응답 파싱.

- [ ] **Step 1: 코드 작성**

```csharp
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools.Input;

[McpServerToolType]
public static class ClickTool
{
    [McpServerTool(Name = "unity_input_click"), Description("Play Mode에서 GameView UI/3D 오브젝트를 클릭합니다. target/position/worldPoint 중 하나를 지정하세요.")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("타겟 path (예: \"Canvas/Panel/Button\") 또는 JSON 객체 (예: {\"path\":\"Button\",\"index\":1} / {\"instanceId\":123} / {\"ve\":\"root/start-button\"})")] string? target = null,
        [Description("스크린 좌표 JSON (예: {\"x\":480,\"y\":320})")] string? position = null,
        [Description("3D 월드 좌표 JSON (예: {\"x\":0,\"y\":1,\"z\":5})")] string? worldPoint = null,
        [Description("마우스 버튼 (\"left\"|\"right\"|\"middle\")")] string button = "left",
        [Description("클릭 횟수 (2면 더블클릭)")] int count = 1,
        [Description("입력 후 대기 프레임 수")] int waitFrames = 1,
        [Description("대기 조건 JSON (5종 predicate)")] string? waitFor = null,
        [Description("true면 스크린샷 + 콘솔 로그를 응답에 포함")] bool captureResult = false,
        CancellationToken ct = default)
    {
        var paramsObj = new Dictionary<string, object?>
        {
            ["button"] = button,
            ["count"] = count,
            ["waitFrames"] = waitFrames,
            ["captureResult"] = captureResult
        };
        if (!string.IsNullOrEmpty(target)) paramsObj["target"] = TryParseOrString(target);
        if (!string.IsNullOrEmpty(position)) paramsObj["position"] = JsonDocument.Parse(position).RootElement;
        if (!string.IsNullOrEmpty(worldPoint)) paramsObj["worldPoint"] = JsonDocument.Parse(worldPoint).RootElement;
        if (!string.IsNullOrEmpty(waitFor)) paramsObj["waitFor"] = JsonDocument.Parse(waitFor).RootElement;

        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_input_click", paramsJson.RootElement, ct);
        return BuildResult(result.RootElement);
    }

    internal static object? TryParseOrString(string s)
    {
        s = s.Trim();
        if (s.StartsWith("{") || s.StartsWith("["))
        {
            try { return JsonDocument.Parse(s).RootElement; }
            catch { /* fall through */ }
        }
        return s;
    }

    internal static IEnumerable<AIContent> BuildResult(JsonElement root)
    {
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return new AIContent[] { new TextContent($"Error: {root.GetProperty("error").GetString()}") };

        var data = root.GetProperty("data");
        var contents = new List<AIContent>();

        if (data.TryGetProperty("screenshotBase64", out var b64) && b64.GetString() is string base64 && base64.Length > 0)
        {
            var mime = data.GetProperty("mimeType").GetString() ?? "image/jpeg";
            contents.Add(new DataContent(Convert.FromBase64String(base64), mime));
        }

        contents.Add(new TextContent(data.GetRawText()));
        return contents;
    }
}
```

- [ ] **Step 2: 컴파일 확인**

```bash
dotnet build UnityMcpBridge/UnityMcpBridge.csproj
```

기대: 빌드 성공.

- [ ] **Step 3: Commit**

```bash
git add UnityMcpBridge/Tools/Input/ClickTool.cs
git commit -m "feat(bridge): add ClickTool MCP tool surface"
```

---

## Task 11: DragHandler (Editor)

**Files:**
- Create: `UnityMcpEditor/Editor/Input/Handlers/DragHandler.cs`

**Responsibility:** `unity_input_drag` 처리. `from`/`to` 두 타겟 + 옵션 경유점 + `durationMs` 분할.

- [ ] **Step 1: 코드 작성**

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity.Input
{
    public class DragHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_drag";

        public async Task<object> HandleAsync(JObject @params)
        {
            var fromObj = @params["from"] as JObject ?? throw new System.ArgumentException("'from' 필요");
            var toObj = @params["to"] as JObject ?? throw new System.ArgumentException("'to' 필요");
            var fromSpec = TargetSpec.Parse(fromObj);
            var toSpec = TargetSpec.Parse(toObj);

            var pointsArr = @params["points"] as JArray;
            var durationMs = @params["durationMs"]?.Value<int?>() ?? 200;
            var buttonStr = @params["button"]?.Value<string>() ?? "left";
            var opts = CommonOptions.Parse(@params);

            var button = buttonStr switch
            {
                "right" => MouseButton.Right,
                "middle" => MouseButton.Middle,
                _ => MouseButton.Left
            };

            var fromR = TargetResolver.Resolve(fromSpec);
            var toR = TargetResolver.Resolve(toSpec);
            InputSystemGuard.EnsureReady(fromR.Kind);
            VirtualInputDevices.EnsureRegistered();

            // 경유점 빌드
            var path = new List<Vector2> { fromR.ScreenPoint };
            if (pointsArr != null)
            {
                foreach (var p in pointsArr)
                {
                    if (p is JObject po)
                    {
                        var ps = TargetSpec.Parse(po);
                        path.Add(TargetResolver.Resolve(ps).ScreenPoint);
                    }
                }
            }
            else
            {
                // duration / 16ms = 분할 수
                int steps = Mathf.Max(2, durationMs / 16);
                for (int i = 1; i < steps; i++)
                {
                    var t = (float)i / steps;
                    path.Add(Vector2.Lerp(fromR.ScreenPoint, toR.ScreenPoint, t));
                }
            }
            path.Add(toR.ScreenPoint);

            // 시퀀스
            InputInjector.MouseMove(path[0]);
            await MainThreadDispatcher.DelayFrames(1);
            InputInjector.MouseDown(button);
            await MainThreadDispatcher.DelayFrames(1);

            for (int i = 1; i < path.Count; i++)
            {
                InputInjector.MouseMove(path[i]);
                await MainThreadDispatcher.DelayFrames(1);
            }

            InputInjector.MouseUp(button);

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                return new JObject
                {
                    ["type"] = "drag",
                    ["from"] = ClickHandler.BuildResolvedJson(fromR),
                    ["to"] = ClickHandler.BuildResolvedJson(toR),
                    ["pathLength"] = path.Count
                };
            });
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity Editor 컴파일 에러 없음.

- [ ] **Step 3: Commit**

```bash
git add UnityMcpEditor/Editor/Input/Handlers/DragHandler.cs
git commit -m "feat(input): add DragHandler for unity_input_drag"
```

---

## Task 12: DragTool (Bridge)

**Files:**
- Create: `UnityMcpBridge/Tools/Input/DragTool.cs`

- [ ] **Step 1: 코드 작성**

```csharp
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools.Input;

[McpServerToolType]
public static class DragTool
{
    [McpServerTool(Name = "unity_input_drag"), Description("Play Mode에서 from 좌표/오브젝트에서 to 좌표/오브젝트로 드래그합니다. 슬라이더, 스크롤뷰, 드래그앤드롭 테스트용.")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("시작 타겟 JSON (예: {\"target\":\"Canvas/Slider/Handle\"} 또는 {\"position\":{\"x\":100,\"y\":200}})")] string from,
        [Description("종료 타겟 JSON (from과 동일 형식)")] string to,
        [Description("경유점 배열 JSON (예: [{\"position\":{\"x\":150,\"y\":200}}])")] string? points = null,
        [Description("드래그 지속 시간 (ms). points 미지정 시 16ms 단위로 분할")] int durationMs = 200,
        [Description("마우스 버튼")] string button = "left",
        [Description("입력 후 대기 프레임 수")] int waitFrames = 1,
        [Description("대기 조건 JSON")] string? waitFor = null,
        [Description("스크린샷+로그 캡처")] bool captureResult = false,
        CancellationToken ct = default)
    {
        var paramsObj = new Dictionary<string, object?>
        {
            ["from"] = JsonDocument.Parse(from).RootElement,
            ["to"] = JsonDocument.Parse(to).RootElement,
            ["durationMs"] = durationMs,
            ["button"] = button,
            ["waitFrames"] = waitFrames,
            ["captureResult"] = captureResult
        };
        if (!string.IsNullOrEmpty(points)) paramsObj["points"] = JsonDocument.Parse(points).RootElement;
        if (!string.IsNullOrEmpty(waitFor)) paramsObj["waitFor"] = JsonDocument.Parse(waitFor).RootElement;

        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_input_drag", paramsJson.RootElement, ct);
        return ClickTool.BuildResult(result.RootElement);
    }
}
```

- [ ] **Step 2: 컴파일 확인**

```bash
dotnet build UnityMcpBridge/UnityMcpBridge.csproj
```

- [ ] **Step 3: Commit**

```bash
git add UnityMcpBridge/Tools/Input/DragTool.cs
git commit -m "feat(bridge): add DragTool MCP tool surface"
```

---

## Task 13: 플러그인 매니페스트 버전 bump

**Files:**
- Modify: `.claude-plugin/plugin.json`

- [ ] **Step 1: 현재 버전 확인**

```bash
grep '"version"' .claude-plugin/plugin.json
```

기대: `"version": "0.3.1"` 비슷한 형태.

- [ ] **Step 2: 0.4.0으로 변경**

`.claude-plugin/plugin.json`에서 `"version": "0.3.1"` → `"version": "0.4.0"`. 다른 필드는 변경하지 않음.

- [ ] **Step 3: Commit**

```bash
git add .claude-plugin/plugin.json
git commit -m "chore: bump plugin to 0.4.0 for input simulation tools"
```

---

## Task 14: 수동 검증 — Click

**Pre-condition:** Unity Editor가 열려 있고, MCP Bridge가 실행 중.

- [ ] **Step 1: 검증용 씬 만들기**

`unity_execute_code`로:

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);

// EventSystem (InputSystemUIInputModule)
var es = new GameObject("EventSystem");
es.AddComponent<UnityEngine.EventSystems.EventSystem>();
es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

// Canvas + Button
var canvasGo = new GameObject("Canvas");
var canvas = canvasGo.AddComponent<Canvas>();
canvas.renderMode = RenderMode.ScreenSpaceOverlay;
canvasGo.AddComponent<CanvasScaler>();
canvasGo.AddComponent<GraphicRaycaster>();

var btnGo = new GameObject("ClickMe", typeof(RectTransform));
btnGo.transform.SetParent(canvasGo.transform, false);
var rt = btnGo.GetComponent<RectTransform>();
rt.sizeDelta = new Vector2(200, 80);
var img = btnGo.AddComponent<Image>();
img.color = Color.cyan;
var btn = btnGo.AddComponent<Button>();
btn.onClick.AddListener(() => Debug.Log("[CLICK_VERIFIED]"));

EditorSceneManager.SaveScene(scene, "Assets/_McpInputTest.unity");
return "scene ready";
```

- [ ] **Step 2: Play Mode 진입**

`unity_play_mode` 도구로 `action: "enter"`.

- [ ] **Step 3: Click 도구 호출**

`unity_input_click` 도구로:
- `target`: `"Canvas/ClickMe"`
- `captureResult`: `true`
- `waitFor`: `{"kind":"consoleLogContains","pattern":"CLICK_VERIFIED","timeoutMs":2000}`

기대 응답: `ok: true`, `waitFor.satisfied: true`. 콘솔에 `[CLICK_VERIFIED]` 로그.

- [ ] **Step 4: Edit Mode 복귀**

`unity_play_mode` 도구로 `action: "exit"`.

- [ ] **Step 5: Commit (검증 통과 시)**

검증이 성공했을 때 verification 시나리오 문서를 spec과 함께 보존:

```bash
# 별도 파일은 만들지 않음 — spec §8.1에 이미 시나리오가 있음
# 통과만 확인. 실패 시 핸들러 코드를 수정하고 재시도.
```

---

## Task 15: 수동 검증 — Drag

**Pre-condition:** Task 14의 씬에 Slider 추가.

- [ ] **Step 1: Slider 추가**

Edit Mode에서 `unity_execute_code`로:

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

var scene = SceneManager.GetActiveScene();
var canvasGo = GameObject.Find("Canvas");

// 간단한 Slider 만들기 (Unity 메뉴 GameObject > UI > Slider 생성과 등가)
var sliderGo = new GameObject("MySlider", typeof(RectTransform));
sliderGo.transform.SetParent(canvasGo.transform, false);
var rt = sliderGo.GetComponent<RectTransform>();
rt.anchoredPosition = new Vector2(0, -150);
rt.sizeDelta = new Vector2(300, 30);

var bg = new GameObject("Background", typeof(RectTransform));
bg.transform.SetParent(sliderGo.transform, false);
bg.GetComponent<RectTransform>().anchorMin = Vector2.zero;
bg.GetComponent<RectTransform>().anchorMax = Vector2.one;
bg.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
bg.AddComponent<Image>().color = Color.gray;

var fillArea = new GameObject("Fill Area", typeof(RectTransform));
fillArea.transform.SetParent(sliderGo.transform, false);
fillArea.GetComponent<RectTransform>().anchorMin = Vector2.zero;
fillArea.GetComponent<RectTransform>().anchorMax = Vector2.one;
fillArea.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

var fill = new GameObject("Fill", typeof(RectTransform));
fill.transform.SetParent(fillArea.transform, false);
fill.AddComponent<Image>().color = Color.green;
fill.GetComponent<RectTransform>().anchorMin = Vector2.zero;
fill.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1);
fill.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
handleArea.transform.SetParent(sliderGo.transform, false);
handleArea.GetComponent<RectTransform>().anchorMin = Vector2.zero;
handleArea.GetComponent<RectTransform>().anchorMax = Vector2.one;

var handle = new GameObject("Handle", typeof(RectTransform));
handle.transform.SetParent(handleArea.transform, false);
handle.AddComponent<Image>().color = Color.white;
handle.GetComponent<RectTransform>().sizeDelta = new Vector2(20, 30);

var slider = sliderGo.AddComponent<Slider>();
slider.fillRect = fill.GetComponent<RectTransform>();
slider.handleRect = handle.GetComponent<RectTransform>();
slider.targetGraphic = handle.GetComponent<Image>();
slider.direction = Slider.Direction.LeftToRight;
slider.minValue = 0; slider.maxValue = 1; slider.value = 0;
slider.onValueChanged.AddListener(v => Debug.Log($"[SLIDER]={v:F2}"));

EditorSceneManager.SaveScene(scene);
return "slider added";
```

- [ ] **Step 2: Play Mode 진입**

`unity_play_mode` 도구로 `action: "enter"`.

- [ ] **Step 3: Drag 도구 호출**

`unity_input_drag` 도구로:
- `from`: `{"target":"Canvas/MySlider/Handle Slide Area/Handle"}`
- `to`: `{"target":"Canvas/MySlider","options":{}}`  ← 단순화: 슬라이더 본체 중앙 = 0.5 부근으로
- `durationMs`: 300
- `waitFrames`: 2
- `captureResult`: true

(또는 더 명시적으로 `from`/`to`를 `position`으로 지정해 슬라이더 좌→우 끝 좌표 계산.)

기대 응답: `ok: true`, 콘솔에 `[SLIDER]=...` 다수 로그(드래그 중 값 변화).

- [ ] **Step 4: Edit Mode 복귀**

`unity_play_mode` 도구로 `action: "exit"`.

---

## Task 16: PR

- [ ] **Step 1: 빌드 검증**

```bash
dotnet build UnityMcpBridge/UnityMcpBridge.csproj
```

기대: 빌드 성공, 경고 0~소수.

- [ ] **Step 2: Unity Editor 컴파일 검증**

Unity Console이 비어 있는지 (컴파일 에러/경고 없음) 확인.

- [ ] **Step 3: 변경 요약 확인**

```bash
git log --oneline main..HEAD
git diff --stat main..HEAD
```

기대 파일:
- 신규: `UnityMcpEditor/Editor/Input/` 8개 (asmdef + 6 core + 2 handler)
- 신규: `UnityMcpBridge/Tools/Input/` 2개
- 수정: `.claude-plugin/plugin.json`

- [ ] **Step 4: PR 생성**

```bash
gh pr create --title "feat: Play Mode input simulation Phase 1 (click + drag)" --body "$(cat <<'EOF'
## Summary
- New `unity_input_click` and `unity_input_drag` MCP tools for Play Mode UI testing
- Unified injection path via virtual InputSystem `Mouse` device — covers uGUI, UI Toolkit, and 3D objects
- Common infrastructure (TargetResolver, InputInjector, WaitConditions, ResultSnapshot) ready for Phase 2/3 (hold/swipe/scroll/key/text/pinch)
- Conditional asmdef (`BreadPack.Mcp.Unity.Input`) — silently skips when `com.unity.inputsystem` is absent

## Spec
`docs/superpowers/specs/2026-04-26-playmode-input-simulation-design.md`

## Test plan
- [ ] Click on Button — `[CLICK_VERIFIED]` log appears via `waitFor.consoleLogContains`
- [ ] Drag a Slider handle — value change logs appear during drag
- [ ] Edit Mode call returns guard error
- [ ] Click on inactive object returns descriptive error

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Self-Review Checklist (반영 결과)

플랜 작성 후 spec 대비 검증 결과:

**Spec coverage:**
- §4 아키텍처(InputSystemGuard, VirtualInputDevices, TargetResolver, InputInjector, ResultSnapshot, WaitConditions): Task 2/3/5/6/7/8 ✅
- §5.1 Click 데이터 흐름: Task 9 ✅
- §5.2 Drag 시퀀스: Task 11 ✅
- §6.1 공통 옵션: Task 4 ✅
- §6.2 타겟 명세 (path/object/instanceId/ve/position/worldPoint): Task 4/5 ✅
- §6.4 응답 페이로드: Task 8/9 ✅
- §7.1 가드 분기: Task 2 ✅
- §7.2 TargetResolver 실패 케이스: Task 5 ✅ (force 옵션 미지원 명시)
- §7.3 WaitCondition 5종: Task 7 ✅
- §7.4 도메인 리로드 복구: Task 3 ✅
- §8 검증 시나리오: Task 14/15 (Click/Drag만 — Phase 1 범위)
- §11 Phase 분할: 이 플랜은 Phase 1만 다룸 ✅

**미포함 (의도적 — Phase 2/3로 이연):**
- Hold/Swipe/Pinch/Key/TypeText/Scroll 핸들러 — 해당 phase 플랜에서 추가
- Touchscreen/Keyboard 가상 디바이스 — Phase 3에서 `VirtualInputDevices`에 추가

**Placeholder scan:** 확인 — 모든 step에 실제 코드 또는 명령 포함, "TODO"/"TBD" 없음.

**Type consistency:**
- `MouseButton` 열거자 (Task 6) ↔ Click/Drag에서 사용 (Task 9, 11) ✅
- `TargetSpec.Parse(JObject, string)` (Task 4) ↔ 호출부 (Task 5, 9, 11) ✅
- `ResolvedTarget` 필드 (`Kind`, `ScreenPoint`, `ResolvedPath`, `GameObject`) ↔ 사용처 일관 ✅
- `CommonOptions` (Task 4) ↔ ResultSnapshot (Task 8) ↔ 핸들러 (Task 9, 11) ✅
- `WaitResult.ToJson()` (Task 7) ↔ ResultSnapshot 응답 빌드 (Task 8) ✅

---

**Plan complete and saved to `docs/superpowers/plans/2026-04-26-playmode-input-phase1-click-drag.md`. Two execution options:**

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
