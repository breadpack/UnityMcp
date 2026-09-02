---
name: unity-cli-workflow
description: Unity CLI(`unity command`)와 UnityMcp MCP 도구의 역할 분담 - 어떤 작업을 CLI로 하고 어떤 작업을 MCP로 하는지, 연결 확인·컴파일·테스트·빌드 루프
---

# Unity CLI × UnityMcp 역할 분담

Unity 6 프로젝트에 `com.unity.pipeline`이 설치되어 있으면 Unity 공식 CLI가 Editor를 직접 제어한다. **CLI로 되는 일은 CLI로, CLI에 없는 일은 UnityMcp 도구로** 한다.
세션 시작 훅이 `[Unity connection]` 컨텍스트로 어느 경로가 살아 있는지 알려준다.

## 어느 쪽을 쓰나

| 작업 | 경로 | 이유 |
|---|---|---|
| 씬 계층 조회, GameObject 생성/삭제/이동, 컴포넌트 추가/속성 설정 | `unity command` | Pipeline 내장, Undo 그룹 자동 |
| 에셋 검색·이동·복사·삭제, 폴더, 임포트 설정 | `unity command` | Pipeline 내장 |
| Play Mode 진입/종료, 콘솔 로그, 스크린샷 | `unity command` | Pipeline 내장 |
| 재컴파일, 테스트, 빌드, 프로젝트 설정, 패키지 | `unity command` / `unity test` / `unity build` | CLI 전용 옵션(shard, coverage, profile)이 더 풍부 |
| C# 즉석 실행 | `unity command eval` | Roslyn, 도메인 리로드 없음 |
| 머티리얼 생성·속성, AnimationClip·AnimatorController 편집 | `unity command` | Pipeline 내장 |
| **Play Mode 입력** (클릭·드래그·홀드·스와이프·스크롤·핀치·키·텍스트) | `unity_input_*` | Pipeline은 좌표 기반 `simulate_*` 두 개뿐 (Input System·런타임 서버 전용) |
| **UI 트리 인식** (uGUI 트리, UI Toolkit 트리, 활성 화면, 클릭 가능 요소) | `unity_get_ugui_tree`, `unity_get_ui_tree`, `unity_get_screen`, `unity_get_available_actions` | Pipeline은 캡처만 있음 |
| **Prefab 선언적 편집·스테이지 편집·디스크 구조 조회** | `unity_prefab_apply`, `unity_prefab_edit`, `unity_get_asset_hierarchy` | Pipeline `save_prefab_contents`는 rename/active만 |
| **오프스크린 렌더** (UXML, Prefab 프리뷰) | `unity_render_uxml`, `unity_render_prefab_preview` | Pipeline에 없음 |
| **Addressable 등록·주소 변경** | `unity_addressable_*` | Pipeline에 없음 |
| **Undo/Redo 히스토리** | `unity_undo` | Pipeline에 없음 |
| **런타임 Animator 파라미터·상태**, 클립 포즈 샘플링 | `unity_animator_control`, `unity_animation_clip`(sample) | Pipeline에 없음 |
| 에셋 참조 배열·중첩 필드 설정 | `unity_set_asset_reference` | Pipeline `set_component_properties`의 handle 지원 범위 확인 전까지 |

Pipeline 서버가 없으면(Unity 5.x, 패키지 미설치, Safe Mode) 모든 작업을 MCP 도구로 한다. MCP 도구도 없고 Pipeline만 있으면 위 표의 MCP 전용 작업은 `unity command eval`로 대체한다.

## 설치·연결 확인

```bash
unity --version                       # CLI 존재 확인 (없으면 winget install Unity.CLI / brew install --cask unity-cli)
unity pipeline list --json            # 실행 중인 Editor 와 Pipeline 설치·도달 여부, safeMode 판정
unity pipeline install                # 프로젝트에 com.unity.pipeline 추가 (한 번만, Editor 재컴파일 유발)
unity status --json                   # 연결된 Editor: port, project, version, state(ready/settling)
unity command --tag breadpack --json  # UnityMcp 고유 도구가 [CliCommand] 로 노출된 명령만 보기
```

## UnityMcp 고유 도구를 CLI로 호출하기

`com.breadpack.unity-mcp`와 `com.unity.pipeline`이 같이 설치되어 있으면 위 표의 MCP 전용 도구 27개가 **같은 이름·같은 파라미터**로 `unity command`에도 나타난다(태그 `breadpack/input`, `breadpack/ui`, `breadpack/prefab`, `breadpack/render`, `breadpack/addressable`, `breadpack/animation`, `breadpack/editor`, `breadpack/custom`). MCP 서버 없이 CLI만으로도 전체 워크플로우가 가능하고, `unity mcp`·`unity run --command`(헤드리스)에서도 그대로 쓸 수 있다.

```bash
unity command --tag breadpack/input --json
unity command unity_input_click --json -- --target Canvas/Panel/Button --captureResult true
unity command unity_input_drag --json -- --from '{"target":"Canvas/Slider/Handle"}' --to '{"position":{"x":400,"y":200}}'
unity command unity_get_ugui_tree --json -- --maxDepth 4 --includeDetails true
unity command unity_prefab_apply --json -- --assetPath Assets/Prefabs/Enemy.prefab --edits '[{"op":"add_component","target":"","componentType":"BoxCollider2D"}]'
unity command unity_render_uxml --json -- --uxmlPath Assets/UI/Main.uxml --width 1080 --height 1920
unity command unity_custom_tool --json -- --toolName my_tool --parameters '{"key":"value"}'   # 사용자 [McpTool] 도구
```

- 파라미터 이름은 MCP 도구와 동일한 camelCase다(Pipeline 내장 명령의 snake_case와 다르다). JSON을 받는 인자는 문자열로 넘기면 자동 파싱된다.
- MCP 도구가 함께 살아 있을 때는 MCP 호출이 더 빠르다(CLI 스폰 비용 없음). CLI 형식은 MCP를 붙일 수 없는 환경, 헤드리스 CI, 셸 스크립트에서 쓴다.

- `unity status`가 `settling`이면 Editor가 임포트/컴파일 중이다. `ready`가 될 때까지 기다린다.
- 항상 `--json`을 붙여 `{ success, data, errors }` 봉투를 파싱한다. 종료 코드 8은 테스트 실패, 7은 재시도 가능한 서비스 불가.
- 여러 Editor가 열려 있으면 `--project-path <path>`로 지정한다.

## 명령 실행 형식

```bash
unity command <name> --json [-- --arg value ...]
unity command get_scene_hierarchy --json
unity command create_gameobject --json -- --name Enemy --primitive cube
unity command set_component_properties --json -- --target '{"hierarchyPath":"/Enemy"}' --type Rigidbody --properties '{"mass":2}'
unity command delete_asset --json -- --path Assets/Old.mat --confirm true
unity command eval --json -- --code 'return UnityEngine.Application.unityVersion;'
```

- 오브젝트 참조는 `ObjectRef` JSON 하나로 넘긴다: `{"hierarchyPath":"/Root/Child"}`, `{"instanceId":123}`, `{"path":"Assets/X.prefab"}`, `{"guid":"..."}`, `{"globalId":"..."}`. 명령 출력의 `globalId`/`instanceId`를 다음 명령 입력으로 그대로 쓴다.
- 파괴적 명령(delete_*, clear_*, set_*_settings, package_add/remove)은 `--confirm true`가 없으면 거부된다. 먼저 `--dry_run true`로 미리 본다.
- 인자 이름은 snake_case이며 `unity command <name> --help` 또는 `unity list --json`에서 스키마를 확인한다.

## 편집 → 컴파일 → 테스트 루프

```bash
unity command set_autotick --json -- --enable true     # 세션 훅이 자동으로 켜준다. 수동 세션이면 먼저 실행 (Pipeline 없으면 MCP `unity_set_autotick`)
# ... .cs 파일 수정 ...
unity command recompile --json
unity command recompile_status --json                  # completed | up_to_date 까지 폴링, failed=true 면 errors[] 확인
unity command list_tests --json -- --mode editor
unity command run_tests --json -- --mode editor --filter MyFixture
```

- 도메인 리로드 중 연결 오류는 정상이다. 훅(PreToolUse)이 MCP 도구 호출 전에는 대기해 주지만, Bash로 `unity command`를 직접 칠 때는 `recompile_status`를 직접 폴링한다.
- 헤드리스 CI: `unity test . --mode EditMode --report-format junit --output results.xml`, `unity build . --profile "Windows Release"`. 긴 작업은 `unity command <name> --detach` 후 `unity job wait <id>`.

## Play Mode 검증 루프 (CLI + MCP 조합)

1. `unity command editor_play --json`
2. `unity_get_screen` / `unity_get_ugui_tree`로 현재 화면 파악 (MCP)
3. `unity_input_click`(target="Canvas/Panel/Button", captureResult=true) 등으로 조작 (MCP)
4. `unity command get_console_logs --json -- --severity error` 로 에러 확인
5. `unity command editor_stop --json`

## 함정

- **Safe Mode**: 컴파일 에러로 Editor가 Safe Mode에 들어가면 Pipeline·UnityMcp 모두 로드되지 않는다. `unity pipeline list`의 `safeMode.detected`를 보고, `Logs/Editor.log`의 `error CS` 줄을 읽어 코드를 고친 뒤 Editor를 재시작한다. 훅이 실패 진단 시 이 에러를 컨텍스트로 넣어 준다.
- **Editor 포커스**: `set_autotick`이 꺼져 있으면 비포커스 상태에서 컴파일·테스트가 멈춘 것처럼 보인다.
- **CLI 호출 지연**: 호출당 약 0.8초(프로세스 스폰). 수십 개 오브젝트를 만들 때는 `create_gameobjects`(복수형)나 `eval` 한 번으로 묶는다.
- **`--group_by`는 언더스코어**다. CLI 옵션 이름이 릴리스마다 바뀌므로 확신이 없으면 `--help`를 먼저 본다.
- **Prefab 스테이지가 열려 있는 동안** `unity command`의 씬 명령은 스테이지가 아니라 열린 씬을 대상으로 할 수 있다. Prefab 편집은 `unity_prefab_apply`로 한다.
