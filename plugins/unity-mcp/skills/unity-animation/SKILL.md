---
name: unity-animation
description: Unity AnimationClip·AnimatorController를 생성·조회·편집합니다 (Edit Mode) - 커브 편집, 상태 그래프 조립, 에디터 샘플링 검증
---

# Unity Animation

AnimationClip과 AnimatorController를 Edit Mode에서 직접 만들고 편집할 때 사용합니다.
Play Mode에서 이미 붙어있는 Animator의 파라미터를 조작·조회할 때는 `unity_animator_control`을 대신 씁니다(이 skill의 범위 밖).

## 도구 선택

| 하려는 일 | 도구 | 필요 모드 |
|---|---|---|
| 클립 자산 생성·커브 편집·설정 변경 | `unity_animation_clip` | Edit Mode |
| Play Mode 없이 특정 시각의 포즈를 확인 | `unity_animation_clip`(action=sample/stop_sample) | Edit Mode |
| 컨트롤러 자산 생성·상태·전이·파라미터 편집 | `unity_animator_controller` | Edit Mode |
| GameObject에 컨트롤러 연결 | `unity_animator_controller`(action=assign) | Edit Mode |
| 이미 붙어있는 Animator의 파라미터 조작·현재 상태 조회 | `unity_animator_control` | **Play Mode 필수** |

## 표준 워크플로 — 인트로/아웃트로 연출 한 벌 만들기

1. `unity_animation_clip`(action=create, assetPath, frameRate=60)로 INTRO/IDLE/OUTRO 클립을 각각 생성
2. `unity_animation_clip`(action=set_curve)로 각 클립에 커브를 채운다 (아래 "커브 레시피" 참조)
3. `unity_animation_clip`(action=set_settings)로 IDLE에는 loopTime=true를 준다
4. `unity_animator_controller`(action=create)로 컨트롤러 생성
5. `add_state`로 INTRO(isDefault=true)/IDLE/OUTRO 상태를 만들고 motionPath로 1)의 클립을 연결
6. `add_transition`(from=INTRO, to=IDLE, hasExitTime=true, exitTime=1)로 자동 전이 연결
7. `assign`으로 대상 GameObject의 Animator에 컨트롤러 연결 (Animator 컴포넌트가 없으면 먼저 `unity_add_component`로 추가)
8. OUTRO는 파라미터로 트리거하지 않고, 코드에서 `Animator.Play("OUTRO")`로 직접 재생하는 것이 팀 관례다 — add_transition으로 OUTRO행 전이를 미리 만들지 않아도 된다
9. `unity_animation_clip`(action=sample)로 t=0/중간/끝 시점을 순서대로 샘플링하고 매번 스크린샷으로 캡처해 검증, 끝나면 stop_sample로 되돌린다

## 커브 레시피 — CounterSide 팀 관례

Unity 직렬화 프로퍼티명을 그대로 쓴다(Inspector 표시명이 아니다). componentType은 Component 파생 타입명, `GameObject`는 활성 토글 전용 특수값이다.

| 연출 | componentType | propertyPath | 비고 |
|---|---|---|---|
| 알파 페이드 | `CanvasGroup` | `m_Alpha` | **Image/BImage의 m_Color.a가 아니다** — 알파 페이드는 CanvasGroup이 팀 표준 |
| 위치 슬라이드 | `RectTransform` | `m_AnchoredPosition.x` / `.y` | |
| 크기 변화 | `RectTransform` | `m_SizeDelta.x` / `.y` | |
| 스케일 팝 | `Transform` | `m_LocalScale.x` / `.y` / `.z` | |
| 자식 활성 토글 | `GameObject` | `m_IsActive` | componentType은 "GameObject", Component 아님 |
| 배경 UV 스크롤(루프) | `RawImage` | `m_UVRect.x` / `.y` | 위치 이동이 아니라 UV 오프셋으로 흐르게 함 |

예시 — CanvasGroup 알파 0→1 페이드:
```
unity_animation_clip:
  action: set_curve
  assetPath: "Assets/.../NKM_UI_X_INTRO.anim"
  targetPath: ""              # 클립 루트 자신
  componentType: "CanvasGroup"
  propertyPath: "m_Alpha"
  keys: [{"time":0,"value":0},{"time":0.5,"value":1}]
```

## 팀 표준값 (실측 기반, CounterSide UI)

- **샘플레이트 60fps**
- **duration**: 인트로 0.4~0.5초, 아웃트로 0.25초 (열림은 느리게, 닫힘은 빠르게 — 약 2배 차이).
  일반 전환·페이드 표준은 0.4초. duration은 이동 거리·영역 크기에 비례시킨다(화면 전체 > 패널 > 슬롯)
- **오버슈트는 위치·스케일 커브에만**: 색·알파 커브가 목표값을 넘었다 되돌아오게 만들지 않는다.
  팝 연출의 시작 스케일은 0이 아니라 0.6~0.9에서 시작한다(0에서 시작하면 "무에서 출현"처럼 어색)
- **순차 등장(stagger)**: 슬롯·카드 목록을 순차 등장시킬 때는 개당 0.02~0.05초 지연, 전체 완료 0.5초 이내
- **IDLE 상태**: 실제 애니메이션이 아니라 1프레임 loopTime=true 더미 — INTRO 종료 후 상태를 유지하기 위한 구조적 종착점이다. 시각 효과를 넣지 않는다
- **파라미터를 웬만하면 안 쓴다**: 팀 컨트롤러의 81%가 파라미터 0개다. Trigger/Bool로 상태를 조건 분기하지 말고, ExitTime 기반 자동 전이(INTRO→IDLE) + 코드에서 `Animator.Play("OUTRO")` 직접 재생 조합이 표준이다
- **탄젠트**: `set_curve`의 tangentMode 기본값 `auto`(ClampedAuto)면 충분하다. 딱딱 끊기는 연출이 필요할 때만 `linear`, 계단식 전환은 `constant`

## Unity CLI로 할 때

클립·컨트롤러 에셋 편집은 Pipeline 명령으로도 가능하다. **포즈 샘플링(`sample`/`stop_sample`)과 런타임 파라미터 조작(`unity_animator_control`)은 Pipeline에 없으므로 MCP 도구를 쓴다.**

```bash
unity command create_animation_clip --json -- --path Anim/NKM_UI_X_INTRO --frame_rate 60
unity command set_animation_curve --json -- --clip '{"path":"Assets/Anim/NKM_UI_X_INTRO.anim"}' --target_path "" --type CanvasGroup --property m_Alpha --keys '[{"time":0,"value":0},{"time":0.5,"value":1}]'
unity command get_animation_clip --json -- --clip '{"path":"Assets/Anim/NKM_UI_X_INTRO.anim"}'
unity command create_animator_controller --json -- --path Anim/NKM_UI_X
unity command add_animator_state --json -- --controller '{"path":"Assets/Anim/NKM_UI_X.controller"}' --name INTRO --motion '{"path":"Assets/Anim/NKM_UI_X_INTRO.anim"}' --is_default true
unity command add_animator_transition --json -- --controller '{"path":"Assets/Anim/NKM_UI_X.controller"}' --from INTRO --to IDLE --has_exit_time true --exit_time 1
unity command get_animator_controller --json -- --controller '{"path":"Assets/Anim/NKM_UI_X.controller"}'
```

인자 이름은 `unity command <name> --help`로 확인한다(위는 0.5.0-exp.1 기준). 커브 레시피와 팀 표준값은 어느 경로든 동일하게 적용한다.

## Play Mode 검증 (unity_animator_control)

Animator가 실제로 붙어 재생되는지 런타임 확인이 필요하면:
1. `unity_play_mode`(action=enter)
2. `unity_animator_control`(action=get_current_state, path=...)로 현재 상태 확인
3. `unity_animator_control`(action=set_trigger/set_bool/...)로 파라미터가 있는 경우만 조작
4. `unity_take_screenshot`으로 프레임 캡처

## 함정

- **`unity_animation_clip`/`unity_animator_controller`는 Edit Mode 전용이다.** Play Mode 중 호출하면 실패한다 — Play Mode를 먼저 빠져나온다.
- **componentType "GameObject"는 ComponentResolver로 풀리지 않는다** — Component가 아니라 GameObject 자체를 바인딩 타입으로 쓰는 특수 케이스다(자식 활성 토글용).
- **루프 여부는 커브가 아니라 클립 설정이다** — `set_settings`(loopTime)로 지정한다. 클립에 루프하는 커브를 넣어도 loopTime을 안 켜면 마지막 프레임에서 멈춘다.
- **ObjectReference 커브(스프라이트 교체 등) 쓰기는 미지원**이다. `get_info`로 존재 확인만 가능 — 필요하면 기존 `unity_execute_code` 경로를 쓴다.
- **sample은 상태를 되돌리지 않는다** — 스크린샷을 다 찍었으면 `stop_sample`을 호출해 AnimationMode를 종료한다. 안 하면 씬이 샘플링된 포즈로 남는다.
- **assign 전에 Animator 컴포넌트가 있어야 한다** — 없으면 명시적으로 에러를 던진다. `unity_add_component`(componentType="Animator")로 먼저 추가한다.
- **상태·전이 이름 대소문자는 팀 내에서도 통일돼 있지 않다**(INTRO/Intro, OUTRO/OUT/out 혼용 실측). 새로 만들 때는 `INTRO`/`IDLE`/`OUTRO` 대문자로 통일한다.
