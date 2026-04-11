# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

UnityMcp는 AI 에이전트(Claude, Cursor, VS Code 등)가 MCP(Model Context Protocol)를 통해 Unity Editor를 제어할 수 있게 하는 브릿지 시스템이다. 두 개의 독립 컴포넌트로 구성된다:

- **UnityMcpBridge** (.NET 9 콘솔 앱) — MCP stdio 서버. AI 에이전트와 stdio로 통신하고, Unity Editor와 TCP로 통신한다.
- **UnityMcpEditor** (Unity Editor 플러그인, UPM 패키지) — Unity Editor 내 TCP 서버. 핸들러를 통해 Unity API를 호출한다.

## Architecture

```
AI Agent ←(stdio/MCP JSON-RPC)→ UnityMcpBridge ←(TCP localhost:9876, length-prefixed JSON)→ UnityMcpEditor → Unity API
```

- TCP 프로토콜: 4바이트 big-endian length prefix + UTF-8 JSON
- 요청: `{ id, tool, params }` / 응답: `{ id, success, data/error }`
- Bridge 측 도구는 `[McpServerToolType]` + `[McpServerTool]` 어트리뷰트로 자동 등록 (`WithToolsFromAssembly()`)
- Editor 측 핸들러는 `McpServerBootstrap.StartServer()`에서 수동 등록
- `MainThreadDispatcher`는 `EditorApplication.update` 콜백 큐를 사용하여 메인 스레드 실행 보장
- 포트 자동 탐색: 9876~9885 범위에서 사용 가능한 포트 선택 (환경변수 `UNITY_TCP_PORT`로 커스텀 가능)

## Plugin Structure

이 저장소는 Claude Code 플러그인으로도 동작한다 (`.claude-plugin/plugin.json` v0.3.0).

- **agents/** — 전문 에이전트 3종 (scene-architect, debugger, asset-manager). 각 에이전트는 특정 skills와 `mcp__unity-bridge__*` 도구를 번들한다.
- **hooks/hooks.json** — SessionStart/PreToolUse/PostToolUse/PostToolUseFailure 훅. `scripts/check-unity.js`를 호출해 Unity 컴파일/도메인 리로드 상태를 감지하고, 진행 중이면 대기 루프로 완료를 기다린다.
- **scripts/** — 훅 스크립트와 브릿지 실행 래퍼 (`run-bridge.js`는 `${CLAUDE_PLUGIN_DATA}/bin/`의 번들 바이너리 → GitHub Release lazy download → `npx -y unity-mcp-bridge` 순으로 fallback).
- **skills/** — 워크플로우 가이드 6종 (유지).

플러그인 매니페스트의 `userConfig`로 `unity_tcp_port`, `auto_save_scene`, `check_compile_status`, `check_domain_reload`, `max_wait_seconds`를 사용자가 설치 시 설정한다. 이 값들은 `${userConfig.xxx}` 치환으로 `mcpServers.env`와 `hooks` 커맨드 인자에 전달된다.

### 새 hook 스크립트 추가 시
- TCP 통신은 `scripts/unity-client.js`의 `tcpPing`/`sendRequest`/`getEditorState`를 재사용
- Bridge가 기동 전/중단 상태일 수 있으므로 Editor TCP 서버(9876)와 직접 통신한다 (Bridge 경유 X)
- 에러는 stderr에 `[Unity MCP] ...` 형식으로 출력, exit code는 차단 목적(1) vs 정보성(0) 구분

## Build & Run

```bash
# Bridge 빌드
dotnet build UnityMcpBridge/UnityMcpBridge.csproj

# Bridge 실행
dotnet run --project UnityMcpBridge/UnityMcpBridge.csproj

# Release 퍼블리시 (플랫폼별)
dotnet publish UnityMcpBridge/UnityMcpBridge.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
# RIDs: win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64
```

Editor 플러그인은 별도 빌드 불필요 — Unity Package Manager로 설치되어 Unity Editor에서 직접 로드된다.

## Testing

현재 테스트 프로젝트 없음.

## Key Patterns

### Bridge 도구 추가 (UnityMcpBridge/Tools/)
```csharp
[McpServerToolType]
public static class NewTool
{
    [McpServerTool(Name = "unity_tool_name"), Description("설명")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("파라미터 설명")] Type param,
        CancellationToken ct = default)
    {
        var result = await connection.SendRequestAsync("handler_name", paramsJson, ct);
        // 결과 파싱 및 반환
    }
}
```

### Editor 핸들러 추가 (UnityMcpEditor/Editor/Handlers/)
- `IRequestHandler` (동기) 또는 `IAsyncRequestHandler` (비동기) 구현
- `McpServerBootstrap.StartServer()`에 핸들러 등록 필요
- 유틸리티: `GameObjectResolver` (path/instanceId→GameObject), `ComponentResolver` (문자열→Component Type, 네임스페이스 퍼지 검색), `PropertySetter` (리플렉션 기반 프로퍼티 설정), `AssetResolver` (path/GUID→Object)
- 모든 씬 변경은 `UndoHelper`를 통해 Unity Undo 시스템 사용
- 변경 후 `EditorUtility.SetDirty()` 호출 필수

### Safety Constraints
- Transform 컴포넌트 삭제 차단
- 순환 부모-자식 관계 방지 (IsChildOf 체크)
- Addressable 도구는 Undo 미지원 (API 제한)

## CI/CD

`.github/workflows/publish.yml` — `v*` 태그 push 시:
1. 6개 플랫폼 바이너리 빌드
2. GitHub Release 생성 + 바이너리 첨부
3. NuGet 퍼블리시 (`dev.breadpack.UnityMcpBridge`)
4. npm 퍼블리시 (`unity-mcp-bridge`)

시크릿 필요: `NUGET_API_KEY`, `NPM_TOKEN`

## Dependencies

- **Bridge**: `ModelContextProtocol` v0.2.*, `Microsoft.Extensions.Hosting` v9.* (.NET 9.0)
- **Editor**: `com.unity.nuget.newtonsoft-json` v3.2.1 (Unity 6000.0+)
- **npm**: Node >= 16.0.0 (postinstall에서 GitHub Release 바이너리 다운로드)
