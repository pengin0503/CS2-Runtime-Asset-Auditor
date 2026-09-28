import React from "react";
import { Button } from "cs2/ui";
import type { UiMetricRow, UiSnapshot } from "../bindings";
import { formatMetricValue, formatPercent, formatSpeed, shortMetricName } from "../format";
import { MetricBadge } from "../components/MetricBadge";
import { captureStateLabel, exportResultLabel } from "../text";
import styles from "../profiler.module.scss";

interface OverviewTabProps {
  snapshot: UiSnapshot;
  onManualCapture: () => void;
  onExport: () => void;
  exportResult: string;
}

function MetricRow({ metric }: { metric: UiMetricRow }) {
  return (
    <div className={styles.metricRow}>
      <div className={styles.metricIdentity}>
        <strong>{shortMetricName(metric.id)}</strong>
        <small>{metric.id.includes("\u001f") ? metric.id.split("\u001f")[0] : "ランタイム"}</small>
      </div>
      <span className={styles.metricValue}>{formatMetricValue(metric)}</span>
      <MetricBadge confidence={metric.confidence} availability={metric.availability} reason={metric.reason} />
    </div>
  );
}

export function OverviewTab({ snapshot, onManualCapture, onExport, exportResult }: OverviewTabProps) {
  const pathfinding = snapshot.pathfinding?.metrics ?? [];
  const recorders = snapshot.global?.recorderMetrics ?? [];
  const canManualCapture = snapshot.capture.state === "Monitoring" || snapshot.capture.state === "Cooldown";

  return (
    <div className={styles.tabBody}>
      <section className={styles.summaryGrid}>
        <div className={styles.summaryCard}><span>指定速度</span><strong>{formatSpeed(snapshot.global.selectedSpeed)}</strong></div>
        <div className={styles.summaryCard}><span>実効速度</span><strong>{formatSpeed(snapshot.global.actualSpeed)}</strong></div>
        <div className={styles.summaryCard}><span>シミュレーション効率</span><strong>{formatPercent(snapshot.global.efficiency)}</strong></div>
        <div className={styles.summaryCard}><span>キャプチャ状態</span><strong>{captureStateLabel(snapshot.capture.state, snapshot.capture.isDeepCapture)}</strong></div>
        <div className={styles.summaryCard} title="詳細キャプチャ制御・集計処理を0.5秒の監視周期に対する割合で示します。ゲーム全体のプロファイラー負荷ではありません。"><span>キャプチャ処理負荷</span><strong>{formatPercent(snapshot.diagnostics.profilerOverheadShare)}</strong></div>
        <div className={styles.summaryCard} title="現在のC#スナップショットにはFPS専用フィールドがありません。"><span>FPS</span><strong>—</strong><small>利用不可</small></div>
      </section>

      <div className={styles.actionRow}>
        <Button as="button" variant="flat" onSelect={onManualCapture} disabled={!canManualCapture}>手動詳細キャプチャ</Button>
        <Button as="button" variant="flat" onSelect={onExport}>JSONをエクスポート</Button>
        {exportResult && <span className={styles.exportResult}>{exportResultLabel(exportResult)}</span>}
      </div>

      <section>
        <h3>グローバル記録メトリクス</h3>
        {recorders.length ? recorders.map(metric => <MetricRow key={metric.id} metric={metric} />) : <p className={styles.empty}>現在利用できるグローバル記録メトリクスはありません。</p>}
      </section>

      <section>
        <h3>経路探索サマリー</h3>
        {pathfinding.length ? pathfinding.slice(0, 6).map(metric => <MetricRow key={metric.id} metric={metric} />) : <p className={styles.empty}>対応するランタイムメンバーが見つかるまで、経路探索カウンターは利用できません。</p>}
      </section>
    </div>
  );
}
