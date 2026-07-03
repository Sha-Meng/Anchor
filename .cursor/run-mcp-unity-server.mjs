import { existsSync, readdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { spawn, spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const currentDir = dirname(fileURLToPath(import.meta.url));
const workspaceRoot = resolve(currentDir, "..");
const packageCache = join(workspaceRoot, "Library", "PackageCache");
const packagePrefix = "com.gamelovers.mcp-unity@";

function fail(message) {
  console.error(`[mcp-unity] ${message}`);
  process.exit(1);
}

function newestPackageDir() {
  if (!existsSync(packageCache)) {
    return null;
  }

  const matches = readdirSync(packageCache, { withFileTypes: true })
    .filter((entry) => entry.isDirectory() && entry.name.startsWith(packagePrefix))
    .map((entry) => join(packageCache, entry.name))
    .sort()
    .reverse();

  return matches[0] ?? null;
}

function run(command, args, cwd) {
  const result = spawnSync(command, args, {
    cwd,
    stdio: "inherit",
    shell: process.platform === "win32",
  });

  if (result.status !== 0) {
    fail(`Command failed: ${command} ${args.join(" ")}`);
  }
}

const packageDir = newestPackageDir();
if (!packageDir) {
  fail(
    "Unity package cache not found. Open this project in Tuanjie Editor and wait for Package Manager to resolve com.gamelovers.mcp-unity."
  );
}

const serverDir = join(packageDir, "Server~");
const entry = join(serverDir, "build", "index.js");
const packageJson = join(serverDir, "package.json");

if (!existsSync(packageJson)) {
  fail(`Server package.json not found at ${packageJson}`);
}

if (!existsSync(entry)) {
  console.error("[mcp-unity] build/index.js not found; installing and building Node server...");
  run("npm", ["install"], serverDir);
  run("npm", ["run", "build"], serverDir);
}

if (!existsSync(entry)) {
  fail(`Server entry was not generated at ${entry}`);
}

const child = spawn(process.execPath, [entry], {
  cwd: serverDir,
  stdio: "inherit",
});

child.on("exit", (code, signal) => {
  if (signal) {
    process.kill(process.pid, signal);
  }

  process.exit(code ?? 0);
});
