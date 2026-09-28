import fs from "node:fs";
import path from "node:path";
import { expect, test } from "vitest";

test("registers a single launcher for the unified product", () => {
  const mod = JSON.parse(fs.readFileSync(path.resolve(process.cwd(), "mod.json"), "utf8"));
  const index = fs.readFileSync(path.resolve(process.cwd(), "src/index.tsx"), "utf8");
  const hud = fs.readFileSync(path.resolve(process.cwd(), "src/profiler/components/ProfilerHud.tsx"), "utf8");
  expect(mod.id).toBe("CS2RuntimeAssetAuditor");
  expect(index.match(/append\("GameTopLeft"/g)).toHaveLength(1);
  expect(hud).toContain("CS2 Runtime Asset Auditor");
});
