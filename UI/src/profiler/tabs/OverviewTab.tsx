import React from "react";
import { Button } from "cs2/ui";
import type { UiMetricRow, UiSnapshot } from "../bindings";
import { formatFramesPerSecond, formatMetricValue, formatMilliseconds, formatPercent, formatSpeed, shortMetricName } from "../format";
import { MetricBadge } from "../components/MetricBadge";
import { captureStateLabel, exportResultLabel } from "../text";
import { useText } from "../../i18n/locale";
import styles from "../profiler.module.scss";

interface OverviewTabProps {
  snapshot: UiSnapshot;
  onManualCapture: () => void;
  onExport: () => void;
  exportResult: string;
}

function MetricRow({ metric }: { metric: UiMetricRow }) {
  const { t } = useText();
  return (
    <div className={styles.metricRow}>
      <div className={styles.metricIdentity}>
        <strong>{shortMetricName(metric.id)}</strong>
        <small>{metric.id.includes("\u001f") ? metric.id.split("\u001f")[0] : t("overview.runtimeMetric")}</small>
      </div>
      <span className={styles.metricValue}>{formatMetricValue(metric)}</span>
      <MetricBadge confidence={metric.confidence} availability={metric.availability} reason={metric.reason} />
    </div>
  );
}

export function OverviewTab({ snapshot, onManualCapture, onExport, exportResult }: OverviewTabProps) {
  const pathfinding = snapshot.pathfinding?.metrics ?? [];
  const recorders = snapshot.global?.recorderMetrics ?? [];
  const { locale, t } = useText();
  const canManualCapture = snapshot.capture.state === "Monitoring" || snapshot.capture.state === "Cooldown";

  return (
    <div className={styles.tabBody}>
      <section className={styles.summaryGrid}>
        <div className={styles.summaryCard}><span>{t("overview.selectedSpeed")}</span><strong>{formatSpeed(snapshot.global.selectedSpeed)}</strong></div>
        <div className={styles.summaryCard}><span>{t("overview.actualSpeed")}</span><strong>{formatSpeed(snapshot.global.actualSpeed)}</strong></div>
        <div className={styles.summaryCard}><span>{t("overview.efficiency")}</span><strong>{formatPercent(snapshot.global.efficiency)}</strong></div>
        <div className={styles.summaryCard}><span>{t("overview.captureState")}</span><strong>{captureStateLabel(snapshot.capture.state, snapshot.capture.isDeepCapture, locale)}</strong></div>
        <div className={styles.summaryCard} title={t("overview.captureOverheadTooltip")}><span>{t("overview.captureOverhead")}</span><strong>{formatPercent(snapshot.diagnostics.profilerOverheadShare)}</strong></div>
        <div className={styles.summaryCard} title={t("overview.fpsTooltip")}>
          <span>{t("overview.fps")}</span>
          <strong>{formatFramesPerSecond(snapshot.global.framesPerSecond)}</strong>
          <small>{snapshot.global.framesPerSecond == null
            ? t("common.unavailable")
            : t("overview.frameTimes", {
              median: formatMilliseconds(snapshot.global.frameMsMedian),
              p95: formatMilliseconds(snapshot.global.frameMsP95)
            })}</small>
        </div>
      </section>

      <div className={styles.actionRow}>
        <Button as="button" variant="flat" onSelect={onManualCapture} disabled={!canManualCapture}>{t("overview.manualCapture")}</Button>
        <Button as="button" variant="flat" onSelect={onExport}>{t("overview.exportJson")}</Button>
        {exportResult && <span className={styles.exportResult}>{exportResultLabel(exportResult, locale)}</span>}
      </div>

      <section>
        <h3>{t("overview.recorders")}</h3>
        {recorders.length ? recorders.map(metric => <MetricRow key={metric.id} metric={metric} />) : <p className={styles.empty}>{t("overview.recordersEmpty")}</p>}
      </section>

      <section>
        <h3>{t("overview.pathfinding")}</h3>
        {pathfinding.length ? pathfinding.slice(0, 6).map(metric => <MetricRow key={metric.id} metric={metric} />) : <p className={styles.empty}>{t("overview.pathfindingEmpty")}</p>}
      </section>
    </div>
  );
}
