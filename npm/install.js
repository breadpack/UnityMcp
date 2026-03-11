#!/usr/bin/env node

const fs = require("fs");
const path = require("path");
const https = require("https");
const { execSync } = require("child_process");

const VERSION = require("./package.json").version;
const REPO = "breadpack/UnityMcp";
const BIN_DIR = path.join(__dirname, "bin");

function getPlatformAsset() {
  const platform = process.platform;
  const arch = process.arch;

  const map = {
    "win32-x64": "win-x64",
    "win32-arm64": "win-arm64",
    "linux-x64": "linux-x64",
    "linux-arm64": "linux-arm64",
    "darwin-x64": "osx-x64",
    "darwin-arm64": "osx-arm64",
  };

  const rid = map[`${platform}-${arch}`];
  if (!rid) {
    console.error(`Unsupported platform: ${platform}-${arch}`);
    process.exit(1);
  }

  const ext = platform === "win32" ? ".zip" : ".tar.gz";
  return { rid, fileName: `unity-mcp-bridge-${rid}${ext}` };
}

function httpsGet(url) {
  return new Promise((resolve, reject) => {
    https
      .get(url, { headers: { "User-Agent": "unity-mcp-bridge-npm" } }, (res) => {
        if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
          return httpsGet(res.headers.location).then(resolve, reject);
        }
        if (res.statusCode !== 200) {
          return reject(new Error(`HTTP ${res.statusCode} for ${url}`));
        }
        const chunks = [];
        res.on("data", (chunk) => chunks.push(chunk));
        res.on("end", () => resolve(Buffer.concat(chunks)));
        res.on("error", reject);
      })
      .on("error", reject);
  });
}

async function extractZip(buffer, destDir) {
  const tmpZip = path.join(destDir, "_tmp.zip");
  fs.writeFileSync(tmpZip, buffer);
  try {
    if (process.platform === "win32") {
      execSync(
        `powershell -NoProfile -Command "Expand-Archive -Path '${tmpZip}' -DestinationPath '${destDir}' -Force"`,
        { stdio: "pipe" }
      );
    } else {
      execSync(`unzip -o "${tmpZip}" -d "${destDir}"`, { stdio: "pipe" });
    }
  } finally {
    fs.unlinkSync(tmpZip);
  }
}

async function extractTarGz(buffer, destDir) {
  const tmpTar = path.join(destDir, "_tmp.tar.gz");
  fs.writeFileSync(tmpTar, buffer);
  try {
    execSync(`tar -xzf "${tmpTar}" -C "${destDir}"`, { stdio: "pipe" });
  } finally {
    fs.unlinkSync(tmpTar);
  }
}

async function main() {
  const { rid, fileName } = getPlatformAsset();
  const url = `https://github.com/${REPO}/releases/download/v${VERSION}/${fileName}`;

  console.log(`Downloading unity-mcp-bridge ${VERSION} for ${rid}...`);

  let buffer;
  try {
    buffer = await httpsGet(url);
  } catch (err) {
    console.error(`Failed to download: ${err.message}`);
    console.error(`URL: ${url}`);
    console.error(
      `\nIf the release does not exist yet, install via dotnet tool instead:`
    );
    console.error(`  dotnet tool install -g dev.breadpack.UnityMcpBridge`);
    process.exit(1);
  }

  fs.mkdirSync(BIN_DIR, { recursive: true });

  if (fileName.endsWith(".zip")) {
    await extractZip(buffer, BIN_DIR);
  } else {
    await extractTarGz(buffer, BIN_DIR);
  }

  // Make executable on Unix
  if (process.platform !== "win32") {
    const exePath = path.join(BIN_DIR, "UnityMcpBridge");
    if (fs.existsSync(exePath)) {
      fs.chmodSync(exePath, 0o755);
    }
  }

  console.log(`unity-mcp-bridge ${VERSION} installed successfully.`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
