import fs from "node:fs";
import path from "node:path";
import { expect, it } from "vitest";

const source = fs.readFileSync(
  path.resolve(process.cwd(), "../src/CS2RuntimeAssetAuditor/UI/ProfilerUISystem.cs"),
  "utf8"
);

// Which bindings a refresh or an open/close updates is decided by Core/PanelVisibility.cs and covered by the
// NUnit PanelVisibilityTests; this file keeps only the capture-detail wiring.

it("sends the capture detail binding only when the detail was rebuilt", () => {
  expect(source).toContain('new RawValueBinding(Group, "captureDetail", WriteCaptureDetail)');
  expect(source).toMatch(
    /private void PublishSnapshot\(\)\s*\{\s*if \(RefreshSnapshot\(\)\)\s*_captureDetailBinding\.Update\(\);\s*_snapshotBinding\.Update\(\);\s*\}/
  );
});
