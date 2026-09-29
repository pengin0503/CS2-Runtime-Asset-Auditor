import React from "react";
import { formatObservation } from "../bindings";
import type { AssetRow } from "../types";
import { useText } from "../../i18n/locale";

export function LodDetails({ asset }: { asset: AssetRow }): React.JSX.Element {
  const { t } = useText();
  return (
    <section className="apa__detail-section" aria-label={t("details.lodAria")}>
      <h3>{t("details.lod")}</h3>
      <p><strong>{t("details.lod1")}</strong> {formatObservation(asset.lod1RetentionPercent ?? { availability: "NotScanned" }, t)}</p>
    </section>
  );
}
