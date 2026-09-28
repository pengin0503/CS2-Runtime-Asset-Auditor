import React from "react";
import { formatObservation } from "../bindings";
import type { AssetRow } from "../types";

export function GeometryDetails({ asset }: { asset: AssetRow }): React.JSX.Element {
  return (
    <section className="apa__detail-section" aria-label="Geometry details">
      <h3>Geometry</h3>
      <p><strong>LOD0 vertices:</strong> {formatObservation(asset.lod0Vertices ?? { availability: "NotScanned" })}</p>
    </section>
  );
}
