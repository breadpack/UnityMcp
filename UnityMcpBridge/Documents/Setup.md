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
      "args": ["run", "--project", "path/to/UnityMcpBridge/UnityMcpBridge.csproj"]
    }
  }
}
```

> 포트는 workspace(projectPath) 기준으로 9876~9885를 자동 탐색하므로 보통 설정이 필요 없다.
> 특정 포트를 강제하려면 `env` 에 `UNITY_TCP_PORT` 를 지정한다(자동 탐색을 우회한다).

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

- Unity Editor 측은 9876~9885 범위에서 사용 가능한 포트를 자동 점유한다 (인스턴스별 배타 점유).
- Bridge 측은 workspace(projectPath)를 기준으로 해당 Unity 인스턴스의 포트를 자동 탐색·매칭하므로, 다중 인스턴스 환경에서도 포트를 수동으로 맞출 필요가 없다.
- 컴파일/리로드로 포트가 바뀌어도 재연결·주기적 핸드셰이크로 따라간다.
- 특정 포트를 강제하려면 `UNITY_TCP_PORT` 환경변수를 지정한다(자동 탐색을 우회한다).

## 트러블슈팅

| 문제 | 원인 | 해결 |
|------|------|------|
| 연결 실패 | Unity Editor가 실행되지 않음 | Unity Editor 실행 후 재시도 |
| 연결 실패 | 포트 불일치 | Unity MCP Server 창에서 실제 포트 확인 |
| Play Mode 전환 후 연결 끊김 | 도메인 리로드로 서버 재시작 | 자동 재연결 대기 (수 초) |
| 컴파일 중 작업 실패 | Unity가 컴파일 중 | 컴파일 완료 대기 |
| render_uxml 잘못된 캡처 | 에디터 창이 가려짐 | 에디터 창을 최상단으로 |
