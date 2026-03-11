# UnityMCP Architecture

## 개요

UnityMCP는 Claude Code가 Unity Editor를 프로그래매틱하게 제어할 수 있도록 하는 MCP 서버 시스템이다.
Bridge 아키텍처를 통해 MCP stdio 프로토콜과 Unity Editor TCP 통신을 연결한다.

## 통신 구조

```
Claude Code (stdin/stdout)
    | MCP JSON-RPC Protocol (stdio)
    v
UnityMcpBridge.exe (.NET 9 Console App)
    | TCP localhost:9876 (Length-Prefixed JSON)
    v
Unity Editor Plugin (TCP Server + EditorWindow)
    | UniTask MainThread 전환
    v
Unity API (Scene, Asset, Play Mode)
```

## 컴포넌트

### 1. UnityMcpBridge (.NET 9 Console App)

MCP 표준 stdio 프로토콜을 구현하고 Unity Editor와 TCP로 통신하는 브릿지 애플리케이션.

**위치**: `CODE/UnityMcpBridge/`

**기술 스택**:
- .NET 9.0
- `ModelContextProtocol` v0.2.* (NuGet) — MCP 서버 프레임워크
- `Microsoft.Extensions.Hosting` v9.* — 호스팅 인프라
- `System.Net.Sockets` — TCP 클라이언트

**핵심 파일**:
| 파일 | 역할 |
|------|------|
| `Program.cs` | MCP 서버 초기화, Ping 도구 정의 |
| `UnityConnection.cs` | Unity TCP 클라이언트 (자동 재연결, Length-Prefixed 프로토콜) |
| `Tools/*.cs` | 개별 MCP 도구 구현 (20개) |

**도구 등록 방식**: `[McpServerToolType]` + `[McpServerTool]` 어트리뷰트를 통해 어셈블리 스캔으로 자동 등록.

### 2. Unity Editor Plugin (C#)

Unity Editor 내에서 실행되는 TCP 서버. 요청을 수신하여 Unity API를 호출하고 결과를 반환한다.

**위치**: `CODE/Client/Assets/Scripts/Editor/Mcp/`

**기술 스택**:
- Unity 6000.0.27f1 Editor API
- Cysharp.Threading.Tasks (UniTask) — 메인 스레드 전환
- Newtonsoft.Json — JSON 직렬화

**핵심 파일**:
| 파일 | 역할 |
|------|------|
| `McpServerBootstrap.cs` | `[InitializeOnLoad]` 서버 자동 시작, 핸들러 등록 |
| `McpEditorPlugin.cs` | EditorWindow (Tools > MCP Server 메뉴) |
| `McpTcpServer.cs` | TCP 리스너 (비동기, Length-Prefixed) |
| `McpRequestDispatcher.cs` | 도구명 → 핸들러 매핑 |
| `Models/McpRequest.cs` | 요청 모델 `{ id, tool, params }` |
| `Models/McpResponse.cs` | 응답 모델 `{ id, success, data/error }` |
| `Handlers/*.cs` | 개별 핸들러 구현 (21개) |
| `Utilities/*.cs` | 공용 유틸리티 |

**유틸리티 클래스**:
| 클래스 | 역할 |
|--------|------|
| `GameObjectResolver` | path/instanceId → GameObject 해석 |
| `ComponentResolver` | 문자열 → Type (네임스페이스 자동 탐색) |
| `PropertySetter` | Reflection 기반 프로퍼티 설정, 자동 타입 변환 |
| `AssetResolver` | assetPath/assetGuid → UnityEngine.Object |
| `UndoHelper` | Unity Undo API 래핑 |
| `ConsoleLogBuffer` | Application.logMessageReceived 버퍼 (최대 200개) |
| `ViewModelReflector` | ViewModel 프로퍼티 리플렉션 |
| `VisualElementSerializer` | VisualElement 트리 직렬화 |

## 프로토콜

### TCP Length-Prefixed JSON

```
[4 bytes: big-endian uint32 payload length][UTF-8 JSON payload]
```

### 요청 형식

```json
{
  "id": "uuid",
  "tool": "tool_name",
  "params": { }
}
```

### 응답 형식

```json
{
  "id": "uuid",
  "success": true,
  "data": { },
  "error": "error message (success=false일 때)"
}
```

## 핵심 메커니즘

### 메인 스레드 전환

Unity API는 메인 스레드에서만 호출 가능하다. TCP 요청은 별도 스레드에서 수신되므로 UniTask를 사용하여 메인 스레드로 전환한다.

```csharp
await UniTask.SwitchToMainThread();
// 이후 Unity API 안전하게 호출
```

### Undo 지원

모든 씬 조작 도구는 Unity Undo 시스템과 통합되어 있다.

| 작업 | Undo API |
|------|----------|
| 생성 | `Undo.RegisterCreatedObjectUndo()` |
| 수정 | `Undo.RecordObject()` |
| 삭제 | `Undo.DestroyObjectImmediate()` |
| 부모 변경 | `Undo.SetTransformParent()` |
| 컴포넌트 추가 | `Undo.AddComponent()` |

### 자동 포트 할당

포트 충돌 시 9876~9885 범위에서 자동으로 사용 가능한 포트를 찾는다.

### Property 타입 자동 변환

`PropertySetter`가 JSON 값을 C# 타입으로 자동 변환한다.

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
| `{"$asset": "path"}` | UnityEngine.Object (Material, Texture 등) |
| `{"$guid": "guid"}` | UnityEngine.Object (GUID 기반) |

**Dot-Notation**: 중첩 struct 필드 접근 — `"boxCollider.center": {"x": 0, "y": 1, "z": 2}`

### Component 해석 순서

1. Fully Qualified Name: `"UnityEngine.UI.Button"`
2. 공통 네임스페이스 탐색: `UnityEngine`, `UnityEngine.UI`, `TMPro` 등
3. 전체 어셈블리 스캔 (폴백)

### GameObject 식별

- `path`: 계층 경로 (`"Canvas/Panel/Button"`)
- `instanceId`: Unity InstanceID (정수)
- **우선순위**: instanceId > path (둘 다 있으면 instanceId 사용)

## 안전성 제약

- Transform 컴포넌트 삭제 차단
- 순환 참조 방지 (reparent에서 `IsChildOf` 검사)
- 모든 변경 후 `EditorUtility.SetDirty()` 호출
- Addressable 도구는 Undo 미지원 (Unity Addressables API 제약)
