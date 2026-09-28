import React from "react";
import { ObservationValue } from "../components/ScanStatus";
import type { AssetAuditorBindings, UiSnapshot } from "../types";

export function OverviewTab({
  snapshot,
  bindings,
}: {
  snapshot: UiSnapshot;
  bindings: AssetAuditorBindings;
}): React.JSX.Element {
  const { summary, settings } = snapshot;
  const isActive = snapshot.scanStatus.state === "Running" || snapshot.scanStatus.state === "CancellationRequested";

  return (
    <section className="apa__tab-content" aria-labelledby="apa-overview-title">
      <div className="apa__section-heading">
        <div>
          <p className="apa__eyebrow">Snapshot auditor</p>
          <h2 id="apa-overview-title">Overview</h2>
        </div>
        <div className="apa__actions">
          <button
            type="button"
            className="apa__button apa__button--primary"
            disabled={isActive}
            onClick={() => bindings.requestCensus(settings)}
          >
            Run Census
          </button>
          <button
            type="button"
            className="apa__button apa__button--primary"
            disabled={isActive}
            onClick={() => bindings.requestAssetAudit(settings)}
          >
            Run Asset Audit
          </button>
        </div>
      </div>

      <div className="apa__metric-grid">
        <Metric label="Registered Prefabs" value={String(summary.catalogCount)} detail={`Catalog generation ${summary.catalogGeneration}`} />
        <Metric label="Game version" value={summary.gameVersion} detail={`Compatibility: ${summary.compatibility}`} />
        <Metric label="Top-level objects" value={<ObservationValue label="Top-level objects" observation={summary.censusCounts.topLevelObjects} />} detail="Current top-level object references" />
        <Metric label="Live object references" value={<ObservationValue label="Live object references" observation={summary.censusCounts.liveObjectReferences} />} detail="Top-level plus subordinate objects" />
      </div>

      <div className="apa__metadata-line">
        <span>{summary.censusWasScanned ? `Census captured ${summary.censusCapturedAt ?? "at an unknown time"}` : "Census not scanned"}</span>
        {summary.censusWasScanned && !summary.censusMatchesCatalog ? <span>Census uses catalog generation {summary.censusCatalogGeneration ?? "unknown"}</span> : null}
        <span>Query profile {summary.queryProfileVersion ?? "not available"}</span>
        <span>Mod {summary.modVersion}</span>
      </div>
      {summary.capabilities.length > 0 ? (
        <ul className="apa__capability-list" aria-label="Capability status">
          {summary.capabilities.map((capability) => (
            <li key={capability.id}>
              <span>{capability.id}</span>
              <strong>{capability.state}</strong>
            </li>
          ))}
        </ul>
      ) : <p className="apa__muted">Capability data has not been published.</p>}
    </section>
  );
}

function Metric({
  label,
  value,
  detail,
}: {
  label: string;
  value: React.ReactNode;
  detail: string;
}): React.JSX.Element {
  return (
    <article className="apa__metric-card">
      <p className="apa__eyebrow">{label}</p>
      <strong className="apa__metric-value">{value}</strong>
      <span className="apa__muted">{detail}</span>
    </article>
  );
}
