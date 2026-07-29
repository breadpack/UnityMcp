"use strict";

const assert = require("assert");
const { run } = require("./post-tool-check");

async function testAutoSaveDisabledDoesNotConnect() {
  let discovered = false;
  const result = await run({
    autoSaveEnabled: false,
    discoverPortFn: async () => {
      discovered = true;
      return 9876;
    },
  });

  assert.deepStrictEqual(result, { skipped: "auto_save_disabled" });
  assert.strictEqual(discovered, false);
}

async function testPrefabStageSkipsSceneSave() {
  let saved = false;
  const messages = [];
  const result = await run({
    autoSaveEnabled: true,
    discoverPortFn: async () => 9876,
    getEditorStateFn: async () => ({ inPrefabStage: true }),
    sendRequestFn: async () => {
      saved = true;
    },
    logFn: (message) => messages.push(message),
  });

  assert.deepStrictEqual(result, { skipped: "prefab_stage" });
  assert.strictEqual(saved, false);
  assert.strictEqual(messages.length, 1);
}

async function testMainStageSavesScene() {
  const requests = [];
  const result = await run({
    autoSaveEnabled: true,
    discoverPortFn: async () => 9876,
    getEditorStateFn: async () => ({ inPrefabStage: false }),
    sendRequestFn: async (...args) => requests.push(args),
    logFn: () => {},
  });

  assert.deepStrictEqual(result, { saved: true });
  assert.deepStrictEqual(requests, [[9876, "unity_save_scene", {}, 5000]]);
}

async function testUnavailableEditorSkipsSave() {
  let saved = false;
  const result = await run({
    autoSaveEnabled: true,
    discoverPortFn: async () => 9876,
    getEditorStateFn: async () => {
      throw new Error("offline");
    },
    sendRequestFn: async () => {
      saved = true;
    },
  });

  assert.deepStrictEqual(result, { skipped: "editor_unavailable" });
  assert.strictEqual(saved, false);
}

(async () => {
  await testAutoSaveDisabledDoesNotConnect();
  await testPrefabStageSkipsSceneSave();
  await testMainStageSavesScene();
  await testUnavailableEditorSkipsSave();
  process.stdout.write("post-tool-check tests passed\n");
})().catch((error) => {
  process.stderr.write(`${error.stack || error.message}\n`);
  process.exitCode = 1;
});
