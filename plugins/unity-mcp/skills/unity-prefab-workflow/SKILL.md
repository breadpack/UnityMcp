---
name: unity-prefab-workflow
description: Unity Prefab 워크플로우 - 인스턴스화, 편집, 저장
---

# Unity Prefab Workflow

Prefab 관련 작업을 수행할 때 사용합니다.

## Prefab 인스턴스화

1. `unity_instantiate_prefab`으로 Prefab을 씬에 배치합니다
   - `assetPath` 또는 `assetGuid`로 Prefab 지정
   - `parentPath`로 부모 오브젝트 지정 가능
2. 반환된 `instanceId`로 후속 작업 (Transform, Property 등)

## Prefab 편집 모드

1. **진입**: `unity_prefab_edit`(action="enter", assetPath="Assets/Prefabs/MyPrefab.prefab")
2. **편집**: 일반 씬 편집 도구 사용 (create, add_component, set_property 등)
3. **저장**: `unity_prefab_edit`(action="save")
4. **종료**: `unity_prefab_edit`(action="exit")

## Prefab 구조 확인

- `unity_get_asset_hierarchy`로 Prefab 내부 구조를 파일을 열지 않고 조회할 수 있습니다

## 주의사항

- Prefab 편집 모드 중에는 메인 씬이 아닌 Prefab 스테이지에서 작업합니다
- 저장하지 않고 exit하면 변경사항이 사라집니다
- 편집 모드에서 `unity_get_hierarchy`를 호출하면 Prefab 내부 구조가 반환됩니다
