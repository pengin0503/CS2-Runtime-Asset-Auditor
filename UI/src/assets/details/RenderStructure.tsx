import React from "react";
import type { RenderCoverage, UiRenderRelation } from "../types";

export function RenderStructure({
  coverage,
  relations,
  onDeepInspect,
}: {
  coverage: RenderCoverage;
  relations: UiRenderRelation[];
  onDeepInspect?: (renderKey: string) => void;
}): React.JSX.Element {
  if (coverage !== "Available") {
    const label = coverage === "NotScanned" ? "Not scanned" : coverage;
    return (
      <section className="apa__detail-section" aria-label="Render structure">
        <h3>Render Structure</h3>
        <p>{label}</p>
        <p className="apa__muted">Render coverage is unavailable; no zero-geometry inference is made.</p>
      </section>
    );
  }

  return (
    <section className="apa__detail-section" aria-label="Render structure">
      <h3>Render Structure</h3>
      {relations.length === 0 ? <p>No render relations were recorded.</p> : (
        <ul>
          {relations.map((relation, index) => (
            <li key={`${relation.kind}:${relation.from}:${relation.to}:${index}`}>
              <span>{relation.from} → {relation.to} ({relation.kind}{relation.lodLevel == null ? "" : ` LOD${relation.lodLevel}`})</span>
              {onDeepInspect ? (
                <button type="button" className="apa__link-button" onClick={() => onDeepInspect(relation.to)}>Deep inspect</button>
              ) : null}
              {relation.deepInspection ? (
                <div className="apa__deep-inspection">
                  <p><strong>Deep inspection:</strong> {relation.deepInspection.availability}</p>
                  {relation.deepInspection.materials.map((material, materialIndex) => (
                    <p key={`${relation.to}:material:${materialIndex}`} className="apa__muted">
                      {material.materialName || "Unnamed material"} · {material.shaderName || "Unknown shader"} · {material.passCount} passes
                    </p>
                  ))}
                </div>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
