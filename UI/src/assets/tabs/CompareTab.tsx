import React from "react";
import { formatObservation } from "../bindings";
import type { AssetRow, UiFinding } from "../types";
import { useText } from "../../i18n/locale";

export function normalizeCompareSelection(ids: string[]): string[] {
  const result: string[] = [];
  for (const id of ids) {
    if (!result.includes(id)) result.push(id);
    if (result.length === 4) break;
  }
  return result;
}

export function CompareTab({ assets, findings }: { assets: AssetRow[]; findings: UiFinding[] }): React.JSX.Element {
  const { t } = useText();
  const selected = assets.slice(0, 4);
  const notScanned = { availability: "NotScanned" } as const;
  return (
    <section className="apa__tab-content" aria-labelledby="apa-compare-title">
      <div className="apa__section-heading">
        <div><p className="apa__eyebrow">{t("compare.eyebrow")}</p><h2 id="apa-compare-title">{t("compare.title")}</h2></div>
        <span className="apa__muted">{t("compare.range")}</span>
      </div>
      {selected.length < 2 ? <p>{t("compare.selectHint")}</p> : (
        <div role="list" className="apa__compare-list">
          {selected.map((asset) => <article role="listitem" className="apa__metric-card" key={`${asset.prefabType}:${asset.prefabId}`}>
            <h3>{asset.displayName}</h3>
            <p>{t("compare.instances", { value: formatObservation(asset.instances, t) })}</p>
            <p>{t("compare.vertices", { value: formatObservation(asset.lod0Vertices ?? notScanned, t) })}</p>
            <p>{t("compare.retention", { value: formatObservation(asset.lod1RetentionPercent ?? notScanned, t) })}</p>
            <p>{t("compare.materials", { value: formatObservation(asset.materialCount ?? notScanned, t) })}</p>
            <p>{t("compare.payload", { value: formatObservation(asset.estimatedTexturePayload ?? notScanned, t) })}</p>
            <p>{t("compare.findings", { count: findings.filter((finding) => (!finding.prefabId || finding.prefabId === asset.prefabId) && (!finding.prefabType || finding.prefabType === asset.prefabType)).length })}</p>
          </article>)}
        </div>
      )}
      <p className="apa__muted">{t("compare.note")}</p>
    </section>
  );
}
