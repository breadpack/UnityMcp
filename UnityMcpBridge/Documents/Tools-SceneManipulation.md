# Scene Manipulation Tools Reference

Unity Scene을 편집하는 씬 조작 도구 11개. 모두 **Edit Mode 전용**이며 **Undo 지원** (Addressable 제외).

---

## Phase 1: Core GameObject/Component (7개)

### unity_create_gameobject

GameObject 생성.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `name` | string | X | "GameObject" | 오브젝트 이름 |
| `parentPath` | string | X | null | 부모 계층 경로 (예: `"Canvas/Panel"`) |
| `parentId` | int | X | null | 부모 instanceID |

**반환**:
```json
{
  "name": "GameObject",
  "path": "Canvas/Panel/GameObject",
  "instanceId": 12345
}
```

---

### unity_delete_gameobject

GameObject 삭제.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `path` | string | X | - | 대상 계층 경로 |
| `instanceId` | int | X | - | 대상 instanceID |
| `includeChildren` | bool | X | true | false면 자식을 부모로 이동 후 삭제 |

**반환**:
```json
{ "deletedCount": 1 }
```

---

### unity_set_transform

Transform Position/Rotation/Scale 설정.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `path` | string | X | - | 대상 계층 경로 |
| `instanceId` | int | X | - | 대상 instanceID |
| `position` | string | X | - | JSON: `{"x", "y", "z"}` |
| `rotation` | string | X | - | JSON: `{"x", "y", "z"}` (Euler도) |
| `scale` | string | X | - | JSON: `{"x", "y", "z"}` |
| `space` | string | X | "local" | `"local"` 또는 `"world"` |

**반환**: 변경된 값

---

### unity_reparent_gameobject

GameObject 부모 변경.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `path` | string | X | - | 대상 계층 경로 |
| `instanceId` | int | X | - | 대상 instanceID |
| `newParentPath` | string | X | null | 새 부모 경로 (null이면 루트) |
| `newParentId` | int | X | null | 새 부모 instanceID |
| `worldPositionStays` | bool | X | true | 월드 좌표 유지 여부 |

**반환**:
```json
{ "newPath": "NewParent/GameObject" }
```

**안전성**: 순환 참조 방지 (`IsChildOf` 검사)

---

### unity_add_component

Component 추가. 추가된 컴포넌트의 프로퍼티/필드 목록도 반환한다.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `path` | string | X | - | 대상 계층 경로 |
| `instanceId` | int | X | - | 대상 instanceID |
| `componentType` | string | O | - | 타입명 (예: `"BoxCollider"`, `"TF.UI.Toolkit.SharedMenuController"`) |

**반환**:
```json
{
  "gameObject": "GameObject",
  "path": "Canvas/Panel/GameObject",
  "componentType": "BoxCollider",
  "fullTypeName": "UnityEngine.BoxCollider",
  "fields": [
    {"name": "size", "type": "Vector3"},
    {"name": "center", "type": "Vector3"}
  ],
  "properties": [
    {"name": "bounds", "type": "Bounds"},
    {"name": "enabled", "type": "Boolean"}
  ]
}
```

**Component 해석 순서**:
1. Fully Qualified Name: `"UnityEngine.UI.Button"`
2. 공통 네임스페이스 탐색: `UnityEngine`, `UnityEngine.UI`, `TMPro` 등
3. 전체 어셈블리 스캔 (폴백)

---

### unity_remove_component

Component 제거.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `path` | string | X | - | 대상 계층 경로 |
| `instanceId` | int | X | - | 대상 instanceID |
| `componentType` | string | O | - | 제거할 타입명 |
| `index` | int | X | 0 | 동일 타입이 여러 개일 때 인덱스 |

**반환**:
```json
{ "success": true }
```

**안전성**: Transform 컴포넌트 삭제 차단

---

### unity_set_property

Component 필드/프로퍼티를 Reflection으로 설정.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `path` | string | X | - | 대상 계층 경로 |
| `instanceId` | int | X | - | 대상 instanceID |
| `componentType` | string | O | - | 대상 컴포넌트 타입명 |
| `properties` | string | O | - | 설정할 프로퍼티 (JSON) |
| `index` | int | X | 0 | 동일 타입이 여러 개일 때 인덱스 |

**properties 예시**:
```json
{
  "size": {"x": 1, "y": 2, "z": 3},
  "center": {"x": 0, "y": 0, "z": 0},
  "enabled": true,
  "layer": 5,
  "material": {"$asset": "Assets/Materials/Mat_Red.mat"},
  "colors": [
    {"r": 1, "g": 0, "b": 0, "a": 1}
  ]
}
```

**반환**:
```json
{
  "size": "ok",
  "center": "ok",
  "enabled": "ok"
}
```

**Dot-Notation 지원**: `"boxCollider.center": {"x": 0, "y": 1, "z": 2}`

**자동 타입 변환 표**:

| JSON | C# 타입 |
|------|---------|
| 정수 | int, float, double, long |
| 실수 | float, double |
| 문자열 | string, Enum |
| boolean | bool |
| `{"x", "y"}` | Vector2 |
| `{"x", "y", "z"}` | Vector3 |
| `{"x", "y", "z", "w"}` | Vector4, Quaternion |
| `{"r", "g", "b", "a"}` | Color |
| `{"x", "y", "width", "height"}` | Rect |
| 배열 | T[], List\<T\> |
| `{"$asset": "path"}` | UnityEngine.Object 서브클래스 |
| `{"$guid": "guid"}` | UnityEngine.Object 서브클래스 |

---

## Phase 2: Asset/Prefab (2개)

### unity_instantiate_prefab

Prefab 인스턴스 생성.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `assetPath` | string | X | - | Prefab 경로 (예: `"Assets/Prefabs/Enemy.prefab"`) |
| `assetGuid` | string | X | - | Prefab GUID (assetPath 대체) |
| `parentPath` | string | X | null | 부모 경로 |
| `parentId` | int | X | null | 부모 instanceID |
| `name` | string | X | null | 미지정 시 Prefab 이름 사용 |

**반환**:
```json
{
  "path": "Canvas/Enemy",
  "instanceId": 12345
}
```

---

### unity_set_asset_reference

Component 필드에 Asset 참조 할당.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `path` | string | X | - | 대상 계층 경로 |
| `instanceId` | int | X | - | 대상 instanceID |
| `componentType` | string | O | - | 대상 컴포넌트 타입명 |
| `propertyName` | string | O | - | 설정할 프로퍼티명 |
| `assetPath` | string | X | - | Asset 경로 |
| `assetGuid` | string | X | - | Asset GUID (assetPath 대체) |
| `index` | int | X | 0 | 동일 타입이 여러 개일 때 인덱스 |

**반환**: 설정된 Asset 경로

---

## Phase 3: Addressable (2개)

> Addressable 도구는 **Undo 미지원** (Unity Addressables API 제약).

### unity_addressable_add

Asset을 Addressable 그룹에 등록.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `assetPath` | string | X | - | Asset 경로 |
| `assetGuid` | string | X | - | Asset GUID (assetPath 대체) |
| `groupName` | string | X | null | 그룹명 (미지정 시 Default Group) |
| `address` | string | X | null | 주소 (미지정 시 Asset 경로 사용) |

**반환**: 등록 결과

---

### unity_addressable_set_address

Addressable 주소/레이블 설정.

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `address` | string | O | - | Addressable 주소 |
| `assetPath` | string | X | - | Asset 경로 |
| `assetGuid` | string | X | - | Asset GUID (assetPath 대체) |
| `labels` | string | X | null | JSON 배열: `["label1", "label2"]` |

**반환**: 설정된 주소/레이블

---

## GameObject 식별 규칙

모든 씬 조작 도구에서 대상 GameObject를 지정할 때:

- `path`: 계층 경로 (`"Canvas/Panel/Button"`) — 루트부터 `/` 구분자
- `instanceId`: Unity InstanceID (정수)
- **우선순위**: instanceId > path (둘 다 있으면 instanceId 사용)
- path 또는 instanceId 중 하나는 필수
