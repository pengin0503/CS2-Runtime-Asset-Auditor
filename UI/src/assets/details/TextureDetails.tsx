import React from "react";
import { formatObservation } from "../bindings";
import type { UiObservation } from "../types";
import { useText } from "../../i18n/locale";

export function TextureDetails({ payload }: { payload: UiObservation<number> }): React.JSX.Element {
  const { t } = useText();
  return (
    <section className="apa__detail-section" aria-label={t("details.texturesAria")}>
      <h3>{t("details.textures")}</h3>
      <p><strong>{t("details.payload")}</strong> {formatObservation(payload, t)}</p>
      <p className="apa__muted">{t("details.payloadNote")}</p>
    </section>
  );
}
