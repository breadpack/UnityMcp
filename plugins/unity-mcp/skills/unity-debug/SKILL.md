---
name: unity-debug
description: Unity 프로젝트를 디버깅하고 검사합니다
---

# Unity Debug

Unity 프로젝트의 상태를 검사하고 디버깅할 때 사용합니다.

## 상태 확인

1. **연결 확인**: `unity_ping`으로 Unity Editor 연결 상태를 확인합니다
2. **씬 구조**: `unity_get_hierarchy`로 현재 씬의 GameObject 트리를 조회합니다
3. **컴포넌트 상세**: `unity_get_component_details`로 특정 컴포넌트의 모든 프로퍼티 현재값을 확인합니다
4. **콘솔 로그**: `unity_get_console_logs`로 에러/경고/로그를 확인합니다
   - `logType="Error"`: 에러만 필터링
   - `includeStackTrace=true`: 스택 트레이스 포함 (토큰 주의)

## Play Mode 디버깅

1. `unity_play_mode`(action="enter")로 Play Mode 진입
2. `unity_take_screenshot`으로 게임 화면 캡처 (maxWidth로 해상도 제한 가능)
3. `unity_get_screen`으로 활성 UI 상태 확인
4. `unity_animator_control`(action="get_parameters")로 Animator 상태 조회
5. `unity_animator_control`(action="get_current_state")로 현재 애니메이션 상태 확인
6. `unity_get_console_logs`로 런타임 에러 확인
7. `unity_play_mode`(action="exit")로 종료

## Unity CLI로 할 때 (Pipeline 연결 시 우선)

상태 확인과 Play Mode 제어·로그·스크린샷은 Pipeline 명령이 그대로 대체한다. 입력 시뮬레이션과 UI 트리·Animator 런타임 조회만 MCP 도구가 필요하다.

```bash
unity status --json                                              # ready / settling, project, version
unity command editor_status --json                               # compiling, domainReloadInProgress, playMode
unity command get_console_logs --json -- --severity error --limit 50
unity command recompile --json && unity command recompile_status --json   # failed=true 면 errors[] 에 CS 에러
unity command editor_play --json
unity command capture_game_view --json -- --source screen --max_resolution 640   # overlay UI 포함, base64 인라인
unity command get_performance_stats --json                       # 렌더·메모리·프레임 타임
unity command editor_stop --json
unity command audit --json && unity command audit_status --json  # Project Auditor 정적 분석 CSV
```

Safe Mode(컴파일 에러로 패키지 미로드)면 두 경로 모두 불통이다. `unity pipeline list --json`의 `safeMode.detected`를 확인하고 `Logs/Editor.log`의 `error CS` 줄을 읽는다.

## 에셋 검사

- `unity_get_asset_hierarchy`로 Prefab/Scene 파일 내부 구조를 열지 않고 확인
- `unity_manage_asset`(action="move/copy/delete")로 에셋 정리

## 주의사항

- Play Mode 전환 시 도메인 리로드가 발생하여 MCP 연결이 일시 끊김 → 자동 재연결됨
- `unity_get_console_logs`의 기본 반환 수는 50개, StackTrace는 기본 미포함
