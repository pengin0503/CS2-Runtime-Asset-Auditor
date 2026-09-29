import React, { useMemo, useState } from "react";
import { Button } from "cs2/ui";
import type { SystemUiRow } from "../bindings";
import { formatInteger, formatMilliseconds } from "../format";
import { MetricBadge } from "../components/MetricBadge";
import styles from "../profiler.module.scss";
import { useText, type Translate } from "../../i18n/locale";
import type { MessageKey } from "../../i18n/messages";

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

function measurementSource(confidence: string, t: Translate): string {
  switch (confidence) {
    case "Full": return t("systems.source.full");
    case "Managed": return t("systems.source.managed");
    case "Indirect": return t("systems.source.indirect");
    default: return t("systems.source.unknown");
  }
}

const COLUMNS: Array<[SortKey, MessageKey, MessageKey]> = [
  ["frame", "systems.col.frame", "systems.col.frameTip"],
  ["mean", "systems.col.mean", "systems.col.meanTip"],
  ["p95", "systems.col.p95", "systems.col.p95Tip"],
  ["p99", "systems.col.p99", "systems.col.p99Tip"],
  ["max", "systems.col.max", "systems.col.maxTip"],
  ["calls", "systems.col.calls", "systems.col.callsTip"]
];

export function SystemsTab({ systems }: SystemsTabProps) {
  const { t } = useText();
  const [sortKey, setSortKey] = useState<SortKey>("frame");
  const [limit, setLimit] = useState(PAGE_SIZE);
  const [expanded, setExpanded] = useState<string | null>(null);
  const rows = useMemo(() => [...systems].sort((a, b) => {
    const av = sortValue(a, sortKey);
    const bv = sortValue(b, sortKey);
    if (typeof av === "string" && typeof bv === "string") return av.localeCompare(bv);
    return Number(bv) - Number(av) || a.id.localeCompare(b.id);
  }), [systems, sortKey]);

  if (!rows.length) return <p className={styles.empty}>{t("systems.empty")}</p>;

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
          ? t("systems.explainerPerFrame")
          : t("systems.explainerNoFrames")}
      </p>
      <div className={styles.systemList} role="table" aria-label={t("systems.tableAria")}>
        <div className={`${styles.systemRow} ${styles.systemHeader}`} role="row">
          <span className={styles.systemNameCell}>
            <Button as="button" variant="flat" className={`${styles.sortButton} ${sortKey === "name" ? styles.sortActive : ""}`} onSelect={() => changeSort("name")}>{t("systems.system")}</Button>
          </span>
          {COLUMNS.map(([key, label, tooltip]) => (
            <span key={key} className={styles.systemValueCell} title={t(tooltip)}>
              <Button as="button" variant="flat" className={`${styles.sortButton} ${sortKey === key ? styles.sortActive : ""}`} onSelect={() => changeSort(key)}>{t(label)}</Button>
            </span>
          ))}
          <span className={styles.systemBadgeCell}>{t("systems.basis")}</span>
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
                      {t("systems.owner", { owner: row.ownerAssembly || "—" })}
                      {patchOwners ? t("systems.patches", { owners: patchOwners }) : ""}
                      {row.isAggregateContainer ? t("systems.aggregate") : ""}
                    </small>
                  </Button>
                </span>
                <span className={styles.systemValueCell}>{formatMilliseconds(frameCost(row))}</span>
                <span className={styles.systemValueCell}>{formatMilliseconds(row.meanMilliseconds)}</span>
                <span className={styles.systemValueCell}>{formatMilliseconds(row.p95Milliseconds)}</span>
                <span className={styles.systemValueCell}>{formatMilliseconds(row.p99Milliseconds)}</span>
                <span className={styles.systemValueCell}>{formatMilliseconds(row.maxMilliseconds)}</span>
                <span className={styles.systemValueCell}>{formatInteger(row.calls)}</span>
                <span className={styles.systemBadgeCell} title={t("systems.source", { source: measurementSource(row.confidence, t) })}>
                  <MetricBadge confidence={row.confidence} availability={row.confidence === "Unavailable" ? "Unavailable" : "Available"} />
                </span>
              </div>
              {isExpanded && (
                <div className={styles.details}>
                  <div>{t("systems.source", { source: measurementSource(row.confidence, t) })}</div>
                  <div>{t("systems.detailTimes", { last: formatMilliseconds(row.currentMilliseconds), median: formatMilliseconds(row.medianMilliseconds), total: formatMilliseconds(row.totalMilliseconds) })}</div>
                  <div>{t("systems.patchOwners", { owners: patchOwners || t("systems.noPatchOwners") })}</div>
                  <div>{t("systems.noMarkerDetails")}</div>
                </div>
              )}
            </div>
          );
        })}
      </div>
      <div className={styles.actionRow}>
        <span className={styles.exportResult}>{t("systems.shown", { shown: shown.length, total: rows.length })}</span>
        {shown.length < rows.length && (
          <Button as="button" variant="flat" onSelect={() => setLimit(limit + PAGE_SIZE)}>{t("systems.showMore", { count: Math.min(PAGE_SIZE, rows.length - shown.length) })}</Button>
        )}
      </div>
    </div>
  );
}
