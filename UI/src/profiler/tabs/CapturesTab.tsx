import React, { useState } from "react";
import { Button } from "cs2/ui";
import type { CaptureSummaryUi, CorrelatedChangeUi } from "../bindings";
import { formatByUnit, formatPercent, shortMetricName } from "../format";
import { MetricBadge } from "../components/MetricBadge";
import { captureWarningLabel, triggerKindLabel } from "../text";
import styles from "../profiler.module.scss";

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

export function CapturesTab({ captures, onSelect }: { captures: CaptureSummaryUi[]; onSelect?: (id: string) => void }) {
  const [expanded, setExpanded] = useState<string | null>(captures?.[0]?.id ?? null);
  if (!captures?.length) return <p className={styles.empty}>完了した詳細キャプチャはまだありません。</p>;

  return (
    <div className={styles.captureList}>
      {[...captures].reverse().map(capture => {
        const open = expanded === capture.id;
        return (
          <article className={styles.captureCard} key={capture.id}>
            <Button as="button" variant="flat" className={styles.captureHeader} onSelect={() => { setExpanded(open ? null : capture.id); onSelect?.(capture.id); }}>
              <span><strong>{triggerKindLabel(capture.triggerKind)}</strong><small>{capture.id}</small></span>
              <span>{capture.durationSeconds.toFixed(1)} 秒</span>
              <span>サンプル取得率 {formatPercent(capture.coverageRatio)}</span>
              <span>{capture.batched ? "分割計測" : "同時計測"}</span>
              <span>負荷 {formatPercent(capture.profilerOverheadShare)}</span>
            </Button>
            {open && (
              <div className={styles.captureBody}>
                <div className={styles.captureFacts}>
                  <span>サンプル取得マーカー <b>{capture.capturedMarkers}/{capture.discoveredMarkers}</b></span>
                  <span>警告 <b>{capture.warningCount}</b></span>
                  <span>トリガー時刻 <b>{capture.triggeredAtSeconds.toFixed(2)} 秒</b></span>
                </div>
                <h3>相関変化が大きい項目</h3>
                <p className={styles.explainer}>イベント前後で観測された変化量の大きい項目です。相関だけでは因果関係を示しません。</p>
                {capture.correlatedChanges?.length
                  ? capture.correlatedChanges.map(change => <ChangeRow key={change.metric} change={change} />)
                  : <p className={styles.empty}>このキャプチャには比較可能な前後サンプルが保持されていません。</p>}
                {!!capture.warnings?.length && (
                  <div className={styles.warningBox}>
                    <strong>キャプチャ警告</strong>
                    {capture.warnings.map(warning => <span key={warning}>{captureWarningLabel(warning)}</span>)}
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
