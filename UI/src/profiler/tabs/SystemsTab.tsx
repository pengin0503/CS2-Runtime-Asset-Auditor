import React, { useMemo, useState } from "react";
import { Button } from "cs2/ui";
import type { SystemUiRow } from "../bindings";
import { formatInteger, formatMilliseconds } from "../format";
import { MetricBadge } from "../components/MetricBadge";
import styles from "../profiler.module.scss";

type SortKey = "frame" | "mean" | "p95" | "p99" | "max" | "calls" | "name";

interface SystemsTabProps {
  systems: SystemUiRow[];
}

// Rendering every row of a 1,000+ system catalog on each 0.5 s refresh makes Gameface scrolling sluggish.
const PAGE_SIZE = 100;

/** Additive per-frame impact; falls back to the per-sample mean (marker timing) or the last value. */
export function frameCost(row: SystemUiRow): number {
  return row.millisecondsPerFrame ?? row.meanMilliseconds ?? row.currentMilliseconds;
}

function sortValue(row: SystemUiRow, key: SortKey): number | string {
  switch (key) {
    case "frame": return frameCost(row);
    case "mean": return row.meanMilliseconds ?? -1;
    case "p95": return row.p95Milliseconds ?? -1;
    case "p99": return row.p99Milliseconds ?? -1;
    case "max": return row.maxMilliseconds ?? -1;
    case "calls": return row.calls ?? -1;
    case "name": return row.id;
  }
}

function measurementSource(confidence: string): string {
  switch (confidence) {
    case "Full": return "Unity ECS プロファイラーマーカー";
    case "Managed": return "管理システムの実行境界（管理コードの OnUpdate 同期実行時間。ジョブ/Burst のワーカー時間は含みません）";
    case "Indirect": return "間接的なランタイムカウンター";
    default: return "不明";
  }
}

const COLUMNS: Array<[SortKey, string, string]> = [
  ["frame", "フレーム平均", "キャプチャ中の合計時間 ÷ 描画フレーム数。更新頻度の異なるシステム同士を比較でき、合算できます。"],
  ["mean", "平均/回", "1回の呼び出しあたりの平均時間。まれに実行されるシステムでは大きく見えます。"],
  ["p95", "P95", "1回あたりの時間の95パーセンタイル。"],
  ["p99", "P99", "1回あたりの時間の99パーセンタイル。"],
  ["max", "最大", "キャプチャ中で最も長かった1回の時間。"],
  ["calls", "呼出回数", "キャプチャ中の呼び出し回数。"]
];

export function SystemsTab({ systems }: SystemsTabProps) {
  const [sortKey, setSortKey] = useState<SortKey>("frame");
  const [limit, setLimit] = useState(PAGE_SIZE);
  const [expanded, setExpanded] = useState<string | null>(null);
  const rows = useMemo(() => [...systems].sort((a, b) => {
    const av = sortValue(a, sortKey);
    const bv = sortValue(b, sortKey);
    if (typeof av === "string" && typeof bv === "string") return av.localeCompare(bv);
    return Number(bv) - Number(av) || a.id.localeCompare(b.id);
  }), [systems, sortKey]);

  if (!rows.length) return <p className={styles.empty}>システム別の実行時間は、対応する詳細キャプチャが作成されるまで利用できません。</p>;

  const changeSort = (key: SortKey) => {
    setSortKey(key);
    setLimit(PAGE_SIZE);
  };
  const perFrameAvailable = rows.some(row => row.millisecondsPerFrame != null);
  const shown = rows.slice(0, limit);

  return (
    <div className={styles.tabBody}>
      <p className={styles.explainer}>
        {perFrameAvailable
          ? "既定では「フレーム平均」（合計時間 ÷ フレーム数）の大きい順に並べます。オートセーブ時のシリアライズのように1回だけ長く実行されたシステムは、平均/回・最大が大きくてもフレーム平均は小さくなります。"
          : "このキャプチャにはフレーム数がないため、「フレーム平均」列はサンプル平均で代用しています。"}
      </p>
      <div className={styles.systemList} role="table" aria-label="システム別の実行時間">
        <div className={`${styles.systemRow} ${styles.systemHeader}`} role="row">
          <span className={styles.systemNameCell}>
            <Button as="button" variant="flat" className={`${styles.sortButton} ${sortKey === "name" ? styles.sortActive : ""}`} onSelect={() => changeSort("name")}>システム</Button>
          </span>
          {COLUMNS.map(([key, label, tooltip]) => (
            <span key={key} className={styles.systemValueCell} title={tooltip}>
              <Button as="button" variant="flat" className={`${styles.sortButton} ${sortKey === key ? styles.sortActive : ""}`} onSelect={() => changeSort(key)}>{label}</Button>
            </span>
          ))}
          <span className={styles.systemBadgeCell}>根拠</span>
        </div>

        {shown.map(row => {
          const isExpanded = expanded === row.id;
          const patchOwners = row.patchOwners.length ? row.patchOwners.join(", ") : "";
          return (
            <div key={row.id} className={styles.systemEntry}>
              <div className={styles.systemRow} role="row">
                <span className={styles.systemNameCell}>
                  <Button as="button" variant="flat" className={styles.systemNameButton} onSelect={() => setExpanded(isExpanded ? null : row.id)} aria-expanded={isExpanded}>
                    <strong>{isExpanded ? "▾ " : "▸ "}{row.id}</strong>
                    <small>
                      所有元: {row.ownerAssembly || "—"}
                      {patchOwners ? ` ・ パッチ: ${patchOwners}` : ""}
                      {row.isAggregateContainer ? " ・ 集約グループ（MOD合計に含めません）" : ""}
                    </small>
                  </Button>
                </span>
                <span className={styles.systemValueCell}>{formatMilliseconds(frameCost(row))}</span>
                <span className={styles.systemValueCell}>{formatMilliseconds(row.meanMilliseconds)}</span>
                <span className={styles.systemValueCell}>{formatMilliseconds(row.p95Milliseconds)}</span>
                <span className={styles.systemValueCell}>{formatMilliseconds(row.p99Milliseconds)}</span>
                <span className={styles.systemValueCell}>{formatMilliseconds(row.maxMilliseconds)}</span>
                <span className={styles.systemValueCell}>{formatInteger(row.calls)}</span>
                <span className={styles.systemBadgeCell} title={`測定元: ${measurementSource(row.confidence)}`}>
                  <MetricBadge confidence={row.confidence} availability={row.confidence === "Unavailable" ? "Unavailable" : "Available"} />
                </span>
              </div>
              {isExpanded && (
                <div className={styles.details}>
                  <div>測定元: {measurementSource(row.confidence)}</div>
                  <div>最後の呼び出し: {formatMilliseconds(row.currentMilliseconds)} ・ 中央値: {formatMilliseconds(row.medianMilliseconds)} ・ キャプチャ中の合計: {formatMilliseconds(row.totalMilliseconds)}</div>
                  <div>パッチ所有者: {patchOwners || "検出なし"}</div>
                  <div>マーカーや更新間隔の詳細は現在のスナップショットでは公開されていません。</div>
                </div>
              )}
            </div>
          );
        })}
      </div>
      <div className={styles.actionRow}>
        <span className={styles.exportResult}>{shown.length} / {rows.length} 件を表示</span>
        {shown.length < rows.length && (
          <Button as="button" variant="flat" onSelect={() => setLimit(limit + PAGE_SIZE)}>さらに {Math.min(PAGE_SIZE, rows.length - shown.length)} 件表示</Button>
        )}
      </div>
    </div>
  );
}
