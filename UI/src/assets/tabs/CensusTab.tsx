import React from "react";
import { ObservationValue } from "../components/ScanStatus";
import type { UiSnapshot } from "../types";
import { useText } from "../../i18n/locale";

export function CensusTab({ snapshot }: { snapshot: UiSnapshot }): React.JSX.Element {
  const { t } = useText();
  const { summary } = snapshot;
  const metrics = [
    [t("census.topLevel"), summary.censusCounts.topLevelObjects, t("census.topLevelDetail")],
    [t("census.subordinate"), summary.censusCounts.subordinateObjects, t("census.subordinateDetail")],
    [t("census.live"), summary.censusCounts.liveObjectReferences, t("census.liveDetail")],
    [t("census.edges"), summary.censusCounts.networkEdges, t("census.edgesDetail")],
  ] as const;

  return (
    <section className="apa__tab-content" aria-labelledby="apa-census-title">
      <div className="apa__section-heading">
        <div>
          <p className="apa__eyebrow">{t("census.queryProfile", { version: summary.queryProfileVersion ?? t("census.notScannedProfile") })}</p>
          <h2 id="apa-census-title">{t("census.title")}</h2>
        </div>
        <span className="apa__muted">{summary.censusCapturedAt ?? t("census.noSnapshot")}</span>
      </div>
      {!summary.censusWasScanned ? (
        <p className="apa__empty-state">{t("census.runHint")}</p>
      ) : null}
      {summary.censusWasScanned && !summary.censusMatchesCatalog ? (
        <p className="apa__empty-state">
          {t("census.stale", { censusGeneration: summary.censusCatalogGeneration ?? "?", catalogGeneration: summary.catalogGeneration })}
        </p>
      ) : null}
      <div className="apa__metric-grid">
        {metrics.map(([label, observation, detail]) => (
          <article className="apa__metric-card" key={label}>
            <p className="apa__eyebrow">{label}</p>
            <strong className="apa__metric-value"><ObservationValue label={label} observation={observation} /></strong>
            <span className="apa__muted">{detail}</span>
          </article>
        ))}
      </div>
      <p className="apa__muted">{t("census.note")}</p>
    </section>
  );
}
