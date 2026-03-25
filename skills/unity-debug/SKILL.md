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

## 에셋 검사

- `unity_get_asset_hierarchy`로 Prefab/Scene 파일 내부 구조를 열지 않고 확인
- `unity_manage_asset`(action="move/copy/delete")로 에셋 정리

## 주의사항

- Play Mode 전환 시 도메인 리로드가 발생하여 MCP 연결이 일시 끊김 → 자동 재연결됨
- `unity_get_console_logs`의 기본 반환 수는 50개, StackTrace는 기본 미포함
