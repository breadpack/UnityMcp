#!/usr/bin/env node

const { spawn } = require("child_process");
const path = require("path");
const fs = require("fs");

const binDir = __dirname;
const exeName = process.platform === "win32" ? "UnityMcpBridge.exe" : "UnityMcpBridge";
const exePath = path.join(binDir, exeName);

if (!fs.existsSync(exePath)) {
  console.error("unity-mcp-bridge binary not found. Run 'npm install' to download it.");
  process.exit(1);
}

const child = spawn(exePath, process.argv.slice(2), {
  stdio: "inherit",
  env: process.env,
});

child.on("error", (err) => {
  console.error(`Failed to start unity-mcp-bridge: ${err.message}`);
  process.exit(1);
});

child.on("exit", (code) => {
  process.exit(code ?? 0);
});
