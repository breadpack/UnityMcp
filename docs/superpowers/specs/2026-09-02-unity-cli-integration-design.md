---
title: Unity CLI / Pipeline 연동 설계 — UnityMcp 역할 재정의
date: 2026-09-02
status: draft
related:
  - 2026-04-11-unity-mcp-plugin-upgrade-design.md
  - 2026-04-26-playmode-input-simulation-design.md
---

# Unity CLI / Pipeline 연동 설계

## 0. 요약

Unity가 2026년 4~8월에 걸쳐 **Unity CLI**(`unity` 바이너리, 1.0.0-beta.6)와 **Unity Pipeline 패키지**(`com.unity.pipeline` 0.5.0-exp.1)를 출시했다. Pipeline은 Editor 안에서 localhost HTTP 서버를 띄우고 153개의 내장 명령을 노출하며, CLI는 이를 `unity command`, `unity eval`, `unity mcp`(MCP stdio 서버)로 감싼다. 프로젝트가 `[CliCommand]` 어트리뷰트로 명령을 추가하면 CLI 목록과 MCP 툴 목록에 자동으로 나타난다.

UnityMcp의 현재 도구 60여 개 중 **약 2/3는 Pipeline 내장 명령과 기능이 겹친다**. 나머지 1/3(Play Mode 입력 시뮬레이션, UI 트리 인식, 선언적 Prefab 편집, UXML/Prefab 오프스크린 렌더, Addressable, 런타임 Animator 제어, Undo 등)은 Pipeline에 없거나 훨씬 약하다.

**권장 방향: UnityMcp를 "독립 브릿지"에서 "Pipeline 위의 확장 명령 팩(command pack)"으로 전환한다.**

- Bridge(.NET stdio 서버)·자체 TCP 프로토콜·포트 탐색·버전 동기화 기능을 단계적으로 폐기하고, Unity 공식 전송 계층(Pipeline HTTP + CLI)을 쓴다.
- UnityMcpEditor의 고유 핸들러를 `[CliCommand]`로 재노출한다. 그러면 `unity command`, `unity mcp`, `unity run --command`(헤드리스 CI)에서 전부 쓸 수 있다.
- 플러그인(hooks/skills/agents)은 Pipeline의 상태 API를 기준으로 재작성하고, CLI로 충분한 작업은 skill 문서로 안내한다.

전환은 4단계로 나누며, 각 단계는 독립적으로 릴리스 가능하다(§5).

## 1. 리서치 결과

### 1.1 조사 범위와 출처

| 출처 | 내용 |
|---|---|
| docs.unity.com/unity-cli (reference, use, replace-mcp-server, release-notes) | 명령 전체 목록, 출력 포맷, 종료 코드, 환경변수, 릴리스 이력 |
| `com.unity.pipeline@0.5.0-exp.1` 패키지 tarball (Unity 레지스트리에서 직접 다운로드) | `Editor/Commands`, `Runtime/Commands` 소스, `Documentation~`, 번들 skill `.claude/skills/unity-pipeline/SKILL.md` |
| Unity-Technologies/skills 저장소 `skills/unity-cli` | Unity 공식 에이전트 skill (SKILL.md + references 8종) |
| 로컬 설치된 `unity` 1.0.0-beta.5의 `--help` 출력 | 실제 옵션 확인 (`command`, `run`, `test`, `mcp`, `pipeline`, `job`) |
| 서드파티 분석 (vindler.solutions, gamineai.com) | 성능·토큰·안정성 평가 |

검증 한계: 이 머신에 열려 있는 Unity 인스턴스(CSClient, 6000.3.15f1)는 manifest에 `com.unity.pipeline` 0.5.0-exp.1이 있으나 Pipeline 서버가 reachable 상태가 아니어서(`unity pipeline list` → `isReachable: false`, `Library/PackageCache`에 패키지 미존재), **살아 있는 Editor에 대한 `unity command` / `unity mcp tools/list` 실제 호출은 확인하지 못했다.** §6의 오픈 이슈 일부는 이 검증이 선행되어야 한다.

### 1.2 Unity CLI 개요

- 배포: Unity Hub에 동봉되며 독립 설치도 가능 (`winget install Unity.CLI`, `brew install --cask unity-cli`, install.sh/ps1). 현재 beta 채널, 실험적(experimental) 지위.
- 지원: Editor 설치·관리는 모든 버전, **살아 있는 Editor 제어는 Unity 6.0 LTS 이상**(Pipeline 패키지 요건). UnityMcpEditor의 `"unity": "6000.0"` 요건과 동일하다.
- 출력: `--format human|json|tsv|ndjson|github`. JSON 봉투 `{ success, command, data, errors, warnings }`. 종료 코드 0/1/2/3/4/6/7/8/130/143.
- 명령 그룹:
  - Editor/모듈: `install`, `editors`, `modules`, `releases`, `hub`
  - 프로젝트/빌드: `open`, `projects`(create/clone/verify/clean/exec/close…), `templates`, **`build`**, **`run`**, **`test`**
  - 계정: `auth`(서비스 계정 지원), `license`, `cloud`
  - **연결된 Editor / AI 에이전트: `command`, `list`, `status`, `pipeline`, `job`, `mcp`, `skill`**
  - 진단: `doctor`, `diagnose`, `logs`, `env`, `config`, `cache`, `shell`(REPL, `--protocol ndjson` 기계 모드)

### 1.3 Editor 제어 계층 (Pipeline 패키지)

```
unity CLI ──HTTP(localhost:7800~7849, Bearer token)──▶ EditorPipelineServer ──▶ CommandRegistry ──▶ [CliCommand] 메서드
                                                        │
                                                        └─ /api/status, /api/editor_status, /api/commands,
                                                           /api/exec, /api/progress, /api/job, /api/test-status
```

- **디스커버리**: `<project>/Library/Pipeline/.unity-pipeline-port` 파일에 `{ pid, port, projectPath, unityVersion, mode, evalToken, lastHeartbeat }`. CLI는 cwd 또는 `--project-path`로 인스턴스를 자동 매칭한다. 별도 브로드캐스트/레지스트리 없음.
- **인증**: 모든 요청에 `Authorization: Bearer <evalToken>`. 토큰은 SessionState에 저장되어 **도메인 리로드·Play Mode 진입에도 유지**된다(0.4에서 수정됨).
- **상태 모델**: `/api/status` → `settling | ready | error`. 콜드 임포트/컴파일 중에는 메인스레드 명령을 HTTP 503으로 거부한다. `editor_status`는 `Compiling`, `DomainReloadInProgress`, `PlayMode`를 돌려준다.
- **실행 모델**: `/api/exec`는 1건씩 직렬 처리, 진행률은 `/api/progress`(메인스레드 블록 중에도 응답). `"job": true`로 **분리 잡(detached job)** 제출 후 `unity job wait/status/cancel`로 재접속. `eval` 타임아웃 상한 24시간.
- **안전 모델**: 파괴적 명령은 `confirm=true` 필수, `dry_run` 지원. 씬/오브젝트 변경은 `AuthoringUndoScope`로 단일 Undo 스텝. 경로는 `set_authoring_root`로 지정한 폴더(기본 `Assets`) 안으로 강제(`..` 차단).
- **오브젝트 참조 규약**: `ObjectRef { globalId | path | guid(+fileId) | instanceId | hierarchyPath }`. 명령 출력의 `AuthoringResult`를 다음 명령 입력으로 그대로 넘긴다.
- **커스텀 명령**: Editor asmdef가 `Unity.Pipeline`을 참조하고 `[CliCommand("name", "desc", MainThreadRequired, RuntimeOnly, Tags)]` + `[CliArg]`를 붙이면 TypeCache로 자동 발견. 인자는 primitive, `ObjectRef`, `JObject`, `IStructuredCommandInput` DTO(중첩 JSON 스키마 자동 생성). 반환은 임의 직렬화 객체. **재컴파일 후 즉시 `unity command`·`unity mcp` 목록에 노출된다.**
- **런타임 서버**: 개발용 Player에 `RuntimePipelineManager`를 넣으면 7900~7949 포트에서 `runtime_status`, `set_timescale`, `simulate_key/pointer`, `eval`, hot reload(`reload_file`)를 제공. IL2CPP 미지원.

### 1.4 Pipeline 내장 명령 153개 (0.5.0-exp.1, 카테고리별)

| 카테고리 | 명령 |
|---|---|
| 에셋/파일 (12) | create_asset, import_asset, move/copy/rename/delete_asset, find_assets, get/set_import_settings, create_folder, read/write_text_file |
| 씬 (9) | create/open/save_scene, save_all, list_open_scenes, set_active_scene, **get_scene_hierarchy**, add/remove_scene_to_build |
| GameObject/컴포넌트 (14) | create_gameobject(s), find_gameobjects, set_transform, set_parent, set_active, set_tag, set_layer, rename/delete_gameobject, add/remove_component, **get/set_component_properties** |
| Prefab (7) | create_prefab, instantiate_prefab, create_prefab_variant, apply/revert_prefab_overrides, unpack_prefab, save_prefab_contents(rename·active만) |
| 스크립트 (4) | create_script, attach_script, get/set_serialized_field(s) |
| 애니메이션 (14) | create_animation_clip, set/get/remove_animation_curve, create_animator_controller, add_animator_parameter/layer/state/transition, get_animator_controller, create_timeline, add_timeline_track/clip, get_timeline |
| 머티리얼/셰이더 (4) | get/set_material_properties, list_shaders, get_shader_properties |
| 베이킹 (17) | lighting / navmesh / occlusion의 bake·status·cancel·clear·get/set_settings |
| 네비게이션/선택 (3) | get/set_selection, search(Unity Search) |
| 캡처 (3) | capture_game_view(source=camera/screen, base64 or path), capture_scene_view, capture_editor_element(UI Toolkit VisualElement) |
| 빌드/컴파일/테스트 (12) | build(+status), switch_build_target(+status), list_build_targets/profiles, get/set_build_settings, recompile(+status: errors 배열), list/run/cancel_tests, test_status |
| 프로젝트 설정 (16) | audio/graphics/input/physics/player/quality/tags_layers/time 각 get/set |
| 패키지 (6) | package_list/search/add/remove/resolve/status |
| Editor 생명주기/관측 (12) | editor_play/stop/pause/status/focus, menu(실행·목록), screenshot, **set_autotick**, get_console_logs, clear_console, get_performance_stats, audit(+status) |
| 인증 루트 (2) | get/set_authoring_root |
| 런타임 (14) | runtime_status, quit, set_target_framerate, set_timescale, simulate_key, simulate_pointer, log, console, eval, eval_file, reload_file(_override), hotreload_status, cleanup_hotreload |

### 1.5 AI 에이전트 통합 방식 (Unity 공식)

- `unity mcp` — Pipeline 명령 전체를 MCP 툴로 노출하는 stdio 서버. `unity mcp configure <client>`가 Claude/Cursor/VS Code/Windsurf 등 16개 클라이언트 설정 파일을 직접 갱신한다. 기존 `com.unity.ai.assistant` 내장 MCP 서버는 **deprecated**.
- `unity skill install <client>` — Unity 공식 `unity-cli` skill을 클라이언트 skills 폴더에 설치. Pipeline 패키지 자체도 `.claude/skills/unity-pipeline/SKILL.md`를 동봉한다.
- Unity 문서는 "셸을 실행할 수 있는 에이전트는 MCP 대신 `unity command` / `unity eval`을 직접 쓰는 편이 더 빠르고 토큰이 적다"고 명시한다.

### 1.6 알려진 한계·평가 (서드파티 및 소스 확인)

| 항목 | 내용 | UnityMcp 대비 |
|---|---|---|
| 호출 지연 | CLI 프로세스 스폰 포함 호출당 약 0.8초 (자체 TCP 툴링 0.05초 대비 16배) | UnityMcp TCP 직결이 더 빠름. 훅처럼 빈번한 폴링은 CLI가 아니라 HTTP 직접 호출이 낫다 |
| 토큰 효율 | 153개 툴 스키마가 MCP 컨텍스트에 그대로 실림. 세밀한 명령을 여러 번 호출해야 하는 구조 | UnityMcp의 `prefab_apply` 같은 복합·원자적 도구가 유리 |
| 포커스/틱 | 비포커스 시 Editor 틱이 멈춤 → `set_autotick --enable true` 선행 필수 | UnityMcp도 동일 문제. 훅에서 autotick을 켜주면 해결 |
| Safe Mode | 컴파일 에러로 Safe Mode 진입 시 Pipeline 패키지 자체가 로드 안 되어 연결 불가. 로그 파일로만 진단 | UnityMcpEditor도 동일 조건에서 로드 불가. 현 `get_compile_errors`도 무력 |
| run_tests | 실패 시 상세가 불투명하게 나올 수 있음(공식 skill 명시) | UnityMcp `run_tests` 결과 파싱이 현재 더 상세 |
| 입력 시뮬레이션 | `simulate_key/pointer`는 **Input System 전용, 런타임 서버 전용**, 좌표 기반. uGUI 타깃 지정·드래그·스와이프·핀치·텍스트 입력 없음 | UnityMcp Input 8종이 압도적으로 우위 |
| UI 인식 | 캡처는 있으나 uGUI/UI Toolkit 트리 구조 조회 없음 | UnityMcp `get_ugui_tree`, `get_ui_tree`, `get_available_actions` 고유 |
| Prefab 편집 | `save_prefab_contents`는 rename/active 두 가지만. 스테이지 진입 API 없음 | UnityMcp `prefab_apply`(9개 op 선언적 편집), `prefab_edit` 고유 |
| Addressable | 없음 | UnityMcp 고유 |
| Undo/Redo | 명령 단위로 Undo 그룹만 생성. 히스토리 조회·실행 명령 없음 | UnityMcp `unity_undo` 고유 |
| Animator 런타임 제어 | 없음 (컨트롤러 에셋 편집만) | UnityMcp `animator_control`, `animation_clip sample` 고유 |
| 오프스크린 렌더 | 없음 | UnityMcp `render_uxml`, `render_prefab_preview` 고유 |
| 안정성 | CLI beta, 패키지 experimental. 옵션 이름·명령이 릴리스마다 바뀜(예: `--instance` 제거) | 하드 의존을 피하고 버전 핀 + 어댑터 계층 필요 |

## 2. 기능 매핑: UnityMcp 도구 ↔ Pipeline 명령

분류 기준
- **A 대체**: Pipeline 명령이 동등 이상. UnityMcp 도구는 deprecate 후 제거.
- **B 보강**: Pipeline에 기본형은 있으나 UnityMcp 쪽이 더 풍부. UnityMcp 기능을 `[CliCommand]`로 남기되 Pipeline 규약(ObjectRef, confirm/dry_run)에 맞춰 재설계.
- **C 고유**: Pipeline에 없음. `[CliCommand]`로 이식하여 유지·강화.
- **D 폐기**: 전송 계층이 바뀌면 존재 이유가 없어짐.

| UnityMcp 도구 | 분류 | Pipeline 대응 | 비고 |
|---|---|---|---|
| unity_ping, unity_get_editor_state | D | `/api/status`, `editor_status` | 훅이 HTTP로 직접 호출 |
| unity_bridge_self_update | D | — | Bridge 제거와 함께 소멸 |
| unity_list_custom_tools, unity_custom_tool (`[McpTool]`) | D | `[CliCommand]` 자동 발견 | 사용자에게 `[McpTool]` → `[CliCommand]` 마이그레이션 안내 |
| unity_execute_code | A | `eval`, `eval_file` | Roslyn 기반, 로드된 어셈블리 전부 참조 |
| unity_execute_menu_item | A | `menu` | 목록 조회까지 지원 |
| unity_play_mode | A | `editor_play/stop/pause`, `editor_status` | |
| unity_get_console_logs | A | `get_console_logs`(severity/limit), `console`(cursor follow) | 0.5에서 Play Mode 종료 후 캡처 유실 버그 수정됨 |
| unity_take_screenshot, unity_take_scene_view_screenshot | A | `capture_game_view`(source=screen이 overlay UI 포함), `capture_scene_view` | base64/path/max_resolution 모두 지원 |
| unity_get_hierarchy | A | `get_scene_hierarchy` | instanceId + hierarchyPath 반환 |
| unity_create_gameobject, unity_delete_gameobject, unity_set_active, unity_set_transform, unity_reparent_gameobject | A | create_gameobject(s), delete_gameobject, set_active, set_transform, set_parent | Undo 그룹 포함 |
| unity_add_component, unity_remove_component | A | add_component, remove_component | |
| unity_get_component_details, unity_set_property | A | get/set_component_properties | 벡터·색은 배열, 오브젝트 참조는 handle |
| unity_set_asset_reference | B | set_component_properties(handle 객체) | 배열 요소·중첩 필드 지원 여부 검증 후 결정 |
| unity_get_selection, unity_select_gameobject | A | get_selection, set_selection | |
| unity_find_assets, unity_manage_asset, unity_refresh_assets | A | find_assets, move/copy/delete_asset, create_folder, `menu Assets/Refresh` 또는 `package_resolve` | |
| unity_load_scene, unity_save_scene | A | open_scene, save_scene/save_all | |
| unity_instantiate_prefab | A | instantiate_prefab | |
| unity_prefab_apply, unity_prefab_edit | **C** | save_prefab_contents(rename/active만) | 선언적 9-op 편집은 고유. `ObjectRef` + `IStructuredCommandInput`로 재설계 |
| unity_get_asset_hierarchy | C | — | Prefab 내부 구조 조회. `get_scene_hierarchy`가 prefab stage를 못 봄 |
| unity_create_material, unity_set_material_property | A | create_asset(type=Material) + set_material_properties, list_shaders, get_shader_properties | |
| unity_animation_clip | B | create_animation_clip, set/get/remove_animation_curve | **sample/stop_sample(에디터 포즈 미리보기)는 고유** |
| unity_animator_controller | A | create_animator_controller, add_animator_*, get_animator_controller | assign은 set_component_properties |
| unity_animator_control | C | — | Play Mode 파라미터 조작·현재 상태 조회 |
| unity_get_ugui_tree, unity_get_ui_tree, unity_get_screen, unity_get_available_actions | **C** | capture_editor_element(캡처만) | UI 인식 계층 전체가 고유 |
| unity_input_click/drag/hold/swipe/scroll/pinch/key/type_text | **C** | simulate_key, simulate_pointer(Input System·런타임 서버·좌표만) | 8종 모두 유지. Editor Play Mode에서 동작하는 점이 결정적 |
| unity_render_uxml, unity_render_prefab_preview | C | — | 오프스크린 렌더 |
| unity_addressable_add, unity_addressable_set_address | C | — | |
| unity_undo | C | — | 히스토리·redo |
| unity_get_compile_errors | B | recompile_status(errors[]) | 재컴파일 없이 현재 에러만 읽는 경로가 없으면 유지 |
| unity_get_project_info | B | editor_status + package_list + get_build_settings | 한 번에 요약하는 편의 명령으로 유지 가능 |
| unity_build | A | build, build_status, list_build_profiles / CLI `unity build`(헤드리스) | |
| unity_run_tests | B | run_tests, test_status / CLI `unity test`(NUnit·JUnit·coverage·shard) | 실패 상세 파싱이 약하면 결과 포매터만 남김 |
| unity_project_settings | A | get/set_* 16종 | Pipeline이 더 세분화 |
| unity_manage_package | A | package_list/search/add/remove/resolve/status | |

집계: A 대체 26, B 보강 5, C 고유 16(도구 기준), D 폐기 5.

## 3. 설계 대안 비교

| 대안 | 내용 | 장점 | 단점 |
|---|---|---|---|
| **α. 현상 유지 + 병행** | Bridge/TCP 유지, 사용자가 `unity mcp`를 따로 붙임 | 작업 없음 | 툴 200개 이상 이중 노출, 포트·상태 체계 2벌, 유지보수 부담 지속 |
| **β. 명령 팩 전환 (권장)** | UnityMcpEditor 고유 핸들러를 `[CliCommand]`로 재노출. Bridge·TCP·포트 탐색·버전 동기화 제거. 플러그인은 Pipeline HTTP/CLI 위에서 동작 | 전송·인증·디스커버리·잡·진행률을 Unity가 책임. CI 헤드리스(`unity run --command`)까지 공짜로 확보. 코드 약 1/3 삭제 | Pipeline experimental 의존. 명령 규약(ObjectRef, confirm)에 맞춘 재설계 필요 |
| **γ. Bridge를 Pipeline 프록시로 재작성** | Bridge가 TCP 대신 Pipeline HTTP를 호출하고, 선별·복합 툴만 MCP로 노출 | 툴 수와 토큰 통제 가능, 지연 0.8초 회피 | .NET 서버 배포 파이프라인(6 플랫폼 바이너리, NuGet, npm) 유지 비용 그대로 |

β를 기본으로 하되, **γ의 "선별 노출" 아이디어는 MCP 진입점 결정(§6 Q1)에 따라 옵션으로 남긴다.** `unity mcp`가 툴 필터링을 지원하면 γ는 불필요하다.

## 4. 목표 아키텍처 (β)

```
              ┌──────────── Claude Code / Cursor / CI ────────────┐
              │  Bash: unity command <name> --json   (기본 경로)   │
              │  MCP : unity mcp --project-path …    (선택)        │
              │  CI  : unity run . --command <name>  (헤드리스)    │
              └────────────────────────┬──────────────────────────┘
                                       │ HTTP localhost:7800+ (Bearer)
                       ┌───────────────▼────────────────┐
                       │ com.unity.pipeline (Unity 공식) │  153 내장 명령
                       └───────────────┬────────────────┘
                                       │ [CliCommand] 자동 발견
                       ┌───────────────▼────────────────┐
                       │ com.breadpack.unity-mcp         │  UnityMcp 고유 명령 팩
                       │  Input/, UiTree/, PrefabApply/,│  (Editor asmdef → Unity.Pipeline 참조)
                       │  Render/, Addressable/, Undo/, │
                       │  AnimatorControl/, Sampling/   │
                       └────────────────────────────────┘

플러그인(plugins/unity-mcp)
  hooks   : Library/Pipeline/.unity-pipeline-port 읽기 → /api/status(settling/ready) → editor_status 폴링
  skills  : "CLI로 할 일"과 "팩 명령으로 할 일"을 워크플로우별로 안내, Unity 공식 skill 참조
  agents  : Bash(unity command) + 선택적 MCP
```

설계 원칙
1. **Unity가 제공하는 것은 만들지 않는다.** 전송·인증·디스커버리·직렬화·잡·진행률·Undo 그룹·경로 샌드박스는 Pipeline 것을 쓴다.
2. **고유 명령은 Pipeline 규약을 따른다.** 입력은 `ObjectRef`/`IStructuredCommandInput`, 파괴적 동작은 `confirm`/`dry_run`, 출력은 `AuthoringResult`를 포함해 다음 명령에 체이닝 가능해야 한다. `Tags`로 `breadpack/input`, `breadpack/ui` 등 서브트리를 부여해 `unity command --tag breadpack`으로 필터 가능하게 한다.
3. **소프트 의존.** asmdef `versionDefines`로 `com.unity.pipeline` 존재 시 `UNITY_PIPELINE_PRESENT`를 정의하고, 없으면 명령 팩이 컴파일에서 빠지도록 한다(패키지 설치만으로 프로젝트가 깨지지 않게).
4. **복합 도구는 유지·확대.** 토큰과 왕복 횟수를 줄이는 `prefab_apply`형 원자 명령은 Pipeline 세밀 명령보다 우선 권장한다.
5. **버전 핀.** 지원하는 `com.unity.pipeline` 버전 범위를 package.json과 훅 스크립트에 명시하고, CLI 옵션 변경은 어댑터(`scripts/unity-cli.js`) 한 곳에서만 흡수한다.

## 5. 단계별 계획

각 Phase는 patch 릴리스 단위(버전 정책은 CLAUDE.md의 patch 기본 원칙). Bridge 제거(Phase 3)는 사용자 설정이 바뀌므로 minor.

### Phase 0 — 검증 스파이크 (코드 변경 없음)

Pipeline 서버가 reachable한 Unity 6 프로젝트에서 다음을 확인하고 결과를 이 문서 §6에 기록한다.
- `unity command --format json`의 실제 명령 수·스키마, `unity mcp` tools/list의 툴 수와 이름 규칙, 툴 필터링 옵션 유무
- `set_component_properties`로 배열 요소·중첩 필드·에셋 참조 설정 가능 여부 (→ `set_asset_reference` 존폐)
- `recompile_status` 없이 현재 컴파일 에러를 읽을 수 있는지 (→ `get_compile_errors` 존폐)
- `run_tests` 실패 상세의 실제 형태
- 도메인 리로드 중 `/api/status` 응답 타이밍 (훅 대기 루프 설계 입력)
- `simulate_pointer`가 Editor Play Mode에서 동작하는지 (런타임 서버 전용인지)
- 콜드 스타트 시 `settling` 지속 시간, `set_autotick` 효과

### Phase 1 — 플러그인 계층을 Pipeline 위로 이전 (Bridge 유지, 0.7.x)

- `scripts/unity-client.js`에 Pipeline 클라이언트 추가: port 파일 파싱, Bearer 헤더, `/api/status`, `/api/exec(editor_status)`. 기존 TCP 경로는 fallback으로 남긴다.
- `check-unity.js`: 연결 확인·컴파일/리로드 대기를 Pipeline 상태(`settling`/`Compiling`/`DomainReloadInProgress`)로 판정. SessionStart에서 `set_autotick --enable true`를 자동 실행(userConfig `auto_tick`, 기본 true).
- 버전 불일치 안내를 "Pipeline 미설치/미도달 → `unity pipeline install` 안내"로 확장.
- skills 6종에 "CLI 우선" 섹션 추가: 씬/에셋/설정/빌드/테스트는 `unity command …` 예시를, 고유 기능은 기존 MCP 도구를 안내. Unity 공식 skill(`unity skill install claude`) 설치를 권장 문구로 명시.
- agents 3종의 tools에 Bash 추가(현재 asset-manager, scene-architect는 Bash 없음).

### Phase 2 — 고유 핸들러를 `[CliCommand]`로 이중 노출 (0.7.x → 0.8.0)

> **상태: 구현 완료 (v0.6.30, 2026-09-02).** 실제 구현은 아래 계획과 다음 점이 다르다.
> - 어댑터를 손으로 쓰지 않고 `scripts/gen-pipeline-commands.js`가 Bridge의 `[McpServerTool]` 정의에서 생성한다. MCP와 CLI의 이름·파라미터·설명이 구조적으로 일치한다.
> - 입력은 `ObjectRef`/DTO로 재설계하지 않고 MCP 파라미터(camelCase)를 그대로 쓴다. Pipeline 규약 정합은 Phase 3에서 Bridge를 제거할 때 함께 다룬다.
> - `[McpTool]` 브리징 어댑터 대신 `unity_custom_tool`/`unity_list_custom_tools` 명령을 노출해 사용자 커스텀 도구를 CLI에서 호출한다.
> - 스텁 어트리뷰트로 컴파일 검증은 했으나, 살아 있는 Pipeline 서버에서의 실행 검증은 Phase 0과 함께 남아 있다.

- UnityMcpEditor에 `Pipeline/` 폴더와 asmdef(`Unity.Pipeline` 참조, `versionDefines`) 추가.
- C·B 분류 핸들러를 `[CliCommand]` 어댑터로 감싼다. 핸들러 본체는 재사용하되 입력을 `ObjectRef`/DTO로 받고 `AuthoringResult`를 반환하도록 정리.
  - `breadpack/input`: click, drag, hold, swipe, scroll, pinch, key, type_text
  - `breadpack/ui`: ugui_tree, ui_tree, screen, available_actions
  - `breadpack/prefab`: prefab_apply, prefab_edit(enter/edit/save/exit), asset_hierarchy
  - `breadpack/render`: render_uxml, render_prefab_preview
  - `breadpack/addressable`: add, set_address
  - `breadpack/animation`: animator_control, animation_sample
  - `breadpack/editor`: undo, project_info, compile_errors(존치 시)
- `[McpTool]` 커스텀 도구: `CustomToolRegistry`가 `[McpTool]` 메서드를 `[CliCommand]`로 브리징하는 어댑터를 제공하되 deprecated 표시. 문서에 `[CliCommand]` 직접 사용 권장.
- 이 시점에서 동일 기능이 MCP(Bridge)와 CLI 양쪽에서 동작한다. 사용자는 아무것도 바꾸지 않아도 된다.

### Phase 3 — Bridge·TCP 폐기 (0.9.0, minor)

- A·D 분류 도구를 Bridge와 Editor 핸들러에서 제거. `McpServerBootstrap`, `TcpServer`, `PortManager`, `MainThreadDispatcher`(Pipeline 것 사용), `version-utils`의 패키지 동기화 로직 제거.
- `plugin.json`의 `mcpServers`를 §6 Q1 결정에 따라 `unity mcp --project-path ${CLAUDE_PROJECT_DIR}`로 교체하거나 제거. `run-bridge.js`와 바이너리 lazy download 삭제.
- `publish.yml`에서 6플랫폼 빌드·NuGet·npm 잡 제거. 릴리스 산출물은 UPM 패키지 태그와 플러그인뿐.
- README/CLAUDE.md/AGENTS.md 아키텍처 절 전면 개정. 마이그레이션 가이드(`docs/migration-0.9.md`) 작성: `unity pipeline install` → 플러그인 업데이트 → `unity skill install claude` 순.

### Phase 4 — CLI 전용 이점 활용 (0.9.x)

- 헤드리스 검증 워크플로우: `unity run . --command breadpack_ui_snapshot` 식으로 CI에서 UI 트리·스크린샷을 산출하는 명령 추가.
- `unity test --shard`, `--format github`를 쓰는 GitHub Actions 예제 제공.
- 긴 작업(빌드, 베이크, 대량 prefab_apply)을 `--detach` + `unity job wait`로 다루는 skill 문서화.

## 6. 오픈 이슈 (Phase 0에서 답을 낸다)

| # | 질문 | 영향 |
|---|---|---|
| Q1 | `unity mcp`가 툴 필터링(tag/allowlist)을 지원하는가? 153+α 툴이 그대로 노출된다면 컨텍스트 비용이 크다 | 플러그인의 `mcpServers` 유지 여부, 대안 γ 채택 여부 |
| Q2 | Editor Play Mode에서 런타임 서버(7900+)가 뜨는가? `simulate_*`가 Editor에서 동작하는가 | Input 8종의 포지셔닝(완전 고유 vs 보강) |
| Q3 | `set_component_properties`의 handle 객체가 배열·중첩·에셋 참조를 커버하는가 | `set_asset_reference` 존폐 |
| Q4 | 재컴파일을 유발하지 않고 현재 컴파일 에러를 읽는 경로가 있는가 | `get_compile_errors` 존폐 |
| Q5 | 도메인 리로드 중 HTTP 서버가 얼마나 오래 죽어 있는가, 재기동 후 port 파일 갱신 지연은 | 훅 대기 루프의 타임아웃·재시도 설계 |
| Q6 | Pipeline 패키지 minor 업데이트 시 `[CliCommand]` API의 호환성 정책 | versionDefines 범위, 지원 버전 매트릭스 |
| Q7 | 프로젝트별 `evalToken` 노출 정책: 훅 스크립트가 토큰을 읽어 HTTP를 직접 치는 것이 허용되는가(파일 권한, 로그 마스킹) | Phase 1 클라이언트 구현 |

## 7. 리스크와 완화

| 리스크 | 완화 |
|---|---|
| Pipeline/CLI가 experimental이라 명령·옵션이 바뀜 | 버전 핀, 어댑터 단일화, Phase 2까지는 Bridge 병행 유지 |
| Unity 6 미만 프로젝트 지원 상실 | 이미 `6000.0` 요건이므로 실질 영향 없음. 명시적으로 문서화 |
| 호출 지연 0.8초(CLI 스폰) | 훅은 HTTP 직접 호출. 에이전트 작업은 복합 명령으로 왕복 수 절감. 빈번 폴링은 `--detach`/`job wait` |
| 툴 수 폭증으로 토큰 낭비 | Q1 결과에 따라 필터링 또는 Bash 우선 정책. skills에서 "작업당 1~2개 명령" 레시피 제공 |
| Safe Mode에서 양쪽 다 불통 | 훅이 `unity pipeline list`의 `safeMode.detected`와 `Logs/Editor.log`를 읽어 컴파일 에러를 컨텍스트로 주입 |
| 사용자 `[McpTool]` 코드 호환 | Phase 2 어댑터로 1 minor 동안 유지, deprecation 경고 |

## 8. 참고 링크

- Unity CLI 문서: https://docs.unity.com/en-us/unity-cli/unity-cli-reference , https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli , https://docs.unity.com/en-us/unity-cli/release-notes
- Pipeline 패키지 문서: https://docs.unity3d.com/Packages/com.unity.pipeline@0.5/manual/index.html
- Unity 공식 에이전트 skill: https://github.com/Unity-Technologies/skills/tree/main/skills/unity-cli
- 기술 워크스루: https://unity.com/resources/unity-pipeline-cli-technical-walkthrough
- 서드파티 평가: https://vindler.solutions/blog/unity-cli-agent-automation , https://gamineai.com/blog/how-to-use-unity-cli-first-agent-safe-editor-session-2026
