import React from "react";
import { ObservationValue } from "../components/ScanStatus";
import type { UiSnapshot } from "../types";

export function CensusTab({ snapshot }: { snapshot: UiSnapshot }): React.JSX.Element {
  const { summary } = snapshot;
  const metrics = [
    ["Top-level objects", summary.censusCounts.topLevelObjects, "Building, service, and tree instances"],
    ["Subordinate objects", summary.censusCounts.subordinateObjects, "Owned or controlled child objects"],
    ["Live object references", summary.censusCounts.liveObjectReferences, "Top-level plus subordinate objects"],
    ["Network edges", summary.censusCounts.networkEdges, "Current top-level edge PrefabRefs"],
  ] as const;

  return (
    <section className="apa__tab-content" aria-labelledby="apa-census-title">
      <div className="apa__section-heading">
        <div>
          <p className="apa__eyebrow">Query profile {summary.queryProfileVersion ?? "not scanned"}</p>
          <h2 id="apa-census-title">Census</h2>
        </div>
        <span className="apa__muted">{summary.censusCapturedAt ?? "No successful snapshot"}</span>
      </div>
      {!summary.censusWasScanned ? (
        <p className="apa__empty-state">Run Census to capture a city snapshot. Unscanned counts stay distinct from zero.</p>
      ) : null}
      {summary.censusWasScanned && !summary.censusMatchesCatalog ? (
        <p className="apa__empty-state">
          This Census belongs to catalog generation {summary.censusCatalogGeneration ?? "unknown"};
          current catalog generation is {summary.catalogGeneration}. Totals below are from that older snapshot, and asset row counts are marked Not scanned until the next Census completes.
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
      <p className="apa__muted">Vehicle instances are a snapshot count. Disabled metrics remain Not scanned.</p>
    </section>
  );
}
