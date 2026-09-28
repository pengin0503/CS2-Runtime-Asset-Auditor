import React, { useMemo, useState } from "react";
import { Button } from "cs2/ui";
import type { TimelinePoint } from "../bindings";
import { formatByUnit, shortMetricName } from "../format";
import { confidenceLabel } from "../text";
import styles from "../profiler.module.scss";

interface MetricIdentity {
  kind: string;
  qualifier: string;
  shortName: string;
}

function metricIdentity(metric: string): MetricIdentity {
  const prefix = metric.match(/^(recorder|marker|system):/);
  const kind = prefix?.[1] ?? "metric";
  const body = metric.replace(/^(recorder|marker|system):/, "");
  const parts = body.split("\u001f");
  return {
    kind,
    qualifier: parts.length > 1 ? parts.slice(0, -1).join(" / ") : "",
    shortName: shortMetricName(body)
  };
}

export function buildSeriesLabels(metrics: string[]): Map<string, string> {
  const identities = metrics.map(metric => ({ metric, ...metricIdentity(metric) }));
  const shortCounts = new Map<string, number>();
  for (const item of identities) shortCounts.set(item.shortName, (shortCounts.get(item.shortName) ?? 0) + 1);

  const firstPass = identities.map(item => ({
    ...item,
    label: (shortCounts.get(item.shortName) ?? 0) > 1
      ? `${item.qualifier || item.kind} / ${item.shortName}`
      : item.shortName
  }));
  const labelCounts = new Map<string, number>();
  for (const item of firstPass) labelCounts.set(item.label, (labelCounts.get(item.label) ?? 0) + 1);

  const result = new Map<string, string>();
  for (const item of firstPass) {
    result.set(item.metric, (labelCounts.get(item.label) ?? 0) > 1
      ? `${item.kind} / ${item.label}`
      : item.label);
  }
  return result;
}

interface GeometryPoint {
  point: TimelinePoint;
  x: number;
  y: number;
}

interface Series {
  metric: string;
  points: TimelinePoint[];
  unitType: string;
}

const SERIES_COLORS = ["#63b5ff", "#f2b84b", "#6fd08c", "#e07bd8", "#ff7a6b", "#9ea7ff", "#4fd1c5", "#d7e36b"];
const SERIES_DASHES = ["", "10 5", "3 4", "10 4 2 4"];

export function seriesVisualIdentity(index: number): { color: string; dash: string } {
  const safeIndex = Math.max(0, Math.floor(index));
  return {
    color: SERIES_COLORS[safeIndex % SERIES_COLORS.length],
    dash: SERIES_DASHES[Math.floor(safeIndex / SERIES_COLORS.length) % SERIES_DASHES.length]
  };
}

function geometry(points: TimelinePoint[], minTime: number, maxTime: number): GeometryPoint[] {
  if (!points.length) return [];
  const values = points.map(point => point.value);
  const min = Math.min(...values);
  const max = Math.max(...values);
  const timeSpan = Math.max(0.0001, maxTime - minTime);
  const valueSpan = Math.max(0.0001, max - min);
  return points.map(point => ({
    point,
    x: 28 + ((point.timestampSeconds - minTime) / timeSpan) * 944,
    y: max === min ? 130 : 232 - ((point.value - min) / valueSpan) * 204
  }));
}

/**
 * Only series with at least two samples can form a line. Single end-of-capture readings (one per
 * profiler marker and one per system, over a thousand in a real city) are not a time series; they
 * are summarized as a count and remain available in the Systems tab and the JSON export.
 */
export function chartableSeries(points: TimelinePoint[]): { series: Series[]; singleSampleCount: number } {
  const grouped = new Map<string, TimelinePoint[]>();
  for (const point of points ?? []) {
    if (point.metric.startsWith("system:")) continue;
    const list = grouped.get(point.metric);
    if (list) list.push(point); else grouped.set(point.metric, [point]);
  }
  const series: Series[] = [];
  let singleSampleCount = 0;
  for (const [metric, list] of grouped) {
    if (list.length < 2) {
      singleSampleCount++;
      continue;
    }
    list.sort((a, b) => a.timestampSeconds - b.timestampSeconds);
    series.push({ metric, points: list, unitType: list.find(point => point.unitType)?.unitType ?? "" });
  }
  series.sort((a, b) => a.metric.localeCompare(b.metric));
  return { series, singleSampleCount };
}

export function TimelineTab({ points }: { points: TimelinePoint[] }) {
  const { series, singleSampleCount } = useMemo(() => chartableSeries(points), [points]);
  const labels = useMemo(() => buildSeriesLabels(series.map(item => item.metric)), [series]);
  const [hidden, setHidden] = useState<Set<string>>(() => new Set());
  const [selectedTime, setSelectedTime] = useState<number | null>(null);

  const toggle = (metric: string) => setHidden(previous => {
    const next = new Set(previous);
    if (next.has(metric)) next.delete(metric); else next.add(metric);
    return next;
  });

  if (!series.length) {
    return <p className={styles.empty}>保持されているタイムライン履歴はまだありません。単一時点のカウンターから履歴を捏造することはありません。</p>;
  }

  const allPoints = series.flatMap(item => item.points);
  const minTime = Math.min(...allPoints.map(point => point.timestampSeconds));
  const maxTime = Math.max(...allPoints.map(point => point.timestampSeconds));
  const visualOf = (metric: string) => seriesVisualIdentity(series.findIndex(item => item.metric === metric));
  const labelOf = (metric: string) => labels.get(metric) ?? metricIdentity(metric).shortName;
  const visible = series.filter(item => !hidden.has(item.metric));
  const selected = selectedTime == null
    ? []
    : series.flatMap(item => item.points
        .filter(point => Math.abs(point.timestampSeconds - selectedTime) < 0.0001)
        .map(point => ({ point, unitType: item.unitType })));

  return (
    <div className={styles.tabBody}>
      <div className={styles.seriesControls}>
        {series.map(item => {
          const shown = !hidden.has(item.metric);
          const visual = visualOf(item.metric);
          return (
            <Button
              as="button"
              variant="flat"
              key={item.metric}
              selected={shown}
              className={`${styles.seriesToggle} ${shown ? styles.seriesToggleOn : ""}`}
              onSelect={() => toggle(item.metric)}
              aria-pressed={shown}
            >
              <svg className={styles.seriesSwatch} viewBox="0 0 18 10" aria-hidden="true">
                <line x1="1" y1="5" x2="17" y2="5" stroke={visual.color} strokeWidth="3"
                  strokeDasharray={visual.dash || undefined} opacity={shown ? 1 : 0.45} />
              </svg>
              <span>{labelOf(item.metric)}</span>
            </Button>
          );
        })}
      </div>

      <div className={styles.chartWrap}>
        <svg className={styles.timelineChart} viewBox="0 0 1000 260" role="img" aria-label="プロファイラーのタイムライン">
          <line x1="28" y1="232" x2="972" y2="232" className={styles.chartAxis} />
          <line x1="28" y1="28" x2="28" y2="232" className={styles.chartAxis} />
          {visible.map(item => {
            const coords = geometry(item.points, minTime, maxTime);
            const visual = visualOf(item.metric);
            return (
              <g key={item.metric} data-series={item.metric}>
                <polyline points={coords.map(coord => `${coord.x.toFixed(1)},${coord.y.toFixed(1)}`).join(" ")}
                  fill="none" stroke={visual.color} strokeDasharray={visual.dash || undefined} className={styles.chartLine} />
                {coords.map(({ point, x, y }, index) => (
                  <circle key={`${point.timestampSeconds}-${index}`} cx={x} cy={y} r="5" fill={visual.color} className={styles.chartPoint} onClick={() => setSelectedTime(point.timestampSeconds)}>
                    <title>{`${labelOf(item.metric)} / ${point.timestampSeconds.toFixed(2)}秒 = ${formatByUnit(point.value, item.unitType)}（${confidenceLabel(point.confidence)}）`}</title>
                  </circle>
                ))}
              </g>
            );
          })}
        </svg>
      </div>
      <p className={styles.chartNote}>
        形状比較のため各系列は観測範囲ごとに正規化しています。生の値はポイントのホバー／選択で確認できます。色が再利用される場合は線種も変わります。
        {singleSampleCount > 0 ? ` 1点しかない値（${singleSampleCount} 件）は線にならないため表示していません。` : ""}
      </p>
      {selectedTime != null && (
        <div className={styles.selectedPoint}>
          <strong>時刻 = {selectedTime.toFixed(2)} 秒</strong>
          {selected.map(({ point, unitType }) => (
            <span key={point.metric}>{labelOf(point.metric)}: {formatByUnit(point.value, unitType)}（{confidenceLabel(point.confidence)}）</span>
          ))}
        </div>
      )}
    </div>
  );
}
