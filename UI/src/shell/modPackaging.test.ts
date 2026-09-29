import os from "node:os";
import path from "node:path";
import { mkdtemp, readFile, rm, stat } from "node:fs/promises";
import { createRequire } from "node:module";
import webpack, { type Configuration, type Stats } from "webpack";
import { afterEach, describe, expect, it } from "vitest";

const require = createRequire(import.meta.url);
const MOD_ID = "CS2RuntimeAssetAuditor";

function compile(config: Configuration): Promise<Stats> {
  return new Promise((resolve, reject) => {
    webpack(config, (error, stats) => {
      if (error) reject(error);
      else if (!stats) reject(new Error("Webpack completed without returning compilation stats."));
      else resolve(stats);
    });
  });
}

function loadWebpackConfig(): Configuration {
  const configPath = require.resolve("../../webpack.config.js");
  delete require.cache[configPath];
  return require(configPath) as Configuration;
}

// Mirrors Colossal.IO.AssetDatabase.UIModuleAsset.ParseModuleInfo: the game only registers a .mjs whose first
// comment block names the module header and an Id; without it the file is ignored and no launcher appears.
function parseModuleId(source: string): string | null {
  let inComment = false;
  let id: string | null = null;
  for (const rawLine of source.split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!inComment) {
      if (line.startsWith("/*")) inComment = true;
      continue;
    }
    if (!line.startsWith("*")) break;
    const content = line.replace(/^\*+/, "").trim();
    if (!content) continue;
    if (content.startsWith("Cities: Skylines II UI Module")) {
      if (content !== "Cities: Skylines II UI Module") break;
      continue;
    }
    const separator = content.indexOf(":");
    if (separator < 0) break;
    if (content.slice(0, separator).trim() === "Id") id = content.slice(separator + 1).trim();
  }
  return id;
}

const savedEnvironment = { ...process.env };
const temporaryDirectories: string[] = [];

afterEach(async () => {
  process.env = { ...savedEnvironment };
  await Promise.all(temporaryDirectories.splice(0).map(directory => rm(directory, { recursive: true, force: true })));
});

describe("mod UI packaging", () => {
  it("builds every shipped UI file into the mod output folder the mod build passes", async () => {
    const outputDir = await mkdtemp(path.join(os.tmpdir(), "cs2-runtime-asset-auditor-mod-output-"));
    temporaryDirectories.push(outputDir);
    process.env.CS2_MOD_UI_OUTPUT_DIR = outputDir;
    // The mod build must not depend on the user data path to place the UI beside the DLL.
    delete process.env.CSII_USERDATAPATH;

    const stats = await compile(loadWebpackConfig());
    expect(stats.toJson({ all: false, errors: true }).errors ?? []).toEqual([]);

    // The same files the mod project verifies before deployment (ModUiRequiredFile).
    for (const file of [`${MOD_ID}.mjs`, `${MOD_ID}.css`, path.join("cs2-runtime-asset-auditor-images", "profiler-icon.svg")]) {
      expect((await stat(path.join(outputDir, file))).isFile(), file).toBe(true);
    }

    const moduleSource = await readFile(path.join(outputDir, `${MOD_ID}.mjs`), "utf8");
    expect(parseModuleId(moduleSource)).toBe(MOD_ID);
  }, 30_000);

  it("still requires a destination when neither the mod output nor the user data path is set", () => {
    delete process.env.CS2_MOD_UI_OUTPUT_DIR;
    delete process.env.CSII_USERDATAPATH;
    expect(() => loadWebpackConfig()).toThrow(/CSII_USERDATAPATH/);
  });
});

describe("module header parser", () => {
  it("rejects a bundle without the CS2 UI module header", () => {
    expect(parseModuleId("/*! some other banner */\nconst x = 1;")).toBeNull();
    expect(parseModuleId("const x = 1;")).toBeNull();
  });
});
