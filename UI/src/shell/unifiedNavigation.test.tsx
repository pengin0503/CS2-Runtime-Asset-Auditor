import { expect, test } from "vitest";
import { TOP_SECTIONS, RUNTIME_SECTIONS, ASSET_SECTIONS } from "./navigation";

test("keeps five top-level sections with focused runtime and asset views", () => {
  expect(TOP_SECTIONS.map(item => item.id)).toEqual(["overview", "runtime", "assets", "advisor", "diagnostics"]);
  expect(RUNTIME_SECTIONS.map(item => item.id)).toEqual(["systems", "mods", "pathfinding", "timeline", "captures"]);
  expect(ASSET_SECTIONS.map(item => item.id)).toEqual(["catalog", "census", "findings", "compare", "settings"]);
});
