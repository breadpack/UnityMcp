# UnityMCP Setup Guide

## 요구사항

- .NET 9.0 SDK
- Unity 6000.0.27f1 이상
- UniTask (Cysharp.Threading.Tasks)
- Newtonsoft.Json (Unity Package)

## 구성 요소

UnityMCP는 두 개의 컴포넌트로 구성된다:

1. **UnityMcpBridge** — .NET 9 콘솔 앱 (MCP stdio ↔ TCP 변환)
2. **Unity Editor Plugin** — Unity Editor 내 TCP 서버

## 설치 순서

### 1. Unity Editor Plugin 설치

다음 파일들을 Unity 프로젝트의 `Assets/Scripts/Editor/Mcp/` 디렉토리에 복사:

```
Editor/Mcp/
├── McpServerBootstrap.cs
├── McpEditorPlugin.cs
├── McpTcpServer.cs
├── McpRequestDispatcher.cs
├── Models/
│   ├── McpRequest.cs
│   └── McpResponse.cs
├── Handlers/
│   ├── IRequestHandler.cs
│   ├── IAsyncRequestHandler.cs
│   └── (21개 핸들러 파일)
└── Utilities/
    ├── ConsoleLogBuffer.cs
    ├── GameObjectResolver.cs
    ├── ComponentResolver.cs
    ├── PropertySetter.cs
    ├── AssetResolver.cs
    ├── UndoHelper.cs
    ├── ViewModelReflector.cs
    └── VisualElementSerializer.cs
```

`McpServerBootstrap.cs`의 `[InitializeOnLoad]` 어트리뷰트에 의해 Unity Editor가 로드되면 TCP 서버가 자동 시작된다.

### 2. UnityMcpBridge 빌드

```bash
cd UnityMcpBridge
dotnet build
```

### 3. Claude Code MCP 설정

프로젝트 루트에 `.mcp.json` 파일 생성:

```json
{
  "mcpServers": {
    "unity": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/UnityMcpBridge/UnityMcpBridge.csproj"],
      "env": { "UNITY_TCP_PORT": "9876" }
    }
  }
}
```

> `UNITY_TCP_PORT` 환경변수로 TCP 포트를 변경할 수 있다 (기본값: 9876).

### 4. Skill 배포 (선택)

`Documents/Skill-Template.md`의 내용을 프로젝트의 `.claude/skills/unity-mcp-guide/SKILL.md`에 복사하면 Claude Code가 MCP 도구 사용 가이드를 자동으로 참조할 수 있다.

## 서버 상태 확인

### Unity Editor에서

- **메뉴**: Tools > MCP Server
- EditorWindow에서 서버 상태, 포트, 클라이언트 연결 확인 가능
- Start/Stop/Restart 버튼 제공

### Claude Code에서

```
unity_ping 도구를 호출하여 연결 확인
```

## 포트 설정

- 기본 포트: 9876
- 포트 충돌 시 자동으로 9876~9885 범위에서 사용 가능한 포트 할당
- Unity 측과 Bridge 측 포트가 일치해야 함
- 포트 불일치 시 `UNITY_TCP_PORT` 환경변수를 Unity에서 실제 사용 중인 포트로 변경

## 트러블슈팅

| 문제 | 원인 | 해결 |
|------|------|------|
| 연결 실패 | Unity Editor가 실행되지 않음 | Unity Editor 실행 후 재시도 |
| 연결 실패 | 포트 불일치 | Unity MCP Server 창에서 실제 포트 확인 |
| Play Mode 전환 후 연결 끊김 | 도메인 리로드로 서버 재시작 | 자동 재연결 대기 (수 초) |
| 컴파일 중 작업 실패 | Unity가 컴파일 중 | 컴파일 완료 대기 |
| render_uxml 잘못된 캡처 | 에디터 창이 가려짐 | 에디터 창을 최상단으로 |
