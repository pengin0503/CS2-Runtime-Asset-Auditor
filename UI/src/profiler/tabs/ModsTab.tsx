import React, { useState } from "react";
import { Button } from "cs2/ui";
import type { ModUiRow, SystemUiRow } from "../bindings";
import { formatMilliseconds } from "../format";
import { frameCost } from "./SystemsTab";
import styles from "../profiler.module.scss";
import { useText, type Translate } from "../../i18n/locale";

interface ModsTabProps {
  mods: ModUiRow[];
  systems?: SystemUiRow[];
}

const TOP_SYSTEMS = 10;

function costLabel(row: ModUiRow, t: Translate): string {
  return row.directCostBasis === "perSample" ? t("mods.costPerSample") : t("mods.costPerFrame");
}

export function ModsTab({ mods, systems = [] }: ModsTabProps) {
  const { t } = useText();
  const [expanded, setExpanded] = useState<string | null>(null);
  const rows = [...mods].sort((a, b) => b.directSystemMilliseconds - a.directSystemMilliseconds || a.assemblyName.localeCompare(b.assemblyName));
  if (!rows.length) return <p className={styles.empty}>{t("mods.empty")}</p>;

  return (
    <div className={styles.tabBody}>
      <p className={styles.explainer}>{t("mods.explainer")}</p>
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
              <span>{costLabel(row, t)} <b>{formatMilliseconds(row.directSystemMilliseconds)}</b></span>
              <span>{t("mods.ownedSystems")} <b>{row.directSystemCount}</b></span>
              <span>{t("mods.patchedSystems")} <b>{row.patchedVanillaSystemCount}</b></span>
              {isExpanded && (
                <span className={styles.modBreakdown}>
                  {owned.slice(0, TOP_SYSTEMS).map(system => (
                    <span key={system.id} className={styles.modBreakdownRow}>
                      <em>{system.id}</em>
                      <b>{formatMilliseconds(frameCost(system))}</b>
                    </span>
                  ))}
                  {owned.length > TOP_SYSTEMS && <small>{t("mods.more", { count: owned.length - TOP_SYSTEMS })}</small>}
                  {patched.length > 0 && <small>{t("mods.patchTargets", { targets: patched.slice(0, TOP_SYSTEMS).map(system => system.id).join(", ") + (patched.length > TOP_SYSTEMS ? " …" : "") })}</small>}
                  {owned.length === 0 && patched.length === 0 && <small>{t("mods.noBreakdown")}</small>}
                </span>
              )}
            </Button>
          );
        })}
      </div>
    </div>
  );
}
