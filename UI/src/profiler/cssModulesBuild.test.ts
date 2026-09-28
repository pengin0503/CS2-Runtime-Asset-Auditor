import os from "node:os";
import path from "node:path";
import { readFile } from "node:fs/promises";
import { createRequire } from "node:module";
import webpack, { type Configuration, type Stats } from "webpack";
import { describe, expect, it } from "vitest";

const require = createRequire(import.meta.url);
const testUserDataPath = path.join(os.tmpdir(), "cs2-runtime-asset-auditor-ui-vitest");

function compile(config: Configuration): Promise<Stats> {
  return new Promise((resolve, reject) => {
    webpack(config, (error, stats) => {
      if (error) {
        reject(error);
        return;
      }

      if (!stats) {
        reject(new Error("Webpack completed without returning compilation stats."));
        return;
      }

      resolve(stats);
    });
  });
}

async function withBuildEnvironment<T>(action: () => Promise<T>): Promise<T> {
  const previousUserDataPath = process.env.CSII_USERDATAPATH;
  process.env.CSII_USERDATAPATH = testUserDataPath;

  try {
    return await action();
  } finally {
    if (previousUserDataPath === undefined) {
      delete process.env.CSII_USERDATAPATH;
    } else {
      process.env.CSII_USERDATAPATH = previousUserDataPath;
    }
  }
}

function loadWebpackConfig(): Configuration {
  const configPath = require.resolve("../../webpack.config.js");
  delete require.cache[configPath];
  return require(configPath) as Configuration;
}

describe("CSS Modules build", () => {
  it("does not emit missing default-export warnings for profiler.module.scss", async () => {
    await withBuildEnvironment(async () => {
      const stats = await compile(loadWebpackConfig());
      const result = stats.toJson({ all: false, errors: true, warnings: true });

      expect(result.errors ?? []).toEqual([]);

      const cssModuleWarnings = (result.warnings ?? [])
        .map((warning) => (typeof warning === "string" ? warning : warning.message ?? String(warning)))
        .filter((message) => message.includes("export 'default'") && message.includes("profiler.module.scss"));

      expect(cssModuleWarnings).toEqual([]);
    });
  }, 20_000);

  it("emits the profiler stylesheet and exports hasCSS for the CS2 UI loader", async () => {
    await withBuildEnvironment(async () => {
      const stats = await compile(loadWebpackConfig());
      const result = stats.toJson({ all: false, errors: true, assets: true });

      expect(result.errors ?? []).toEqual([]);
      expect((result.assets ?? []).map((asset) => asset.name)).toContain("CS2RuntimeAssetAuditor.css");

      const modulePath = path.join(testUserDataPath, "Mods", "CS2RuntimeAssetAuditor", "CS2RuntimeAssetAuditor.mjs");
      const moduleSource = await readFile(modulePath, "utf8");
      expect(moduleSource).toMatch(/hasCSS/);
    });
  }, 20_000);
});
