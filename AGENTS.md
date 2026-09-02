# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Project Overview

UnityMcp는 AI 에이전트(Codex, Cursor, VS Code 등)가 MCP(Model Context Protocol)를 통해 Unity Editor를 제어할 수 있게 하는 브릿지 시스템이다. 두 개의 독립 컴포넌트로 구성된다:

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
- 포트 관리: Editor 측은 9876~9885 범위에서 인스턴스별 배타 포트를 점유하고, Bridge 측은 workspace(projectPath) 기준으로 해당 인스턴스의 포트를 자동 탐색·매칭한다. 컴파일/리로드로 포트가 바뀌어도 재연결·주기적 핸드셰이크로 따라간다. `UNITY_TCP_PORT` 환경변수를 지정하면 고정 포트로 자동 탐색을 우회한다.

## Plugin Structure

이 저장소는 Codex 플러그인으로도 동작한다 (`.Codex-plugin/plugin.json` v0.3.0).

- **agents/** — 전문 에이전트 3종 (scene-architect, debugger, asset-manager). 각 에이전트는 특정 skills와 `mcp__unity-bridge__*` 도구를 번들한다.
- **hooks/hooks.json** — SessionStart/PreToolUse/PostToolUse/PostToolUseFailure 훅. `scripts/check-unity.js`를 호출해 Unity 컴파일/도메인 리로드 상태를 감지하고, 진행 중이면 대기 루프로 완료를 기다린다.
- **scripts/** — 훅 스크립트와 브릿지 실행 래퍼 (`run-bridge.js`는 `${CLAUDE_PLUGIN_DATA}/bin/`의 번들 바이너리 → GitHub Release lazy download → `npx -y unity-mcp-bridge` 순으로 fallback).
- **skills/** — 워크플로우 가이드 9종. `unity-cli-workflow`가 CLI ↔ MCP 역할 분담의 기준 문서이고, 나머지 skill은 각자 "Unity CLI로 할 때" 절에서 이를 참조한다.

플러그인 매니페스트의 `userConfig`로 `auto_save_scene`, `check_compile_status`, `check_domain_reload`, `auto_tick`을 사용자가 설치 시 설정한다. 이 값들은 `${userConfig.xxx}` 치환으로 `hooks` 커맨드 인자에 전달된다. 포트·대기 시간은 userConfig가 아니라 환경변수(`UNITY_TCP_PORT`, `UNITY_MAX_WAIT_SEC`)로만 조정한다 — 포트는 기본이 workspace 자동 탐색이므로 설치 옵션에서 제외했다.

### Unity CLI / Pipeline 연동 (2026-09 이후)

Unity 공식 CLI(`unity`)와 `com.unity.pipeline` 패키지가 Editor 제어의 전송 계층을 제공한다. 설계 문서는 `docs/superpowers/specs/2026-09-02-unity-cli-integration-design.md`.

- 훅은 `Library/Pipeline/.unity-pipeline-port` 디스크립터를 찾으면 Pipeline HTTP(`/api/status`, `editor_status`)를 우선 사용하고, 없거나 도달 불가면 UnityMcp TCP로 폴백한다 (`getUnifiedEditorState`).
- 역할 분담 원칙: **Pipeline 내장 명령이 있는 작업(씬·에셋·설정·빌드·테스트·Play Mode 제어)은 `unity command`로, Pipeline에 없는 작업(Play Mode 입력, UI 트리, `prefab_apply`, 오프스크린 렌더, Addressable, Undo, 런타임 Animator)은 MCP 도구로.** 새 도구를 추가하기 전에 Pipeline 0.5 내장 명령 153개(설계 문서 §1.4)에 이미 있는지 확인한다.
- **Phase 2 (완료)**: 고유 도구 27종이 `UnityMcpEditor/Editor/Pipeline/`의 `[CliCommand]` 어댑터로 이중 노출된다. 어댑터는 `scripts/gen-pipeline-commands.js`가 `UnityMcpBridge/Tools`의 `[McpServerTool]` 정의에서 **생성**한다(`Generated/Commands_*.cs`, 직접 수정 금지). Bridge 도구 시그니처를 바꾸면 생성기를 다시 실행하고, `--check`로 최신 여부를 검증한다. 어댑터는 `PipelineBridge.Invoke` → `McpServerBootstrap.DispatchAsync`로 기존 핸들러에 위임하므로 핸들러 본체는 한 벌이다.
- 어댑터 asmdef(`BreadPack.Mcp.Unity.Pipeline`)는 `versionDefines`로 `com.unity.pipeline` 존재 시에만 `UNITY_PIPELINE_PRESENT`를 정의하고, 모든 소스가 이 심볼로 감싸져 있어 Pipeline 미설치 프로젝트에서는 빈 어셈블리가 된다(소프트 의존).
- 다음 단계(Phase 3)는 Bridge·TCP 제거이며 minor 버전에서 한다.

### 새 hook 스크립트 추가 시
- Editor 상태는 `scripts/unity-client.js`의 `getUnifiedEditorState(workspaceDir, { port })`를 쓴다 (Pipeline 우선, TCP 폴백). Pipeline 명령 실행은 `pipelineExec(desc, name, params)`, TCP 는 `sendRequest`
- CLI 프로세스(`unity …`)를 훅에서 스폰하지 않는다 — 호출당 약 0.8초라 폴링에 부적합하다. HTTP 직접 호출을 쓴다
- Bridge가 기동 전/중단 상태일 수 있으므로 Editor 서버와 직접 통신한다 (Bridge 경유 X)
- 에러는 stderr에 `[Unity MCP] ...` 형식으로 출력, exit code는 차단 목적(1) vs 정보성(0) 구분. 에이전트에게 줄 정보는 stdout에 `[Unity ...]` 헤더 블록으로 출력

## Versioning Policy

`plugins/unity-mcp/.Codex-plugin/plugin.json`의 `version` 필드는 **patch 단위 증가가 기본**이다 (예: 0.6.0 → 0.6.1 → 0.6.2).

- **Patch (기본)**: 신규 기능 추가, 핸들러/도구 추가, 버그 수정, 리팩토링 — 대부분의 머지가 여기 해당
- **Minor**: 외부 통합 방식이 바뀌는 큰 변화 (MCP 프로토콜 시그니처 변경, asmdef 구조 재편 등 사용자 설정·다른 도구가 영향받는 경우)
- **Major**: 0.x → 1.0 안정화, 호환되지 않는 마이그레이션이 필요한 변경

**중요 — 모든 버전 파일을 항상 함께 올린다.** 버전을 올릴 때 `UnityMcpEditor/package.json`, Claude/Codex `plugin.json`, 루트 `.claude-plugin/marketplace.json`의 `plugins[].version`을 **동일한 값으로 동시에** 수정해야 한다. 플러그인 업데이트는 `marketplace.json`의 `version`을 기준으로 갱신을 판단하므로(이 값은 main 브랜치 HEAD에서 읽는다), 일부 파일만 올리면 UPM 패키지와 사용자에게 노출되는 플러그인 버전이 어긋난다. 릴리스 태그가 아니라 main 에 버전 커밋이 push 되어야 반영된다.

**버전 변경은 반드시 `scripts/bump-version.js` 로 한다 — 버전 파일을 손으로 따로 고치지 않는다.**

```bash
node scripts/bump-version.js patch        # 또는 minor | major | <x.y.z>
git add -A && git commit -m "chore(release): vX.Y.Z"
git tag vX.Y.Z && git push origin main && git push origin vX.Y.Z
```

`publish.yml` 의 `verify-version` job 이 배포 시작 전에 **태그 · UPM package.json · Claude/Codex plugin.json · marketplace.json 버전의 일치**를 검증한다(`node scripts/bump-version.js --verify <버전>`). 하나라도 어긋나면 빌드·NuGet·npm·Release 가 모두 중단되므로, 일부 버전 파일이 누락된 배포가 구조적으로 차단된다.

근거: 8개 입력 도구를 3 phase로 머지하면서 매번 minor를 올린 결과 0.3.1 → 0.6.0이 되어 변화 폭에 비해 버전이 빠르게 소진되었다. 0.x 단계에서는 SemVer 엄격 해석보다 변화 무게에 비례하는 패치 정책이 적합하다.

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
