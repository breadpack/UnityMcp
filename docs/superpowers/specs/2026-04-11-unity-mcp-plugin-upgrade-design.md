# UnityMcp Claude Code Plugin 업그레이드 설계

- **작성일**: 2026-04-11
- **대상 버전**: UnityMcp v0.3.0
- **목표**: 현재 부분적으로 플러그인화된 UnityMcp를 GitHub 기반 단일 플러그인으로 완성하여, `/plugin install` 한 줄로 설치 가능하게 만든다. Agents, Hooks, userConfig를 추가하여 사용자 경험을 개선한다.

## 배경

현재 UnityMcp는 다음 구성요소를 이미 갖추고 있다:

- `.claude-plugin/plugin.json` (v0.2.0, 기본 메타데이터)
- `.mcp.json` (npx 기반 MCP 서버 등록)
- `skills/` (6개 워크플로우 가이드)
- npm 패키지 `unity-mcp-bridge` (플랫폼별 바이너리 배포)
- GitHub Release 기반 바이너리 배포 파이프라인

부족한 부분은 다음과 같다:

- Agents 없음 — 사용자가 전문 에이전트를 호출할 수 없음
- Hooks 없음 — Unity의 컴파일/도메인 리로드 상태를 사전 감지하지 못해 도구 호출이 타임아웃되는 문제
- userConfig 없음 — 포트, 자동 저장 등을 설치 시 설정 불가
- 플러그인 설치 경로 불명확 — 사용자가 `.mcp.json`을 수동 복사하거나 npm으로 설정해야 함

## 비목표

- 기존 npm 패키지(`unity-mcp-bridge`) 제거 — 독립 배포 경로로 유지
- 마켓플레이스 구성 — 단일 플러그인에는 오버스펙, 향후 플러그인이 늘어나면 추가
- Bridge/Editor 아키텍처 변경 — 신규 핸들러 추가만 수행

## 디렉토리 구조

```
UnityMcp/
├── .claude-plugin/
│   └── plugin.json              # 플러그인 매니페스트 (mcpServers, userConfig 인라인)
├── agents/
│   ├── unity-scene-architect.md
│   ├── unity-debugger.md
│   └── unity-asset-manager.md
├── hooks/
│   └── hooks.json
├── scripts/
│   ├── run-bridge.js            # 바이너리 우선 → lazy download → npx fallback
│   ├── check-unity.js           # 연결 체크 + 컴파일/리로드 대기 루프
│   └── post-tool-check.js       # 씬 변경 후 알림
├── skills/                      # 기존 6개 그대로 유지
│   ├── unity-scene-setup/
│   ├── unity-ui-build/
│   ├── unity-debug/
│   ├── unity-prefab-workflow/
│   ├── unity-material-setup/
│   └── unity-build-deploy/
├── bin/                         # .gitignore, 런타임 다운로드
├── UnityMcpBridge/              # .NET 소스 (기존)
├── UnityMcpEditor/              # Unity 플러그인 (기존)
├── npm/                         # 독립 npm 배포 (기존 유지)
└── CLAUDE.md
```

사용자 설치 플로우:

```
/plugin install --from github:breadpack/UnityMcp
```

또는 `.claude/settings.json`에 `enabledPlugins`로 등록.

## plugin.json

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
      "description": "씬 변경 후 자동 저장 여부 (true/false)",
      "sensitive": false
    },
    "check_compile_status": {
      "description": "도구 호출 전 컴파일 상태 체크 여부 (true/false)",
      "sensitive": false
    },
    "check_domain_reload": {
      "description": "도구 호출 전 도메인 리로드 상태 체크 여부 (true/false)",
      "sensitive": false
    },
    "max_wait_seconds": {
      "description": "컴파일/리로드 대기 최대 시간 (초, 기본: 60)",
      "sensitive": false
    }
  }
}
```

## Agents

### agents/unity-scene-architect.md

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
1. 현재 씬 상태를 unity_get_hierarchy로 파악
2. 사용자 요구에 맞는 구조 설계
3. 단계적으로 구현 (생성 → 계층 → 컴포넌트 → 프로퍼티)
4. 완료 후 검증 (hierarchy 재확인, screenshot)

## 제약
- 모든 변경은 Undo 가능하도록
- 순환 부모-자식 관계 금지
- Transform 컴포넌트 삭제 금지
```

### agents/unity-debugger.md

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
1. unity_ping으로 연결 상태 확인
2. unity_get_console_logs로 에러/경고 수집
3. 에러 원인 추적 (코드 읽기, 컴포넌트 검사)
4. Play Mode에서 런타임 동작 확인
5. 수정 방안 제시 또는 직접 수정

## 제약
- Play Mode 전환 시 재연결 대기 필요
- 스크린샷은 maxWidth로 해상도 제한하여 토큰 절약
- StackTrace는 필요할 때만 포함
```

### agents/unity-asset-manager.md

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
1. unity_find_assets로 기존 에셋 탐색
2. 필요한 에셋 생성/수정
3. Addressable 필요 시 등록
4. unity_refresh_assets로 에셋 DB 갱신

## 제약
- Addressable 작업은 Undo 미지원
- 에셋 삭제 전 dryRun으로 영향 범위 확인
- 빌드 전 컴파일 에러 확인 필수
```

## Hooks

### hooks/hooks.json

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

### Hook별 동작

**SessionStart** — `check-unity.js --mode=session-start`
- TCP ping으로 Unity 연결 확인
- 성공 시: 프로젝트 이름, Unity 버전 출력
- 실패 시: 경고 메시지 출력 (세션 차단하지 않음)

**PreToolUse** — `check-unity.js --mode=pre-tool`
- TCP ping 시도 → 실패하거나 check 옵션 활성 시 대기 루프 진입
- 대기 루프: `unity_get_editor_state`로 `isCompiling`, `isUpdating` 폴링
- 폴링 간격 500ms, 매 1초마다 진행 로그 (stderr)
- 최대 `max_wait_seconds` 초 대기
- 준비 완료 → exit 0 (도구 실행)
- 타임아웃 → exit 1 + 안내 메시지 (도구 차단)

**PostToolUse** (씬 변경 도구 한정) — `post-tool-check.js`
- `auto_save_scene=true`이면 `unity_save_scene` 호출
- 새 콘솔 에러 있으면 요약 출력
- exit 0 고정 (정보 제공만)

**PostToolUseFailure** — `check-unity.js --mode=failure-diagnosis`
- 실행 중 리로드 진입으로 실패한 경우 복구 대기
- 연결 복구되면 "재시도 가능" 안내
- exit 0 고정

### 컴파일/리로드 대응 매트릭스

| 상황 | 감지 | 대응 |
|------|------|------|
| 컴파일 시작 전 (연결 있음) | `isCompiling=true` 조회 | PreToolUse에서 대기 → 완료 후 실행 |
| 리로드 진행 중 (연결 끊김) | TCP ping 실패 | PreToolUse에서 복구 대기 → 실행 |
| 도구 실행 중 리로드 진입 | 실행 실패/타임아웃 | PostToolUseFailure에서 복구 대기 → 재시도 안내 |
| 리로드 완료 후 | TCP 재연결 성공 | 정상 동작 재개 |

## scripts/

### scripts/run-bridge.js

```javascript
#!/usr/bin/env node
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const https = require('https');

const pluginRoot = path.resolve(__dirname, '..');
const dataDir = process.env.CLAUDE_PLUGIN_DATA || path.join(pluginRoot, 'bin');
const exeName = process.platform === 'win32' ? 'UnityMcpBridge.exe' : 'UnityMcpBridge';
const binaryPath = path.join(dataDir, 'bin', exeName);

async function downloadBinary() {
  // GitHub Release에서 현재 플랫폼 바이너리 다운로드
  // RID: win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64
  // URL: https://github.com/breadpack/UnityMcp/releases/download/v{version}/UnityMcpBridge-{rid}.zip
  // 다운로드 후 압축 해제 → binaryPath에 배치
  // 실패 시 throw
}

async function main() {
  if (!fs.existsSync(binaryPath)) {
    try { await downloadBinary(); } catch { /* ignore */ }
  }

  let child;
  if (fs.existsSync(binaryPath)) {
    child = spawn(binaryPath, [], { stdio: 'inherit', env: process.env });
  } else {
    const npxCmd = process.platform === 'win32' ? 'npx.cmd' : 'npx';
    child = spawn(npxCmd, ['-y', 'unity-mcp-bridge'], { stdio: 'inherit', env: process.env });
  }
  child.on('exit', code => process.exit(code ?? 0));
}

main();
```

### scripts/check-unity.js

```javascript
#!/usr/bin/env node
const net = require('net');

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

async function tcpPing(timeoutMs = 1000) {
  return new Promise(resolve => {
    const sock = new net.Socket();
    const done = ok => { sock.destroy(); resolve(ok); };
    sock.setTimeout(timeoutMs);
    sock.once('connect', () => done(true));
    sock.once('error', () => done(false));
    sock.once('timeout', () => done(false));
    sock.connect(port, '127.0.0.1');
  });
}

async function queryEditorState() {
  // TCP로 unity_get_editor_state 호출
  // 반환: { isCompiling, isUpdating, unityVersion, projectName }
  // 4바이트 big-endian length prefix + JSON 프로토콜
}

async function waitForReady() {
  const startMs = Date.now();
  let lastReport = 0;
  while ((Date.now() - startMs) / 1000 < maxWaitSec) {
    const connected = await tcpPing();
    if (connected) {
      const state = await queryEditorState().catch(() => null);
      if (state && !state.isCompiling && !state.isUpdating) return true;
    }
    const elapsed = Math.floor((Date.now() - startMs) / 1000);
    if (elapsed - lastReport >= 1) {
      process.stderr.write(`[Unity MCP] 컴파일/리로드 대기 중... (${elapsed}s)\n`);
      lastReport = elapsed;
    }
    await new Promise(r => setTimeout(r, 500));
  }
  return false;
}

(async () => {
  if (mode === 'session-start') {
    const ok = await tcpPing();
    if (!ok) {
      console.error('[Unity MCP] Unity Editor 연결 실패 — Editor가 실행 중이고 UnityMcpEditor 플러그인이 설치되었는지 확인하세요.');
      process.exit(0);
    }
    const state = await queryEditorState().catch(() => null);
    if (state) console.error(`[Unity MCP] 연결됨 — ${state.projectName} (Unity ${state.unityVersion})`);
    process.exit(0);
  }

  if (mode === 'pre-tool') {
    const connected = await tcpPing();
    if (!connected || checkCompile || checkReload) {
      const ready = await waitForReady();
      if (!ready) {
        console.error(`[Unity MCP] 대기 시간 초과 (${maxWaitSec}s) — 나중에 재시도하세요.`);
        process.exit(1);
      }
    }
    process.exit(0);
  }

  if (mode === 'failure-diagnosis') {
    const ok = await tcpPing();
    if (!ok) {
      console.error('[Unity MCP] 연결 끊김 — 컴파일/리로드가 시작되었을 수 있습니다. 복구 대기 중...');
      await waitForReady();
      console.error('[Unity MCP] 복구 완료 — 재시도 가능');
    }
    process.exit(0);
  }
})();
```

### scripts/post-tool-check.js

```javascript
#!/usr/bin/env node
const args = Object.fromEntries(
  process.argv.slice(2).map(a => {
    const [k, v] = a.replace(/^--/, '').split('=');
    return [k, v ?? 'true'];
  })
);

const autoSave = args['auto-save'] === 'true';

(async () => {
  // 1. dirty 상태 조회 (Bridge 통해)
  // 2. auto_save=true이면 unity_save_scene 자동 호출
  // 3. 새 콘솔 에러 있으면 요약 출력
  process.exit(0);
})();
```

## Bridge/Editor 신규 도구

check-unity.js가 동작하려면 Editor 상태를 조회하는 핸들러가 필요하다.

### Editor 측: GetEditorStateHandler

```csharp
// UnityMcpEditor/Editor/Handlers/GetEditorStateHandler.cs
public class GetEditorStateHandler : IRequestHandler
{
    public string ToolName => "unity_get_editor_state";

    public object Handle(JObject @params)
    {
        return new
        {
            isCompiling = EditorApplication.isCompiling,
            isUpdating = EditorApplication.isUpdating,
            unityVersion = Application.unityVersion,
            projectName = Application.productName,
        };
    }
}
```

`McpServerBootstrap.StartServer()`에 등록. 파라미터리스 생성자이므로 자동 등록 경로로 들어간다.

### Bridge 측: GetEditorState 도구

```csharp
// UnityMcpBridge/Tools/GetEditorState.cs
[McpServerToolType]
public static class GetEditorState
{
    [McpServerTool(Name = "unity_get_editor_state"), Description("Unity Editor의 현재 상태를 조회합니다 (컴파일/리로드 여부, 버전, 프로젝트명)")]
    public static async Task<string> Execute(
        UnityConnection connection,
        CancellationToken ct = default)
    {
        var result = await connection.SendRequestAsync("unity_get_editor_state", "{}", ct);
        return ResponseFormatter.Format(result);
    }
}
```

`WithToolsFromAssembly()`로 자동 등록.

## bin/ 바이너리 배포 전략

플러그인 자체는 바이너리를 번들하지 않는다. `scripts/run-bridge.js`가 필요 시 lazy download한다.

**실행 시 해결 순서:**

1. `${CLAUDE_PLUGIN_DATA}/bin/UnityMcpBridge(.exe)` 존재 확인 → 있으면 실행
2. 없으면 GitHub Release에서 현재 플랫폼 바이너리 다운로드 후 실행
3. 다운로드 실패 시 `npx -y unity-mcp-bridge` fallback

**저장 위치:** `${CLAUDE_PLUGIN_DATA}/bin/` (플러그인 업데이트 시에도 유지)

**버전 관리:** `plugin.json`의 `version`과 일치하는 Release 태그에서 다운로드. 버전 불일치 시 재다운로드.

**장점:**
- 레포 크기 폭증 방지 (플랫폼당 수십 MB 회피)
- 오프라인/다운로드 실패 시에도 npm 경로로 동작
- 플러그인 캐시 교체와 무관하게 바이너리 보존

**.gitignore:**
```
bin/
```

## 마이그레이션 단계

1. **파일 추가 (비파괴)**: agents/, hooks/, scripts/ 생성. 기존 `.mcp.json`, `skills/`, npm은 그대로 유지.
2. **plugin.json 업데이트**: `mcpServers`, `userConfig` 인라인 추가, version → 0.3.0.
3. **Bridge/Editor 신규 도구**: `GetEditorStateHandler`, `GetEditorState` 추가 및 등록.
4. **`.mcp.json` 제거 검토**: plugin.json의 mcpServers가 우선. README 병기 후 다음 메이저 버전에서 제거.
5. **문서 업데이트**: README.md에 `/plugin install` 안내, CLAUDE.md에 agents/hooks 구조 설명.

## 테스트 계획

| 테스트 | 방법 | 기대 결과 |
|--------|------|----------|
| 로컬 플러그인 로드 | `claude --plugin-dir .` | skills/agents 나타남 |
| MCP 서버 기동 | `unity_ping` 호출 | Bridge 시작, ping 응답 |
| SessionStart hook | 세션 시작 로그 | Unity 연결 상태 메시지 출력 |
| PreToolUse 대기 | 코드 수정 후 즉시 도구 호출 | 컴파일 대기 로그 → 완료 후 실행 |
| PreToolUse 타임아웃 | Unity 종료 상태에서 도구 호출 | 60초 후 exit 1, 안내 메시지 |
| PostToolUseFailure | 실행 중 Unity 리로드 트리거 | 복구 대기 → 재시도 안내 |
| Agent 호출 | `@unity-scene-architect 씬 만들어줘` | skills + tools로 작업 진행 |
| 바이너리 lazy download | `bin/` 비운 첫 실행 | Release에서 다운로드 후 실행 |
| npx fallback | 네트워크 차단 후 실행 | npm 경로 fallback 성공 |
| userConfig 반영 | `unity_tcp_port=9880` 설정 | Bridge가 해당 포트로 시도 |

## 롤백 계획

- 각 단계를 독립 커밋으로 분리하여 revert 가능하게 유지.
- 기존 npm 배포(`unity-mcp-bridge`)는 변경하지 않으므로 기존 사용자 영향 없음.
- 문제 발생 시 `plugin.json`을 v0.2.0으로 되돌리면 원상복구.

## 결정 사항 요약

- 단일 플러그인 구조, 마켓플레이스 미도입
- GitHub 레포에서 직접 설치 (`/plugin install --from github:breadpack/UnityMcp`)
- Skills 6개 유지 + Agents 3개 신규 추가
- Hooks 4종 (SessionStart, PreToolUse, PostToolUse, PostToolUseFailure)
- userConfig 5개 필드 (port, auto_save, check_compile, check_reload, max_wait)
- 컴파일/리로드 상태 감지 시 **대기 후 재시도** (차단 아님)
- 바이너리는 lazy download + npx fallback
- 기존 `.mcp.json`, npm 패키지는 병행 유지
