"use strict";

const assert = require("assert");
const fs = require("fs");
const os = require("os");
const path = require("path");
const {
  buildVersionMismatchContext,
  runSessionStart,
} = require("./check-unity");
const {
  extractSemanticVersion,
  readUnityPackageVersion,
} = require("./version-utils");

function testMatchingVersionsProduceNoContext() {
  assert.strictEqual(buildVersionMismatchContext("0.6.26", "0.6.26"), null);
}

function testOlderUnityPackageRecommendsExactTag() {
  const context = buildVersionMismatchContext("0.6.26", "0.6.25");

  assert.match(context, /Agent plugin: v0\.6\.26/);
  assert.match(context, /Unity package: v0\.6\.25/);
  assert.match(context, /exact tag v0\.6\.26/);
  assert.match(context, /explicit user approval/);
}

function testNewerUnityPackageRecommendsPluginUpdate() {
  const context = buildVersionMismatchContext("0.6.25", "0.6.26");

  assert.match(context, /agent plugin to v0\.6\.26/);
  assert.match(context, /do not automatically downgrade/);
}

function testPinnedGitPackageVersionIsParsed() {
  assert.strictEqual(
    extractSemanticVersion("https://github.com/breadpack/UnityMcp.git?path=UnityMcpEditor#v0.6.25"),
    "0.6.25"
  );
}

function testOlderPackageFallbackReadsPackagesLock() {
  const projectPath = fs.mkdtempSync(path.join(os.tmpdir(), "unity-mcp-version-"));
  const packagesPath = path.join(projectPath, "Packages");

  try {
    fs.mkdirSync(packagesPath);
    fs.writeFileSync(
      path.join(packagesPath, "packages-lock.json"),
      JSON.stringify({
        dependencies: {
          "com.breadpack.unity-mcp": {
            version: "https://github.com/breadpack/UnityMcp.git?path=UnityMcpEditor#v0.6.24",
          },
        },
      })
    );

    assert.strictEqual(readUnityPackageVersion(projectPath), "0.6.24");
  } finally {
    fs.rmSync(projectPath, { recursive: true, force: true });
  }
}

async function testSessionStartUsesStatePackageVersion() {
  const contexts = [];
  const fallbackReads = [];

  await runSessionStart({
    port: 9876,
    workspaceDir: "D:\\Projects\\Example",
    tcpPingFn: async () => true,
    sendRequestFn: async () => ({}),
    getEditorStateFn: async () => ({
      projectName: "Example",
      projectPath: "D:\\Projects\\Example",
      unityVersion: "6000.0.51f1",
      packageVersion: "0.6.25",
    }),
    readPluginVersionFn: () => "0.6.26",
    readUnityPackageVersionFn: projectPath => fallbackReads.push(projectPath),
    logFn: () => {},
    contextFn: context => contexts.push(context),
  });

  // TCP 경로: [Unity connection] 안내 + 버전 불일치 컨텍스트
  assert.strictEqual(contexts.length, 2);
  assert.match(contexts[0], /unity pipeline install/);
  assert.match(contexts[1], /version mismatch/);
  assert.strictEqual(fallbackReads.length, 0);
}

async function testSessionStartFallsBackForOlderPackage() {
  const contexts = [];

  await runSessionStart({
    port: 9876,
    workspaceDir: "D:\\Projects\\Example",
    tcpPingFn: async () => true,
    sendRequestFn: async () => ({}),
    getEditorStateFn: async () => ({
      projectName: "Example",
      projectPath: "D:\\Projects\\Example",
      unityVersion: "6000.0.51f1",
    }),
    readPluginVersionFn: () => "0.6.26",
    readUnityPackageVersionFn: projectPath => {
      assert.strictEqual(projectPath, "D:\\Projects\\Example");
      return "0.6.25";
    },
    logFn: () => {},
    contextFn: context => contexts.push(context),
  });

  assert.strictEqual(contexts.length, 2);
  assert.match(contexts[1], /version mismatch/);
}

const {
  readPipelineDescriptor,
  getUnifiedEditorState,
} = require("./unity-client");
const { buildConnectionContext, readCompileErrorsFromLog } = require("./check-unity");

function testPipelineDescriptorIsParsed() {
  const projectPath = fs.mkdtempSync(path.join(os.tmpdir(), "unity-mcp-pipeline-"));
  try {
    assert.strictEqual(readPipelineDescriptor(projectPath), null);
    const dir = path.join(projectPath, "Library", "Pipeline");
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(
      path.join(dir, ".unity-pipeline-port"),
      JSON.stringify({ pid: 1, port: 7800, projectName: "Example", unityVersion: "6000.3.0f1", evalToken: "abc" })
    );
    const desc = readPipelineDescriptor(projectPath);
    assert.strictEqual(desc.port, 7800);
    assert.strictEqual(desc.evalToken, "abc");
    assert.strictEqual(desc.projectPath, projectPath);
  } finally {
    fs.rmSync(projectPath, { recursive: true, force: true });
  }
}

async function testUnifiedStatePrefersPipeline() {
  const desc = { port: 7800, projectPath: "D:\\Projects\\Example", projectName: "Example", unityVersion: "6000.3.0f1" };
  const state = await getUnifiedEditorState("D:\\Projects\\Example", {
    port: 9876,
    findPipelineDescriptorFn: () => desc,
    pipelineStatusFn: async () => ({ status: "ready" }),
    pipelineEditorStatusFn: async () => ({ compiling: false, domainReloadInProgress: false, playMode: "playing" }),
    tcpPingFn: async () => { throw new Error("must not fall back to tcp"); },
  });
  assert.strictEqual(state.source, "pipeline");
  assert.strictEqual(state.ready, true);
  assert.strictEqual(state.isPlaying, true);
}

async function testUnifiedStateReportsSettlingAsBusy() {
  const state = await getUnifiedEditorState("D:\\Projects\\Example", {
    findPipelineDescriptorFn: () => ({ port: 7800, projectPath: "D:\\Projects\\Example" }),
    pipelineStatusFn: async () => ({ status: "settling" }),
    pipelineEditorStatusFn: async () => { throw new Error("must not be called while settling"); },
  });
  assert.strictEqual(state.connected, true);
  assert.strictEqual(state.ready, false);
  assert.strictEqual(state.isCompiling, true);
}

async function testUnifiedStateFallsBackToTcp() {
  const state = await getUnifiedEditorState("D:\\Projects\\Example", {
    port: 9877,
    findPipelineDescriptorFn: () => ({ port: 7800, projectPath: "D:\\Projects\\Example" }),
    pipelineStatusFn: async () => { throw new Error("ECONNREFUSED"); },
    tcpPingFn: async () => true,
    sendRequestFn: async () => ({}),
    getEditorStateFn: async () => ({ isCompiling: true, isUpdating: false, projectName: "Example" }),
  });
  assert.strictEqual(state.source, "tcp");
  assert.strictEqual(state.port, 9877);
  assert.strictEqual(state.ready, false);
  assert.strictEqual(state.isCompiling, true);
}

async function testSessionStartEnablesAutotickOnTcp() {
  const calls = [];
  await runSessionStart({
    port: 9876,
    workspaceDir: "D:\Projects\Example",
    getUnifiedEditorStateFn: async () => ({
      source: "tcp", connected: true, ready: true, port: 9877,
      projectName: "Example", unityVersion: "6000.3.0f1", packageVersion: "1.0.0",
    }),
    pipelineExecFn: async () => { throw new Error("pipeline must not be used on tcp path"); },
    sendRequestFn: async (port, tool, params) => { calls.push([port, tool, params]); return {}; },
    readPluginVersionFn: () => "1.0.0",
    logFn: () => {},
    contextFn: () => {},
  });
  assert.deepStrictEqual(calls, [[9877, "unity_set_autotick", { enable: true }]]);
}

async function testSessionStartEnablesAutotickOnPipeline() {
  const execCalls = [];
  const contexts = [];
  await runSessionStart({
    port: 9876,
    workspaceDir: "D:\\Projects\\Example",
    getUnifiedEditorStateFn: async () => ({
      source: "pipeline", connected: true, ready: true,
      projectName: "Example", unityVersion: "6000.3.0f1",
      pipeline: { port: 7800, projectPath: "D:\\Projects\\Example" },
    }),
    pipelineExecFn: async (desc, command, params) => { execCalls.push([command, params]); return {}; },
    readPluginVersionFn: () => { throw new Error("version sync must not run on pipeline path"); },
    logFn: () => {},
    contextFn: context => contexts.push(context),
  });
  assert.deepStrictEqual(execCalls, [["set_autotick", { enable: true }]]);
  assert.strictEqual(contexts.length, 1);
  assert.match(contexts[0], /unity command/);
}

function testConnectionContextMentionsPipelineInstallOnTcpOnly() {
  const context = buildConnectionContext({
    source: "tcp", connected: true, port: 9876, projectName: "Example", unityVersion: "6000.3.0f1",
  });
  assert.match(context, /unity pipeline install/);
}

function testCompileErrorsAreReadFromEditorLog() {
  const projectPath = fs.mkdtempSync(path.join(os.tmpdir(), "unity-mcp-log-"));
  try {
    fs.mkdirSync(path.join(projectPath, "Logs"));
    fs.writeFileSync(
      path.join(projectPath, "Logs", "Editor.log"),
      ["noise", "Assets/A.cs(3,5): error CS0103: The name 'x' does not exist", "more noise"].join("\n")
    );
    const errors = readCompileErrorsFromLog(projectPath);
    assert.strictEqual(errors.length, 1);
    assert.match(errors[0], /CS0103/);
    assert.deepStrictEqual(readCompileErrorsFromLog(path.join(projectPath, "missing")), []);
  } finally {
    fs.rmSync(projectPath, { recursive: true, force: true });
  }
}

(async () => {
  testPipelineDescriptorIsParsed();
  await testUnifiedStatePrefersPipeline();
  await testUnifiedStateReportsSettlingAsBusy();
  await testUnifiedStateFallsBackToTcp();
  await testSessionStartEnablesAutotickOnPipeline();
  await testSessionStartEnablesAutotickOnTcp();
  testConnectionContextMentionsPipelineInstallOnTcpOnly();
  testCompileErrorsAreReadFromEditorLog();
  testMatchingVersionsProduceNoContext();
  testOlderUnityPackageRecommendsExactTag();
  testNewerUnityPackageRecommendsPluginUpdate();
  testPinnedGitPackageVersionIsParsed();
  testOlderPackageFallbackReadsPackagesLock();
  await testSessionStartUsesStatePackageVersion();
  await testSessionStartFallsBackForOlderPackage();
  process.stdout.write("check-unity tests passed\n");
})().catch(error => {
  process.stderr.write(`${error.stack || error.message}\n`);
  process.exitCode = 1;
});
