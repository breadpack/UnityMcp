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

## Claude Code Plugin (Skills 포함)

Claude Code에서 스킬(슬래시 커맨드)과 함께 사용하려면 플러그인으로 설치할 수 있습니다.

### Plugin 설치

**방법 A: GitHub에서 설치 (권장)**

```bash
claude plugin install --from https://github.com/breadpack/UnityMcp
```

**방법 B: 로컬 클론 후 설치**

```bash
git clone https://github.com/breadpack/UnityMcp.git
claude --plugin-dir ./UnityMcp
```

> Plugin 설치 시 MCP 서버 설정(`.mcp.json`)이 자동으로 적용되므로, Step 3의 수동 설정은 불필요합니다.
> Unity Editor 패키지(Step 2)는 별도로 설치해야 합니다.

### 제공 Skills

Plugin을 설치하면 다음 슬래시 커맨드를 사용할 수 있습니다:

| Skill | 명령어 | 설명 |
|-------|--------|------|
| **Scene Setup** | `/unity-mcp:unity-scene-setup` | 씬 구성 워크플로우 — GameObject 생성, 계층 구조, 컴포넌트 설정 |
| **UI Build** | `/unity-mcp:unity-ui-build` | UGUI(Canvas) 및 UI Toolkit UI 구축 가이드 |
| **Material Setup** | `/unity-mcp:unity-material-setup` | Material 생성, 셰이더 프로퍼티 설정 레퍼런스 |
| **Prefab Workflow** | `/unity-mcp:unity-prefab-workflow` | Prefab 인스턴스화, 편집 모드, 저장 |
| **Debug** | `/unity-mcp:unity-debug` | 콘솔 로그, 스크린샷, Animator 상태 검사 |
| **Build & Deploy** | `/unity-mcp:unity-build-deploy` | 빌드 실행, Project Settings 관리 |

### Plugin 사용 예시

```
> /unity-mcp:unity-scene-setup
> 3D 플랫포머 게임의 기본 씬을 구성해줘.
> 바닥(Plane), 플레이어(Capsule+Rigidbody+CapsuleCollider), 카메라를 배치해줘.
```

```
> /unity-mcp:unity-debug
> 현재 씬에서 에러가 발생하는 원인을 찾아줘.
```

---

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
