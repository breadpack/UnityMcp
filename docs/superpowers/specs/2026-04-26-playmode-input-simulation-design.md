# Play Mode Input Simulation — Design

작성일: 2026-04-26
대상 컴포넌트: UnityMcpBridge, UnityMcpEditor

## 1. 목표 (Goal)

AI 에이전트가 Unity Editor의 Play Mode 중 GameView UI/3D 오브젝트에 대해 클릭·드래그·키 입력 등 **실제 사용자 입력과 동등한 시뮬레이션**을 MCP를 통해 수행할 수 있게 한다. 이를 통해 게임 플레이 시나리오를 자동화·반복 검증 가능한 형태로 만든다.

## 2. 비목표 (Non-goals)

- Old Input Manager (`UnityEngine.Input`) 단독 환경 지원 — New Input System 사용 프로젝트만 대상.
- macOS/Windows 네이티브 윈도우 메시지 주입 — 모든 입력은 Unity 내부(`InputSystem` API + 가상 디바이스)로 한정.
- Edit Mode에서의 입력 시뮬레이션.
- 빌드된 플레이어(런타임 빌드)에서의 동작 — Editor 한정.
- Play Mode Tests(`[UnityTest]`) 프레임워크와의 통합 — 별도 설계 사항.

## 3. 결정 사항 요약

| 항목 | 결정 | 근거 |
|---|---|---|
| 대상 UI/오브젝트 | uGUI + UI Toolkit + 3D 모두 (통합 입력 주입) | 한 가지 도구로 모든 시나리오 커버 |
| 입력 시스템 | New Input System 전용 | 프로젝트 표준 가정. 가상 디바이스 사용 가능 |
| 타겟 지정 | 이름/path 우선 + 자동 좌표 변환 후 가상 마우스 주입 | 에이전트 친화적 + 실제 입력과 동등 경로 |
| 동작 범위 | Click, Drag, Hold, Swipe, Pinch, Key, TypeText, Scroll | 본격 자동화 도구 |
| 결과 확인 | Fire-and-forget 기본 + `waitFrames`/`waitFor`/`captureResult` 옵트인 | 가벼운 호출은 가볍게, 검증 시에만 무겁게 |
| MCP 도구 표면 | 동작별 개별 도구 8개 | 기존 핸들러 1:1 컨벤션과 일치 |

## 4. 아키텍처

```
AI Agent
   │ MCP (stdio)
   ▼
UnityMcpBridge ── 8개 신규 Tool (Tools/Input/)
   │ TCP localhost:9876
   ▼
UnityMcpEditor ── 8개 신규 Handler (Handlers/Input/)
   │
   ├─→ InputSystemGuard      ── Play Mode/패키지/InputModule 검증
   ├─→ VirtualInputDevices   ── Mouse/Keyboard/Touchscreen 영속 등록
   ├─→ TargetResolver        ── 이름/path → 스크린 좌표
   ├─→ InputInjector         ── 가상 디바이스에 InputState.Change/QueueStateEvent
   └─→ ResultSnapshot        ── waitFrames/waitFor/captureResult 처리
         └─→ WaitConditions  ── 제한된 predicate 평가
```

### 4.1 책임 분리

- **InputSystemGuard**: 매 호출 첫 단계에서 환경 사전 검증. 명확한 에러 메시지로 실패.
- **VirtualInputDevices**: 정적 클래스. Lazy 등록(`Mouse.current` 자리에 가상 디바이스 추가) + 도메인 리로드/Play Mode 전환 시 재등록.
- **TargetResolver**: 입력 시뮬레이션과 무관. 단독으로 단위 테스트 가능 — name/path/instanceId/visualElement → `Vector2 screenPoint`.
- **InputInjector**: 저수준. 좌표/버튼/키 입력을 가상 디바이스에 큐잉.
- **ResultSnapshot**: 후처리. 프레임 진행 + 조건부 대기 + 결과 묶음(스크린샷, 콘솔 로그 delta, 옵션별 UI 트리).
- **각 Handler**: 위 4개 컴포넌트 조합만 하는 얇은 어댑터(목표 ≤100 LOC). `IAsyncRequestHandler` 구현(프레임 대기 필요).

### 4.2 파일 구조 (신규)

```
UnityMcpEditor/Editor/Handlers/Input/
  ClickHandler.cs            DragHandler.cs
  HoldHandler.cs             SwipeHandler.cs
  PinchHandler.cs            KeyHandler.cs
  TypeTextHandler.cs         ScrollHandler.cs
UnityMcpEditor/Editor/Input/
  VirtualInputDevices.cs     TargetResolver.cs
  InputInjector.cs           ResultSnapshot.cs
  WaitConditions.cs          InputSystemGuard.cs
UnityMcpBridge/Tools/Input/
  ClickTool.cs DragTool.cs HoldTool.cs SwipeTool.cs
  PinchTool.cs KeyTool.cs TypeTextTool.cs ScrollTool.cs
```

`McpServerBootstrap.StartServer()`에 8개 핸들러 수동 등록. Bridge 측은 `[McpServerToolType]` 어트리뷰트로 자동 등록.

### 4.3 어셈블리/의존성

- Editor 측은 `com.unity.inputsystem` 패키지 의존. asmdef에 `Unity.InputSystem` 참조 추가.
- 패키지 미설치 환경에서도 다른 핸들러는 정상 동작해야 하므로, **Editor asmdef에 `versionDefines`로 `com.unity.inputsystem`을 조건 컴파일**한다. 미설치 시 8개 입력 핸들러 등록 자체를 스킵하고 `McpServerBootstrap`은 다른 핸들러만 등록. 사용자가 입력 도구를 호출하면 "도구를 찾을 수 없음" 에러가 자연스럽게 반환됨.

## 5. 데이터 흐름

### 5.1 Click 예시

```
1. AI: unity_input_click({
     target: "Canvas/StartButton",
     waitFrames: 2,
     captureResult: true
   })
2. Bridge.ClickTool → TCP "input_click"
3. ClickHandler.HandleAsync:
   a. InputSystemGuard.EnsureReady(targetType="ugui")
   b. VirtualInputDevices.EnsureRegistered()
   c. var pos = TargetResolver.ToScreenPoint(target)
   d. InputInjector.MouseMove(pos);  await DelayFrames(1)
   e. InputInjector.MouseDown(Left); await DelayFrames(1)
   f. InputInjector.MouseUp(Left)
   g. var snapshot = await ResultSnapshot.Capture(
         waitFrames: 2, waitFor: null, captureResult: true)
4. 응답: {
     ok: true,
     resolved: { type: "ugui", path: "Canvas/StartButton", screen: {x,y} },
     screenshotBase64, mimeType, width, height,
     consoleLogsDelta: [...],
   }
```

### 5.2 동작별 입력 시퀀스

- **Drag**: `MouseMove(from)` → `MouseDown` → 경유점 N개에 대해 `MouseMove + DelayFrames(1)` → `MouseUp`. 경유점 미지정 시 `from→to` 직선을 분할(분할 수 = `max(2, durationMs / 16)` — 16ms는 60fps 기준 한 프레임 가정).
- **Hold**: `MouseMove` → `MouseDown` → `Delay(holdMs)` → `MouseUp`.
- **Swipe**: `direction`/`distance`로 `to` 계산 후 Drag와 동일.
- **Pinch**: `Touchscreen.current` 가상 디바이스에 두 finger touch (`primaryTouch` + `touches[1]`)를 동시에 이동. `startSpread`/`endSpread`로 두 손가락 거리 보간.
- **Key**: `Keyboard` 가상 디바이스에 `KeyControl` press/down/up. `modifiers`는 별도 keypress 이벤트로 선행/후행.
- **TypeText**: 각 문자에 대해 (1) 해당 키의 KeyDown→KeyUp, (2) 가상 `Keyboard` 디바이스의 `onTextInput` 콜백 invoke (reflection으로 backing delegate 직접 호출). `intervalMs` 간격으로 반복. ASCII 우선 — 한글/IME는 §10 미해결 사항.
- **Scroll**: `Mouse.current.scroll` 컨트롤에 `Vector2(dx, dy)` 변경 이벤트.

## 6. MCP 도구 표면

### 6.1 공통 옵션 (모든 도구)

```jsonc
{
  "waitFrames": 1,            // 입력 후 추가 대기 프레임 수 (기본 1 — 입력 처리 1프레임 보장)
  "waitFor": null,            // §7.3 WaitCondition (옵션)
  "captureResult": false      // true면 스크린샷 + 콘솔 로그 delta 포함
}
```

`waitFrames`의 기본값을 1로 두는 이유: New Input System의 이벤트는 `InputSystem.Update` 시점에 처리되므로 입력 주입 직후 0프레임 반환 시 콜백이 아직 실행되지 않은 상태가 된다. 핸들러 내부의 down/up 사이 `DelayFrames(1)`과는 별개로, 입력 시퀀스 전체가 끝난 후에도 최소 1프레임을 진행시켜 결과가 반영되도록 한다.

### 6.2 타겟 명세

```jsonc
"target": "Canvas/Panel/Button"             // 단순 path
"target": { "path": "Button", "index": 1 } // 동명 분별
"target": { "instanceId": 12345 }
"target": { "ve": "root/start-button" }    // UI Toolkit VisualElement name/path
"position": { "x": 480, "y": 320 }          // 직접 스크린 좌표 (target 대체)
"worldPoint": { "x": 0, "y": 1, "z": 5 }    // 3D 월드 좌표 (target 대체)
```

`target` / `position` / `worldPoint` 중 정확히 하나만 지정. 동시 지정 시 거부.

### 6.3 도구별 시그니처

`<Target>`은 §6.2의 타겟 명세 전체를 의미 — `target`/`position`/`worldPoint` 중 하나를 갖는 객체. 모든 좌표 입력 자리(`from`/`to`/`center`/단일 타겟)에 동일하게 적용.

```jsonc
unity_input_click {
  ...<Target>,                          // target | position | worldPoint
  button: "left" | "right" | "middle" = "left",
  count: 1                              // 2 = 더블클릭
}

unity_input_drag {
  from: <Target>, to: <Target>,
  points?: [<Target>],                  // 경유점
  durationMs: 200,
  button: "left"
}

unity_input_hold {
  ...<Target>,
  holdMs: 500,
  button: "left"
}

unity_input_swipe {
  from: <Target>,
  direction: "up" | "down" | "left" | "right",
  distance: 200,                        // 픽셀
  durationMs: 150
}

unity_input_pinch {
  center: <Target>,
  startSpread: 100, endSpread: 300,     // 픽셀 (두 손가락 거리)
  durationMs: 300
}

unity_input_key {
  key: "Enter" | "Escape" | ...,        // UnityEngine.InputSystem.Key 열거자
  modifiers?: ["Ctrl", "Shift", "Alt"],
  action: "press" | "down" | "up" = "press"
}

unity_input_type_text {
  text: string,
  intervalMs: 20
}

unity_input_scroll {
  ...<Target>,
  dx: 0, dy: 100                        // 양수=위/우
}
```

### 6.4 응답 페이로드 공통 형식

```jsonc
{
  "ok": true,
  "tool": "unity_input_click",
  "resolved": {                 // TargetResolver 결과 (디버깅/로그 용도)
    "type": "ugui" | "uitk" | "world" | "screen",
    "path": "Canvas/StartButton",
    "screen": { "x": 480, "y": 320 }
  },
  "waitFor": {                  // waitFor 사용 시
    "kind": "objectActive",
    "satisfied": true,
    "timedOut": false,
    "elapsedMs": 124
  },
  "screenshotBase64": "...",   // captureResult: true 시
  "mimeType": "image/jpeg",
  "width": 1920, "height": 1080,
  "consoleLogsDelta": [        // captureResult: true 시
    { "level": "Log", "message": "..." }
  ]
}
```

## 7. 환경 가드 / 에러 처리 / 대기 조건

### 7.1 InputSystemGuard

매 호출 첫 단계 검증 (실패 시 명확한 에러):

1. `EditorApplication.isPlaying == true` — "Play Mode 진입 필요"
2. `EditorApplication.isCompiling == false` — "컴파일 완료 대기"
3. New Input System 패키지 존재 (`versionDefines`로 컴파일 가드 + 런타임 reflection 더블체크) — "com.unity.inputsystem 패키지 필요"
4. **타겟 종류별 분기 검증**:
   - **uGUI 타겟**: `EventSystem.current?.currentInputModule is InputSystemUIInputModule` — 미충족 시 "InputSystemUIInputModule 사용 필요. EventSystem에 추가하세요."
   - **UI Toolkit 타겟**: `EventSystem.current`에 `UIElementsRuntimePanel` 처리 컴포넌트(`PanelEventHandler`/`PanelRaycaster`) 존재 또는 자동 생성 — Unity가 UIDocument 사용 시 자동 추가하므로 보통 통과.
   - **3D 월드 타겟 (`worldPoint` / 3D Collider)**: 추가 검증 없음 — `InputInjector`가 가상 마우스로 좌표를 보내고, 게임 코드가 raycast하면 됨.

### 7.2 TargetResolver 실패 케이스

- **동명 객체 다수**: `index` 미지정 시 후보 목록 포함한 에러 반환 (`{candidates: ["Canvas/A/Button#0", "Canvas/B/Button#1"]}`).
- **비활성 부모로 인해 렌더되지 않는 타겟**: 거부. `force: true` 옵션은 초기 버전에서 지원하지 않음 — 실제 사용자가 클릭 불가능한 상태이므로 기본 거부가 안전.
- **카메라 절두체 밖 (3D)**: 거부 + 카메라 위치/타겟 위치 디버그 정보 반환.
- **WorldSpace Canvas**: `canvas.worldCamera`가 있으면 그 카메라로, 없으면 `Camera.main`으로 변환. 둘 다 없으면 거부.
- **ScreenSpace-Camera Canvas**: `canvas.worldCamera`로 변환.
- **ScreenSpace-Overlay Canvas**: 픽셀 좌표 직접.

### 7.3 WaitCondition (제한된 predicate 셋)

DSL 아닌 enum 기반 — 매 프레임 폴링.

```jsonc
{ "kind": "objectActive",       "target": <Target>, "expected": true,  "timeoutMs": 3000 }
{ "kind": "objectExists",       "target": <Target>, "expected": true,  "timeoutMs": 3000 }
{ "kind": "consoleLogContains", "pattern": "regex", "level": "Log|Warning|Error|Any", "timeoutMs": 3000 }
{ "kind": "sceneLoaded",        "name": "MainMenu", "timeoutMs": 5000 }
{ "kind": "frames",             "count": 10 }
```

평가 루프: `for elapsed < timeoutMs: await DelayFrames(1); if (predicate()) break;`. 타임아웃 시 예외 X — `{satisfied: false, timedOut: true, elapsedMs}` 반환(검증 실패는 정상 결과).

### 7.4 도메인 리로드 / Play Mode 전환 복구

`VirtualInputDevices`:
- `[InitializeOnLoadMethod]`에서 `AssemblyReloadEvents.afterAssemblyReload`/`EditorApplication.playModeStateChanged` 구독
- 이벤트 발생 시 등록 플래그 리셋 → 다음 입력 호출에서 lazy 재등록
- 가상 디바이스는 `InputSystem.AddDevice<Mouse>("McpVirtualMouse")` 등 명시적 이름으로 등록하여 식별 가능

### 7.5 안전 제약

- 입력 시뮬레이션 도구는 모두 Play Mode에서만 동작 (Edit Mode 호출 거부).
- TargetResolver는 비활성/오프스크린 타겟을 기본 거부 → 의도하지 않은 입력 누수 방지.
- 가상 디바이스는 Editor 종료/Play Mode 종료 시 자동 정리 (InputSystem 자체 라이프사이클 + 명시 cleanup).

## 8. 테스트 / 검증 전략

현 프로젝트에 테스트 인프라가 없으므로 신설하지 않는다. 대신:

### 8.1 수동 검증 시나리오 (구현 후 spec과 함께 보존)

샘플 씬 `PlaymodeInputTest.unity`를 만들어 다음 위젯 배치 후 각 도구 검증:

1. **Click**: `Button` + OnClick 콜백 → `Debug.Log("clicked")`. `unity_input_click` 후 콘솔 로그 확인.
2. **Drag**: `Slider` 핸들 → 값 변경 확인.
3. **Hold**: `Button` + `IPointerDownHandler`/`IPointerUpHandler` 시간 측정.
4. **Swipe**: `ScrollRect` content → 스크롤 위치 변경 확인.
5. **Pinch**: 3D 카메라 zoom 스크립트 → 거리 변경 확인.
6. **Key**: 게임 매니저 단축키(예: ESC=일시정지) → 상태 전환 확인.
7. **TypeText**: `TMP_InputField` → text 값 확인.
8. **Scroll**: `ScrollRect` → 휠 스크롤 위치 확인.
9. **3D Click**: Cube + `IPointerClickHandler` → 콜백 확인.
10. **WorldSpace Canvas**: 같은 시나리오를 worldspace로 재현.

### 8.2 자가 검증 (에이전트 활용)

`unity_execute_code`로 클릭 후 즉시 `EventSystem.current.currentSelectedGameObject?.name`을 회수하여 정확성 확인 가능. 통합 테스트는 에이전트가 수행.

## 9. 마이그레이션 / 호환성

- 기존 도구/핸들러 변경 없음 — 순수 추가 기능.
- Bridge는 새 Tool 클래스만 추가, 기존 어셈블리 시그니처 유지.
- Editor asmdef에 `versionDefines`로 `com.unity.inputsystem` 조건 컴파일 추가 → 미설치 환경 안전.
- 플러그인 매니페스트(`plugin.json`) 버전 bump (예: 0.3.1 → 0.4.0).

## 10. 위험 / 미해결 사항

- **TypeText의 IME/한글 입력**: `Keyboard.onTextInput`이 모든 InputField 구현에서 동등하게 동작하는지 검증 필요. 초기 버전은 ASCII 우선, 한글은 후속 이슈.
- **멀티 디스플레이**: GameView 대상 디스플레이가 다수일 경우의 좌표계는 초기 버전에서 `Display.main`만 지원.
- **High-DPI / Game Resolution**: GameView의 `Render Resolution` vs 화면 해상도 차이로 좌표 변환 어긋남 가능 → `RectTransformUtility`가 사용하는 카메라/캔버스 기준을 명시적으로 따른다.
- **InputSystemUIInputModule 미사용 프로젝트**: 가드에서 거부 — 폴백을 만들 만큼 흔하지 않다고 판단.
- **EventSystem 다중 존재**: 첫 번째만 사용. 다중 EventSystem 환경은 비표준이라 가정.

## 11. 구현 단계 권장

스펙 전체가 한 plan으로는 길어질 수 있으므로 다음 phase로 분할 권장:

- **Phase 1 — 기반 + 핵심 동작**: `InputSystemGuard`, `VirtualInputDevices`, `TargetResolver`, `InputInjector`, `ResultSnapshot`, `WaitConditions` + `unity_input_click`, `unity_input_drag`. 이 단계만으로 우선 PR 가능.
- **Phase 2 — 보조 동작**: `unity_input_hold`, `unity_input_swipe`, `unity_input_scroll`.
- **Phase 3 — 키보드/멀티터치**: `unity_input_key`, `unity_input_type_text`, `unity_input_pinch`.

각 Phase는 독립적으로 작동·릴리즈 가능. plan 작성 시 이 분할에 맞춰 작업 단위를 잘게 나눈다.
