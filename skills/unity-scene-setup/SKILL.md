---
name: unity-scene-setup
description: Unity 씬을 구성합니다 - GameObject 생성, 컴포넌트 추가, 계층 구조 설정
---

# Unity Scene Setup

Unity 씬을 구성할 때 다음 워크플로우를 따르세요.

## 워크플로우

1. **현재 씬 파악**: `unity_get_hierarchy`로 현재 씬 구조를 확인합니다
2. **GameObject 생성**: `unity_create_gameobject`로 필요한 오브젝트를 생성합니다
3. **계층 구조 설정**: `unity_reparent_gameobject`로 부모-자식 관계를 구성합니다
4. **컴포넌트 추가**: `unity_add_component`로 필요한 컴포넌트를 추가합니다
5. **Transform 설정**: `unity_set_transform`으로 위치/회전/스케일을 설정합니다
6. **프로퍼티 설정**: `unity_set_property`로 컴포넌트 값을 설정합니다
7. **검증**: `unity_get_hierarchy`로 최종 구조를 확인합니다
8. **저장**: `unity_save_scene`으로 씬을 저장합니다

## 주의사항

- GameObject 식별에는 `instanceId`(정확) 또는 `path`(직관적)를 사용합니다
- 생성된 오브젝트의 응답에서 `instanceId`를 받아 후속 작업에 사용하세요
- 모든 씬 조작은 Undo를 지원합니다
- `unity_set_active`로 오브젝트를 활성화/비활성화할 수 있습니다
- 삭제 전 `unity_delete_gameobject`에 `dryRun=true`로 미리보기하세요
