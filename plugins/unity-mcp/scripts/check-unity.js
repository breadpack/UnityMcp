#!/usr/bin/env node
'use strict';

const fs = require('fs');
const path = require('path');
const {
  discoverPort,
  getUnifiedEditorState, pipelineExec,
} = require('./unity-client');
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
// workspace 에 해당하는 UnityMcp TCP 포트 — main 진입 시 디스커버리로 확정 (Pipeline 폴백용)
let port = parseInt(process.env.UNITY_TCP_PORT || '9876', 10);
const checkCompile = args['check-compile'] === 'true';
const checkReload = args['check-reload'] === 'true';
// Pipeline 연결 시 set_autotick 을 켜서 Editor 가 비포커스 상태에서도 컴파일/테스트가 멈추지 않게 한다.
const autoTick = args['auto-tick'] !== 'false';
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

// 세션 시작 시 에이전트에게 주입할 연결 요약 — 어떤 경로(CLI/MCP)가 쓸 수 있는지 한 줄로 알린다.
function buildConnectionContext(state) {
  if (!state.connected) return null;
  const lines = ['[Unity connection]'];
  if (state.source === 'pipeline') {
    lines.push(`Unity Pipeline reachable (port ${state.pipeline.port}, ${state.projectName}, Unity ${state.unityVersion}).`);
    lines.push('Prefer `unity command <name> --json` (Bash) for scene/asset/settings/build/test work; use mcp__unity-bridge__* tools for Play Mode input, UI trees, prefab_apply, rendering, Addressables and undo. See the unity-cli-workflow skill.');
    if (state.port) lines.push(`UnityMcp TCP server also reachable on port ${state.port}.`);
  } else {
    lines.push(`UnityMcp TCP server reachable (port ${state.port}, ${state.projectName}, Unity ${state.unityVersion}).`);
    lines.push('Unity Pipeline server not reachable. Run `unity pipeline install` in the project to enable `unity command` / `unity mcp` (Unity 6.0+).');
  }
  return lines.join('\n');
}

// Safe Mode(컴파일 에러로 패키지 미로드) 의심 시 Editor.log 꼬리에서 CS 에러를 뽑아 컨텍스트로 준다.
function readCompileErrorsFromLog(projectDir, maxLines = 20) {
  if (!projectDir) return [];
  const file = path.join(projectDir, 'Logs', 'Editor.log');
  try {
    const text = fs.readFileSync(file, 'utf8');
    const lines = text.split(/\r?\n/);
    const errors = [];
    for (let i = lines.length - 1; i >= 0 && errors.length < maxLines; i--) {
      if (/error CS\d{4}/.test(lines[i])) errors.unshift(lines[i].trim());
    }
    return errors;
  } catch {
    return [];
  }
}

async function runSessionStart({
  port: sessionPort,
  workspaceDir: sessionWorkspaceDir,
  autoTick: sessionAutoTick = autoTick,
  getUnifiedEditorStateFn = getUnifiedEditorState,
  pipelineExecFn = pipelineExec,
  readPluginVersionFn = readPluginVersion,
  readUnityPackageVersionFn = readUnityPackageVersion,
  logFn = log,
  contextFn = context => process.stdout.write(`${context}\n`),
  // 하위 호환: 예전 시그니처(tcpPingFn/getEditorStateFn)를 넘기면 TCP 경로만 사용
  tcpPingFn,
  getEditorStateFn,
}) {
  const state = await getUnifiedEditorStateFn(sessionWorkspaceDir, {
    port: sessionPort,
    ...(tcpPingFn ? { tcpPingFn } : {}),
    ...(getEditorStateFn ? { getEditorStateFn } : {}),
    ...(tcpPingFn || getEditorStateFn ? { findPipelineDescriptorFn: () => null } : {}),
  });

  if (!state.connected) {
    if (state.pipeline) {
      logFn('Unity Pipeline 디스크립터는 있으나 서버에 연결할 수 없습니다 — Editor 가 settling 중이거나 Safe Mode 일 수 있습니다.');
      const errors = readCompileErrorsFromLog(state.pipeline.projectPath);
      if (errors.length) contextFn(['[Unity compile errors from Logs/Editor.log]', ...errors].join('\n'));
    } else {
      logFn('Unity Editor 연결 실패 — Editor가 실행 중이고 UnityMcpEditor 또는 com.unity.pipeline 이 설치되었는지 확인하세요.');
    }
    return;
  }

  logFn(`연결됨 (${state.source}) — ${state.projectName} (Unity ${state.unityVersion})`);
  const connectionContext = buildConnectionContext(state);
  if (connectionContext) contextFn(connectionContext);

  if (state.source === 'pipeline' && sessionAutoTick) {
    try {
      await pipelineExecFn(state.pipeline, 'set_autotick', { enable: true }, 3000);
      logFn('set_autotick 활성화 — 비포커스 상태에서도 Editor 가 계속 틱합니다');
    } catch (e) {
      logFn(`set_autotick 실패(무시): ${e.message}`);
    }
  }

  // UnityMcp 패키지 버전 동기화는 TCP(UnityMcpEditor) 경로가 살아 있을 때만 의미가 있다.
  if (state.source !== 'tcp') return;
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
    const state = await getUnifiedEditorState(workspaceDir, { port });
    if (state.ready) return true;
    const elapsed = Math.floor((Date.now() - startMs) / 1000);
    if (elapsed !== lastReport) {
      const phase = state.isCompiling ? '컴파일' : state.isUpdating ? '도메인 리로드' : '연결';
      log(`${phase} 대기 중... (${elapsed}s)`);
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
    const state = await getUnifiedEditorState(workspaceDir, { port });
    let needsWait = !state.connected;
    if (state.connected) {
      if (checkCompile && state.isCompiling) needsWait = true;
      if (checkReload && state.isUpdating) needsWait = true;
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
    const state = await getUnifiedEditorState(workspaceDir, { port });
    if (!state.connected) {
      log('연결 끊김 — 컴파일/리로드가 시작되었을 수 있습니다. 복구 대기 중...');
      const ready = await waitForReady();
      if (ready) log('복구 완료 — 재시도 가능');
      else {
        log('복구 실패 — 수동 확인 필요');
        const projectDir = (state.pipeline && state.pipeline.projectPath) || workspaceDir;
        const errors = readCompileErrorsFromLog(projectDir);
        if (errors.length) {
          process.stdout.write(['[Unity compile errors from Logs/Editor.log]', ...errors].join('\n') + '\n');
        }
      }
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
  buildConnectionContext,
  readCompileErrorsFromLog,
  main,
  runSessionStart,
};
