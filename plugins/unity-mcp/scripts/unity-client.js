'use strict';

const net = require('net');
const path = require('path');

function tcpPing(port, timeoutMs = 1000) {
  return new Promise(resolve => {
    const sock = new net.Socket();
    let done = false;
    const finish = ok => {
      if (done) return;
      done = true;
      sock.destroy();
      resolve(ok);
    };
    sock.setTimeout(timeoutMs);
    sock.once('connect', () => finish(true));
    sock.once('error', () => finish(false));
    sock.once('timeout', () => finish(false));
    sock.connect(port, '127.0.0.1');
  });
}

// length-prefixed JSON 요청 전송 후 응답 파싱
// 요청: { id, tool, params }
// 응답: { id, success, data, error }
function sendRequest(port, tool, params = {}, timeoutMs = 3000) {
  return new Promise((resolve, reject) => {
    const sock = new net.Socket();
    let done = false;
    const finish = (err, result) => {
      if (done) return;
      done = true;
      sock.destroy();
      if (err) reject(err); else resolve(result);
    };

    sock.setTimeout(timeoutMs);
    sock.once('timeout', () => finish(new Error('timeout')));
    sock.once('error', err => finish(err));

    let buf = Buffer.alloc(0);
    let expected = null;

    sock.on('data', chunk => {
      buf = Buffer.concat([buf, chunk]);
      if (expected === null && buf.length >= 4) {
        expected = buf.readUInt32BE(0);
        buf = buf.slice(4);
      }
      if (expected !== null && buf.length >= expected) {
        const payload = buf.slice(0, expected).toString('utf8');
        try {
          const parsed = JSON.parse(payload);
          if (parsed.success) finish(null, parsed.data);
          else finish(new Error(parsed.error || 'unknown error'));
        } catch (e) {
          finish(e);
        }
      }
    });

    sock.once('connect', () => {
      const body = JSON.stringify({
        id: Date.now().toString(),
        tool,
        params,
      });
      const bodyBuf = Buffer.from(body, 'utf8');
      const lenBuf = Buffer.alloc(4);
      lenBuf.writeUInt32BE(bodyBuf.length, 0);
      sock.write(Buffer.concat([lenBuf, bodyBuf]));
    });

    sock.connect(port, '127.0.0.1');
  });
}

async function getEditorState(port) {
  return sendRequest(port, 'unity_get_editor_state', {}, 2000);
}

async function getProjectInfo(port) {
  return sendRequest(port, 'unity_get_project_info', {}, 3000);
}

// 절대경로화 → 구분자 '/' 통일 → Windows 는 대소문자 무시
function normalizePath(p) {
  if (!p) return '';
  let full;
  try { full = path.resolve(p); } catch { full = p; }
  full = full.replace(/\\/g, '/').replace(/\/+$/, '');
  if (process.platform === 'win32') full = full.toLowerCase();
  return full;
}

// Unity projectPath 가 workspace 의 하위/동일 경로인지 판정
function matchWorkspace(unityProject, workspaceDir) {
  const u = normalizePath(unityProject);
  const w = normalizePath(workspaceDir);
  if (!u || !w) return false;
  return u === w || u.startsWith(w + '/');
}

// 9876..9885 를 스캔하여 workspace 에 해당하는 Unity 포트를 찾는다.
// UNITY_TCP_PORT 가 설정되어 있으면 그대로 사용(오버라이드). 매칭 실패 시 basePort fallback.
async function discoverPort(workspaceDir, basePort = 9876, range = 10) {
  const envPort = process.env.UNITY_TCP_PORT;
  if (envPort) return parseInt(envPort, 10);

  let bestPort = null;
  let bestLen = -1;
  for (let i = 0; i < range; i++) {
    const port = basePort + i;
    if (!(await tcpPing(port, 300))) continue;

    let projectPath = null;
    const state = await getEditorState(port).catch(() => null);
    if (state && state.projectPath) projectPath = state.projectPath;
    else {
      const info = await getProjectInfo(port).catch(() => null);
      if (info && info.projectPath) projectPath = info.projectPath;
    }
    if (!projectPath) continue;

    if (matchWorkspace(projectPath, workspaceDir)) {
      const np = normalizePath(projectPath);
      if (np.length > bestLen) { bestLen = np.length; bestPort = port; }
    }
  }
  return bestPort != null ? bestPort : basePort;
}

module.exports = {
  tcpPing, sendRequest, getEditorState, getProjectInfo,
  discoverPort, normalizePath, matchWorkspace,
};
