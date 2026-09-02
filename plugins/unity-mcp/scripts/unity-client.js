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

// ---------------------------------------------------------------------------
// Unity Pipeline (com.unity.pipeline) HTTP 클라이언트
//
// Unity 6 + com.unity.pipeline 이 설치된 Editor 는 localhost HTTP 서버(7800~7849)를 띄우고
// <project>/Library/Pipeline/.unity-pipeline-port 에 { pid, port, projectPath, evalToken, ... }
// 디스크립터를 쓴다. 훅은 CLI 프로세스를 스폰하지 않고 이 서버를 직접 호출한다(호출당 수 ms).
// 모든 요청은 Authorization: Bearer <evalToken> 이 필요하다.
// ---------------------------------------------------------------------------

const fs = require('fs');
const http = require('http');

const PIPELINE_DESCRIPTOR = path.join('Library', 'Pipeline', '.unity-pipeline-port');

// 디스크립터 파일을 읽어 파싱한다. 없거나 깨졌으면 null.
function readPipelineDescriptor(projectDir) {
  if (!projectDir) return null;
  const file = path.join(projectDir, PIPELINE_DESCRIPTOR);
  try {
    const raw = fs.readFileSync(file, 'utf8');
    const desc = JSON.parse(raw);
    if (!desc || !desc.port) return null;
    desc.projectPath = desc.projectPath || projectDir;
    return desc;
  } catch {
    return null;
  }
}

// workspace 와 그 1단계 하위 폴더에서 Pipeline 디스크립터를 찾는다.
// UNITY_PROJECT_PATH(Unity CLI 와 동일한 환경변수)가 있으면 그것을 우선한다.
function findPipelineDescriptor(workspaceDir) {
  const candidates = [];
  if (process.env.UNITY_PROJECT_PATH) candidates.push(process.env.UNITY_PROJECT_PATH);
  if (workspaceDir) {
    candidates.push(workspaceDir);
    try {
      for (const entry of fs.readdirSync(workspaceDir, { withFileTypes: true })) {
        if (entry.isDirectory() && !entry.name.startsWith('.')) {
          candidates.push(path.join(workspaceDir, entry.name));
        }
      }
    } catch { /* 읽기 실패는 무시 */ }
  }
  for (const dir of candidates) {
    const desc = readPipelineDescriptor(dir);
    if (desc) return desc;
  }
  return null;
}

function pipelineRequest(desc, method, apiPath, body, timeoutMs = 3000) {
  return new Promise((resolve, reject) => {
    const payload = body ? Buffer.from(JSON.stringify(body), 'utf8') : null;
    const req = http.request({
      host: '127.0.0.1',
      port: desc.port,
      method,
      path: apiPath,
      headers: {
        Authorization: `Bearer ${desc.evalToken || ''}`,
        'Content-Type': 'application/json',
        ...(payload ? { 'Content-Length': payload.length } : {}),
      },
      timeout: timeoutMs,
    }, res => {
      const chunks = [];
      res.on('data', c => chunks.push(c));
      res.on('end', () => {
        const text = Buffer.concat(chunks).toString('utf8');
        let json = null;
        try { json = text ? JSON.parse(text) : null; } catch { /* 본문이 JSON 이 아님 */ }
        resolve({ statusCode: res.statusCode, json, text });
      });
    });
    req.once('timeout', () => { req.destroy(new Error('timeout')); });
    req.once('error', reject);
    if (payload) req.write(payload);
    req.end();
  });
}

// GET /api/status → { status: 'ready' | 'settling' | 'error', lastHeartbeat }
async function pipelineStatus(desc, timeoutMs = 1500) {
  const res = await pipelineRequest(desc, 'GET', '/api/status', null, timeoutMs);
  if (res.statusCode === 401) throw new Error('unauthorized (evalToken 불일치 — Editor 재시작 후 디스크립터 갱신 대기)');
  return res.json || {};
}

// POST /api/exec { command, parameters } → CommandExecutionResponse { success, result, error }
async function pipelineExec(desc, command, parameters = {}, timeoutMs = 5000) {
  const res = await pipelineRequest(desc, 'POST', '/api/exec', { command, parameters }, timeoutMs);
  if (res.statusCode === 503) throw new Error('settling');
  if (res.statusCode === 401) throw new Error('unauthorized');
  const body = res.json || {};
  if (body.success === false) throw new Error(body.error || `command '${command}' failed`);
  return body.result !== undefined ? body.result : body;
}

// editor_status 명령 → { status, compiling, domainReloadInProgress, playMode, projectPath, unityVersion }
async function pipelineEditorStatus(desc, timeoutMs = 2000) {
  return pipelineExec(desc, 'editor_status', {}, timeoutMs);
}

// Pipeline 과 UnityMcp TCP 를 하나의 상태 모델로 합친다.
//   { source: 'pipeline' | 'tcp' | null, connected, ready, isCompiling, isUpdating, isPlaying,
//     projectPath, projectName, unityVersion, pipeline: desc|null, port, raw }
// Pipeline 디스크립터가 있으면 그것을 우선하고, 도달 불가면 TCP 로 폴백한다.
async function getUnifiedEditorState(workspaceDir, {
  port = null,
  findPipelineDescriptorFn = findPipelineDescriptor,
  pipelineStatusFn = pipelineStatus,
  pipelineEditorStatusFn = pipelineEditorStatus,
  tcpPingFn = tcpPing,
  getEditorStateFn = getEditorState,
} = {}) {
  const desc = findPipelineDescriptorFn(workspaceDir);
  if (desc) {
    const status = await pipelineStatusFn(desc).catch(() => null);
    if (status) {
      const settling = status.status === 'settling';
      const es = settling ? null : await pipelineEditorStatusFn(desc).catch(() => null);
      return {
        source: 'pipeline',
        connected: true,
        ready: !settling && !!es && !es.compiling && !es.domainReloadInProgress,
        isCompiling: settling || !!(es && es.compiling),
        isUpdating: settling || !!(es && es.domainReloadInProgress),
        isPlaying: !!(es && es.playMode && es.playMode !== 'stopped'),
        projectPath: (es && es.projectPath) || desc.projectPath,
        projectName: desc.projectName || path.basename(desc.projectPath || ''),
        unityVersion: (es && es.unityVersion) || desc.unityVersion,
        pipeline: desc,
        port: null,
        raw: { status, editorStatus: es },
      };
    }
  }

  if (port != null && await tcpPingFn(port)) {
    const state = await getEditorStateFn(port).catch(() => null);
    return {
      source: 'tcp',
      connected: true,
      ready: !!state && !state.isCompiling && !state.isUpdating,
      isCompiling: !!(state && state.isCompiling),
      isUpdating: !!(state && state.isUpdating),
      isPlaying: !!(state && state.isPlaying),
      projectPath: state && state.projectPath,
      projectName: state && state.projectName,
      unityVersion: state && state.unityVersion,
      packageVersion: state && state.packageVersion,
      inPrefabStage: !!(state && state.inPrefabStage),
      pipeline: desc,
      port,
      raw: { state },
    };
  }

  return {
    source: null, connected: false, ready: false,
    isCompiling: false, isUpdating: false, isPlaying: false,
    pipeline: desc, port, raw: {},
  };
}

module.exports = {
  tcpPing, sendRequest, getEditorState, getProjectInfo,
  discoverPort, normalizePath, matchWorkspace,
  PIPELINE_DESCRIPTOR, readPipelineDescriptor, findPipelineDescriptor,
  pipelineRequest, pipelineStatus, pipelineExec, pipelineEditorStatus,
  getUnifiedEditorState,
};
