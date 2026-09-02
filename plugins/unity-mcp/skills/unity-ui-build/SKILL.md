---
name: unity-ui-build
description: Unity UI를 구축합니다 - UGUI(Canvas) 및 UI Toolkit 지원
---

# Unity UI Build

Unity UI를 구축할 때 사용합니다.

## UGUI (Canvas 기반 Legacy UI)

1. **현재 UI 확인**: `unity_get_ugui_tree`로 Canvas 계층구조를 조회합니다
2. **Canvas 생성**: `unity_create_gameobject` + `unity_add_component`(Canvas, CanvasScaler, GraphicRaycaster)
3. **UI 요소 추가**: Button, Text, Image 등 컴포넌트를 추가합니다
4. **RectTransform 설정**: `unity_set_property`로 anchoredPosition, sizeDelta 등을 설정합니다
5. **검증**: `unity_get_ugui_tree`(includeDetails=true)로 레이아웃 확인

## UI Toolkit

1. **UI 확인**: `unity_get_ui_tree`로 VisualElement 트리를 조회합니다 (Play Mode 필요)
2. **UXML 미리보기**: `unity_render_uxml`로 UXML 파일을 렌더링하여 이미지로 확인합니다
3. **화면 상태**: `unity_get_screen`으로 활성 UIDocument 정보를 확인합니다
4. **인터랙션**: `unity_get_available_actions`로 클릭 가능한 요소를 확인합니다

## Play Mode 인터랙션 검증

구축한 UI가 실제로 동작하는지는 `unity-playmode-input` skill의 `unity_input_click`/`unity_input_drag`/`unity_input_type_text`로 확인한다. UI 트리 조회·입력 시뮬레이션·UXML 렌더는 Unity CLI에 없는 UnityMcp 고유 기능이므로 Pipeline이 연결되어 있어도 MCP 도구를 쓴다.

## Unity CLI로 할 때

GameObject 생성·컴포넌트 추가·RectTransform 값 설정은 Pipeline 명령(`create_gameobject`, `add_component`, `set_component_properties`)으로도 가능하다. UI 요소를 대량으로 만들 때는 `unity command eval`에 C# 블록 하나로 묶는 편이 빠르다. 트리 확인은 여전히 `unity_get_ugui_tree`/`unity_get_ui_tree`.

## RectTransform 프로퍼티 설정 예시

```
unity_set_property:
  componentType: "RectTransform"
  properties: {"anchoredPosition": {"x": 0, "y": 0}, "sizeDelta": {"x": 200, "y": 50}}
```
