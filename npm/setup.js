#!/usr/bin/env node

const fs = require("fs");
const path = require("path");
const os = require("os");

const SUPPORTED_CLIENTS = ["claude-desktop", "cursor", "claude-code", "windsurf", "vscode"];

function getConfigPath(client) {
  const home = os.homedir();
  const platform = process.platform;

  switch (client) {
    case "claude-desktop":
      if (platform === "win32") return path.join(home, "AppData", "Roaming", "Claude", "claude_desktop_config.json");
      if (platform === "darwin") return path.join(home, "Library", "Application Support", "Claude", "claude_desktop_config.json");
      return path.join(home, ".config", "Claude", "claude_desktop_config.json");

    case "cursor":
      if (platform === "win32") return path.join(home, "AppData", "Roaming", "Cursor", "User", "globalStorage", "cursor.mcp", "config.json");
      if (platform === "darwin") return path.join(home, "Library", "Application Support", "Cursor", "User", "globalStorage", "cursor.mcp", "config.json");
      return path.join(home, ".config", "Cursor", "User", "globalStorage", "cursor.mcp", "config.json");

    case "claude-code":
      return path.join(home, ".claude", "settings.json");

    case "windsurf":
      if (platform === "win32") return path.join(home, "AppData", "Roaming", "Windsurf", "User", "globalStorage", "windsurf.mcp", "config.json");
      if (platform === "darwin") return path.join(home, "Library", "Application Support", "Windsurf", "User", "globalStorage", "windsurf.mcp", "config.json");
      return path.join(home, ".config", "Windsurf", "User", "globalStorage", "windsurf.mcp", "config.json");

    case "vscode":
      if (platform === "win32") return path.join(home, "AppData", "Roaming", "Code", "User", "settings.json");
      if (platform === "darwin") return path.join(home, "Library", "Application Support", "Code", "User", "settings.json");
      return path.join(home, ".config", "Code", "User", "settings.json");

    default:
      return null;
  }
}

function getBinaryPath() {
  const binDir = path.join(__dirname, "bin");
  const exeName = process.platform === "win32" ? "UnityMcpBridge.exe" : "UnityMcpBridge";
  const exePath = path.join(binDir, exeName);

  if (fs.existsSync(exePath)) {
    return exePath;
  }

  return null;
}

function getMcpServerConfig(binaryPath) {
  if (binaryPath) {
    return {
      command: binaryPath,
      args: [],
      env: {}
    };
  }

  return {
    command: "npx",
    args: ["-y", "unity-mcp-bridge"],
    env: {}
  };
}

function readConfigSafe(configPath) {
  if (!fs.existsSync(configPath)) {
    return {};
  }

  try {
    const raw = fs.readFileSync(configPath, "utf8");
    return JSON.parse(raw);
  } catch (err) {
    console.warn(`Warning: existing config at ${configPath} is malformed. Existing content will be preserved as a backup.`);
    const backupPath = configPath + ".backup";
    try {
      fs.copyFileSync(configPath, backupPath);
      console.warn(`  Backup saved to: ${backupPath}`);
    } catch (_) {
      // Ignore backup failure
    }
    return {};
  }
}

function writeConfig(configPath, config) {
  fs.mkdirSync(path.dirname(configPath), { recursive: true });
  fs.writeFileSync(configPath, JSON.stringify(config, null, 2) + "\n");
}

function setupVSCode(configPath, serverConfig) {
  const config = readConfigSafe(configPath);

  if (!config.mcp) config.mcp = {};
  if (!config.mcp.servers) config.mcp.servers = {};
  config.mcp.servers["unity-bridge"] = serverConfig;

  writeConfig(configPath, config);
}

function setupStandardClient(configPath, serverConfig) {
  const config = readConfigSafe(configPath);

  if (!config.mcpServers) config.mcpServers = {};
  config.mcpServers["unity-bridge"] = serverConfig;

  writeConfig(configPath, config);
}

function main() {
  const args = process.argv.slice(2);

  // When invoked via run.js, argv will be [..., "setup", "<client>"]
  // When invoked directly, argv will be [..., "<client>"] or [..., "setup", "<client>"]
  // Filter out "setup" if it's the first arg
  let filteredArgs = args;
  if (filteredArgs[0] === "setup") {
    filteredArgs = filteredArgs.slice(1);
  }

  if (filteredArgs.length === 0 || filteredArgs[0] === "--help" || filteredArgs[0] === "-h") {
    console.log("Usage: unity-mcp-bridge setup <client>");
    console.log("");
    console.log("Supported clients:");
    SUPPORTED_CLIENTS.forEach(c => console.log("  - " + c));
    console.log("");
    console.log("Examples:");
    console.log("  npx unity-mcp-bridge setup claude-desktop");
    console.log("  npx unity-mcp-bridge setup cursor");
    console.log("  npx unity-mcp-bridge setup claude-code");
    console.log("  npx unity-mcp-bridge setup windsurf");
    console.log("  npx unity-mcp-bridge setup vscode");
    process.exit(0);
  }

  const client = filteredArgs[0];

  if (!SUPPORTED_CLIENTS.includes(client)) {
    console.error("Unknown client: " + client);
    console.error("Supported clients: " + SUPPORTED_CLIENTS.join(", "));
    process.exit(1);
  }

  const configPath = getConfigPath(client);
  if (!configPath) {
    console.error("Cannot determine config path for " + client);
    process.exit(1);
  }

  const binaryPath = getBinaryPath();
  const serverConfig = getMcpServerConfig(binaryPath);

  // Allow custom port via environment variable
  const port = process.env.UNITY_TCP_PORT;
  if (port) {
    serverConfig.env.UNITY_TCP_PORT = port;
  }

  // Remove empty env object for cleaner config
  if (Object.keys(serverConfig.env).length === 0) {
    delete serverConfig.env;
  }

  try {
    if (client === "vscode") {
      setupVSCode(configPath, serverConfig);
    } else {
      setupStandardClient(configPath, serverConfig);
    }

    console.log("unity-mcp-bridge configured for " + client + ".");
    console.log("  Config: " + configPath);
    console.log("  Command: " + serverConfig.command + (serverConfig.args.length > 0 ? " " + serverConfig.args.join(" ") : ""));
    if (binaryPath) {
      console.log("  Binary: " + binaryPath);
    }
    console.log("");
    console.log("Restart " + client + " to activate the MCP server.");
  } catch (err) {
    console.error("Failed to configure " + client + ": " + err.message);
    process.exit(1);
  }
}

main();
