# Unity MCP

[![npm](https://img.shields.io/npm/v/unity-mcp-bridge)](https://www.npmjs.com/package/unity-mcp-bridge)
[![NuGet](https://img.shields.io/nuget/v/dev.breadpack.UnityMcpBridge)](https://www.nuget.org/packages/dev.breadpack.UnityMcpBridge)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Unity MCP (Model Context Protocol) Bridge — Connect AI agents to Unity Editor.

## Architecture

```
AI Agent (Claude, Cursor, etc.)
    ↕ stdio (MCP protocol)
UnityMcpBridge (.NET)
    ↕ TCP (binary protocol)
Unity Editor (UnityMcpEditor package)
```

Two components are required:

| Component | Role | Install method |
|-----------|------|---------------|
| **UnityMcpBridge** | .NET MCP server (stdio ↔ TCP) | `npx` / `dotnet tool` / clone |
| **UnityMcpEditor** | Unity Editor plugin (TCP server + handlers) | UPM git URL |

## Plugin Marketplace 설치 (권장)

Codex 또는 Claude Code에서 marketplace 방식으로 설치하면 MCP 서버, Skills, Hooks가 한 번에 구성됩니다.
Claude Code에서는 추가로 Agents 3종도 함께 사용할 수 있습니다.

### Step 1. Unity Editor 패키지 설치

> Unity 6000.0+ 필요

Unity Editor > Window > Package Manager > **+** > **Add package from git URL**:

```
https://github.com/breadpack/UnityMcp.git?path=UnityMcpEditor
```

### Step 2. Codex에서 설치

이 저장소는 Codex marketplace 파일을 포함합니다:

- Marketplace: `.agents/plugins/marketplace.json`
- Plugin: `plugins/unity-mcp/.codex-plugin/plugin.json`

Codex CLI에서 marketplace를 추가한 뒤 플러그인을 설치합니다:

```bash
codex plugin marketplace add breadpack/UnityMcp
codex plugin add unity-mcp@breadpack-unitymcp
```

로컬 checkout으로 테스트할 때는 repository root를 marketplace root로 추가합니다:

```bash
git clone https://github.com/breadpack/UnityMcp.git
cd UnityMcp
codex plugin marketplace add .
codex plugin add unity-mcp@breadpack-unitymcp
```

설치 후 새 Codex thread를 시작하면 `unity-bridge` MCP 서버와 Unity workflow skills가 로드됩니다.

Codex에서 포트나 대기 시간을 바꾸려면 Codex를 실행하는 셸/환경에 아래 변수를 설정합니다:

| 환경변수 | 설명 | 기본 |
|----------|------|------|
| `UNITY_TCP_PORT` | Unity TCP 포트. 설정하지 않으면 workspace 기준으로 9876~9885 자동 탐색 | 자동 탐색 |
| `UNITY_MAX_WAIT_SEC` | 컴파일/도메인 리로드 대기 최대 시간(초) | `60` |
| `UNITY_WORKSPACE_DIR` | Unity projectPath 매칭에 사용할 workspace 경로 | 현재 작업 디렉터리 |

### Step 3. Claude Code에서 설치

이 저장소는 Claude Code marketplace 파일도 포함합니다:

- Marketplace: `.claude-plugin/marketplace.json`
- Plugin: `plugins/unity-mcp/.claude-plugin/plugin.json`

Claude Code 세션 내에서:

```
/plugin marketplace add breadpack/UnityMcp
/plugin install unity-mcp@breadpack-UnityMcp
```

또는 `/plugin` 메뉴에서 `breadpack-UnityMcp` marketplace를 선택한 뒤 `unity-mcp`를 설치합니다.

설치 시 아래 옵션을 프롬프트로 설정할 수 있습니다:

| 옵션 | 설명 | 기본 |
|------|------|------|
| `auto_save_scene` | 씬 변경 후 자동 저장 | `false` |
| `check_compile_status` | 도구 호출 전 컴파일 상태 체크 | `true` |
| `check_domain_reload` | 도구 호출 전 도메인 리로드 상태 체크 | `true` |
| `auto_tick` | 세션 시작 시 Unity Pipeline `set_autotick` 활성화 (비포커스 상태에서도 컴파일·테스트 진행) | `true` |

> 포트는 workspace(projectPath) 기준으로 9876~9885를 자동 탐색하므로 별도 설정이 필요 없습니다. 고정 포트를 강제하거나 대기 시간을 바꾸려면 환경변수(`UNITY_TCP_PORT`, `UNITY_MAX_WAIT_SEC`)를 사용하세요.

### Unity CLI / Pipeline 연동 (Unity 6.0+)

프로젝트에 Unity 공식 `com.unity.pipeline` 패키지가 설치되어 있으면(`unity pipeline install`) 훅이 Pipeline HTTP 서버(`Library/Pipeline/.unity-pipeline-port`)를 우선 사용하고, 에이전트에게 **CLI로 할 일과 MCP 도구로 할 일**을 안내합니다.

- 씬·에셋·설정·빌드·테스트·Play Mode 제어처럼 Pipeline 내장 명령이 있는 작업은 `unity command <name> --json`으로 처리합니다.
- Play Mode 입력 시뮬레이션, uGUI/UI Toolkit 트리 조회, 선언적 Prefab 편집(`unity_prefab_apply`), UXML/Prefab 오프스크린 렌더, Addressable, Undo 히스토리, 런타임 Animator 제어는 Pipeline에 없으므로 UnityMcp MCP 도구가 담당합니다.
- Pipeline이 없는 프로젝트에서는 기존과 동일하게 UnityMcp TCP 서버(9876~)로 동작합니다.
- **UnityMcp 고유 도구 27종은 `[CliCommand]`로도 노출됩니다.** `com.unity.pipeline`이 설치되어 있으면 `unity command --tag breadpack`에 같은 이름·같은 파라미터로 나타나고, `unity mcp`와 헤드리스 `unity run --command`에서도 사용할 수 있습니다. 사용자가 `[McpTool]`로 만든 커스텀 도구도 `unity command unity_custom_tool`로 호출됩니다. Pipeline이 없으면 이 어댑터는 컴파일에서 제외되므로 프로젝트에 영향이 없습니다.
- 설계 배경과 단계별 이행 계획은 `docs/superpowers/specs/2026-09-02-unity-cli-integration-design.md`를 참고하세요.

### Step 4. 검증

Unity Editor를 연 상태에서 Codex 또는 Claude Code에 다음과 같이 요청:

> "Unity에 ping을 보내줘"

### 포함 구성요소

**MCP 서버 (unity-bridge)** — 45+ Unity 도구 (씬, 컴포넌트, 에셋, 빌드 등)

**Skills 9종** — Unity workflow 가이드
| Skill | Claude Code 명령어 / Codex trigger |
|-------|------------------------------------|
| CLI Workflow (CLI ↔ MCP 역할 분담) | `/unity-mcp:unity-cli-workflow` / `unity-cli-workflow` |
| Play Mode Input | `/unity-mcp:unity-playmode-input` / `unity-playmode-input` |
| Animation | `/unity-mcp:unity-animation` / `unity-animation` |
| Scene Setup | `/unity-mcp:unity-scene-setup` / `unity-scene-setup` |
| UI Build | `/unity-mcp:unity-ui-build` / `unity-ui-build` |
| Material Setup | `/unity-mcp:unity-material-setup` / `unity-material-setup` |
| Prefab Workflow | `/unity-mcp:unity-prefab-workflow` / `unity-prefab-workflow` |
| Debug | `/unity-mcp:unity-debug` / `unity-debug` |
| Build & Deploy | `/unity-mcp:unity-build-deploy` / `unity-build-deploy` |

**Agents 3종 (Claude Code)** — 특정 도메인 전문 에이전트
| Agent | 역할 |
|-------|------|
| `unity-scene-architect` | 씬 설계, GameObject/컴포넌트/UI 구성 |
| `unity-debugger` | 에러 추적, Play Mode 검사, 성능 분석 |
| `unity-asset-manager` | Material, Prefab, Addressable, 빌드 관리 |

**Hooks** — 자동 상태 관리
- `SessionStart`: Unity 연결 상태 체크(Pipeline 우선, TCP 폴백), 어느 경로가 살아 있는지 `[Unity connection]` 컨텍스트 주입, Pipeline 연결 시 `set_autotick` 활성화
- `PreToolUse`: 도구 호출 전 컴파일/도메인 리로드/settling 감지. 진행 중이면 대기 후 재시도.
- `PostToolUse`: 씬 변경 도구 실행 후 `auto_save_scene=true` 시 자동 저장. Prefab Stage에서는 명시적 Prefab 저장 결정을 보존하기 위해 건너뜀.
- `PostToolUseFailure`: 도구 실행 실패 시 연결 복구 진단. 복구 실패 시 `Logs/Editor.log`의 컴파일 에러를 컨텍스트로 주입.

### 사용 예시

```
> Unity 씬 계층을 확인하고 현재 상태를 요약해줘.

> @unity-scene-architect 3D 플랫포머 기본 씬을 만들어줘. 바닥, 플레이어, 카메라.

> @unity-debugger 현재 씬의 에러 원인을 찾아줘.

> /unity-mcp:unity-build-deploy
> Windows 타겟으로 빌드해줘.
```

---

## 수동 설치 (플러그인 없이)

플러그인을 사용하지 않고 개별 도구에서 MCP 서버만 쓰고 싶다면:

## Quick Start

### Step 1. Install MCP Bridge

No pre-install needed — just configure and go (see Step 3).

Or install globally:

```bash
# npx (recommended, no .NET required)
npx -y unity-mcp-bridge

# dotnet tool (requires .NET 9.0+ SDK)
dotnet tool install -g dev.breadpack.UnityMcpBridge
```

### Step 2. Install Unity Editor Package

> Requires Unity 6000.0+ (Unity 6)

Open Unity Editor > Window > Package Manager > **+** > **Add package from git URL**:

```
https://github.com/breadpack/UnityMcp.git?path=UnityMcpEditor
```

Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`) is auto-resolved as a dependency. No other prerequisites required.

### Step 3. Configure your AI tool

Add MCP server configuration to your AI tool:

<details>
<summary><b>Claude Code</b> (.mcp.json in project root)</summary>

```json
{
  "mcpServers": {
    "unity": {
      "command": "npx",
      "args": ["-y", "unity-mcp-bridge"]
    }
  }
}
```

Or via CLI:
```bash
claude mcp add unity -- npx -y unity-mcp-bridge
```

</details>

<details>
<summary><b>Claude Desktop</b></summary>

Edit `%APPDATA%/Claude/claude_desktop_config.json` (Windows) or `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS):

```json
{
  "mcpServers": {
    "unity": {
      "command": "npx",
      "args": ["-y", "unity-mcp-bridge"]
    }
  }
}
```

</details>

<details>
<summary><b>Cursor / VS Code</b> (.cursor/mcp.json or .vscode/mcp.json)</summary>

```json
{
  "mcpServers": {
    "unity": {
      "command": "npx",
      "args": ["-y", "unity-mcp-bridge"]
    }
  }
}
```

</details>

<details>
<summary><b>Using dotnet tool instead</b></summary>

If you installed via `dotnet tool install -g`, use the command directly:

```json
{
  "mcpServers": {
    "unity": {
      "command": "unity-mcp-bridge"
    }
  }
}
```

</details>

### Step 4. Verify

Open Unity Editor with the package installed, then ask your AI agent:

> "Unity에 ping을 보내줘"

## Alternative: Clone and Build

If you prefer to build from source:

```bash
git clone https://github.com/breadpack/UnityMcp.git
cd UnityMcp/UnityMcpBridge
dotnet run
```

Configure with:
```json
{
  "mcpServers": {
    "unity": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/UnityMcp/UnityMcpBridge"]
    }
  }
}
```

> Requires [.NET 9.0+](https://dotnet.microsoft.com/download) SDK

## Configuration

| Environment Variable | Default | Description |
|---------------------|---------|-------------|
| `UNITY_TCP_PORT` | _(auto-discover)_ | Force a fixed TCP port. When unset, the bridge auto-discovers the Unity instance for the current workspace across ports 9876–9885 |

Example with custom port (overrides auto-discovery):
```json
{
  "mcpServers": {
    "unity": {
      "command": "unity-mcp-bridge",
      "env": { "UNITY_TCP_PORT": "9877" }
    }
  }
}
```

## Available Tools (38)

### Observation (16)

| Tool | Description |
|------|-------------|
| `unity_ping` | Check Unity Editor connection |
| `unity_get_hierarchy` | Get scene hierarchy |
| `unity_get_asset_hierarchy` | Inspect Prefab/Scene hierarchy without opening |
| `unity_get_component_details` | Get component property details |
| `unity_get_screen` | Get current screen info (Play Mode) |
| `unity_get_ui_tree` | Get UI Toolkit visual tree (Play Mode) |
| `unity_get_ugui_tree` | Get UGUI Canvas hierarchy |
| `unity_get_console_logs` | Get console log entries |
| `unity_get_available_actions` | List clickable UI actions (Play Mode) |
| `unity_take_screenshot` | Capture game view screenshot (Play Mode) |
| `unity_take_scene_view_screenshot` | Capture Scene View screenshot (no Play Mode) |
| `unity_render_prefab_preview` | Render a prefab to a high-quality preview image in isolation (no Play Mode) |
| `unity_render_uxml` | Render UXML to image |
| `unity_refresh_assets` | Refresh AssetDatabase |
| `unity_addressable_add` | Add asset to Addressable group (requires package) |
| `unity_addressable_set_address` | Set Addressable asset address (requires package) |

### Scene Manipulation (10)

| Tool | Description |
|------|-------------|
| `unity_create_gameobject` | Create new GameObject |
| `unity_delete_gameobject` | Delete GameObject (dryRun supported) |
| `unity_reparent_gameobject` | Change parent of GameObject |
| `unity_set_transform` | Set position / rotation / scale |
| `unity_set_active` | Activate / deactivate GameObject |
| `unity_select_gameobject` | Select and ping in Editor |
| `unity_save_scene` | Save current scene |
| `unity_load_scene` | Open scene (Single / Additive) |
| `unity_play_mode` | Enter / exit / toggle Play Mode |
| `unity_instantiate_prefab` | Instantiate prefab in scene |

### Component & Property (4)

| Tool | Description |
|------|-------------|
| `unity_add_component` | Add component to GameObject |
| `unity_remove_component` | Remove component (dryRun supported) |
| `unity_set_property` | Set component property (dot-notation, array, asset ref) |
| `unity_set_asset_reference` | Set asset reference on component field |

### Material (2)

| Tool | Description |
|------|-------------|
| `unity_create_material` | Create new Material asset |
| `unity_set_material_property` | Set material property (color, float, texture, vector) |

### Asset Management (1)

| Tool | Description |
|------|-------------|
| `unity_manage_asset` | Move / copy / delete asset, create folder |

### Prefab (2)

| Tool | Description |
|------|-------------|
| `unity_prefab_edit` | Enter / save / explicit save-and-exit or discard-and-exit / status of Prefab edit stage; dirty `exit` fails without opening a modal |
| `unity_prefab_apply` | Atomically edit a prefab in one call (no stage) — apply an `edits[]` batch by root-relative path, then save |

### Animation (1)

| Tool | Description |
|------|-------------|
| `unity_animator_control` | Set Animator parameters, query state (Play Mode) |

### Build & Settings (2)

| Tool | Description |
|------|-------------|
| `unity_build` | Build player (Windows, macOS, Linux, Android, iOS, WebGL) |
| `unity_project_settings` | Read / write PlayerSettings, QualitySettings, Physics, Time |

## Conditional Features

### Addressables Support

To enable Addressable tools, add the scripting define symbol to your Unity project:

1. Edit > Project Settings > Player > Scripting Define Symbols
2. Add `UNITY_MCP_ADDRESSABLES`

## Update

npx는 자동으로 최신 버전을 사용합니다. dotnet tool을 사용하는 경우:

```bash
dotnet tool update -g dev.breadpack.UnityMcpBridge
```

## License

[MIT](LICENSE)
