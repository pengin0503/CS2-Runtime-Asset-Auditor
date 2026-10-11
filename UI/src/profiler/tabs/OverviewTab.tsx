import React from "react";
import { Button, Tooltip } from "cs2/ui";
import type { UiMetricRow, UiSnapshot } from "../bindings";
import { formatFramesPerSecond, formatMetricValue, formatMilliseconds, formatPercent, formatSpeed, shortMetricName } from "../format";
import { MetricBadge } from "../components/MetricBadge";
import { captureStateLabel, exportResultLabel } from "../text";
import { useText } from "../../i18n/locale";
import type { MessageKey } from "../../i18n/messages";
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

// Labels by counter id; an unknown id shows as sent.
const RENDER_COUNTER_KEYS: Record<string, MessageKey> = {
  "render.drawCalls": "overview.render.drawCalls",
  "render.batches": "overview.render.batches",
  "render.setPassCalls": "overview.render.setPassCalls",
  "render.triangles": "overview.render.triangles",
  "render.vertices": "overview.render.vertices",
  "render.shadowCasters": "overview.render.shadowCasters",
  "render.renderTexturesBytes": "overview.render.renderTexturesBytes",
  "render.usedBuffersBytes": "overview.render.usedBuffersBytes"
};

const VIRTUAL_TEXTURING_KEYS: Record<string, MessageKey> = {
  "vt.workingSetLodBias": "overview.vt.workingSetLodBias",
  "vt.workingSetLodBiasLevels": "overview.vt.workingSetLodBiasLevels",
  "vt.mipBias": "overview.vt.mipBias",
  "vt.busyRequests": "overview.vt.busyRequests",
  "vt.availableRequests": "overview.vt.availableRequests",
  "vt.materialsLeftToLoad": "overview.vt.materialsLeftToLoad"
};

function LabelledMetricRow({ metric, labels }: { metric: UiMetricRow; labels: Record<string, MessageKey> }) {
  const { t } = useText();
  const key = labels[metric.id];
  return (
    <div className={styles.metricRow}>
      <div className={styles.metricIdentity}>
        <strong>{key ? t(key) : metric.id}</strong>
      </div>
      <span className={styles.metricValue}>{formatMetricValue(metric)}</span>
      <MetricBadge confidence={metric.confidence} availability={metric.availability} reason={metric.reason} />
    </div>
  );
}

export function OverviewTab({ snapshot, onManualCapture, onExport, exportResult }: OverviewTabProps) {
  const pathfinding = snapshot.pathfinding?.metrics ?? [];
  const recorders = snapshot.global?.recorderMetrics ?? [];
  const renderCounters = snapshot.global?.renderCounters ?? [];
  const virtualTexturing = snapshot.global?.virtualTexturing ?? [];
  const { locale, t } = useText();
  const canManualCapture = snapshot.capture.state === "Monitoring" || snapshot.capture.state === "Cooldown";

  return (
    <div className={styles.tabBody}>
      <section className={styles.summaryGrid}>
        <div className={styles.summaryCard}><span>{t("overview.selectedSpeed")}</span><strong>{formatSpeed(snapshot.global.selectedSpeed)}</strong></div>
        <div className={styles.summaryCard}><span>{t("overview.actualSpeed")}</span><strong>{formatSpeed(snapshot.global.actualSpeed)}</strong></div>
        <div className={styles.summaryCard}><span>{t("overview.efficiency")}</span><strong>{formatPercent(snapshot.global.efficiency)}</strong></div>
        <div className={styles.summaryCard}><span>{t("overview.captureState")}</span><strong>{captureStateLabel(snapshot.capture.state, snapshot.capture.isDeepCapture, locale)}</strong></div>
        <Tooltip tooltip={t("overview.captureOverheadTooltip")}>
          <div className={styles.summaryCard}><span>{t("overview.captureOverhead")}</span><strong>{formatPercent(snapshot.diagnostics.profilerOverheadShare)}</strong></div>
        </Tooltip>
        <Tooltip tooltip={t("overview.fpsTooltip")}><div className={styles.summaryCard}>
          <span>{t("overview.fps")}</span>
          <strong>{formatFramesPerSecond(snapshot.global.framesPerSecond)}</strong>
          <small>{snapshot.global.framesPerSecond == null
            ? t("common.unavailable")
            : t("overview.frameTimes", {
              median: formatMilliseconds(snapshot.global.frameMsMedian),
              p95: formatMilliseconds(snapshot.global.frameMsP95)
            })}</small>
        </div></Tooltip>
      </section>

      <div className={styles.actionRow}>
        <Button as="button" variant="flat" onSelect={onManualCapture} disabled={!canManualCapture}>{t("overview.manualCapture")}</Button>
        <Button as="button" variant="flat" onSelect={onExport}>{t("overview.exportJson")}</Button>
        {exportResult && <span className={styles.exportResult}>{exportResultLabel(exportResult, locale)}</span>}
      </div>

      <section>
        <h3>{t("overview.render")}</h3>
        <p className={styles.empty}>{t("overview.renderNote")}</p>
        {renderCounters.length
          ? <div className={styles.metricList}>{renderCounters.map(metric => <LabelledMetricRow key={metric.id} metric={metric} labels={RENDER_COUNTER_KEYS} />)}</div>
          : <p className={styles.empty}>{t("overview.renderEmpty")}</p>}
      </section>

      <section>
        <h3>{t("overview.vt")}</h3>
        <p className={styles.empty}>{t("overview.vtNote")}</p>
        {virtualTexturing.length
          ? <div className={styles.metricList}>{virtualTexturing.map(metric => <LabelledMetricRow key={metric.id} metric={metric} labels={VIRTUAL_TEXTURING_KEYS} />)}</div>
          : <p className={styles.empty}>{t("overview.vtEmpty")}</p>}
      </section>

      <section>
        <h3>{t("overview.recorders")}</h3>
        {recorders.length ? <div className={styles.metricList}>{recorders.map(metric => <MetricRow key={metric.id} metric={metric} />)}</div> : <p className={styles.empty}>{t("overview.recordersEmpty")}</p>}
      </section>

      <section>
        <h3>{t("overview.pathfinding")}</h3>
        {pathfinding.length ? <div className={styles.metricList}>{pathfinding.slice(0, 6).map(metric => <MetricRow key={metric.id} metric={metric} />)}</div> : <p className={styles.empty}>{t("overview.pathfindingEmpty")}</p>}
      </section>
    </div>
  );
}
