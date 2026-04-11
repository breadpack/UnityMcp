# Unity MCP Plugin Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** UnityMcp를 완전한 Claude Code 플러그인으로 업그레이드한다. 단일 `/plugin install` 명령으로 skills + agents + hooks + MCP server가 한 번에 설치되고, 컴파일/도메인 리로드 상태를 자동 감지해 대기 후 재시도하는 안정적인 개발 경험을 제공한다.

**Architecture:** `.claude-plugin/plugin.json` 중심의 플러그인 구조. `${CLAUDE_PLUGIN_ROOT}` 경로 변수로 번들된 `scripts/`를 참조. Hooks는 TCP ping + `unity_get_editor_state` 폴링으로 Unity 상태를 감지하며, 대기 루프로 컴파일/리로드 완료를 기다린 뒤 도구 실행을 허용한다. 바이너리는 `${CLAUDE_PLUGIN_DATA}`에 lazy download하고 실패 시 `npx -y unity-mcp-bridge`로 fallback.

**Tech Stack:** .NET 9 (Bridge), C# (Unity Editor), Node.js (scripts), Markdown (agents/skills), JSON (manifests/hooks)

**Note on TDD:** 이 프로젝트에는 단위 테스트 프레임워크가 없고, 대부분의 변경은 config/integration 작업이다. 각 태스크의 검증은 "빌드 → 실행 → 출력 검사" 형태의 수동 검증 단계로 대체한다. Bridge `dotnet build`와 플러그인 로드 검증(`claude --plugin-dir .`)이 기본 검증 수단이다.

---

## 파일 구조 개요

```
UnityMcp/
├── .claude-plugin/plugin.json              [수정] v0.3.0, mcpServers+userConfig 인라인
├── .gitignore                              [수정] bin/ 추가
├── agents/
│   ├── unity-scene-architect.md            [신규]
│   ├── unity-debugger.md                   [신규]
│   └── unity-asset-manager.md              [신규]
├── hooks/hooks.json                        [신규]
├── scripts/
│   ├── run-bridge.js                       [신규] 바이너리 실행 래퍼
│   ├── unity-client.js                     [신규] TCP 프로토콜 헬퍼 모듈
│   ├── check-unity.js                      [신규] 연결/상태 체크 + 대기 루프
│   └── post-tool-check.js                  [신규] 후처리 알림
├── UnityMcpBridge/Tools/
│   └── GetEditorStateTool.cs               [신규]
├── UnityMcpEditor/Editor/Handlers/
│   └── GetEditorStateHandler.cs            [신규]
├── README.md                               [수정] 플러그인 설치 섹션
└── CLAUDE.md                               [수정] agents/hooks 구조 설명
```

---

## Task 1: Editor 핸들러 `GetEditorStateHandler` 추가

**Files:**
- Create: `UnityMcpEditor/Editor/Handlers/GetEditorStateHandler.cs`

**배경:** 기존 `GetProjectInfoHandler`는 패키지 목록까지 포함한 무거운 응답을 반환한다. Hook 스크립트는 500ms마다 폴링하므로 `isCompiling`, `isUpdating`, 기본 메타데이터만 반환하는 경량 핸들러가 필요하다.

- [ ] **Step 1: 핸들러 파일 작성**

파일 `UnityMcpEditor/Editor/Handlers/GetEditorStateHandler.cs`:

```csharp
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class GetEditorStateHandler : IRequestHandler
    {
        public string ToolName => "unity_get_editor_state";

        public object Handle(JObject @params)
        {
            return new
            {
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                isPlaying = EditorApplication.isPlaying,
                unityVersion = Application.unityVersion,
                projectName = Application.productName,
            };
        }
    }
}
```

- [ ] **Step 2: 자동 등록 확인**

`McpServerBootstrap.RegisterHandlers()`는 파라미터리스 생성자를 가진 `IRequestHandler` 구현을 리플렉션으로 자동 등록한다 (`UnityMcpEditor/Editor/Core/McpServerBootstrap.cs:117-133`). 새 핸들러는 이 경로로 자동 등록되므로 수동 등록 불필요.

- [ ] **Step 3: Bridge 도구 없이 선 커밋 (Editor만 빌드됨)**

```bash
git add UnityMcpEditor/Editor/Handlers/GetEditorStateHandler.cs
git commit -m "feat(editor): add GetEditorStateHandler for hook polling"
```

---

## Task 2: Bridge 도구 `GetEditorStateTool` 추가

**Files:**
- Create: `UnityMcpBridge/Tools/GetEditorStateTool.cs`

- [ ] **Step 1: Bridge 도구 파일 작성**

파일 `UnityMcpBridge/Tools/GetEditorStateTool.cs`:

```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class GetEditorStateTool
{
    [McpServerTool(Name = "unity_get_editor_state"), Description("Unity Editor의 현재 상태를 조회합니다 (isCompiling, isUpdating, isPlaying, unityVersion, projectName) — Hook 폴링용 경량 엔드포인트")]
    public static async Task<string> Execute(
        UnityConnection connection,
        CancellationToken ct = default)
    {
        var result = await connection.SendRequestAsync("unity_get_editor_state", ct: ct);
        return ResponseFormatter.Format(result);
    }
}
```

- [ ] **Step 2: Bridge 빌드 검증**

Run: `dotnet build UnityMcpBridge/UnityMcpBridge.csproj`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: 커밋**

```bash
git add UnityMcpBridge/Tools/GetEditorStateTool.cs
git commit -m "feat(bridge): add unity_get_editor_state tool"
```

---

## Task 3: TCP 프로토콜 헬퍼 모듈 `scripts/unity-client.js`

**Files:**
- Create: `scripts/unity-client.js`

**배경:** `check-unity.js`, `post-tool-check.js`가 모두 Unity Editor TCP 서버와 직접 통신해야 한다 (Bridge를 경유하지 않음 — Bridge 시작 전에도 동작해야 하므로). 4바이트 big-endian length prefix + JSON 프로토콜을 캡슐화한다.

- [ ] **Step 1: 헬퍼 모듈 작성**

파일 `scripts/unity-client.js`:

```javascript
'use strict';

const net = require('net');

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

module.exports = { tcpPing, sendRequest, getEditorState };
```

- [ ] **Step 2: 수동 검증 — Unity Editor 실행 상태에서 ping 테스트**

Unity Editor가 실행 중이고 UnityMcpEditor가 로드된 상태에서:

```bash
node -e "const {tcpPing} = require('./scripts/unity-client'); tcpPing(9876).then(ok => console.log('ping:', ok));"
```

Expected: `ping: true`

- [ ] **Step 3: 수동 검증 — editor state 조회**

```bash
node -e "const {getEditorState} = require('./scripts/unity-client'); getEditorState(9876).then(s => console.log(s)).catch(e => console.error(e.message));"
```

Expected: `{ isCompiling: false, isUpdating: false, isPlaying: false, unityVersion: '...', projectName: '...' }`

(Task 1, 2가 Unity에 반영되지 않았다면 `unknown tool` 에러가 발생한다. 그 경우 Unity를 재시작하여 Editor 핸들러를 리로드한 뒤 재실행)

- [ ] **Step 4: 커밋**

```bash
git add scripts/unity-client.js
git commit -m "feat(plugin): add TCP client helper for hook scripts"
```

---

## Task 4: `scripts/check-unity.js` — 연결/상태 체크 + 대기 루프

**Files:**
- Create: `scripts/check-unity.js`

- [ ] **Step 1: check-unity.js 작성**

파일 `scripts/check-unity.js`:

```javascript
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
```

- [ ] **Step 2: 수동 검증 — session-start 모드 (Unity 실행 중)**

```bash
node scripts/check-unity.js --mode=session-start
```

Expected stderr: `[Unity MCP] 연결됨 — <projectName> (Unity <version>)`
Expected exit code: 0

- [ ] **Step 3: 수동 검증 — session-start 모드 (Unity 꺼짐)**

Unity Editor 종료 후:
```bash
node scripts/check-unity.js --mode=session-start
```

Expected stderr: `[Unity MCP] Unity Editor 연결 실패 — ...`
Expected exit code: 0

- [ ] **Step 4: 수동 검증 — pre-tool 모드 (Unity 꺼짐, 짧은 대기)**

```bash
UNITY_MAX_WAIT_SEC=3 node scripts/check-unity.js --mode=pre-tool
```

Expected stderr: 매 초마다 대기 로그 → 3초 후 `대기 시간 초과 (3s)`
Expected exit code: 1

- [ ] **Step 5: 수동 검증 — pre-tool 모드 (Unity 켜짐)**

```bash
node scripts/check-unity.js --mode=pre-tool
```

Expected stderr: (출력 없음)
Expected exit code: 0

- [ ] **Step 6: 커밋**

```bash
git add scripts/check-unity.js
git commit -m "feat(plugin): add check-unity hook script with wait-and-retry"
```

---

## Task 5: `scripts/post-tool-check.js` — 후처리 알림

**Files:**
- Create: `scripts/post-tool-check.js`

- [ ] **Step 1: post-tool-check.js 작성**

파일 `scripts/post-tool-check.js`:

```javascript
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
```

**주:** 새 콘솔 에러 요약은 현재 Bridge 프로토콜에서 "이전 호출 이후 신규 에러만" 필터링할 방법이 명확하지 않다. 단순화를 위해 이번 태스크에서는 제외하고, 필요 시 후속 작업으로 분리한다.

- [ ] **Step 2: 수동 검증 — auto-save 끄고 실행 (no-op)**

```bash
node scripts/post-tool-check.js --auto-save=false
```

Expected: 출력 없음, exit 0

- [ ] **Step 3: 수동 검증 — auto-save 켜고 실행**

Unity Editor 실행 중:
```bash
node scripts/post-tool-check.js --auto-save=true
```

Expected stderr: `[Unity MCP] 씬 자동 저장 완료`
Expected exit code: 0

- [ ] **Step 4: 커밋**

```bash
git add scripts/post-tool-check.js
git commit -m "feat(plugin): add post-tool-check hook script with auto-save"
```

---

## Task 6: `scripts/run-bridge.js` — 바이너리 실행 래퍼

**Files:**
- Create: `scripts/run-bridge.js`

**배경:** 기존 `npm/bin/run.js`의 로직을 플러그인용으로 이식한다. `${CLAUDE_PLUGIN_DATA}/bin/`에 바이너리가 있으면 실행, 없으면 GitHub Release에서 lazy download, 실패 시 `npx -y unity-mcp-bridge` fallback.

- [ ] **Step 1: 기존 run.js 참고용으로 읽기**

Run: `node -e "console.log(require('fs').readFileSync('npm/bin/run.js','utf8'))"`

출력을 참고하되 플러그인 환경에 맞게 경로를 재구성한다.

- [ ] **Step 2: run-bridge.js 작성**

파일 `scripts/run-bridge.js`:

```javascript
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
  const archiveName = `UnityMcpBridge-${rid}.${archiveExt}`;
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
```

- [ ] **Step 3: 수동 검증 — bin 없는 상태에서 npx fallback 확인**

```bash
# .data 폴더 비우기
rm -rf .data

# 네트워크 차단 환경 시뮬레이션 어려우므로 CLAUDE_PLUGIN_DATA로 격리
CLAUDE_PLUGIN_DATA=$(mktemp -d) node scripts/run-bridge.js
```

Expected: 다운로드 시도 로그 → 성공 시 바이너리 실행, 실패 시 npx fallback 로그 후 브릿지 시작. 확인되면 `Ctrl+C`로 종료.

- [ ] **Step 4: 커밋**

```bash
git add scripts/run-bridge.js
git commit -m "feat(plugin): add run-bridge wrapper with lazy download + npx fallback"
```

---

## Task 7: Agent — `unity-scene-architect`

**Files:**
- Create: `agents/unity-scene-architect.md`

- [ ] **Step 1: 에이전트 파일 작성**

파일 `agents/unity-scene-architect.md`:

```markdown
---
name: unity-scene-architect
description: Unity 씬을 설계하고 구성하는 전문 에이전트 — 계층 구조, 컴포넌트 배치, 프리팹 활용
model: sonnet
maxTurns: 30
tools: ["mcp__unity-bridge__*", "Read", "Grep", "Glob"]
skills: ["unity-mcp:unity-scene-setup", "unity-mcp:unity-prefab-workflow", "unity-mcp:unity-ui-build"]
---

Unity 씬을 설계하고 구성하는 전문 에이전트입니다.

## 역할
- 씬 계층 구조 설계 및 구현
- GameObject 생성, 컴포넌트 추가, Transform 배치
- Prefab 인스턴스화 및 편집
- UI 구축 (UGUI, UI Toolkit)

## 작업 방식
1. 현재 씬 상태를 `unity_get_hierarchy`로 파악
2. 사용자 요구에 맞는 구조 설계
3. 단계적으로 구현 (생성 → 계층 → 컴포넌트 → 프로퍼티)
4. 완료 후 검증 (hierarchy 재확인, screenshot)

## 제약
- 모든 변경은 Undo 가능하도록 수행
- 순환 부모-자식 관계 금지
- Transform 컴포넌트 삭제 금지
- 생성된 오브젝트의 `instanceId`를 후속 작업에 재사용하여 경로 변경에 영향 받지 않게 한다
```

- [ ] **Step 2: 커밋**

```bash
git add agents/unity-scene-architect.md
git commit -m "feat(plugin): add unity-scene-architect agent"
```

---

## Task 8: Agent — `unity-debugger`

**Files:**
- Create: `agents/unity-debugger.md`

- [ ] **Step 1: 에이전트 파일 작성**

파일 `agents/unity-debugger.md`:

```markdown
---
name: unity-debugger
description: Unity 프로젝트를 진단하고 디버깅하는 전문 에이전트 — 에러 추적, Play Mode 검사, 성능 분석
model: sonnet
maxTurns: 20
tools: ["mcp__unity-bridge__*", "Read", "Grep", "Glob", "Bash"]
skills: ["unity-mcp:unity-debug"]
---

Unity 프로젝트의 문제를 진단하고 해결하는 전문 에이전트입니다.

## 역할
- 컴파일 에러 및 콘솔 로그 분석
- Play Mode 진입 후 런타임 상태 검사
- 스크린샷 기반 시각적 검증
- Animator 상태 및 UI 상태 확인

## 작업 방식
1. `unity_ping`으로 연결 상태 확인
2. `unity_get_console_logs`로 에러/경고 수집
3. 에러 원인 추적 (코드 읽기, 컴포넌트 검사)
4. Play Mode에서 런타임 동작 확인 (`unity_play_mode` enter → 관찰 → exit)
5. 수정 방안 제시 또는 직접 수정

## 제약
- Play Mode 전환 시 도메인 리로드로 연결이 일시 끊김 → 자동 재연결 대기
- 스크린샷은 `maxWidth`로 해상도 제한하여 토큰 절약
- StackTrace는 필요할 때만 `includeStackTrace=true`로 포함
- `unity_get_console_logs` 기본 반환 수 50, 필요 시 `logType="Error"`로 필터링
```

- [ ] **Step 2: 커밋**

```bash
git add agents/unity-debugger.md
git commit -m "feat(plugin): add unity-debugger agent"
```

---

## Task 9: Agent — `unity-asset-manager`

**Files:**
- Create: `agents/unity-asset-manager.md`

- [ ] **Step 1: 에이전트 파일 작성**

파일 `agents/unity-asset-manager.md`:

```markdown
---
name: unity-asset-manager
description: Unity 에셋을 관리하는 전문 에이전트 — Material, Prefab, Addressable, 패키지 관리
model: sonnet
maxTurns: 25
tools: ["mcp__unity-bridge__*", "Read", "Grep", "Glob"]
skills: ["unity-mcp:unity-material-setup", "unity-mcp:unity-prefab-workflow", "unity-mcp:unity-build-deploy"]
---

Unity 에셋 파이프라인을 관리하는 전문 에이전트입니다.

## 역할
- Material 생성 및 셰이더 프로퍼티 설정
- Prefab 워크플로우 (생성, 편집, 인스턴스화)
- Addressable 에셋 등록 및 주소 관리
- 에셋 검색, 이동, 복사, 삭제
- UPM 패키지 관리
- 프로젝트 빌드

## 작업 방식
1. `unity_find_assets`로 기존 에셋 탐색
2. 필요한 에셋 생성/수정
3. Addressable 필요 시 등록
4. `unity_refresh_assets`로 에셋 DB 갱신

## 제약
- Addressable 작업은 Undo 미지원이므로 신중히 처리
- 에셋 삭제 전 `dryRun=true`로 영향 범위 확인
- 빌드 전 `unity_get_compile_errors`로 컴파일 에러 확인
- Material 셰이더는 현재 렌더 파이프라인에 맞게 선택 (Standard / URP-Lit)
```

- [ ] **Step 2: 커밋**

```bash
git add agents/unity-asset-manager.md
git commit -m "feat(plugin): add unity-asset-manager agent"
```

---

## Task 10: `hooks/hooks.json`

**Files:**
- Create: `hooks/hooks.json`

- [ ] **Step 1: hooks.json 작성**

파일 `hooks/hooks.json`:

```json
{
  "hooks": [
    {
      "event": "SessionStart",
      "hooks": [
        {
          "type": "command",
          "command": "node ${CLAUDE_PLUGIN_ROOT}/scripts/check-unity.js --mode=session-start"
        }
      ]
    },
    {
      "event": "PreToolUse",
      "matcher": {
        "toolName": "mcp__unity-bridge__*"
      },
      "hooks": [
        {
          "type": "command",
          "command": "node ${CLAUDE_PLUGIN_ROOT}/scripts/check-unity.js --mode=pre-tool --check-compile=${userConfig.check_compile_status} --check-reload=${userConfig.check_domain_reload}"
        }
      ]
    },
    {
      "event": "PostToolUse",
      "matcher": {
        "toolName": "mcp__unity-bridge__unity_create_gameobject|mcp__unity-bridge__unity_delete_gameobject|mcp__unity-bridge__unity_add_component|mcp__unity-bridge__unity_remove_component|mcp__unity-bridge__unity_set_property|mcp__unity-bridge__unity_set_transform|mcp__unity-bridge__unity_reparent_gameobject|mcp__unity-bridge__unity_instantiate_prefab"
      },
      "hooks": [
        {
          "type": "command",
          "command": "node ${CLAUDE_PLUGIN_ROOT}/scripts/post-tool-check.js --auto-save=${userConfig.auto_save_scene}"
        }
      ]
    },
    {
      "event": "PostToolUseFailure",
      "matcher": {
        "toolName": "mcp__unity-bridge__*"
      },
      "hooks": [
        {
          "type": "command",
          "command": "node ${CLAUDE_PLUGIN_ROOT}/scripts/check-unity.js --mode=failure-diagnosis"
        }
      ]
    }
  ]
}
```

- [ ] **Step 2: JSON 문법 검증**

Run: `node -e "JSON.parse(require('fs').readFileSync('hooks/hooks.json','utf8'))"`
Expected: 출력 없음 (파싱 성공)

- [ ] **Step 3: 커밋**

```bash
git add hooks/hooks.json
git commit -m "feat(plugin): add hooks for unity state detection"
```

---

## Task 11: `.claude-plugin/plugin.json` 업데이트

**Files:**
- Modify: `.claude-plugin/plugin.json`

- [ ] **Step 1: plugin.json 전체 재작성**

파일 `.claude-plugin/plugin.json`을 다음으로 **완전히 교체**한다:

```json
{
  "name": "unity-mcp",
  "description": "AI 에이전트가 Unity Editor를 제어하는 MCP 브릿지 — 씬 편집, 에셋 관리, 빌드 자동화",
  "version": "0.3.0",
  "author": {
    "name": "breadpack",
    "url": "https://github.com/breadpack"
  },
  "homepage": "https://github.com/breadpack/UnityMcp",
  "repository": "https://github.com/breadpack/UnityMcp",
  "license": "MIT",
  "keywords": ["unity", "mcp", "gamedev", "editor", "scene", "build"],
  "mcpServers": {
    "unity-bridge": {
      "command": "node",
      "args": ["${CLAUDE_PLUGIN_ROOT}/scripts/run-bridge.js"],
      "env": {
        "UNITY_TCP_PORT": "${userConfig.unity_tcp_port}",
        "UNITY_MAX_WAIT_SEC": "${userConfig.max_wait_seconds}"
      }
    }
  },
  "userConfig": {
    "unity_tcp_port": {
      "description": "Unity TCP 포트 (기본: 9876)",
      "sensitive": false
    },
    "auto_save_scene": {
      "description": "씬 변경 후 자동 저장 여부 (true/false, 기본: false)",
      "sensitive": false
    },
    "check_compile_status": {
      "description": "도구 호출 전 컴파일 상태 체크 여부 (true/false, 기본: true)",
      "sensitive": false
    },
    "check_domain_reload": {
      "description": "도구 호출 전 도메인 리로드 상태 체크 여부 (true/false, 기본: true)",
      "sensitive": false
    },
    "max_wait_seconds": {
      "description": "컴파일/리로드 대기 최대 시간 (초, 기본: 60)",
      "sensitive": false
    }
  }
}
```

**주:** 이전 v0.2.0에서는 `mcpServers: "./.mcp.json"` 참조 방식이었다. 이제 인라인으로 통합되므로 `.mcp.json` 파일은 남겨두되 플러그인 manifest가 우선한다 (다음 메이저 버전에서 제거 예정).

- [ ] **Step 2: JSON 문법 검증**

Run: `node -e "JSON.parse(require('fs').readFileSync('.claude-plugin/plugin.json','utf8'))"`
Expected: 출력 없음

- [ ] **Step 3: 커밋**

```bash
git add .claude-plugin/plugin.json
git commit -m "feat(plugin): upgrade plugin.json to v0.3.0 with mcpServers+userConfig"
```

---

## Task 12: `.gitignore` 업데이트

**Files:**
- Modify: `.gitignore`

**배경:** `scripts/run-bridge.js`가 `CLAUDE_PLUGIN_DATA` 미설정 시 fallback으로 `pluginRoot/.data/` 를 사용한다. 이 디렉토리가 git에 커밋되지 않도록 한다. 또한 루트 `bin/` 패턴은 이미 `[Bb]in/`로 매칭되지만, 명시성을 위해 주석 추가.

- [ ] **Step 1: .gitignore 수정**

`.gitignore` 끝에 다음 블록 추가:

```
# Plugin runtime data (lazy-downloaded binaries, caches)
.data/

# Plugin bin/ is also ignored by the [Bb]in/ pattern above
```

Run the edit via Edit tool:
- old_string: `npm/bin/UnityMcpBridge*
npm/bin/_tmp*
node_modules/`
- new_string: `npm/bin/UnityMcpBridge*
npm/bin/_tmp*
node_modules/

# Plugin runtime data (lazy-downloaded binaries, caches)
.data/`

- [ ] **Step 2: 커밋**

```bash
git add .gitignore
git commit -m "chore: ignore plugin runtime data directory"
```

---

## Task 13: README.md 업데이트

**Files:**
- Modify: `README.md`

- [ ] **Step 1: README.md 읽기**

현재 README를 읽고 "설치" 섹션 위치를 파악한다. Run: `node -e "console.log(require('fs').readFileSync('README.md','utf8').slice(0, 3000))"`

- [ ] **Step 2: Claude Code 플러그인 설치 섹션 추가**

기존 설치 섹션 **위에** 다음 블록을 삽입한다 (또는 설치 섹션이 없으면 개요 바로 아래):

```markdown
## Claude Code 플러그인으로 설치 (권장)

Claude Code에서 한 줄로 설치:

\`\`\`
/plugin install --from github:breadpack/UnityMcp
\`\`\`

또는 프로젝트 `.claude/settings.json`에 등록:

\`\`\`json
{
  "enabledPlugins": {
    "unity-mcp": {
      "source": {
        "source": "github",
        "repo": "breadpack/UnityMcp"
      }
    }
  }
}
\`\`\`

설치 시 포트, 자동 저장, 컴파일/리로드 체크 등의 옵션을 프롬프트로 설정할 수 있다.

포함 내용:
- MCP 서버 (unity-bridge) — 45+ Unity 도구
- Skills 6개 — 워크플로우 가이드
- Agents 3개 — unity-scene-architect, unity-debugger, unity-asset-manager
- Hooks — 컴파일/도메인 리로드 자동 감지 및 대기
```

**주:** 위 마크다운 코드 블록 내부의 백틱은 실제 파일에 쓸 때 이스케이프 없이 그대로 써야 한다. 구현자는 Edit 도구의 insert 시 위 블록의 `\`\`\``를 일반 ````` 로 바꿔서 넣는다.

- [ ] **Step 3: 커밋**

```bash
git add README.md
git commit -m "docs: add Claude Code plugin install section"
```

---

## Task 14: CLAUDE.md 업데이트

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: 플러그인 구조 섹션 추가**

`CLAUDE.md`의 "Architecture" 섹션 **아래에** 다음 섹션을 삽입한다:

```markdown
## Plugin Structure

이 저장소는 Claude Code 플러그인으로도 동작한다 (`.claude-plugin/plugin.json` v0.3.0).

- **agents/** — 전문 에이전트 3종 (scene-architect, debugger, asset-manager). 각 에이전트는 특정 skills와 `mcp__unity-bridge__*` 도구를 번들한다.
- **hooks/hooks.json** — SessionStart/PreToolUse/PostToolUse/PostToolUseFailure 훅. `scripts/check-unity.js`를 호출해 Unity 컴파일/도메인 리로드 상태를 감지하고, 진행 중이면 대기 루프로 완료를 기다린다.
- **scripts/** — 훅 스크립트와 브릿지 실행 래퍼 (`run-bridge.js`는 `${CLAUDE_PLUGIN_DATA}/bin/`의 번들 바이너리 → GitHub Release lazy download → `npx -y unity-mcp-bridge` 순으로 fallback).
- **skills/** — 워크플로우 가이드 6종 (유지).

플러그인 매니페스트의 `userConfig`로 `unity_tcp_port`, `auto_save_scene`, `check_compile_status`, `check_domain_reload`, `max_wait_seconds`를 사용자가 설치 시 설정한다. 이 값들은 `${userConfig.xxx}` 치환으로 `mcpServers.env`와 `hooks` 커맨드 인자에 전달된다.

### 새 hook 스크립트 추가 시
- TCP 통신은 `scripts/unity-client.js`의 `tcpPing`/`sendRequest`/`getEditorState`를 재사용
- Bridge가 기동 전/중단 상태일 수 있으므로 Editor TCP 서버(9876)와 직접 통신한다 (Bridge 경유 X)
- 에러는 stderr에 `[Unity MCP] ...` 형식으로 출력, exit code는 차단 목적(1) vs 정보성(0) 구분
```

- [ ] **Step 2: 커밋**

```bash
git add CLAUDE.md
git commit -m "docs: add plugin structure section to CLAUDE.md"
```

---

## Task 15: 전체 통합 검증

**Files:** (없음, 실행만)

**목표:** 플러그인을 로컬로 로드해 모든 구성요소가 정상 동작하는지 확인한다.

- [ ] **Step 1: Bridge 빌드 확인**

Run: `dotnet build UnityMcpBridge/UnityMcpBridge.csproj`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 2: 플러그인 구조 유효성 검사 (JSON 파싱)**

Run:
```bash
node -e "
const fs = require('fs');
const files = [
  '.claude-plugin/plugin.json',
  'hooks/hooks.json',
];
for (const f of files) {
  try { JSON.parse(fs.readFileSync(f, 'utf8')); console.log('OK:', f); }
  catch (e) { console.error('FAIL:', f, e.message); process.exit(1); }
}
"
```

Expected: 각 파일마다 `OK: ...` 출력

- [ ] **Step 3: 로컬 플러그인 로드 (Unity Editor 실행 상태에서)**

Unity Editor에 UnityMcpEditor 플러그인이 설치되어 있고, 새로 빌드한 `GetEditorStateHandler`가 로드되었는지 확인한다 (Unity Console에 `[MCP] Server started on port 9876` 메시지).

그 후 별도 터미널에서:
```bash
claude --plugin-dir .
```

Expected 동작:
- SessionStart hook이 실행되어 `[Unity MCP] 연결됨 — <projectName> (Unity <version>)` 메시지 출력
- `/agents` 실행 시 `unity-scene-architect`, `unity-debugger`, `unity-asset-manager` 3개 노출
- `/help` 또는 skills 목록에 6개 unity-mcp 스킬 노출
- MCP 도구 목록에 `mcp__unity-bridge__*` 45+ 개 노출

- [ ] **Step 4: MCP 도구 호출 + Hook 동작 확인**

위 세션에서:
```
unity_ping을 호출해줘
```

Expected:
- PreToolUse hook이 실행 → 빠르게 통과 (연결 OK)
- `unity_ping` 응답 성공
- 도구 실행 후 아무 문제 없음

- [ ] **Step 5: 컴파일 대기 시나리오 검증**

Unity Editor에서 C# 스크립트를 임의로 수정 후 저장하여 컴파일을 트리거한다. 컴파일 진행 중일 때 Claude 세션에서 MCP 도구를 호출:

```
unity_get_hierarchy를 호출해줘
```

Expected:
- PreToolUse hook이 컴파일 감지 → `[Unity MCP] 컴파일/리로드 대기 중... (Ns)` 로그 출력
- 컴파일 완료 시 `[Unity MCP] 준비 완료 — 도구 실행 재개` 로그 후 실제 도구 실행
- 결과 정상 반환

- [ ] **Step 6: 최종 커밋 (검증 통과 시 태그 준비)**

모든 검증이 통과되면:
```bash
git log --oneline -20
```

Task 1~14가 커밋되어 있는지 확인. 만약 통합 검증 중 수정 사항이 발견되면 해당 태스크로 돌아가 수정 후 재커밋한다.

- [ ] **Step 7: 완료 보고**

이 태스크의 완료 시 다음을 출력한다:
- 빌드 성공 여부
- 플러그인 로드 성공 여부
- SessionStart hook 메시지
- Agents 목록
- MCP 도구 개수
- 컴파일 대기 시나리오 동작 여부

검증이 통과하면 v0.3.0 릴리스 준비가 완료된 것이다. Release 태그 및 바이너리 빌드는 기존 `.github/workflows/publish.yml`이 `v*` 태그에서 자동 처리한다.

---

## 자체 검토 체크리스트

- [x] **스펙 커버리지**: 스펙의 모든 섹션(디렉토리 구조, plugin.json, agents 3개, hooks 4종, scripts 4개, Bridge/Editor 신규 도구, bin lazy download, 마이그레이션, 테스트)이 Task 1~15에 매핑됨
- [x] **플레이스홀더 없음**: TODO/TBD/"적절한 에러 처리" 등의 지시어 없음, 모든 코드 블록이 완전함
- [x] **타입 일관성**: `unity_get_editor_state` 반환 필드(`isCompiling`, `isUpdating`, `isPlaying`, `unityVersion`, `projectName`)가 Task 1(Handler), Task 2(Bridge Tool), Task 3(unity-client.js의 getEditorState 소비), Task 4(check-unity.js에서 참조)에서 모두 동일하게 사용됨
- [x] **파일 경로**: 모든 태스크가 정확한 상대 경로를 명시함
- [x] **TDD 대체**: 테스트 프레임워크 부재 사유를 헤더에 명시했고, 각 태스크는 수동 검증 단계로 구성됨
- [x] **DRY**: TCP 프로토콜 로직을 `scripts/unity-client.js`로 분리하여 `check-unity.js`와 `post-tool-check.js`가 재사용
- [x] **YAGNI**: post-tool-check.js의 "신규 콘솔 에러 요약"은 프로토콜 복잡도 때문에 제외하고 후속 작업으로 분리한다고 명시
