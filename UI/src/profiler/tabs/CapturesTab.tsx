import React, { useState } from "react";
import { Button } from "cs2/ui";
import type { CaptureSummaryUi, CorrelatedChangeUi } from "../bindings";
import { formatByUnit, formatPercent, shortMetricName } from "../format";
import { MetricBadge } from "../components/MetricBadge";
import { captureWarningLabel, triggerKindLabel } from "../text";
import styles from "../profiler.module.scss";
import { useText } from "../../i18n/locale";

function metricLabel(metric: string) {
  return shortMetricName(metric.replace(/^(recorder|marker|system):/, ""));
}

function ChangeRow({ change }: { change: CorrelatedChangeUi }) {
  const delta = `${change.delta >= 0 ? "+" : ""}${formatByUnit(change.delta, change.unitType)}`;
  return (
    <div className={styles.changeRow}>
      <span>{metricLabel(change.metric)}</span>
      <span>{formatByUnit(change.before, change.unitType)} → {formatByUnit(change.after, change.unitType)}</span>
      <b>{delta}</b>
      <MetricBadge confidence={change.confidence} availability="Available" />
    </div>
  );
}

export function CapturesTab({ captures, onSelect, onInvestigate }: { captures: CaptureSummaryUi[]; onSelect?: (id: string) => void; onInvestigate?: (id: string) => void }) {
  const { locale, t } = useText();
  const [expanded, setExpanded] = useState<string | null>(captures?.[0]?.id ?? null);
  if (!captures?.length) return <p className={styles.empty}>{t("captures.empty")}</p>;

  return (
    <div className={styles.captureList}>
      {[...captures].reverse().map(capture => {
        const open = expanded === capture.id;
        return (
          <article className={styles.captureCard} key={capture.id}>
            <Button as="button" variant="flat" className={styles.captureHeader} onSelect={() => { setExpanded(open ? null : capture.id); onSelect?.(capture.id); }}>
              <span><strong>{triggerKindLabel(capture.triggerKind, locale)}</strong><small>{capture.id}</small></span>
              <span>{t("captures.duration", { value: capture.durationSeconds.toFixed(1) })}</span>
              <span>{t("captures.coverage", { value: formatPercent(capture.coverageRatio) })}</span>
              <span>{capture.batched ? t("captures.batched") : t("captures.simultaneous")}</span>
              <span>{t("captures.overhead", { value: formatPercent(capture.profilerOverheadShare) })}</span>
            </Button>
            {open && (
              <div className={styles.captureBody}>
                <Button as="button" variant="flat" onSelect={() => onInvestigate?.(capture.id)}>{t("captures.investigate")}</Button>
                <div className={styles.captureFacts}>
                  <span>{t("captures.sampledMarkers")} <b>{capture.capturedMarkers}/{capture.discoveredMarkers}</b></span>
                  <span>{t("captures.warnings")} <b>{capture.warningCount}</b></span>
                  <span>{t("captures.triggeredAt")} <b>{t("common.seconds", { value: capture.triggeredAtSeconds.toFixed(2) })}</b></span>
                </div>
                <h3>{t("captures.correlated")}</h3>
                <p className={styles.explainer}>{t("captures.correlatedExplainer")}</p>
                {capture.correlatedChanges?.length
                  ? capture.correlatedChanges.map(change => <ChangeRow key={change.metric} change={change} />)
                  : <p className={styles.empty}>{t("captures.noComparable")}</p>}
                {!!capture.warnings?.length && (
                  <div className={styles.warningBox}>
                    <strong>{t("captures.warningBox")}</strong>
                    {capture.warnings.map(warning => <span key={warning}>{captureWarningLabel(warning, locale)}</span>)}
                  </div>
                )}
              </div>
            )}
          </article>
        );
      })}
    </div>
  );
}
