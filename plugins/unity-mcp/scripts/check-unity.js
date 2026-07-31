#!/usr/bin/env node
'use strict';

const { tcpPing, getEditorState, discoverPort } = require('./unity-client');
const {
  UNITY_PACKAGE_NAME,
  compareVersions,
  readPluginVersion,
  readUnityPackageVersion,
} = require('./version-utils');

const args = Object.fromEntries(
  process.argv.slice(2).map(a => {
    const [k, v] = a.replace(/^--/, '').split('=');
    return [k, v ?? 'true'];
  })
);

const mode = args.mode || 'session-start';
const workspaceDir = process.env.UNITY_WORKSPACE_DIR
  || process.env.CODEX_PROJECT_DIR
  || process.env.CODEX_WORKSPACE_ROOT
  || process.env.CLAUDE_PROJECT_DIR
  || process.cwd();
// workspace 에 해당하는 Unity 포트 — main 진입 시 디스커버리로 확정
let port = parseInt(process.env.UNITY_TCP_PORT || '9876', 10);
const checkCompile = args['check-compile'] === 'true';
const checkReload = args['check-reload'] === 'true';
const maxWaitSec = parseInt(process.env.UNITY_MAX_WAIT_SEC || '60', 10);

function log(msg) {
  process.stderr.write(`[Unity MCP] ${msg}\n`);
}

function buildVersionMismatchContext(pluginVersion, packageVersion) {
  if (!pluginVersion || !packageVersion || compareVersions(pluginVersion, packageVersion) === 0) {
    return null;
  }

  const packageIsOlder = compareVersions(packageVersion, pluginVersion) < 0;
  const recommendation = packageIsOlder
    ? `Recommend updating the Unity UPM package '${UNITY_PACKAGE_NAME}' to the exact tag v${pluginVersion}.`
    : `Recommend updating the installed Unity MCP agent plugin to v${packageVersion}; do not automatically downgrade the Unity package.`;

  return [
    '[Unity MCP version mismatch]',
    `Agent plugin: v${pluginVersion}`,
    `Unity package: v${packageVersion}`,
    '',
    'Before using Unity MCP tools, guide the user to align these versions.',
    recommendation,
    'Do not modify Packages/manifest.json or perform a downgrade without explicit user approval.',
    'After updating, wait for Unity compilation/domain reload to finish and verify the versions again.',
  ].join('\n');
}

async function runSessionStart({
  port: sessionPort,
  workspaceDir: sessionWorkspaceDir,
  tcpPingFn = tcpPing,
  getEditorStateFn = getEditorState,
  readPluginVersionFn = readPluginVersion,
  readUnityPackageVersionFn = readUnityPackageVersion,
  logFn = log,
  contextFn = context => process.stdout.write(`${context}\n`),
}) {
  const ok = await tcpPingFn(sessionPort);
  if (!ok) {
    logFn('Unity Editor 연결 실패 — Editor가 실행 중이고 UnityMcpEditor 플러그인이 설치되었는지 확인하세요.');
    return;
  }

  const state = await getEditorStateFn(sessionPort).catch(() => null);
  if (!state) return;

  logFn(`연결됨 — ${state.projectName} (Unity ${state.unityVersion})`);

  const pluginVersion = readPluginVersionFn();
  const packageVersion = state.packageVersion
    || readUnityPackageVersionFn(state.projectPath || sessionWorkspaceDir);
  const context = buildVersionMismatchContext(pluginVersion, packageVersion);
  if (context) contextFn(context);
}

async function waitForReady() {
  const startMs = Date.now();
  let lastReport = -1;
  while ((Date.now() - startMs) / 1000 < maxWaitSec) {
    const connected = await tcpPing(port);
    if (connected) {
      const state = await getEditorState(port).catch(() => null);
      if (state && !state.isCompiling && !state.isUpdating) return true;
    }
    const elapsed = Math.floor((Date.now() - startMs) / 1000);
    if (elapsed !== lastReport) {
      log(`컴파일/리로드 대기 중... (${elapsed}s)`);
      lastReport = elapsed;
    }
    await new Promise(r => setTimeout(r, 500));
  }
  return false;
}

async function main() {
  port = await discoverPort(workspaceDir);

  if (mode === 'session-start') {
    await runSessionStart({ port, workspaceDir });
    return 0;
  }

  if (mode === 'pre-tool') {
    const connected = await tcpPing(port);
    let needsWait = !connected;

    if (connected && (checkCompile || checkReload)) {
      const state = await getEditorState(port).catch(() => null);
      if (state) {
        if (checkCompile && state.isCompiling) needsWait = true;
        if (checkReload && state.isUpdating) needsWait = true;
      }
    }

    if (needsWait) {
      const ready = await waitForReady();
      if (!ready) {
        log(`대기 시간 초과 (${maxWaitSec}s) — 나중에 재시도하세요.`);
        return 1;
      }
      log('준비 완료 — 도구 실행 재개');
    }
    return 0;
  }

  if (mode === 'failure-diagnosis') {
    const ok = await tcpPing(port);
    if (!ok) {
      log('연결 끊김 — 컴파일/리로드가 시작되었을 수 있습니다. 복구 대기 중...');
      const ready = await waitForReady();
      if (ready) log('복구 완료 — 재시도 가능');
      else log('복구 실패 — 수동 확인 필요');
    }
    return 0;
  }

  log(`알 수 없는 모드: ${mode}`);
  return 0;
}

if (require.main === module) {
  main()
    .then(code => {
      process.exitCode = code;
    })
    .catch(error => {
      log(error.stack || error.message);
      process.exitCode = 1;
    });
}

module.exports = {
  buildVersionMismatchContext,
  main,
  runSessionStart,
};
