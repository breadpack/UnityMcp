#!/usr/bin/env node
'use strict';

const { tcpPing, sendRequest, discoverPort } = require('./unity-client');

const args = Object.fromEntries(
  process.argv.slice(2).map(a => {
    const [k, v] = a.replace(/^--/, '').split('=');
    return [k, v ?? 'true'];
  })
);

const autoSave = args['auto-save'] === 'true';
const workspaceDir = process.env.UNITY_WORKSPACE_DIR
  || process.env.CODEX_PROJECT_DIR
  || process.env.CODEX_WORKSPACE_ROOT
  || process.env.CLAUDE_PROJECT_DIR
  || process.cwd();

function log(msg) {
  process.stderr.write(`[Unity MCP] ${msg}\n`);
}

(async () => {
  const port = await discoverPort(workspaceDir);
  const connected = await tcpPing(port);
  if (!connected) {
    process.exit(0);
  }

  if (autoSave) {
    try {
      await sendRequest(port, 'unity_save_scene', {}, 5000);
      log('씬 자동 저장 완료');
    } catch (e) {
      log(`씬 자동 저장 실패: ${e.message}`);
    }
  }

  process.exit(0);
})();
