import React, { useState } from "react";
import type { ExportAssetKey, ExportFormat, ExportRequest, ExportScope } from "../types";
import { ChoiceControl } from "./ChoiceControl";
import { useText } from "../../i18n/locale";
import type { MessageKey } from "../../i18n/messages";

export function ExportControls({
  selectedAsset,
  onExport,
}: {
  selectedAsset: ExportAssetKey | null;
  onExport: (request: ExportRequest) => void;
}): React.JSX.Element {
  const { t } = useText();
  const [format, setFormat] = useState<ExportFormat>("Json");
  const [scope, setScope] = useState<ExportScope>("Full");
  const selectedUnavailable = !selectedAsset;
  const effectiveScope: ExportScope = selectedUnavailable && scope === "Selected" ? "Full" : scope;
  const scopes: ExportScope[] = ["Full", "Filtered", ...(!selectedUnavailable ? ["Selected" as ExportScope] : []), "Census", "Findings"];

  return (
    <section className="apa__export-controls" aria-label={t("exportc.aria")}>
      <div className="apa__section-heading">
        <div><p className="apa__eyebrow">{t("exportc.eyebrow")}</p><h3>{t("exportc.title")}</h3></div>
      </div>
      <div className="apa__filters">
        <ChoiceControl label={t("exportc.format")} value={format} choices={[["Json", "JSON"], ["Csv", "CSV"]]} onChange={setFormat} />
        <ChoiceControl label={t("exportc.scope")} value={effectiveScope}
          choices={scopes.map(value => [value, t(`exportc.scope.${value}` as MessageKey)] as const)} onChange={setScope} />
        <button
          type="button"
          className="apa__button"
          onClick={() => onExport({
            format,
            scope: effectiveScope,
            selectedKeys: effectiveScope === "Selected" && selectedAsset ? [selectedAsset] : [],
          })}
        >
          {format === "Json" ? t("exportc.saveJson") : t("exportc.saveCsv")}
        </button>
      </div>
      {selectedUnavailable ? <p className="apa__muted">{t("exportc.selectHint")}</p> : null}
      <p className="apa__muted">{t("exportc.location")}</p>
    </section>
  );
}
