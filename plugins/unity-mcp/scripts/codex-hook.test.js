"use strict";

const assert = require("assert");
const path = require("path");
const { spawnSync } = require("child_process");
const { readConfig, resolveHook, buildArgv } = require("./codex-hook");

const pluginRoot = path.resolve(__dirname, "..");
const hooksFile = require("../hooks/codex-hooks.json");
const codexManifest = require("../.codex-plugin/plugin.json");
const claudeManifest = require("../.claude-plugin/plugin.json");

function testDefaultsMatchClaudeUserConfig() {
  const config = readConfig({});
  const userConfig = claudeManifest.userConfig;
  assert.strictEqual(config.autoSave, userConfig.auto_save_scene.default);
  assert.strictEqual(config.checkCompile, userConfig.check_compile_status.default);
  assert.strictEqual(config.checkReload, userConfig.check_domain_reload.default);
  assert.strictEqual(config.autoTick, userConfig.auto_tick.default);
}

function testEnvironmentOverrides() {
  const config = readConfig({
    UNITY_MCP_AUTO_SAVE_SCENE: "true",
    UNITY_MCP_CHECK_COMPILE_STATUS: "0",
    UNITY_MCP_CHECK_DOMAIN_RELOAD: "off",
    UNITY_MCP_AUTO_TICK: "no",
  });
  assert.deepStrictEqual(config, {
    autoSave: true,
    checkCompile: false,
    checkReload: false,
    autoTick: false,
  });
  assert.strictEqual(readConfig({ UNITY_MCP_AUTO_TICK: "garbage" }).autoTick, true);
}

function testResolveHookFromEvalAndFileInvocation() {
  assert.strictEqual(resolveHook(["node", "pre-tool"]), "pre-tool");
  assert.strictEqual(resolveHook(["node", "/x/codex-hook.js", "post-tool"]), "post-tool");
  assert.strictEqual(resolveHook(["node", "unknown"]), null);
}

function testArgvMatchesClaudeHookArguments() {
  const config = readConfig({});
  assert.deepStrictEqual(buildArgv("pre-tool", config), [
    "--mode=pre-tool",
    "--check-compile=true",
    "--check-reload=true",
  ]);
  assert.deepStrictEqual(buildArgv("session-start", config), [
    "--mode=session-start",
    "--auto-tick=true",
  ]);
  assert.deepStrictEqual(buildArgv("post-tool", config), ["--auto-save=false"]);
}

function testManifestPointsToCodexHooks() {
  assert.strictEqual(codexManifest.hooks, "./hooks/codex-hooks.json");
  const text = JSON.stringify(hooksFile);
  assert.ok(!text.includes("${CLAUDE_PLUGIN_ROOT}"), "Codex hooks must not rely on Claude substitution");
  assert.ok(!text.includes("${userConfig"), "Codex has no userConfig substitution");
}

// codex-hooks.json 의 명령을 그대로 실행해 `node -e` 진입이 실제로 스크립트를 돌리는지 본다.
function testEvalCommandRunsLauncher() {
  const command = hooksFile.hooks.SessionStart[0].hooks[0].command.replace(/ session-start$/, " unknown-hook");
  const result = spawnSync(command, {
    shell: true,
    env: { ...process.env, PLUGIN_ROOT: pluginRoot },
    encoding: "utf8",
  });
  assert.strictEqual(result.status, 0, result.stderr);
  assert.match(result.stderr, /알 수 없는 Codex hook: unknown-hook/);
}

function testEvalCommandRunsPostToolWithoutConnecting() {
  const command = hooksFile.hooks.PostToolUse[0].hooks[0].command;
  const result = spawnSync(command, {
    shell: true,
    env: { ...process.env, PLUGIN_ROOT: pluginRoot, UNITY_MCP_AUTO_SAVE_SCENE: "false" },
    encoding: "utf8",
  });
  assert.strictEqual(result.status, 0, result.stderr);
  assert.strictEqual(result.stdout, "");
}

testDefaultsMatchClaudeUserConfig();
testEnvironmentOverrides();
testResolveHookFromEvalAndFileInvocation();
testArgvMatchesClaudeHookArguments();
testManifestPointsToCodexHooks();
testEvalCommandRunsLauncher();
testEvalCommandRunsPostToolWithoutConnecting();
console.log("codex-hook tests passed");
