import React, { useState } from "react";
import { Button } from "cs2/ui";
import type { ModUiRow, SystemUiRow } from "../bindings";
import { formatMilliseconds } from "../format";
import { frameCost } from "./SystemsTab";
import styles from "../profiler.module.scss";

interface ModsTabProps {
  mods: ModUiRow[];
  systems?: SystemUiRow[];
}

const TOP_SYSTEMS = 10;

function costLabel(row: ModUiRow): string {
  return row.directCostBasis === "perSample" ? "直接システム時間（サンプル平均）" : "直接システム時間（フレーム平均）";
}

export function ModsTab({ mods, systems = [] }: ModsTabProps) {
  const [expanded, setExpanded] = useState<string | null>(null);
  const rows = [...mods].sort((a, b) => b.directSystemMilliseconds - a.directSystemMilliseconds || a.assemblyName.localeCompare(b.assemblyName));
  if (!rows.length) return <p className={styles.empty}>現在のスナップショットには、MODが直接所有するシステムの計測値がありません。</p>;

  return (
    <div className={styles.tabBody}>
      <p className={styles.explainer}>
        直接時間には、そのアセンブリが所有するシステムだけを含めます。各システムの合計時間をフレーム数で割った値を合算するため、1回だけ長く実行された処理（オートセーブ等）が合計を支配しません。バニラシステムへのパッチはメタデータとして表示し、その実行時間をパッチ所有者へ付け替えません。カードを選ぶと内訳を表示します。
      </p>
      <div className={styles.modGrid}>
        {rows.map(row => {
          const isExpanded = expanded === row.assemblyName;
          const owned = isExpanded
            ? systems.filter(system => system.ownerAssembly === row.assemblyName && !system.isAggregateContainer)
                .sort((a, b) => frameCost(b) - frameCost(a))
            : [];
          const patched = isExpanded
            ? systems.filter(system => system.ownerAssembly !== row.assemblyName && system.patchOwners.includes(row.assemblyName))
            : [];
          return (
            <Button
              as="button"
              variant="flat"
              key={row.assemblyName}
              className={`${styles.modCard} ${isExpanded ? styles.modCardExpanded : ""}`}
              onSelect={() => setExpanded(isExpanded ? null : row.assemblyName)}
              aria-expanded={isExpanded}
            >
              <strong>{isExpanded ? "▾ " : "▸ "}{row.assemblyName}</strong>
              <span>{costLabel(row)} <b>{formatMilliseconds(row.directSystemMilliseconds)}</b></span>
              <span>直接所有システム <b>{row.directSystemCount}</b></span>
              <span>パッチ対象システム <b>{row.patchedVanillaSystemCount}</b></span>
              {isExpanded && (
                <span className={styles.modBreakdown}>
                  {owned.slice(0, TOP_SYSTEMS).map(system => (
                    <span key={system.id} className={styles.modBreakdownRow}>
                      <em>{system.id}</em>
                      <b>{formatMilliseconds(frameCost(system))}</b>
                    </span>
                  ))}
                  {owned.length > TOP_SYSTEMS && <small>ほか {owned.length - TOP_SYSTEMS} 件（システムタブで確認できます）</small>}
                  {patched.length > 0 && <small>パッチ対象: {patched.slice(0, TOP_SYSTEMS).map(system => system.id).join(", ")}{patched.length > TOP_SYSTEMS ? " …" : ""}</small>}
                  {owned.length === 0 && patched.length === 0 && <small>このキャプチャには内訳がありません。</small>}
                </span>
              )}
            </Button>
          );
        })}
      </div>
    </div>
  );
}
