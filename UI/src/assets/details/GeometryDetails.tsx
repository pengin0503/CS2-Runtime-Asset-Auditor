import React from "react";
import { formatObservation } from "../bindings";
import type { AssetRow } from "../types";
import { useText } from "../../i18n/locale";

export function GeometryDetails({ asset }: { asset: AssetRow }): React.JSX.Element {
  const { t } = useText();
  return (
    <section className="apa__detail-section" aria-label={t("details.geometryAria")}>
      <h3>{t("details.geometry")}</h3>
      <p><strong>{t("details.lod0")}</strong> {formatObservation(asset.lod0Vertices ?? { availability: "NotScanned" }, t)}</p>
    </section>
  );
}
