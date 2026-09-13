#!/usr/bin/env node
'use strict';

// Codex 전용 hook 진입점 — hooks/codex-hooks.json 이 부른다.
//
// Codex hook 은 Claude Code 와 두 가지가 다르다:
// 1. `${CLAUDE_PLUGIN_ROOT}` 치환이 없고 PLUGIN_ROOT 환경변수만 준다. 명령 문자열에서
//    셸 치환에 기대면 Windows(cmd/PowerShell)에서 경로가 깨지므로, codex-hooks.json 은
//    `node -e` 로 환경변수를 읽어 이 파일을 require 한다.
// 2. `${userConfig.*}` 가 없다. 설치 옵션 대신 환경변수(UNITY_MCP_*)로 받고,
//    없으면 Claude 매니페스트 userConfig 의 기본값을 쓴다.
// 또 Codex 에는 PostToolUseFailure 가 없어 failure-diagnosis 는 PostToolUse 에서 돈다
// (연결이 살아 있으면 상태 조회 한 번으로 끝난다).

const HOOKS = {
  'session-start': {
    script: './check-unity',
    argv: config => ['--mode=session-start', `--auto-tick=${config.autoTick}`],
  },
  'pre-tool': {
    script: './check-unity',
    argv: config => [
      '--mode=pre-tool',
      `--check-compile=${config.checkCompile}`,
      `--check-reload=${config.checkReload}`,
    ],
  },
  'post-tool': {
    script: './post-tool-check',
    argv: config => [`--auto-save=${config.autoSave}`],
  },
  'failure-diagnosis': {
    script: './check-unity',
    argv: () => ['--mode=failure-diagnosis'],
  },
};

function readBool(env, name, fallback) {
  const value = (env[name] || '').trim().toLowerCase();
  if (['1', 'true', 'on', 'yes'].includes(value)) return true;
  if (['0', 'false', 'off', 'no'].includes(value)) return false;
  return fallback;
}

// 기본값은 .claude-plugin/plugin.json 의 userConfig default 와 같다.
function readConfig(env = process.env) {
  return {
    autoSave: readBool(env, 'UNITY_MCP_AUTO_SAVE_SCENE', false),
    checkCompile: readBool(env, 'UNITY_MCP_CHECK_COMPILE_STATUS', true),
    checkReload: readBool(env, 'UNITY_MCP_CHECK_DOMAIN_RELOAD', true),
    autoTick: readBool(env, 'UNITY_MCP_AUTO_TICK', true),
  };
}

// `node -e "..." <hook>` 로 불리면 process.argv 는 [node, <hook>] 이고,
// `node codex-hook.js <hook>` 로 불리면 [node, file, <hook>] 이다 — 이름으로 찾는다.
function resolveHook(argv) {
  return argv.slice(1).find(arg => Object.prototype.hasOwnProperty.call(HOOKS, arg)) || null;
}

function buildArgv(hook, config) {
  return HOOKS[hook].argv(config);
}

async function run({ argv = process.argv, env = process.env } = {}) {
  const hook = resolveHook(argv);
  if (!hook) {
    process.stderr.write(`[Unity MCP] 알 수 없는 Codex hook: ${argv.slice(1).join(' ')}\n`);
    return 0;
  }
  const { script } = HOOKS[hook];
  // 대상 스크립트는 모듈 로드 시점에 process.argv 를 파싱하므로 require 전에 바꾼다.
  process.argv = [argv[0], require.resolve(script), ...buildArgv(hook, readConfig(env))];
  const target = require(script);
  if (script === './post-tool-check') {
    await target.run();
    return 0;
  }
  return target.main();
}

// `node -e` 에서 require 되면 require.main 이 없고 부모가 [eval] 이다.
if (require.main === module || (module.parent && module.parent.id === '[eval]')) {
  run()
    .then(code => {
      process.exitCode = code;
    })
    .catch(error => {
      process.stderr.write(`[Unity MCP] ${error.stack || error.message}\n`);
      process.exitCode = 1;
    });
}

module.exports = { HOOKS, readConfig, resolveHook, buildArgv };
