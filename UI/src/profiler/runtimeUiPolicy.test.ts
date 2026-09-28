import fs from "node:fs";
import path from "node:path";
import { expect, it } from "vitest";

const source = fs.readFileSync(
  path.resolve(process.cwd(), "../src/CS2RuntimeProfiler/UI/ProfilerUISystem.cs"),
  "utf8"
);

it("updates only the lightweight HUD on periodic refresh while the panel is closed", () => {
  expect(source).toContain('new RawValueBinding(Group, "hudSnapshot", WriteHudSnapshot)');
  expect(source).toMatch(
    /_hudSnapshotBinding\.Update\(\);\s*if \(!_panelVisible\)\s*return;\s*RefreshSnapshot\(\);\s*_snapshotBinding\.Update\(\);/s
  );
});

it("rebuilds the full snapshot immediately when the panel opens", () => {
  expect(source).toMatch(
    /private void SetPanelVisible\(bool visible\)[\s\S]*?_panelVisibleBinding\.Update\(_panelVisible\);[\s\S]*?if \(_panelVisible\)\s*\{\s*RefreshSnapshot\(\);\s*_snapshotBinding\.Update\(\);\s*\}/
  );
});
