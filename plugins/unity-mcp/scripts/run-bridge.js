#!/usr/bin/env node
'use strict';

const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const https = require('https');
const os = require('os');

const pluginRoot = path.resolve(__dirname, '..');
const dataRoot = process.env.PLUGIN_DATA
  || process.env.CODEX_PLUGIN_DATA
  || process.env.CLAUDE_PLUGIN_DATA
  || path.join(pluginRoot, '.data');
const binDir = path.join(dataRoot, 'bin');
const exeName = process.platform === 'win32' ? 'UnityMcpBridge.exe' : 'UnityMcpBridge';
const binaryPath = path.join(binDir, exeName);
// 마지막으로 다운로드한 바이너리의 버전 기록. 시작 시 plugin 버전과 비교해
// 구버전 캐시가 고착되는 것을 막는다(자동 디스커버리/포트 개선 등이 누락되던 원인).
const versionMarkerPath = path.join(binDir, 'installed.version');

// SelfUpdateTool 과 약속된 "업데이트 후 재시작" 종료 코드. 양쪽을 함께 바꿔야 한다.
const UPDATE_EXIT_CODE = 42;
const REPO = 'breadpack/UnityMcp';

function readPluginVersion() {
  const manifestPaths = [
    path.join(pluginRoot, '.codex-plugin', 'plugin.json'),
    path.join(pluginRoot, '.claude-plugin', 'plugin.json'),
  ];
  for (const manifestPath of manifestPaths) {
    try {
      const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
      if (manifest.version) return manifest.version;
    } catch {
      // Try the next supported plugin manifest location.
    }
  }
  return '0.0.0';
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

// ── 버전 마커 ─────────────────────────────────────────────────────────────
function readInstalledVersion() {
  try { return fs.readFileSync(versionMarkerPath, 'utf8').trim() || null; }
  catch { return null; }
}

function writeInstalledVersion(version) {
  try { fs.writeFileSync(versionMarkerPath, version, 'utf8'); } catch { /* best-effort */ }
}

// "0.6.15" → [0,6,15]. a<b 면 음수, 같으면 0, a>b 면 양수.
function compareVersions(a, b) {
  const pa = String(a).replace(/^v/, '').split('.').map(n => parseInt(n, 10) || 0);
  const pb = String(b).replace(/^v/, '').split('.').map(n => parseInt(n, 10) || 0);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const d = (pa[i] || 0) - (pb[i] || 0);
    if (d !== 0) return d;
  }
  return 0;
}

// GitHub 최신 릴리스 태그 조회 (vX.Y.Z → X.Y.Z).
function fetchLatestVersion() {
  return new Promise((resolve, reject) => {
    const req = https.get({
      hostname: 'api.github.com',
      path: `/repos/${REPO}/releases/latest`,
      headers: {
        'User-Agent': 'unity-mcp-bridge-launcher',
        'Accept': 'application/vnd.github+json',
      },
    }, res => {
      let body = '';
      res.on('data', c => (body += c));
      res.on('end', () => {
        if (res.statusCode !== 200) {
          return reject(new Error(`GitHub API HTTP ${res.statusCode}`));
        }
        try {
          const tag = JSON.parse(body).tag_name;
          if (!tag) return reject(new Error('release tag_name 없음'));
          resolve(String(tag).replace(/^v/, ''));
        } catch (e) {
          reject(e);
        }
      });
    });
    req.on('error', reject);
    req.setTimeout(10000, () => req.destroy(new Error('GitHub API timeout')));
  });
}

// ── 바이너리 다운로드 ─────────────────────────────────────────────────────
async function downloadBinary(version) {
  const rid = getRid();
  if (!rid) throw new Error(`unsupported platform: ${process.platform}-${process.arch}`);

  const archiveExt = process.platform === 'win32' ? 'zip' : 'tar.gz';
  // Asset name must match publish.yml: lowercase-hyphenated `unity-mcp-bridge-${rid}.${ext}`.
  const archiveName = `unity-mcp-bridge-${rid}.${archiveExt}`;
  const url = `https://github.com/${REPO}/releases/download/v${version}/${archiveName}`;

  fs.mkdirSync(binDir, { recursive: true });
  const tmpArchive = path.join(binDir, `_${archiveName}`);

  process.stderr.write(`[Unity MCP] Downloading bridge v${version} from ${url}\n`);
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

  writeInstalledVersion(version);
}

// targetVersion 으로 바이너리를 맞춘다. 반환값: 실제로 새로 받았으면 true.
//  - 바이너리 없음             → 받는다
//  - force=true                → 무조건 받는다(self-update 경로)
//  - 마커 없음 + 바이너리 존재  → 건드리지 않는다(기존 설치 호환)
//  - 설치버전 < target         → 업그레이드 (다운그레이드는 하지 않는다)
async function ensureBinary(targetVersion, { force = false } = {}) {
  const exists = fs.existsSync(binaryPath);
  const installed = readInstalledVersion();

  let need;
  if (!exists) need = true;
  else if (force) need = true;
  else if (!installed) need = false;
  else need = compareVersions(installed, targetVersion) < 0;

  if (!need) return false;

  try {
    await downloadBinary(targetVersion);
    return true;
  } catch (e) {
    process.stderr.write(`[Unity MCP] Binary download (v${targetVersion}) failed: ${e.message}\n`);
    if (!exists) throw e; // 받은 적도 없으면 치명적 → 상위에서 npx fallback
    return false;          // 기존 바이너리가 있으면 그대로 진행
  }
}

// 바이너리(없으면 npx)로 Bridge 를 1회 실행하고, 종료 코드를 돌려준다.
function runOnce() {
  return new Promise(resolve => {
    let child;
    if (fs.existsSync(binaryPath)) {
      child = spawn(binaryPath, [], { stdio: 'inherit', env: process.env });
    } else {
      const npxCmd = process.platform === 'win32' ? 'npx.cmd' : 'npx';
      child = spawn(npxCmd, ['-y', 'unity-mcp-bridge'], { stdio: 'inherit', env: process.env, shell: process.platform === 'win32' });
    }
    child.on('exit', code => resolve(code ?? 0));
    child.on('error', err => {
      process.stderr.write(`[Unity MCP] Failed to start bridge: ${err.message}\n`);
      resolve(1);
    });
  });
}

async function main() {
  // (C) 시작 시 정합: plugin 버전보다 캐시 바이너리가 낮으면 갱신한다.
  //     plugin 만 올라가고 바이너리가 따라오지 않아 구버전이 고착되던 문제를 막는다.
  const pluginVersion = readPluginVersion();
  try {
    await ensureBinary(pluginVersion);
  } catch (e) {
    process.stderr.write(`[Unity MCP] initial binary ensure failed: ${e.message} — npx fallback 시도\n`);
  }

  // (A) self-update 루프: SelfUpdateTool 이 UPDATE_EXIT_CODE 로 종료하면
  //     GitHub 최신을 받아 재spawn 한다(세션 재시작 불필요). 그 외 종료는 그대로 전파.
  while (true) {
    const code = await runOnce();

    if (code === UPDATE_EXIT_CODE) {
      process.stderr.write('[Unity MCP] self-update 요청 감지 — 최신 릴리스 확인 중...\n');
      try {
        const latest = await fetchLatestVersion();
        const updated = await ensureBinary(latest);
        process.stderr.write(updated
          ? `[Unity MCP] v${latest} 로 갱신 완료. 재시작합니다.\n`
          : `[Unity MCP] 이미 최신(v${latest}). 재시작합니다.\n`);
      } catch (e) {
        process.stderr.write(`[Unity MCP] self-update 실패: ${e.message} — 기존 버전으로 재시작합니다.\n`);
      }
      continue; // 새 프로세스 재spawn
    }

    process.exit(code);
  }
}

main();
