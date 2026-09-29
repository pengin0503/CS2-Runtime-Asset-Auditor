import React from "react";
import type { RenderCoverage, UiRenderRelation } from "../types";
import { useText } from "../../i18n/locale";

export function RenderStructure({
  coverage,
  relations,
  onDeepInspect,
}: {
  coverage: RenderCoverage;
  relations: UiRenderRelation[];
  onDeepInspect?: (renderKey: string) => void;
}): React.JSX.Element {
  const { t } = useText();
  if (coverage !== "Available") {
    const label = coverage === "NotScanned" ? t("obs.notScanned") : coverage;
    return (
      <section className="apa__detail-section" aria-label={t("render.aria")}>
        <h3>{t("render.title")}</h3>
        <p>{label}</p>
        <p className="apa__muted">{t("render.unavailableNote")}</p>
      </section>
    );
  }

  return (
    <section className="apa__detail-section" aria-label={t("render.aria")}>
      <h3>{t("render.title")}</h3>
      {relations.length === 0 ? <p>{t("render.none")}</p> : (
        <ul>
          {relations.map((relation, index) => (
            <li key={`${relation.kind}:${relation.from}:${relation.to}:${index}`}>
              <span>{relation.from} → {relation.to} ({relation.kind}{relation.lodLevel == null ? "" : ` LOD${relation.lodLevel}`})</span>
              {onDeepInspect ? (
                <button type="button" className="apa__link-button" onClick={() => onDeepInspect(relation.to)}>{t("render.deepInspect")}</button>
              ) : null}
              {relation.deepInspection ? (
                <div className="apa__deep-inspection">
                  <p><strong>{t("render.deepInspection")}</strong> {relation.deepInspection.availability}</p>
                  {relation.deepInspection.materials.length > 0 ? <p className="apa__muted">{t("render.deepInspectionBasis")}</p> : null}
                  {relation.deepInspection.materials.map((material, materialIndex) => (
                    <p key={`${relation.to}:material:${materialIndex}`} className="apa__muted">
                      {material.materialName || t("render.unnamedMaterial")} · {material.shaderName || t("render.unknownShader")} · {t("render.passes", { count: material.passCount })}
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
