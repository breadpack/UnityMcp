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

  assert.strictEqual(contexts.length, 1);
  assert.strictEqual(fallbackReads.length, 0);
}

async function testSessionStartFallsBackForOlderPackage() {
  const contexts = [];

  await runSessionStart({
    port: 9876,
    workspaceDir: "D:\\Projects\\Example",
    tcpPingFn: async () => true,
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

  assert.strictEqual(contexts.length, 1);
}

(async () => {
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
