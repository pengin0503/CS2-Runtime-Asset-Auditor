import React from "react";
import { formatObservation } from "../bindings";
import type { AssetRow } from "../types";

export function LodDetails({ asset }: { asset: AssetRow }): React.JSX.Element {
  return (
    <section className="apa__detail-section" aria-label="LOD details">
      <h3>LOD</h3>
      <p><strong>LOD1 vertex retention:</strong> {formatObservation(asset.lod1RetentionPercent ?? { availability: "NotScanned" })}</p>
    </section>
  );
}
