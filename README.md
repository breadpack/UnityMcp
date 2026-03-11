# Unity MCP

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
| **UnityMcpBridge** | .NET MCP server (stdio ↔ TCP) | `dotnet tool` / clone |
| **UnityMcpEditor** | Unity Editor plugin (TCP server + handlers) | UPM git URL |

## Quick Start

### Step 1. Install MCP Bridge

```bash
dotnet tool install -g dev.breadpack.UnityMcpBridge
```

> Requires [.NET 9.0+](https://dotnet.microsoft.com/download) SDK

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
      "command": "unity-mcp-bridge"
    }
  }
}
```

Or via CLI:
```bash
claude mcp add unity -- unity-mcp-bridge
```

</details>

<details>
<summary><b>Claude Desktop</b></summary>

Edit `%APPDATA%/Claude/claude_desktop_config.json` (Windows) or `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS):

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

<details>
<summary><b>Cursor / VS Code</b> (.cursor/mcp.json or .vscode/mcp.json)</summary>

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

If you prefer not to use `dotnet tool`:

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

## Available Tools (23)

### Observation (12)

| Tool | Description |
|------|-------------|
| `unity_ping` | Check Unity Editor connection |
| `unity_get_hierarchy` | Get scene hierarchy |
| `unity_get_asset_hierarchy` | Inspect Prefab/Scene hierarchy without opening |
| `unity_get_component_details` | Get component property details |
| `unity_get_screen` | Get current screen info (Play Mode) |
| `unity_get_ui_tree` | Get UI visual tree |
| `unity_get_console_logs` | Get console log entries |
| `unity_get_available_actions` | List available actions |
| `unity_take_screenshot` | Capture Game/Scene view screenshot |
| `unity_addressable_add` | Add asset to Addressable group |
| `unity_addressable_set_address` | Set Addressable asset address |
| `unity_refresh_assets` | Refresh AssetDatabase |

### Scene Manipulation (11)

| Tool | Description |
|------|-------------|
| `unity_create_gameobject` | Create new GameObject |
| `unity_delete_gameobject` | Delete GameObject |
| `unity_reparent_gameobject` | Change parent of GameObject |
| `unity_set_transform` | Set transform properties |
| `unity_add_component` | Add component to GameObject |
| `unity_remove_component` | Remove component |
| `unity_set_property` | Set component property value |
| `unity_set_asset_reference` | Set asset reference on component |
| `unity_instantiate_prefab` | Instantiate prefab in scene |
| `unity_render_uxml` | Render UXML template |
| `unity_play_mode` | Control Play Mode |

## Conditional Features

### Addressables Support

To enable Addressable tools, add the scripting define symbol to your Unity project:

1. Edit > Project Settings > Player > Scripting Define Symbols
2. Add `UNITY_MCP_ADDRESSABLES`

## Update

```bash
dotnet tool update -g dev.breadpack.UnityMcpBridge
```

## License

[MIT](LICENSE)
