---
name: unity-playmode-input
description: Play Mode에서 게임 UI를 실제 입력으로 검증합니다 - 클릭·드래그·홀드·스와이프·스크롤·핀치·키·텍스트 입력과 결과 캡처. Unity CLI에는 없는 UnityMcp 고유 기능
---

# Unity Play Mode Input

Play Mode에서 uGUI·UI Toolkit·3D 오브젝트에 실제 입력 이벤트를 넣어 동작을 검증할 때 사용합니다. Unity CLI의 `simulate_key`/`simulate_pointer`는 좌표 기반이고 Input System·런타임 서버 전용이라 이 작업에는 쓰지 않습니다.

## 도구

| 도구 | 용도 | 타깃 지정 |
|---|---|---|
| `unity_input_click` | 버튼·토글·3D 오브젝트 클릭, `count=2`면 더블클릭 | `target` path / `{"instanceId":..}` / `{"ve":"root/start-button"}` / `position` / `worldPoint` |
| `unity_input_hold` | 길게 누르기 (Down → 대기 → Up) | 위와 동일 |
| `unity_input_drag` | 슬라이더·스크롤뷰·드래그앤드롭 | `from`, `to` JSON (`{"target":..}` 또는 `{"position":{x,y}}`), `durationMs` |
| `unity_input_swipe` | 모바일 플리킹 | `from` + 방향·거리 |
| `unity_input_scroll` | 마우스 휠 (ScrollRect) | `target` 또는 좌표 |
| `unity_input_pinch` | 두 손가락 핀치 (New Input System 가상 터치스크린 전용) | `center` JSON |
| `unity_input_key` | 키 입력. New Input System은 raw 키, Legacy uGUI는 Enter/Escape만 | `key` |
| `unity_input_type_text` | 포커스된 InputField/TMP_InputField에 텍스트 입력 (ASCII, IME 미지원) | `text`, `intervalMs` |

공통 옵션
- `waitFrames`: 입력 후 대기 프레임 수. 애니메이션·코루틴이 끝난 뒤 상태를 읽고 싶을 때 늘린다.
- `waitFor`: 대기 조건 JSON (오브젝트 활성/비활성, 텍스트 일치 등 predicate 5종). 시간 기반 대기보다 우선한다.
- `captureResult=true`: 입력 직후 스크린샷 + 콘솔 로그를 응답에 포함. 별도 `unity_take_screenshot` 호출을 아낀다.

## 워크플로우

1. Play Mode 진입: `unity command editor_play --json` (CLI) 또는 `unity_play_mode`(action="enter")
2. 화면 파악: `unity_get_screen`(활성 화면·ViewModel), `unity_get_ugui_tree`(Canvas 계층) 또는 `unity_get_ui_tree`(UI Toolkit). `unity_get_available_actions`로 지금 클릭 가능한 요소만 추린다
3. 입력: 타깃 path로 `unity_input_click` 등을 호출. 좌표보다 **path/instanceId 타깃을 우선**한다 (해상도에 독립적)
4. 검증: `captureResult`의 스크린샷과 로그, 또는 `unity_get_screen` 재조회. 에러는 `unity command get_console_logs --json -- --severity error`
5. 종료: `unity command editor_stop --json` 또는 `unity_play_mode`(action="exit")

## CLI 형식

Unity Pipeline이 설치된 프로젝트에서는 같은 도구를 `unity command`로도 부를 수 있다(파라미터 이름 동일). MCP 서버가 없는 셸 스크립트나 헤드리스 CI에서 쓴다.

```bash
unity command --tag breadpack/input --json
unity command unity_input_click --json -- --target Canvas/Panel/Button --captureResult true
unity command unity_input_type_text --json -- --text hello --intervalMs 30
```

## 함정

- **Play Mode 전용**이다. Edit Mode에서 호출하면 실패한다.
- Play Mode 진입 시 도메인 리로드로 연결이 잠시 끊긴다. 훅이 대기해 주므로 곧바로 재시도한다.
- 같은 이름의 요소가 여러 개면 `{"path":"Button","index":1}`처럼 index를 준다.
- Legacy Input Manager 프로젝트에서는 `unity_input_pinch`와 raw 키 입력이 동작하지 않는다. `unity_input_key`는 Enter/Escape 의미 이벤트만 전달된다.
- 한글·IME 입력은 미지원이다. 텍스트 검증은 ASCII로 한다.
