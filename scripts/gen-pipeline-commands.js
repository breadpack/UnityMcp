#!/usr/bin/env node
'use strict';

/**
 * UnityMcpBridge/Tools/*.cs 의 [McpServerTool] 정의를 읽어, 같은 이름·같은 파라미터의
 * Unity Pipeline [CliCommand] 어댑터(C#)를 UnityMcpEditor/Editor/Pipeline/Generated/ 에 생성한다.
 *
 * 왜 생성하나: MCP 도구 스키마(이름·파라미터·설명)의 단일 원천은 Bridge 도구 정의다. 손으로
 * 두 벌을 유지하면 반드시 어긋난다. 생성된 어댑터는 파라미터를 JObject 로 모아 기존
 * IRequestHandler 로 그대로 위임하므로 핸들러 본체는 한 벌이다.
 *
 * 대상: Pipeline 내장 명령에 없는(C) 또는 UnityMcp 쪽이 더 풍부한(B) 도구만 (설계 문서 §2).
 *
 * 사용법: node scripts/gen-pipeline-commands.js   (Bridge 도구 시그니처를 바꿨을 때 재실행)
 */

const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const ROOT = path.resolve(__dirname, '..');
const TOOLS_DIR = path.join(ROOT, 'UnityMcpBridge', 'Tools');
const OUT_DIR = path.join(ROOT, 'UnityMcpEditor', 'Editor', 'Pipeline', 'Generated');

// tag 별로 파일 하나. special: 'targetSpec' = 순수 문자열이면 {"target": s} 로 감싼다(Bridge 와 동일),
// 'customTool' = parameters JSON 을 그대로 toolName 핸들러에 전달.
const TARGETS = [
  { file: 'Input/ClickTool.cs', tag: 'breadpack/input' },
  { file: 'Input/DragTool.cs', tag: 'breadpack/input' },
  { file: 'Input/HoldTool.cs', tag: 'breadpack/input' },
  { file: 'Input/SwipeTool.cs', tag: 'breadpack/input', special: { from: 'targetSpec' } },
  { file: 'Input/ScrollTool.cs', tag: 'breadpack/input' },
  { file: 'Input/PinchTool.cs', tag: 'breadpack/input' },
  { file: 'Input/KeyTool.cs', tag: 'breadpack/input' },
  { file: 'Input/TypeTextTool.cs', tag: 'breadpack/input' },
  { file: 'GetUguiTreeTool.cs', tag: 'breadpack/ui' },
  { file: 'GetUiTreeTool.cs', tag: 'breadpack/ui' },
  { file: 'GetScreenTool.cs', tag: 'breadpack/ui' },
  { file: 'GetAvailableActionsTool.cs', tag: 'breadpack/ui' },
  { file: 'PrefabApplyTool.cs', tag: 'breadpack/prefab' },
  { file: 'PrefabEditTool.cs', tag: 'breadpack/prefab' },
  { file: 'GetAssetHierarchyTool.cs', tag: 'breadpack/prefab' },
  { file: 'SetAssetReferenceTool.cs', tag: 'breadpack/prefab' },
  { file: 'RenderUxmlTool.cs', tag: 'breadpack/render' },
  { file: 'RenderPrefabPreviewTool.cs', tag: 'breadpack/render' },
  { file: 'AddressableAddTool.cs', tag: 'breadpack/addressable' },
  { file: 'AddressableSetAddressTool.cs', tag: 'breadpack/addressable' },
  { file: 'AnimatorControlTool.cs', tag: 'breadpack/animation' },
  { file: 'AnimationClipTool.cs', tag: 'breadpack/animation' },
  { file: 'UndoRedoTool.cs', tag: 'breadpack/editor' },
  { file: 'GetCompileErrorsTool.cs', tag: 'breadpack/editor' },
  { file: 'GetProjectInfoTool.cs', tag: 'breadpack/editor' },
  { file: 'ListCustomToolsTool.cs', tag: 'breadpack/custom' },
  { file: 'CustomToolProxyTool.cs', tag: 'breadpack/custom', customTool: true },
];

// ---------------------------------------------------------------------------
// C# 소스 파싱 (Bridge 도구 파일은 형식이 균일하므로 가벼운 스캐너로 충분하다)
// ---------------------------------------------------------------------------

// 인접한 C# 문자열 리터럴("a" + "b")을 하나의 문자열로 합친다.
function joinStringLiterals(src) {
  const parts = [];
  const re = /"((?:[^"\\]|\\.)*)"/g;
  let m;
  while ((m = re.exec(src)) !== null) parts.push(unescapeCSharp(m[1]));
  return parts.join('');
}

function unescapeCSharp(s) {
  return s.replace(/\\(["\\nrt])/g, (_, c) => ({ '"': '"', '\\': '\\', n: '\n', r: '\r', t: '\t' }[c]));
}

function escapeCSharp(s) {
  return s.replace(/\\/g, '\\\\').replace(/"/g, '\\"').replace(/\r/g, '').replace(/\n/g, '\\n');
}

// 괄호 짝을 맞춰 `open` 위치의 '(' 부터 대응하는 ')' 까지의 내부 문자열을 돌려준다. 문자열 리터럴 내부는 무시.
function balanced(src, open) {
  let depth = 0;
  let inStr = false;
  for (let i = open; i < src.length; i++) {
    const c = src[i];
    if (inStr) {
      if (c === '\\') { i++; continue; }
      if (c === '"') inStr = false;
      continue;
    }
    if (c === '"') { inStr = true; continue; }
    if (c === '(') depth++;
    else if (c === ')') { depth--; if (depth === 0) return src.slice(open + 1, i); }
  }
  throw new Error('unbalanced parentheses');
}

// 최상위 콤마로 분리 (문자열·괄호·대괄호 내부 콤마 무시)
function splitTopLevel(src) {
  const out = [];
  let depth = 0, inStr = false, cur = '';
  for (let i = 0; i < src.length; i++) {
    const c = src[i];
    if (inStr) {
      cur += c;
      if (c === '\\') { cur += src[++i]; continue; }
      if (c === '"') inStr = false;
      continue;
    }
    if (c === '"') { inStr = true; cur += c; continue; }
    if (c === '(' || c === '[' || c === '<') depth++;
    if (c === ')' || c === ']' || c === '>') depth--;
    if (c === ',' && depth === 0) { out.push(cur); cur = ''; continue; }
    cur += c;
  }
  if (cur.trim()) out.push(cur);
  return out;
}

function parseTool(file) {
  const src = fs.readFileSync(path.join(TOOLS_DIR, file), 'utf8');

  const nameMatch = /\[McpServerTool\(Name\s*=\s*"([^"]+)"\)/.exec(src);
  if (!nameMatch) throw new Error(`${file}: [McpServerTool(Name=...)] 없음`);
  const toolName = nameMatch[1];

  const descStart = src.indexOf('Description(', nameMatch.index);
  const description = joinStringLiterals(balanced(src, descStart + 'Description'.length));

  const execIdx = src.indexOf('Execute(', descStart);
  if (execIdx < 0) throw new Error(`${file}: Execute( 없음`);
  const sig = balanced(src, execIdx + 'Execute'.length);

  const params = [];
  for (const raw of splitTopLevel(sig)) {
    const p = raw.trim();
    if (!p || p.startsWith('UnityConnection') || p.includes('CancellationToken')) continue;

    let desc = '';
    let rest = p;
    const attr = /^\[Description\(/.exec(p);
    if (attr) {
      const inner = balanced(p, '[Description'.length);
      desc = joinStringLiterals(inner);
      rest = p.slice(p.indexOf(inner) + inner.length + 2).trim(); // ")]" 건너뜀
    }
    const m = /^([A-Za-z]+\??)\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?:=\s*(.+))?$/s.exec(rest);
    if (!m) throw new Error(`${file}: 파라미터 파싱 실패: ${p}`);
    params.push({ type: m[1], name: m[2], def: m[3] ? m[3].trim() : null, desc });
  }
  return { file, toolName, description, params };
}

// ---------------------------------------------------------------------------
// C# 어댑터 생성
// ---------------------------------------------------------------------------

function csType(t) {
  // Bridge 의 nullable 참조 타입(string?) 은 Unity(C# 9 미만 nullable 비활성)에서 string 으로
  if (t === 'string?') return 'string';
  return t; // int, int?, bool, bool?, float, float?, string
}

function csDefault(type, def) {
  if (def == null) return null;
  if (def === 'null') return 'null';
  if (type.startsWith('float') && /^-?\d+(\.\d+)?$/.test(def)) return def.includes('.') ? `${def}f` : `${def}f`;
  return def;
}

function methodName(toolName) {
  return toolName.split('_').map(s => s.charAt(0).toUpperCase() + s.slice(1)).join('');
}

function emitPut(p, target) {
  const special = target.special && target.special[p.name];
  if (special === 'targetSpec') return `            PipelineBridge.PutTargetSpec(p, "${p.name}", ${p.name});`;
  return `            PipelineBridge.Put(p, "${p.name}", ${p.name});`;
}

function emitCommand(tool, target) {
  const lines = [];
  lines.push(`        [CliCommand("${tool.toolName}", "${escapeCSharp(tool.description)}", Tags = new[] { "${target.tag}" })]`);
  lines.push(`        public static Task<object> ${methodName(tool.toolName)}(`);

  const sigs = tool.params.map((p, i) => {
    const required = p.def == null;
    const args = [`"${p.name}"`, `"${escapeCSharp(p.desc)}"`];
    if (required) args.push('Required = true');
    const def = csDefault(p.type, p.def);
    const decl = `${csType(p.type)} ${p.name}${def != null ? ` = ${def}` : ''}`;
    return `            [CliArg(${args.join(', ')})] ${decl}${i < tool.params.length - 1 ? ',' : ''}`;
  });
  lines.push(...sigs);
  lines.push('        )');
  lines.push('        {');

  if (target.customTool) {
    // unity_custom_tool: parameters(JSON) 를 그대로 toolName 핸들러에 전달 (Bridge 와 동일)
    lines.push('            return PipelineBridge.Invoke(toolName, PipelineBridge.ParseObject(parameters));');
  } else {
    lines.push('            var p = new JObject();');
    for (const p of tool.params) lines.push(emitPut(p, target));
    lines.push(`            return PipelineBridge.Invoke("${tool.toolName}", p);`);
  }
  lines.push('        }');
  return lines.join('\n');
}

function emitFile(tag, tools) {
  const cls = 'Commands_' + tag.split('/').pop().replace(/[^A-Za-z0-9]/g, '_');
  const body = tools.map(t => emitCommand(t.tool, t.target)).join('\n\n');
  return `// <auto-generated>
// scripts/gen-pipeline-commands.js 가 UnityMcpBridge/Tools 의 [McpServerTool] 정의에서 생성한 파일.
// 직접 수정하지 말고 Bridge 도구를 고친 뒤 생성기를 다시 실행한다.
// </auto-generated>
#if UNITY_PIPELINE_PRESENT
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;

namespace BreadPack.Mcp.Unity.Pipeline.Generated
{
    /// <summary>UnityMcp 고유 도구를 Unity Pipeline 명령(tag: ${tag})으로 노출한다.</summary>
    public static class ${cls}
    {
${body}
    }
}
#endif
`;
}

// Unity .meta — 경로 기반 결정적 GUID 라 재생성해도 GUID 가 흔들리지 않는다.
function guidFor(relPath) {
  return crypto.createHash('md5').update('unity-mcp-pipeline:' + relPath.replace(/\\/g, '/')).digest('hex');
}

function writeMeta(filePath, kind) {
  const rel = path.relative(ROOT, filePath);
  const guid = guidFor(rel);
  const importer = kind === 'folder'
    ? `folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n`
    : `MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n`;
  fs.writeFileSync(filePath + '.meta', `fileFormatVersion: 2\nguid: ${guid}\n${importer}`);
}

// tag → { fileName, source } 로 생성물을 메모리에 만든다 (쓰기와 검증이 같은 결과를 공유).
function generate() {
  const parsed = TARGETS.map(target => ({ target, tool: parseTool(target.file) }));

  const byTag = new Map();
  for (const item of parsed) {
    if (!byTag.has(item.target.tag)) byTag.set(item.target.tag, []);
    byTag.get(item.target.tag).push(item);
  }

  const files = [];
  for (const [tag, tools] of byTag) {
    const cls = 'Commands_' + tag.split('/').pop().replace(/[^A-Za-z0-9]/g, '_');
    files.push({
      tag,
      fileName: `${cls}.cs`,
      source: emitFile(tag, tools),
      toolNames: tools.map(t => t.tool.toolName),
    });
  }
  return { count: parsed.length, files };
}

// --check: 커밋된 생성물이 현재 Bridge 정의와 일치하는지 검증 (CI 용). 다르면 exit 1.
function check() {
  const { files } = generate();
  const stale = [];
  for (const f of files) {
    const out = path.join(OUT_DIR, f.fileName);
    const current = fs.existsSync(out) ? fs.readFileSync(out, 'utf8').replace(/\r\n/g, '\n') : null;
    if (current !== f.source) stale.push(f.fileName);
  }
  const expected = new Set(files.map(f => f.fileName));
  const orphans = fs.existsSync(OUT_DIR)
    ? fs.readdirSync(OUT_DIR).filter(n => n.startsWith('Commands_') && n.endsWith('.cs') && !expected.has(n))
    : [];
  if (stale.length || orphans.length) {
    process.stderr.write(`[gen-pipeline-commands] 생성물이 Bridge 도구 정의와 다릅니다. 'node scripts/gen-pipeline-commands.js' 를 실행하세요.\n`);
    for (const s of stale) process.stderr.write(`  stale: ${s}\n`);
    for (const o of orphans) process.stderr.write(`  orphan: ${o}\n`);
    return 1;
  }
  process.stdout.write(`[gen-pipeline-commands] OK — ${files.length} files up to date\n`);
  return 0;
}

function main() {
  if (process.argv.includes('--check')) {
    process.exitCode = check();
    return;
  }

  const { count, files } = generate();

  fs.mkdirSync(OUT_DIR, { recursive: true });
  // 이전 생성물 정리 (파일 목록이 바뀌었을 때 고아 파일 방지)
  for (const f of fs.readdirSync(OUT_DIR)) {
    if (f.startsWith('Commands_')) fs.unlinkSync(path.join(OUT_DIR, f));
  }
  writeMeta(OUT_DIR, 'folder');

  for (const f of files) {
    const out = path.join(OUT_DIR, f.fileName);
    fs.writeFileSync(out, f.source);
    writeMeta(out, 'cs');
  }

  process.stdout.write(`[gen-pipeline-commands] ${count} commands → ${path.relative(ROOT, OUT_DIR)}\n`);
  for (const f of files) process.stdout.write(`  ${f.tag}: ${f.toolNames.join(', ')}\n`);
}

if (require.main === module) main();

module.exports = { parseTool, emitCommand, generate, check, TARGETS };
