import React from "react";
import type { CaptureSummaryUi } from "../profiler/bindings";
import type { UiSummary } from "./types";
import { useText } from "../i18n/locale";
import type { MessageKey } from "../i18n/messages";

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
  const { t } = useText();
  const timing = temporalLink(capture, sessionId, summary);
  return <section className="apa__metric-card" aria-label={t("context.aria")}>
    <h3>{t("context.title", { id: capture.id })}</h3>
    {timing ? <p>{t("context.linked", { snapshot: summary.latestAssetSnapshotId ?? "", timing: t(`context.timing.${timing}` as MessageKey) })}</p>
      : <><p>{t("context.none")}</p>
        <button type="button" className="apa__button" onClick={onRunAudit}>{t("context.runAudit")}</button></>}
    <p>{t("context.disclaimer")}</p>
  </section>;
}
