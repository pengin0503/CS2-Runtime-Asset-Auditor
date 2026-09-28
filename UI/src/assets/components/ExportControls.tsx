import React, { useState } from "react";
import type { ExportAssetKey, ExportFormat, ExportRequest, ExportScope } from "../types";
import { ChoiceControl } from "./ChoiceControl";

export function ExportControls({
  selectedAsset,
  onExport,
}: {
  selectedAsset: ExportAssetKey | null;
  onExport: (request: ExportRequest) => void;
}): React.JSX.Element {
  const [format, setFormat] = useState<ExportFormat>("Json");
  const [scope, setScope] = useState<ExportScope>("Full");
  const selectedUnavailable = !selectedAsset;
  const effectiveScope: ExportScope = selectedUnavailable && scope === "Selected" ? "Full" : scope;

  return (
    <section className="apa__export-controls" aria-label="Export audit data">
      <div className="apa__section-heading">
        <div><p className="apa__eyebrow">Shared export action</p><h3>Export</h3></div>
      </div>
      <div className="apa__filters">
        <ChoiceControl label="Format" value={format} choices={[["Json", "JSON"], ["Csv", "CSV"]]} onChange={setFormat} />
        <ChoiceControl label="Scope" value={effectiveScope} choices={([
          ["Full", "Full report"], ["Filtered", "Filtered assets"],
          ...(!selectedUnavailable ? [["Selected", "Selected asset"]] : []),
          ["Census", "Census only"], ["Findings", "Findings only"]
        ] as Array<[ExportScope, string]>)} onChange={setScope} />
        <button
          type="button"
          className="apa__button"
          onClick={() => onExport({
            format,
            scope: effectiveScope,
            selectedKeys: effectiveScope === "Selected" && selectedAsset ? [selectedAsset] : [],
          })}
        >
          Save {format === "Json" ? "unified JSON" : "Asset CSV"} report
        </button>
      </div>
      {selectedUnavailable ? <p className="apa__muted">Select an asset to enable Selected export.</p> : null}
      <p className="apa__muted">Reports are saved in ModsData/CS2RuntimeAssetAuditor. Filtered Asset CSV includes all matching rows, not only the visible page.</p>
    </section>
  );
}
