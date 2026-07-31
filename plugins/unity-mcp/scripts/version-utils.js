'use strict';

const fs = require('fs');
const path = require('path');

const UNITY_PACKAGE_NAME = 'com.breadpack.unity-mcp';

function readJson(filePath) {
  try {
    return JSON.parse(fs.readFileSync(filePath, 'utf8'));
  } catch {
    return null;
  }
}

function readPluginVersion(pluginRoot = path.resolve(__dirname, '..')) {
  const manifestPaths = [
    path.join(pluginRoot, '.codex-plugin', 'plugin.json'),
    path.join(pluginRoot, '.claude-plugin', 'plugin.json'),
  ];
  for (const manifestPath of manifestPaths) {
    const manifest = readJson(manifestPath);
    if (manifest && manifest.version) return String(manifest.version);
  }
  return null;
}

function extractSemanticVersion(value) {
  const match = String(value || '').trim().match(/v?(\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)(?:\+[0-9A-Za-z.-]+)?$/);
  return match ? match[1] : null;
}

function compareVersions(a, b) {
  const pa = String(a).replace(/^v/, '').split('.').map(n => parseInt(n, 10) || 0);
  const pb = String(b).replace(/^v/, '').split('.').map(n => parseInt(n, 10) || 0);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const d = (pa[i] || 0) - (pb[i] || 0);
    if (d !== 0) return d;
  }
  return 0;
}

function readUnityPackageVersion(projectPath) {
  if (!projectPath) return null;

  const embeddedPackage = readJson(
    path.join(projectPath, 'Packages', UNITY_PACKAGE_NAME, 'package.json')
  );
  if (embeddedPackage && embeddedPackage.name === UNITY_PACKAGE_NAME) {
    return extractSemanticVersion(embeddedPackage.version);
  }

  const lock = readJson(path.join(projectPath, 'Packages', 'packages-lock.json'));
  const lockedVersion = lock?.dependencies?.[UNITY_PACKAGE_NAME]?.version;
  const resolvedLockedVersion = extractSemanticVersion(lockedVersion);
  if (resolvedLockedVersion) return resolvedLockedVersion;

  const manifest = readJson(path.join(projectPath, 'Packages', 'manifest.json'));
  return extractSemanticVersion(manifest?.dependencies?.[UNITY_PACKAGE_NAME]);
}

module.exports = {
  UNITY_PACKAGE_NAME,
  compareVersions,
  extractSemanticVersion,
  readPluginVersion,
  readUnityPackageVersion,
};
