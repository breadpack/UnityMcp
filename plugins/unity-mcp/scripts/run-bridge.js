#!/usr/bin/env node
'use strict';

const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const https = require('https');
const os = require('os');

const pluginRoot = path.resolve(__dirname, '..');
const dataRoot = process.env.CLAUDE_PLUGIN_DATA || path.join(pluginRoot, '.data');
const binDir = path.join(dataRoot, 'bin');
const exeName = process.platform === 'win32' ? 'UnityMcpBridge.exe' : 'UnityMcpBridge';
const binaryPath = path.join(binDir, exeName);

function readPluginVersion() {
  try {
    const manifest = JSON.parse(
      fs.readFileSync(path.join(pluginRoot, '.claude-plugin', 'plugin.json'), 'utf8')
    );
    return manifest.version || '0.0.0';
  } catch {
    return '0.0.0';
  }
}

function getRid() {
  const arch = process.arch === 'arm64' ? 'arm64' : 'x64';
  if (process.platform === 'win32') return `win-${arch}`;
  if (process.platform === 'darwin') return `osx-${arch}`;
  if (process.platform === 'linux') return `linux-${arch}`;
  return null;
}

function download(url, destPath) {
  return new Promise((resolve, reject) => {
    const file = fs.createWriteStream(destPath);
    https.get(url, res => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
        file.close();
        fs.unlinkSync(destPath);
        return download(res.headers.location, destPath).then(resolve, reject);
      }
      if (res.statusCode !== 200) {
        file.close();
        fs.unlinkSync(destPath);
        return reject(new Error(`HTTP ${res.statusCode}`));
      }
      res.pipe(file);
      file.on('finish', () => file.close(resolve));
    }).on('error', err => {
      file.close();
      try { fs.unlinkSync(destPath); } catch {}
      reject(err);
    });
  });
}

async function tryDownloadBinary() {
  const rid = getRid();
  if (!rid) throw new Error(`unsupported platform: ${process.platform}-${process.arch}`);

  const version = readPluginVersion();
  const archiveExt = process.platform === 'win32' ? 'zip' : 'tar.gz';
  // Asset name must match publish.yml: lowercase-hyphenated `unity-mcp-bridge-${rid}.${ext}`.
  // Previously read `UnityMcpBridge-${rid}` which 404'd against every release, forcing the
  // npx fallback. Bug surfaced as "binary not found" / silent stdio MCP server failure.
  const archiveName = `unity-mcp-bridge-${rid}.${archiveExt}`;
  const url = `https://github.com/breadpack/UnityMcp/releases/download/v${version}/${archiveName}`;

  fs.mkdirSync(binDir, { recursive: true });
  const tmpArchive = path.join(binDir, `_${archiveName}`);

  process.stderr.write(`[Unity MCP] Downloading binary from ${url}\n`);
  await download(url, tmpArchive);

  // 압축 해제
  if (archiveExt === 'zip') {
    // Windows: PowerShell Expand-Archive
    const ps = spawn('powershell.exe', [
      '-NoProfile', '-Command',
      `Expand-Archive -Path '${tmpArchive}' -DestinationPath '${binDir}' -Force`
    ], { stdio: 'inherit' });
    await new Promise((res, rej) => ps.on('exit', code => code === 0 ? res() : rej(new Error(`unzip failed: ${code}`))));
  } else {
    const tar = spawn('tar', ['-xzf', tmpArchive, '-C', binDir], { stdio: 'inherit' });
    await new Promise((res, rej) => tar.on('exit', code => code === 0 ? res() : rej(new Error(`tar failed: ${code}`))));
  }

  try { fs.unlinkSync(tmpArchive); } catch {}

  if (process.platform !== 'win32') {
    try { fs.chmodSync(binaryPath, 0o755); } catch {}
  }

  if (!fs.existsSync(binaryPath)) {
    throw new Error(`binary not found after extraction: ${binaryPath}`);
  }
}

async function main() {
  if (!fs.existsSync(binaryPath)) {
    try {
      await tryDownloadBinary();
    } catch (e) {
      process.stderr.write(`[Unity MCP] Binary download failed: ${e.message} — falling back to npx\n`);
    }
  }

  let child;
  if (fs.existsSync(binaryPath)) {
    child = spawn(binaryPath, [], { stdio: 'inherit', env: process.env });
  } else {
    const npxCmd = process.platform === 'win32' ? 'npx.cmd' : 'npx';
    child = spawn(npxCmd, ['-y', 'unity-mcp-bridge'], { stdio: 'inherit', env: process.env, shell: process.platform === 'win32' });
  }

  child.on('exit', code => process.exit(code ?? 0));
  child.on('error', err => {
    process.stderr.write(`[Unity MCP] Failed to start bridge: ${err.message}\n`);
    process.exit(1);
  });
}

main();
