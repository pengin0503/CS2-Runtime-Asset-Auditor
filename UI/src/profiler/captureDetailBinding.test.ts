import { expect, it } from "vitest";
import { EMPTY_SNAPSHOT, mergeProfilerSnapshot, type UiCaptureDetail, type UiLiveSnapshot } from "./bindings";

const live: UiLiveSnapshot = {
  global: EMPTY_SNAPSHOT.global,
  capture: EMPTY_SNAPSHOT.capture,
  pathfinding: EMPTY_SNAPSHOT.pathfinding,
  domainMetrics: [],
  diagnostics: EMPTY_SNAPSHOT.diagnostics,
  advisor: EMPTY_SNAPSHOT.advisor
};

const detail: UiCaptureDetail = {
  systems: [{ id: "A.System", ownerAssembly: "A", sourceKind: "Mod", isAggregateContainer: false, currentMilliseconds: 1,
    meanMilliseconds: 1, medianMilliseconds: 1, p95Milliseconds: 1, p99Milliseconds: 1, maxMilliseconds: 1,
    totalMilliseconds: 1, millisecondsPerFrame: 1, calls: 1, confidence: "Managed", patchOwners: [] }],
  mods: [],
  timeline: [],
  captures: []
};

it("takes the systems, mods, timeline and captures from the separately sent capture detail", () => {
  const merged = mergeProfilerSnapshot(live, detail);
  expect(merged.systems).toBe(detail.systems);
  expect(merged.captures).toBe(detail.captures);
  expect(merged.global).toBe(live.global);
});

it("keeps the detail arrays by reference when only the live values change", () => {
  const next = mergeProfilerSnapshot({ ...live, global: { ...live.global, timestampSeconds: 12 } }, detail);
  expect(next.systems).toBe(mergeProfilerSnapshot(live, detail).systems);
  expect(next.global.timestampSeconds).toBe(12);
});

it("shows a snapshot that still carries the detail until the detail binding has been sent", () => {
  const merged = mergeProfilerSnapshot({ ...live, systems: detail.systems }, null);
  expect(merged.systems).toBe(detail.systems);
  expect(merged.timeline).toEqual([]);
});
