import React from "react";
import { formatCountKind, formatObservation } from "../bindings";
import { EvidencePanel } from "../components/EvidencePanel";
import type { AssetRow, UiFinding } from "../types";
import { GeometryDetails } from "./GeometryDetails";
import { LodDetails } from "./LodDetails";
import { MaterialDetails } from "./MaterialDetails";
import { RenderStructure } from "./RenderStructure";
import { TextureDetails } from "./TextureDetails";

export function AssetDetails({
  asset,
  findings,
  onDeepInspect,
  onOpenRuntimeCaptures,
}: {
  asset: AssetRow;
  findings: UiFinding[];
  onDeepInspect?: (renderKey: string) => void;
  onOpenRuntimeCaptures?: () => void;
}): React.JSX.Element {
  const assetFindings = findings.filter((finding) =>
    (!finding.prefabId || finding.prefabId === asset.prefabId)
    && (!finding.prefabType || finding.prefabType === asset.prefabType));

  return (
    <section className="apa__asset-details" aria-label={`Asset details for ${asset.displayName}`}>
      <div className="apa__section-heading">
        <div>
          <p className="apa__eyebrow">Asset Details</p>
          <h2>{asset.displayName}</h2>
        </div>
        <span className="apa__asset-id">{asset.prefabType}:{asset.prefabId}</span>
      </div>
      <section className="apa__detail-section">
        <h3>Summary</h3>
        <p>{asset.sourceLabel} · {asset.traits.join(", ")}</p>
      </section>
      <section className="apa__detail-section">
        <h3>City Exposure</h3>
        <p><strong>{formatCountKind(asset.countKind)}:</strong> {formatObservation(asset.instances)}</p>
        <p className="apa__muted">Instance counts indicate city exposure, not render cost.</p>
        {onOpenRuntimeCaptures && <button type="button" className="apa__button" onClick={onOpenRuntimeCaptures}>ランタイムキャプチャを表示</button>}
      </section>
      <RenderStructure coverage={asset.renderCoverage ?? "NotScanned"} relations={asset.renderRelations ?? []} onDeepInspect={onDeepInspect} />
      <GeometryDetails asset={asset} />
      <LodDetails asset={asset} />
      <MaterialDetails asset={asset} />
      <TextureDetails payload={asset.estimatedTexturePayload ?? { availability: "NotScanned" }} />
      <section className="apa__detail-section">
        <h3>Findings</h3>
        {assetFindings.length === 0 ? <p>No findings recorded for this asset.</p> : assetFindings.map((finding) => <EvidencePanel key={`${finding.ruleId}:${finding.prefabId ?? "global"}`} finding={finding} />)}
      </section>
    </section>
  );
}
