# UnityMCP Skill Template

이 파일은 Claude Code `.claude/skills/` 디렉토리에 배포하기 위한 skill 템플릿이다.

## 배포 방법

1. 프로젝트의 `.claude/skills/unity-mcp-guide/` 디렉토리 생성
2. 아래 `SKILL.md` 내용을 해당 디렉토리에 복사
3. Claude Code가 MCP 관련 요청 시 자동으로 skill을 참조

## 배포 디렉토리 구조

```
.claude/
└── skills/
    └── unity-mcp-guide/
        └── SKILL.md
```

---

## SKILL.md 내용

아래 구분선 사이의 내용을 `.claude/skills/unity-mcp-guide/SKILL.md`로 복사한다.

```markdown
---
name: unity-mcp-guide
description: Unity MCP 도구 활용 가이드. 21개 도구(관찰 10 + 씬 조작 11)의 모범 사례, 워크플로우, 제약사항 안내. "MCP 사용법", "Unity 도구", "렌더링 확인", "스크린샷", "UI 디버깅", "Play Mode 전환", "씬 편집", "GameObject 생성", "Component 추가", "Addressable" 요청 시 활성화.
---

# Unity MCP Guide

Unity Editor와 통신하는 MCP 도구의 활용 가이드.
도구별 제약사항, 모범 사례, 조합 워크플로우를 제공한다.

---

## 도구 요약

| 도구 | 모드 | 용도 |
|------|------|------|
| `unity_ping` | Any | 서버 상태 확인 |
| `unity_render_uxml` | **Edit** | UXML → 이미지 렌더링 |
| `unity_take_screenshot` | **Play** | 게임 뷰 캡처 |
| `unity_get_screen` | **Play** | 현재 화면 + ViewModel 상태 |
| `unity_get_ui_tree` | **Play** | UI Toolkit 비주얼 트리 |
| `unity_get_available_actions` | **Play** | 클릭 가능 UI 요소 목록 |
| `unity_get_hierarchy` | Any | Scene GameObject 트리 |
| `unity_get_console_logs` | Any | 콘솔 로그 조회 |
| `unity_refresh_assets` | Any | AssetDatabase 새로고침 |
| `unity_play_mode` | Any | Play Mode 전환 |

### Scene 조작 도구 (Phase 1: Core)

| 도구 | 모드 | 용도 |
|------|------|------|
| `unity_create_gameobject` | **Edit** | GameObject 생성 |
| `unity_delete_gameobject` | **Edit** | GameObject 삭제 (자식 보존 옵션) |
| `unity_set_transform` | **Edit** | Position/Rotation/Scale 설정 |
| `unity_reparent_gameobject` | **Edit** | 부모 변경 (순환참조 방지) |
| `unity_add_component` | **Edit** | Component 추가 + 프로퍼티 목록 반환 |
| `unity_remove_component` | **Edit** | Component 제거 (Transform 보호) |
| `unity_set_property` | **Edit** | Component 필드/프로퍼티 설정 (Reflection) |

### Asset 도구 (Phase 2)

| 도구 | 모드 | 용도 |
|------|------|------|
| `unity_instantiate_prefab` | **Edit** | Prefab 인스턴스 생성 |
| `unity_set_asset_reference` | **Edit** | Component에 Asset 참조 할당 |

### Addressable 도구 (Phase 3)

| 도구 | 모드 | 용도 |
|------|------|------|
| `unity_addressable_add` | **Edit** | Asset을 Addressable 그룹에 등록 |
| `unity_addressable_set_address` | **Edit** | Addressable 주소/레이블 설정 |

**핵심 구분**: 관찰 도구 (Edit/Play) vs 씬 조작 도구 (Edit Mode 전용, Undo 지원).

---

## 도구별 가이드

### unity_ping

서버 연결 확인. 항상 첫 번째로 호출한다.

**반환 정보**: Play Mode 여부, 컴파일 중 여부, 에디터 설정

**활용**:
- 다른 도구 호출 전 연결 상태 확인
- Play Mode 여부 확인 후 적절한 도구 선택

---

### unity_render_uxml

UXML 파일을 이미지로 렌더링. **Play Mode 불필요**.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `uxmlPath` | string | O | - | Asset 경로 (예: `Assets/_GAME_ASSETS/UI/...`) |
| `width` | int | X | PanelSettings ref | 렌더링 너비 |
| `height` | int | X | PanelSettings ref | 렌더링 높이 |
| `quality` | int | X | 75 | JPEG 품질 (0=PNG) |

**모범 사례**:
- 해상도를 지정하지 않으면 프로젝트 PanelSettings의 referenceResolution을 사용 (scale 1.0)
- 의도적으로 축소 확인할 때만 width/height 지정
- 최소 해상도 50x50 (그 미만은 예외 발생)

**주의사항**:
- 에디터 화면 픽셀을 직접 읽으므로 윈도우가 다른 창에 가려지면 잘못 캡처됨
- USS에서 url() 참조하는 이미지는 실제 Asset이 존재해야 렌더링됨
- DataBinding은 동작하지 않음 (ViewModel 없이 UXML만 렌더링)
- C# 커스텀 Element는 Editor 모드에서 기본 렌더링만 됨

**활용 시나리오**:
```
1. UXML/USS 수정 → render로 결과 확인 → 재수정 반복
2. 여러 Screen UXML을 순차 렌더링하여 전체 UI 리뷰
3. 해상도별 렌더링 비교 (기본 → 축소)
```

---

### unity_take_screenshot

현재 게임 뷰를 캡처. **Play Mode 필수**.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `quality` | int | X | 75 | JPEG 품질 (0=PNG) |

**모범 사례**:
- 캡처 전 Game View를 Repaint하므로 최신 프레임 보장
- 런타임 UI 상태(바인딩, 애니메이션, Spine)를 확인할 때 사용
- render_uxml과 달리 실제 실행 중인 화면을 캡처

---

### unity_get_screen

현재 MenuScreenManager의 활성 화면과 ViewModel 프로퍼티 조회. **Play Mode 필수**.

**반환**: 화면 타입, public 프로퍼티(primitive/enum만)

**활용**:
- 현재 어떤 화면이 표시 중인지 확인
- ViewModel 바인딩 값 디버깅

---

### unity_get_ui_tree

UI Toolkit 비주얼 트리 구조 직렬화. **Play Mode 필수**.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `maxDepth` | int | X | 10 | 트리 탐색 깊이 |

**활용**:
- 런타임 UI 요소의 실제 구조 확인
- USS 클래스가 제대로 적용되었는지 검증
- 요소의 name, type, classes, text 확인

---

### unity_get_available_actions

클릭 가능한 UI 요소 목록 반환. **Play Mode 필수**.

**활용**:
- 현재 화면에서 사용자가 할 수 있는 행동 파악
- 버튼 이름과 텍스트 확인

---

### unity_get_hierarchy

Scene의 GameObject 트리 구조. Edit/Play 모두 동작.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `maxDepth` | int | X | 5 | 트리 탐색 깊이 |
| `includeComponents` | bool | X | false | 컴포넌트 포함 여부 |

**활용**:
- Scene 구조 파악
- UI Document, Canvas 등 UI 루트 오브젝트 확인
- 컴포넌트 구성 확인 시 `includeComponents: true`

---

### unity_get_console_logs

콘솔 로그 버퍼 조회.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `count` | int | X | 50 | 가져올 로그 수 |
| `logType` | string | X | null | 필터: "Log", "Warning", "Error", "Exception" |

**모범 사례**:
- 오류 디버깅 시 `logType: "Error"` 로 필터링
- 로그가 많으면 `count`를 줄여서 최신 것만 확인

---

### unity_refresh_assets

AssetDatabase 새로고침. 파일 수정 후 Unity에 반영할 때 사용.

**활용 타이밍**:
- 외부에서 UXML/USS/스크립트 파일 수정 후
- render_uxml 전에 최신 Asset 상태 보장

---

### unity_play_mode

Play Mode 전환.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `action` | string | X | "toggle" | "enter", "exit", "toggle" |

**중요 제약사항**:
- Play Mode 진입/퇴출 시 **도메인 리로드** 발생
- 도메인 리로드 = MCP 서버 재시작 = **연결 끊김**
- 전환 후 MCP 재연결 필요 (자동 재연결 지원)
- 컴파일 중에는 전환 불가

---

### Scene 조작 도구 공통

**대상 지정**: `path` (계층 경로) 또는 `instanceId` (InstanceID). instanceId 우선.

**Undo**: 모든 조작은 Unity Undo 지원 (Addressable 제외).

**타입 변환** (set_property):

| JSON | C# 타입 |
|------|---------|
| `{"x", "y", "z"}` | Vector3 |
| `{"r", "g", "b", "a"}` | Color |
| `{"$asset": "path"}` | UnityEngine.Object |
| Dot-notation | 중첩 struct 필드 |

---

## 워크플로우

### 1. UXML 디자인 반복 (Edit Mode)

```
파일 수정 → refresh_assets → render_uxml → 확인 → 재수정
```

가장 빠른 디자인 루프. Play Mode 없이 UXML/USS 결과를 즉시 확인.

### 2. 런타임 UI 디버깅 (Play Mode)

```
ping (모드 확인) → take_screenshot → get_screen → get_ui_tree
```

실제 실행 중인 UI의 시각적 상태와 데이터 바인딩을 모두 확인.

### 3. UI 구조 분석

```
get_hierarchy (Scene 구조) → get_ui_tree (UI 트리) → get_available_actions (인터랙션)
```

### 4. 오류 추적

```
get_console_logs(logType: "Error") → take_screenshot → get_ui_tree
```

에러 로그와 현재 UI 상태를 대조하여 원인 파악.

### 5. 전체 UI 리뷰

```
render_uxml(Screen1.uxml) → render_uxml(Screen2.uxml) → ... 순차 렌더링
```

모든 화면을 Edit Mode에서 빠르게 리뷰.

### 6. Scene 셋업 (Edit Mode)

```
create_gameobject(name, parentPath) → add_component(componentType) → set_property(properties) → get_hierarchy (확인)
```

GameObject 생성 → Component 추가 → 프로퍼티 설정까지 한 번에. 모든 작업은 Undo 지원.

### 7. Asset 참조 설정

```
create_gameobject → add_component("UIDocument") → set_asset_reference("UIDocument", "panelSettings", assetPath) → get_hierarchy
```

### 8. Addressable 등록

```
addressable_add(assetPath, groupName, address) → addressable_set_address(address, labels) → 확인
```

### 9. Prefab 인스턴스화

```
instantiate_prefab(assetPath, parentPath) → set_property (필요시 오버라이드) → get_hierarchy
```

---

## 제약사항 요약

| 제약 | 영향 | 대응 |
|------|------|------|
| 도메인 리로드 | Play Mode 전환 시 MCP 연결 끊김 | 재연결 대기 |
| 화면 가림 | render_uxml이 픽셀을 직접 읽음 | 에디터 창 가리지 않기 |
| DataBinding | render_uxml에서 바인딩 미동작 | 더미 텍스트로 확인, 런타임은 screenshot |
| 컴파일 중 | play_mode 전환 불가 | 컴파일 완료 대기 |
| 커스텀 Element | Edit Mode에서 기본 렌더링만 | Play Mode에서 완전 확인 |
| Addressable Undo | Addressable 도구는 Undo 미지원 | Unity Addressables API 제약 |
| 씬 조작 모드 | 씬 조작 도구는 Edit Mode 전용 | Play Mode에서 사용 불가 |
```
