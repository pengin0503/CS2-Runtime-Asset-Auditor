import React from "react";
import type { CaptureSummaryUi } from "../profiler/bindings";
import type { UiSummary } from "./types";

export function temporalLink(capture: CaptureSummaryUi, assetSessionId: string | null | undefined, summary: UiSummary): "Before" | "Overlapping" | "After" | null {
  if (!capture.sessionId || capture.sessionId !== assetSessionId || !capture.startedAtUtc || !capture.completedAtUtc
    || !summary.latestAssetSnapshotId || !summary.latestAssetSnapshotStartedAtUtc || !summary.latestAssetSnapshotCompletedAtUtc) return null;
  const [cs, ce, as, ae] = [capture.startedAtUtc, capture.completedAtUtc, summary.latestAssetSnapshotStartedAtUtc, summary.latestAssetSnapshotCompletedAtUtc].map(Date.parse);
  if ([cs, ce, as, ae].some(Number.isNaN) || ce < cs || ae < as) return null;
  return ae < cs ? "Before" : as > ce ? "After" : "Overlapping";
}

export function RuntimeContextCard({ capture, sessionId, summary, onRunAudit }: {
  capture: CaptureSummaryUi;
  sessionId?: string | null;
  summary: UiSummary;
  onRunAudit: () => void;
}) {
  const timing = temporalLink(capture, sessionId, summary);
  return <section className="apa__metric-card" aria-label="Runtime capture investigation context">
    <h3>Runtime capture: {capture.id}</h3>
    {timing ? <p>Asset snapshot {summary.latestAssetSnapshotId} · {timing} this capture</p>
      : <><p>No time-linked Asset snapshot exists for this city session.</p>
        <button type="button" className="apa__button" onClick={onRunAudit}>Run Asset Audit</button></>}
    <p>Asset geometry, textures, and exposure are investigation evidence, not measured per-asset frame/GPU cost.</p>
  </section>;
}
