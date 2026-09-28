import React from "react";
import { formatObservation } from "../bindings";
import type { AssetRow, UiFinding } from "../types";

export function normalizeCompareSelection(ids: string[]): string[] {
  const result: string[] = [];
  for (const id of ids) {
    if (!result.includes(id)) result.push(id);
    if (result.length === 4) break;
  }
  return result;
}

export function CompareTab({ assets, findings }: { assets: AssetRow[]; findings: UiFinding[] }): React.JSX.Element {
  const selected = assets.slice(0, 4);
  return (
    <section className="apa__tab-content" aria-labelledby="apa-compare-title">
      <div className="apa__section-heading">
        <div><p className="apa__eyebrow">Direct evidence comparison</p><h2 id="apa-compare-title">Compare</h2></div>
        <span className="apa__muted">2–4 assets</span>
      </div>
      {selected.length < 2 ? <p>Select at least two assets from the current bounded result page to compare them.</p> : (
        <div role="list" className="apa__compare-list">
          {selected.map((asset) => <article role="listitem" className="apa__metric-card" key={`${asset.prefabType}:${asset.prefabId}`}>
            <h3>{asset.displayName}</h3>
            <p>Instances: {formatObservation(asset.instances)}</p>
            <p>Geometry / LOD0 vertices: {formatObservation(asset.lod0Vertices ?? { availability: "NotScanned" })}</p>
            <p>LOD1 retention: {formatObservation(asset.lod1RetentionPercent ?? { availability: "NotScanned" })}</p>
            <p>Materials: {formatObservation(asset.materialCount ?? { availability: "NotScanned" })}</p>
            <p>Estimated texture payload: {formatObservation(asset.estimatedTexturePayload ?? { availability: "NotScanned" })}</p>
            <p>Findings: {findings.filter((finding) => (!finding.prefabId || finding.prefabId === asset.prefabId) && (!finding.prefabType || finding.prefabType === asset.prefabType)).length}</p>
          </article>)}
        </div>
      )}
      <p className="apa__muted">Comparison presents independent evidence dimensions only; it does not calculate an overall performance score.</p>
    </section>
  );
}
