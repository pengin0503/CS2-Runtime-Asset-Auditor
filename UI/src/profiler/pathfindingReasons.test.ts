import fs from "node:fs";
import path from "node:path";
import { expect, it } from "vitest";
import { metricReasonLabel } from "./text";

const sources = ["Collectors/PathfindingCollector.cs", "Core/PathfindQueryStatsTracker.cs"]
  .map(file => fs.readFileSync(path.resolve(process.cwd(), "../src/CS2RuntimeAssetAuditor", file), "utf8"))
  .join("\n");

it("translates every fixed pathfinding reason the collector can report", () => {
  const reasons = [...sources.matchAll(/"((?:[A-Z][^"]*?)\.)"/g)]
    .map(match => match[1])
    .filter(text => text.includes(" ") && !text.includes("{"));
  expect(reasons.length).toBeGreaterThan(5);
  for (const reason of reasons) expect(metricReasonLabel(reason, "ja"), reason).not.toBe(reason);
});

it("translates the missing-property reason", () => {
  expect(metricReasonLabel("Runtime property for 'queryStats' is not available.", "ja")).toBe("ランタイムプロパティ「queryStats」を取得できません。");
});
