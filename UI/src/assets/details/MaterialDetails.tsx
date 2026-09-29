import React from "react";
import { formatObservation } from "../bindings";
import type { AssetRow } from "../types";
import { useText } from "../../i18n/locale";

export function MaterialDetails({ asset }: { asset: AssetRow }): React.JSX.Element {
  const { t } = useText();
  return (
    <section className="apa__detail-section" aria-label={t("details.materialsAria")}>
      <h3>{t("details.materials")}</h3>
      <p><strong>{t("details.materialCount")}</strong> {formatObservation(asset.materialCount ?? { availability: "NotScanned" }, t)}</p>
    </section>
  );
}
