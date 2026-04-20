#!/usr/bin/env node
'use strict';

const { tcpPing, getEditorState } = require('./unity-client');

const args = Object.fromEntries(
  process.argv.slice(2).map(a => {
    const [k, v] = a.replace(/^--/, '').split('=');
    return [k, v ?? 'true'];
  })
);

const mode = args.mode || 'session-start';
const port = parseInt(process.env.UNITY_TCP_PORT || '9876', 10);
const checkCompile = args['check-compile'] === 'true';
const checkReload = args['check-reload'] === 'true';
const maxWaitSec = parseInt(process.env.UNITY_MAX_WAIT_SEC || '60', 10);

function log(msg) {
  process.stderr.write(`[Unity MCP] ${msg}\n`);
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

(async () => {
  if (mode === 'session-start') {
    const ok = await tcpPing(port);
    if (!ok) {
      log('Unity Editor 연결 실패 — Editor가 실행 중이고 UnityMcpEditor 플러그인이 설치되었는지 확인하세요.');
      process.exit(0);
    }
    const state = await getEditorState(port).catch(() => null);
    if (state) log(`연결됨 — ${state.projectName} (Unity ${state.unityVersion})`);
    process.exit(0);
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
        process.exit(1);
      }
      log('준비 완료 — 도구 실행 재개');
    }
    process.exit(0);
  }

  if (mode === 'failure-diagnosis') {
    const ok = await tcpPing(port);
    if (!ok) {
      log('연결 끊김 — 컴파일/리로드가 시작되었을 수 있습니다. 복구 대기 중...');
      const ready = await waitForReady();
      if (ready) log('복구 완료 — 재시도 가능');
      else log('복구 실패 — 수동 확인 필요');
    }
    process.exit(0);
  }

  log(`알 수 없는 모드: ${mode}`);
  process.exit(0);
})();
