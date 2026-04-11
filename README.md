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

## Claude Code Plugin 설치 (권장)

Claude Code에서 플러그인으로 설치하면 MCP 서버, Skills, Agents, Hooks가 한 번에 구성됩니다.

### Step 1. Unity Editor 패키지 설치

> Unity 6000.0+ 필요

Unity Editor > Window > Package Manager > **+** > **Add package from git URL**:

```
https://github.com/breadpack/UnityMcp.git?path=UnityMcpEditor
```

### Step 2. Claude Code에서 플러그인 설치

Claude Code 세션 내에서:

```
/plugin install --from github:breadpack/UnityMcp
```

또는 프로젝트 `.claude/settings.json`에 등록:

```json
{
  "enabledPlugins": {
    "unity-mcp": {
      "source": {
        "source": "github",
        "repo": "breadpack/UnityMcp"
      }
    }
  }
}
```

설치 시 아래 옵션을 프롬프트로 설정할 수 있습니다:

| 옵션 | 설명 | 기본 |
|------|------|------|
| `unity_tcp_port` | Unity TCP 포트 | `9876` |
| `auto_save_scene` | 씬 변경 후 자동 저장 | `false` |
| `check_compile_status` | 도구 호출 전 컴파일 상태 체크 | `true` |
| `check_domain_reload` | 도구 호출 전 도메인 리로드 상태 체크 | `true` |
| `max_wait_seconds` | 컴파일/리로드 대기 최대 시간(초) | `60` |

### Step 3. 검증

Unity Editor를 연 상태에서 Claude Code에 다음과 같이 요청:

> "Unity에 ping을 보내줘"

### 포함 구성요소

**MCP 서버 (unity-bridge)** — 45+ Unity 도구 (씬, 컴포넌트, 에셋, 빌드 등)

**Agents 3종** — 특정 도메인 전문 에이전트
| Agent | 역할 |
|-------|------|
| `unity-scene-architect` | 씬 설계, GameObject/컴포넌트/UI 구성 |
| `unity-debugger` | 에러 추적, Play Mode 검사, 성능 분석 |
| `unity-asset-manager` | Material, Prefab, Addressable, 빌드 관리 |

**Skills 6종** — 워크플로우 가이드 (슬래시 커맨드)
| Skill | 명령어 |
|-------|--------|
| Scene Setup | `/unity-mcp:unity-scene-setup` |
| UI Build | `/unity-mcp:unity-ui-build` |
| Material Setup | `/unity-mcp:unity-material-setup` |
| Prefab Workflow | `/unity-mcp:unity-prefab-workflow` |
| Debug | `/unity-mcp:unity-debug` |
| Build & Deploy | `/unity-mcp:unity-build-deploy` |

**Hooks** — 자동 상태 관리
- `SessionStart`: Unity 연결 상태 체크 및 프로젝트 정보 출력
- `PreToolUse`: 도구 호출 전 컴파일/도메인 리로드 감지. 진행 중이면 대기 후 재시도.
- `PostToolUse`: 씬 변경 도구 실행 후 `auto_save_scene=true` 시 자동 저장.
- `PostToolUseFailure`: 도구 실행 실패 시 연결 복구 진단.

### 사용 예시

```
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
| `UNITY_TCP_PORT` | `9876` | TCP port to connect to Unity Editor |

Example with custom port:
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

## Available Tools (35)

### Observation (14)

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

### Prefab (1)

| Tool | Description |
|------|-------------|
| `unity_prefab_edit` | Enter / save / exit Prefab edit mode |

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
