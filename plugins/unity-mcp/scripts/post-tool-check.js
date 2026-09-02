#!/usr/bin/env node
"use strict";

const {
  sendRequest, getEditorState, discoverPort,
  findPipelineDescriptor, pipelineExec,
} = require("./unity-client");

const args = Object.fromEntries(
  process.argv.slice(2).map((a) => {
    const [k, v] = a.replace(/^--/, "").split("=");
    return [k, v ?? "true"];
  }),
);

const autoSave = args["auto-save"] === "true";
const workspaceDir =
  process.env.UNITY_WORKSPACE_DIR ||
  process.env.CODEX_PROJECT_DIR ||
  process.env.CODEX_WORKSPACE_ROOT ||
  process.env.CLAUDE_PROJECT_DIR ||
  process.cwd();

function log(msg) {
  process.stderr.write(`[Unity MCP] ${msg}\n`);
}

async function run({
  autoSaveEnabled = autoSave,
  workspace = workspaceDir,
  discoverPortFn = discoverPort,
  getEditorStateFn = getEditorState,
  sendRequestFn = sendRequest,
  findPipelineDescriptorFn = findPipelineDescriptor,
  pipelineExecFn = pipelineExec,
  logFn = log,
} = {}) {
  // 자동 저장이 꺼져 있으면 연결 확인만을 위한 단명 TCP probe도 만들지 않는다.
  if (!autoSaveEnabled) return { skipped: "auto_save_disabled" };

  const port = await discoverPortFn(workspace);
  let state;
  try {
    state = await getEditorStateFn(port);
  } catch {
    // UnityMcp TCP 가 없어도 Unity Pipeline 이 살아 있으면 그쪽의 save_scene 으로 저장한다.
    const desc = findPipelineDescriptorFn(workspace);
    if (!desc) return { skipped: "editor_unavailable" };
    try {
      await pipelineExecFn(desc, "save_scene", {}, 5000);
      logFn("씬 자동 저장 완료 (pipeline)");
      return { saved: true, source: "pipeline" };
    } catch (e) {
      logFn(`씬 자동 저장 실패 (pipeline): ${e.message}`);
      return { saved: false, error: e.message, source: "pipeline" };
    }
  }

  // Prefab Stage에서 unity_save_scene을 호출하면 preview scene을 잘못 저장하거나
  // Prefab 워크플로우의 명시적 save/discard 결정을 침범할 수 있으므로 건너뛴다.
  if (state && state.inPrefabStage) {
    logFn("Prefab 편집 중이므로 씬 자동 저장을 건너뜁니다");
    return { skipped: "prefab_stage" };
  }

  try {
    await sendRequestFn(port, "unity_save_scene", {}, 5000);
    logFn("씬 자동 저장 완료");
    return { saved: true };
  } catch (e) {
    logFn(`씬 자동 저장 실패: ${e.message}`);
    return { saved: false, error: e.message };
  }
}

if (require.main === module) {
  run().catch((e) => log(`씬 자동 저장 확인 실패: ${e.message}`));
}

module.exports = { run };
