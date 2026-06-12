#!/usr/bin/env node
'use strict';

/**
 * 플러그인 버전 단일 진입점.
 *
 * plugin.json 과 marketplace.json 의 version 을 항상 같은 값으로 유지한다.
 * Claude Code 플러그인 업데이트는 marketplace.json 의 version 으로 갱신을 판단하므로,
 * 두 파일이 어긋나면 배포해도 사용자에게 업데이트가 노출되지 않는다.
 *
 * 사용법:
 *   node scripts/bump-version.js <x.y.z>        # 두 파일을 지정 버전으로 설정
 *   node scripts/bump-version.js patch|minor|major  # plugin.json 기준 증가 후 설정
 *   node scripts/bump-version.js --verify <x.y.z>    # 세 버전(인자/plugin/marketplace) 일치 검증 (CI 게이트)
 *   node scripts/bump-version.js --verify            # plugin/marketplace 두 파일만 일치 검증
 *
 * 종료 코드: 성공 0 / 불일치·오류 1
 */

const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');
const PLUGIN_JSON = path.join(ROOT, 'plugins', 'unity-mcp', '.claude-plugin', 'plugin.json');
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
  const plugin = readJson(PLUGIN_JSON);
  const marketplace = readJson(MARKETPLACE_JSON);
  const entry = getMarketplaceEntry(marketplace);

  plugin.version = version;
  entry.version = version;

  writeJson(PLUGIN_JSON, plugin);
  writeJson(MARKETPLACE_JSON, marketplace);

  process.stdout.write(
    `[bump-version] plugin.json + marketplace.json → ${version}\n` +
    `  다음 단계: git add -A && git commit && git tag v${version} && push (main + 태그)\n`
  );
}

function doVerify(expected) {
  const plugin = readJson(PLUGIN_JSON);
  const marketplace = readJson(MARKETPLACE_JSON);
  const entry = getMarketplaceEntry(marketplace);

  const pv = plugin.version;
  const mv = entry.version;
  const problems = [];

  if (pv !== mv) {
    problems.push(`plugin.json(${pv}) ≠ marketplace.json(${mv})`);
  }
  if (expected != null) {
    if (pv !== expected) problems.push(`plugin.json(${pv}) ≠ 태그/인자(${expected})`);
    if (mv !== expected) problems.push(`marketplace.json(${mv}) ≠ 태그/인자(${expected})`);
  }

  if (problems.length > 0) {
    fail(
      `버전 불일치:\n  - ${problems.join('\n  - ')}\n` +
      `해결: node scripts/bump-version.js ${expected || '<x.y.z>'} 로 두 파일을 맞추고 다시 커밋/태그하세요.`
    );
  }

  process.stdout.write(
    `[bump-version] OK — plugin.json = marketplace.json = ${pv}` +
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
    const plugin = readJson(PLUGIN_JSON);
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
