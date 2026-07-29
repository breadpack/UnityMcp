#!/usr/bin/env node
'use strict';

/**
 * 플러그인 버전 단일 진입점.
 *
 * Unity UPM package.json, Claude/Codex plugin.json, marketplace.json 의 version 을 항상 같은 값으로 유지한다.
 * Claude Code 플러그인 업데이트는 marketplace.json 의 version 으로 갱신을 판단하므로,
 * 이 파일들이 어긋나면 배포 산출물과 사용자에게 노출되는 업데이트 버전이 달라진다.
 *
 * 사용법:
 *   node scripts/bump-version.js <x.y.z>        # 모든 버전 파일을 지정 버전으로 설정
 *   node scripts/bump-version.js patch|minor|major  # plugin.json 기준 증가 후 설정
 *   node scripts/bump-version.js --verify <x.y.z>    # 인자/UPM/plugin/marketplace 일치 검증 (CI 게이트)
 *   node scripts/bump-version.js --verify            # UPM/plugin/marketplace 일치 검증
 *
 * 종료 코드: 성공 0 / 불일치·오류 1
 */

const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');
const UNITY_PACKAGE_JSON = path.join(ROOT, 'UnityMcpEditor', 'package.json');
const CLAUDE_PLUGIN_JSON = path.join(ROOT, 'plugins', 'unity-mcp', '.claude-plugin', 'plugin.json');
const CODEX_PLUGIN_JSON = path.join(ROOT, 'plugins', 'unity-mcp', '.codex-plugin', 'plugin.json');
const MARKETPLACE_JSON = path.join(ROOT, '.claude-plugin', 'marketplace.json');
const PLUGIN_NAME = 'unity-mcp';

const SEMVER = /^\d+\.\d+\.\d+$/;

function fail(msg) {
  process.stderr.write(`[bump-version] ${msg}\n`);
  process.exit(1);
}

function readJson(file) {
  try {
    return JSON.parse(fs.readFileSync(file, 'utf8'));
  } catch (e) {
    fail(`${path.relative(ROOT, file)} 읽기 실패: ${e.message}`);
  }
}

// 2-space indent + 끝 개행 (기존 파일 포맷 유지)
function writeJson(file, obj) {
  fs.writeFileSync(file, JSON.stringify(obj, null, 2) + '\n');
}

function getMarketplaceEntry(marketplace) {
  const entry = (marketplace.plugins || []).find(p => p.name === PLUGIN_NAME);
  if (!entry) fail(`marketplace.json 에서 plugin "${PLUGIN_NAME}" 항목을 찾을 수 없습니다.`);
  return entry;
}

function bump(current, kind) {
  const [maj, min, pat] = current.split('.').map(Number);
  if (kind === 'major') return `${maj + 1}.0.0`;
  if (kind === 'minor') return `${maj}.${min + 1}.0`;
  return `${maj}.${min}.${pat + 1}`; // patch
}

function doSet(version) {
  const unityPackage = readJson(UNITY_PACKAGE_JSON);
  const claudePlugin = readJson(CLAUDE_PLUGIN_JSON);
  const codexPlugin = fs.existsSync(CODEX_PLUGIN_JSON) ? readJson(CODEX_PLUGIN_JSON) : null;
  const marketplace = readJson(MARKETPLACE_JSON);
  const entry = getMarketplaceEntry(marketplace);

  unityPackage.version = version;
  claudePlugin.version = version;
  if (codexPlugin) codexPlugin.version = version;
  entry.version = version;

  writeJson(UNITY_PACKAGE_JSON, unityPackage);
  writeJson(CLAUDE_PLUGIN_JSON, claudePlugin);
  if (codexPlugin) writeJson(CODEX_PLUGIN_JSON, codexPlugin);
  writeJson(MARKETPLACE_JSON, marketplace);

  process.stdout.write(
    `[bump-version] Unity package.json + Claude/Codex plugin.json + marketplace.json → ${version}\n` +
    `  다음 단계: git add -A && git commit && git tag v${version} && push (main + 태그)\n`
  );
}

function doVerify(expected) {
  const unityPackage = readJson(UNITY_PACKAGE_JSON);
  const claudePlugin = readJson(CLAUDE_PLUGIN_JSON);
  const codexPlugin = fs.existsSync(CODEX_PLUGIN_JSON) ? readJson(CODEX_PLUGIN_JSON) : null;
  const marketplace = readJson(MARKETPLACE_JSON);
  const entry = getMarketplaceEntry(marketplace);

  const uv = unityPackage.version;
  const pv = claudePlugin.version;
  const cv = codexPlugin?.version;
  const mv = entry.version;
  const problems = [];

  if (uv !== pv) {
    problems.push(`UnityMcpEditor/package.json(${uv}) ≠ .claude-plugin/plugin.json(${pv})`);
  }
  if (pv !== mv) {
    problems.push(`.claude-plugin/plugin.json(${pv}) ≠ marketplace.json(${mv})`);
  }
  if (cv != null && cv !== pv) {
    problems.push(`.codex-plugin/plugin.json(${cv}) ≠ .claude-plugin/plugin.json(${pv})`);
  }
  if (expected != null) {
    if (uv !== expected) problems.push(`UnityMcpEditor/package.json(${uv}) ≠ 태그/인자(${expected})`);
    if (pv !== expected) problems.push(`.claude-plugin/plugin.json(${pv}) ≠ 태그/인자(${expected})`);
    if (cv != null && cv !== expected) problems.push(`.codex-plugin/plugin.json(${cv}) ≠ 태그/인자(${expected})`);
    if (mv !== expected) problems.push(`marketplace.json(${mv}) ≠ 태그/인자(${expected})`);
  }

  if (problems.length > 0) {
    fail(
      `버전 불일치:\n  - ${problems.join('\n  - ')}\n` +
      `해결: node scripts/bump-version.js ${expected || '<x.y.z>'} 로 모든 버전 파일을 맞추고 다시 커밋/태그하세요.`
    );
  }

  process.stdout.write(
    `[bump-version] OK — Unity package.json = Claude/Codex plugin.json = marketplace.json = ${pv}` +
    (expected != null ? ` (= 태그/인자 ${expected})` : '') + '\n'
  );
}

function main() {
  const args = process.argv.slice(2);
  if (args.length === 0) {
    fail('인자가 필요합니다. 사용법은 스크립트 상단 주석 참고.');
  }

  if (args[0] === '--verify') {
    const expected = args[1];
    if (expected != null && !SEMVER.test(expected.replace(/^v/, ''))) {
      fail(`검증 버전 형식 오류: "${expected}" (x.y.z 기대)`);
    }
    doVerify(expected != null ? expected.replace(/^v/, '') : null);
    return;
  }

  const arg = args[0];
  if (['patch', 'minor', 'major'].includes(arg)) {
    const plugin = readJson(CLAUDE_PLUGIN_JSON);
    doSet(bump(plugin.version, arg));
    return;
  }

  const version = arg.replace(/^v/, '');
  if (!SEMVER.test(version)) {
    fail(`버전 형식 오류: "${arg}" (x.y.z 또는 patch|minor|major 기대)`);
  }
  doSet(version);
}

main();
