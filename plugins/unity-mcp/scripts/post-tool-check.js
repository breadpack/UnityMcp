#!/usr/bin/env node
'use strict';

const { tcpPing, sendRequest } = require('./unity-client');

const args = Object.fromEntries(
  process.argv.slice(2).map(a => {
    const [k, v] = a.replace(/^--/, '').split('=');
    return [k, v ?? 'true'];
  })
);

const autoSave = args['auto-save'] === 'true';
const port = parseInt(process.env.UNITY_TCP_PORT || '9876', 10);

function log(msg) {
  process.stderr.write(`[Unity MCP] ${msg}\n`);
}

(async () => {
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
