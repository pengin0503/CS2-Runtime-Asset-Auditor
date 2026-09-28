import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { RuntimeContextCard, temporalLink } from "../assets/RuntimeContextCard";
import type { CaptureSummaryUi } from "../profiler/bindings";
import type { UiSummary } from "../assets/types";

const capture = { id: "capture", sessionId: "same", startedAtUtc: "2026-09-28T12:00:10Z", completedAtUtc: "2026-09-28T12:00:20Z" } as CaptureSummaryUi;
const summary = { latestAssetSnapshotId: "asset", latestAssetSnapshotStartedAtUtc: "2026-09-28T12:00:00Z", latestAssetSnapshotCompletedAtUtc: "2026-09-28T12:00:05Z" } as UiSummary;

describe("Runtime → Asset investigation", () => {
  it("keeps the selected capture when navigating to Assets", () => {
    const shell = readFileSync(new URL("./RuntimeAssetAuditorRoot.tsx", import.meta.url), "utf8");
    expect(shell).toContain("setInvestigationCaptureId(id)");
    expect(shell).toContain('setSection("assets")');
    expect(shell).toContain("capture={snapshot.captures.find(");
  });
  it("matches only the same session with complete timestamps", () => {
    expect(temporalLink(capture, "same", summary)).toBe("Before");
    expect(temporalLink(capture, "other", summary)).toBeNull();
    expect(temporalLink({ ...capture, completedAtUtc: null }, "same", summary)).toBeNull();
  });

  it("shows a linked snapshot or a scan action with the non-causal notice", () => {
    const linked = renderToStaticMarkup(<RuntimeContextCard capture={capture} sessionId="same" summary={summary} onRunAudit={() => {}} />);
    expect(linked).toContain("asset");
    expect(linked).toContain("Before");
    const missing = renderToStaticMarkup(<RuntimeContextCard capture={capture} sessionId="other" summary={summary} onRunAudit={() => {}} />);
    expect(missing).toContain("Run Asset Audit");
    expect(missing).toContain("not measured per-asset frame/GPU cost");
    expect(missing).not.toContain("perAssetGpuMs");
  });
});
