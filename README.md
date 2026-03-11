# Unity MCP

Unity MCP (Model Context Protocol) Bridge — Connect AI agents to Unity Editor.

## Architecture

```
AI Agent (Claude, Cursor, etc.)
    ↕ stdio (MCP protocol)
UnityMcpBridge (.NET)
    ↕ TCP (binary protocol)
Unity Editor (UnityMcpEditor package)
```

- **UnityMcpBridge** — .NET MCP server, translates MCP protocol to Unity TCP commands
- **UnityMcpEditor** — Unity Editor package with TCP server, request handlers, and utilities

## Installation

### 1. Unity Editor Package

Add `UnityMcpEditor` to your Unity project via UPM git URL:

1. Open Unity Editor
2. Window > Package Manager > "+" > Add package from git URL
3. Enter:
```
https://github.com/breadpack/UnityMcp.git?path=UnityMcpEditor
```

### 2. MCP Bridge

#### Option A: dotnet tool (Recommended)

```bash
dotnet tool install -g dev.breadpack.UnityMcpBridge
```

Then configure your AI tool:

```json
{
  "mcpServers": {
    "unity": {
      "command": "unity-mcp-bridge",
      "env": { "UNITY_TCP_PORT": "9876" }
    }
  }
}
```

#### Option B: Clone and build

```bash
git clone https://github.com/breadpack/UnityMcp.git
cd UnityMcp/UnityMcpBridge
dotnet build
```

Then configure with the built executable path:

```json
{
  "mcpServers": {
    "unity": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/UnityMcp/UnityMcpBridge"],
      "env": { "UNITY_TCP_PORT": "9876" }
    }
  }
}
```

## Configuration

| Environment Variable | Default | Description |
|---------------------|---------|-------------|
| `UNITY_TCP_PORT` | `9876` | TCP port to connect to Unity Editor |

### Claude Code (.mcp.json)

Place `.mcp.json` in your project root:

```json
{
  "mcpServers": {
    "unity": {
      "command": "unity-mcp-bridge",
      "env": { "UNITY_TCP_PORT": "9876" }
    }
  }
}
```

### Claude Desktop (claude_desktop_config.json)

Add to `%APPDATA%/Claude/claude_desktop_config.json` (Windows) or `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS):

```json
{
  "mcpServers": {
    "unity": {
      "command": "unity-mcp-bridge",
      "env": { "UNITY_TCP_PORT": "9876" }
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

## Requirements

- **Unity**: 6000.0+ (Unity 6)
- **.NET**: 9.0+ (for Bridge)
- **Dependencies**: Newtonsoft.Json (Unity), UniTask (Unity)

## Conditional Features

### Addressables Support

To enable Addressable tools, add the scripting define symbol `UNITY_MCP_ADDRESSABLES` to your Unity project:

1. Edit > Project Settings > Player > Script Compilation > Scripting Define Symbols
2. Add `UNITY_MCP_ADDRESSABLES`

## License

MIT
