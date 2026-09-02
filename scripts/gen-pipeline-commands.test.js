'use strict';

const assert = require('assert');
const { parseTool, emitCommand, generate, TARGETS } = require('./gen-pipeline-commands');

function testParsesClickToolSignature() {
  const tool = parseTool('Input/ClickTool.cs');
  assert.strictEqual(tool.toolName, 'unity_input_click');
  assert.match(tool.description, /Play Mode/);
  assert.deepStrictEqual(tool.params.map(p => p.name), [
    'target', 'position', 'worldPoint', 'button', 'count', 'waitFrames', 'waitFor', 'captureResult',
  ]);
  const button = tool.params.find(p => p.name === 'button');
  assert.strictEqual(button.type, 'string');
  assert.strictEqual(button.def, '"left"');
  // 설명 안의 이스케이프된 따옴표가 복원되어야 한다
  assert.match(tool.params[0].desc, /"Canvas\/Panel\/Button"/);
}

function testConcatenatedDescriptionsAreJoined() {
  const tool = parseTool('PrefabApplyTool.cs');
  assert.match(tool.description, /단일 원자 호출/);
  assert.match(tool.description, /op 종류: set_property/);
  const edits = tool.params.find(p => p.name === 'edits');
  assert.strictEqual(edits.def, null, 'edits 는 필수 파라미터');
}

function testEmitsRequiredFlagAndFloatDefaults() {
  const tool = parseTool('Input/PinchTool.cs');
  const cs = emitCommand(tool, { tag: 'breadpack/input' });
  assert.match(cs, /\[CliArg\("center", ".*?", Required = true\)\] string center,/);
  assert.match(cs, /float startSpread = 100f/);
  assert.match(cs, /Tags = new\[\] \{ "breadpack\/input" \}/);
  assert.match(cs, /PipelineBridge\.Invoke\("unity_input_pinch", p\)/);
}

function testSwipeWrapsPlainFromAsTargetSpec() {
  const target = TARGETS.find(t => t.file === 'Input/SwipeTool.cs');
  const cs = emitCommand(parseTool(target.file), target);
  assert.match(cs, /PipelineBridge\.PutTargetSpec\(p, "from", from\)/);
}

function testCustomToolForwardsToNamedHandler() {
  const target = TARGETS.find(t => t.customTool);
  const cs = emitCommand(parseTool(target.file), target);
  assert.match(cs, /PipelineBridge\.Invoke\(toolName, PipelineBridge\.ParseObject\(parameters\)\)/);
}

function testGenerateCoversEveryTargetOnce() {
  const { count, files } = generate();
  assert.strictEqual(count, TARGETS.length);
  const names = files.flatMap(f => f.toolNames);
  assert.strictEqual(new Set(names).size, names.length, '명령 이름 중복');
  for (const f of files) {
    assert.match(f.source, /^#if UNITY_PIPELINE_PRESENT/m);
    assert.match(f.source, /#endif\s*$/);
  }
}

testParsesClickToolSignature();
testConcatenatedDescriptionsAreJoined();
testEmitsRequiredFlagAndFloatDefaults();
testSwipeWrapsPlainFromAsTargetSpec();
testCustomToolForwardsToNamedHandler();
testGenerateCoversEveryTargetOnce();
process.stdout.write('gen-pipeline-commands tests passed\n');
