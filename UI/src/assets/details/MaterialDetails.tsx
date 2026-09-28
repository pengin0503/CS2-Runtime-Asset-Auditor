import React from "react";
import { formatObservation } from "../bindings";
import type { AssetRow } from "../types";

export function MaterialDetails({ asset }: { asset: AssetRow }): React.JSX.Element {
  return (
    <section className="apa__detail-section" aria-label="Material details">
      <h3>Materials</h3>
      <p><strong>Material count:</strong> {formatObservation(asset.materialCount ?? { availability: "NotScanned" })}</p>
    </section>
  );
}
