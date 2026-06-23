---
name: unity-prefab-workflow
description: Unity Prefab 워크플로우 - 인스턴스화, 편집, 저장
---

# Unity Prefab Workflow

Prefab 관련 작업을 수행할 때 사용합니다.

Prefab 편집에는 두 가지 경로가 있습니다. **대부분의 경우 `unity_prefab_apply`(원자적 단일 호출)를 우선 사용**하고, 단계적·탐색적 편집이 필요할 때만 편집 스테이지(enter/save/exit)를 사용합니다.

## 권장: 원자적 편집 (`unity_prefab_apply`)

스테이지 진입 없이 단일 호출로 prefab을 편집·저장합니다. instanceId 왕복이 필요 없고, 중간 도메인 리로드나 부분 적용/유실이 구조적으로 발생하지 않습니다.

1. (선택) `unity_get_asset_hierarchy`로 prefab 내부 구조와 경로를 미리 확인
2. `unity_prefab_apply`(assetPath, edits=[...]) 호출
   - 각 edit: `{op, target, ...}`
   - `target`은 **prefab 루트 기준 상대 경로** (`""` 또는 생략 시 루트, `"루트이름/자식"` 또는 `"자식/손자"` 모두 허용)
   - op: `set_property`, `add_component`(properties 인라인 가능), `remove_component`, `set_transform`, `set_active`, `create_child`, `reparent`, `set_asset_reference`, `delete`
3. 반환의 `hierarchy`로 최종 구조 확인

예시:
```json
{
  "assetPath": "Assets/Prefabs/Enemy.prefab",
  "edits": [
    {"op": "set_property", "target": "Body", "componentType": "SpriteRenderer",
     "properties": {"color": {"r": 1, "g": 0, "b": 0, "a": 1}}},
    {"op": "add_component", "target": "", "componentType": "BoxCollider2D"}
  ]
}
```

> `unity_prefab_apply`는 Undo를 지원하지 않습니다(임시 prefab 콘텐츠 편집 API 제약). 되돌릴 필요가 있는 인터랙티브 작업은 편집 스테이지 경로를 사용하세요.

## 대안: 편집 스테이지 (탐색적·단계적 편집)

1. **진입**: `unity_prefab_edit`(action="enter", assetPath="Assets/Prefabs/MyPrefab.prefab")
   - 반환에 `rootInstanceId`/`rootName`/`rootPath`가 포함됩니다 — 이후 작업의 안정적 핸들로 사용
2. **구조 확인**: `unity_get_hierarchy` 호출 → prefab 내부 구조가 반환됩니다(`isPrefabStage=true`, 각 노드에 `instanceId`/`path` 포함)
3. **편집**: 일반 씬 편집 도구 사용 (`unity_create_gameobject`, `unity_add_component`, `unity_set_property`, `unity_set_transform` 등)
   - 편집 모드 중에는 path/instanceId가 **prefab 스테이지 기준**으로 해석됩니다
   - 안정성을 위해 2에서 얻은 `instanceId` 사용을 권장
4. **저장**: `unity_prefab_edit`(action="save")
5. **종료**: `unity_prefab_edit`(action="exit")

상태 확인: 언제든 `unity_prefab_edit`(action="status")로 현재 편집 모드 여부·assetPath·rootInstanceId를 조회할 수 있습니다. `unity_get_editor_state`/`unity_ping`도 `inPrefabStage`/`prefabStagePath`를 보고합니다.

## Prefab 인스턴스화

1. `unity_instantiate_prefab`으로 Prefab을 씬에 배치합니다
   - `assetPath` 또는 `assetGuid`로 Prefab 지정
   - `parentPath`로 부모 오브젝트 지정 가능
2. 반환된 `instanceId`로 후속 작업 (Transform, Property 등)

## Prefab 구조 확인

- `unity_get_asset_hierarchy`로 Prefab 내부 구조를 편집 모드 진입 없이 디스크에서 조회할 수 있습니다

## 주의사항

- 편집 스테이지 경로에서는 **저장(save)하지 않고 exit하면 변경사항이 사라집니다**
- 편집 스테이지 중 스크립트 재컴파일/도메인 리로드가 일어나면 스테이지가 닫힐 수 있습니다 — `status`로 모드를 재확인하고 필요 시 다시 enter 하세요
- 편집 모드에서 부모 없이 `unity_create_gameobject`를 호출하면 새 오브젝트가 **prefab 루트 아래**에 생성됩니다(저장에 포함되도록)
- 같은 prefab이 편집 스테이지로 열려 있는 동안에는 `unity_prefab_apply`를 사용할 수 없습니다(충돌) — exit 후 사용하거나 스테이지 도구로 편집하세요
- `execute_code`로 prefab을 직접 편집하는 것은 위 도구로 표현되지 않는 예외적 작업에만 사용하세요
