import React from "react";
import { ObservationValue } from "../components/ScanStatus";
import type { AssetAuditorBindings, UiSnapshot } from "../types";
import { useText } from "../../i18n/locale";

export function OverviewTab({
  snapshot,
  bindings,
}: {
  snapshot: UiSnapshot;
  bindings: AssetAuditorBindings;
}): React.JSX.Element {
  const { t } = useText();
  const { summary, settings } = snapshot;
  const isActive = snapshot.scanStatus.state === "Running" || snapshot.scanStatus.state === "CancellationRequested";

  return (
    <section className="apa__tab-content" aria-labelledby="apa-overview-title">
      <div className="apa__section-heading">
        <div>
          <p className="apa__eyebrow">{t("aoverview.eyebrow")}</p>
          <h2 id="apa-overview-title">{t("aoverview.title")}</h2>
        </div>
        <div className="apa__actions">
          <button
            type="button"
            className="apa__button apa__button--primary"
            disabled={isActive}
            onClick={() => bindings.requestCensus(settings)}
          >
            {t("aoverview.runCensus")}
          </button>
          <button
            type="button"
            className="apa__button apa__button--primary"
            disabled={isActive}
            onClick={() => bindings.requestAssetAudit(settings)}
          >
            {t("aoverview.runAudit")}
          </button>
        </div>
      </div>

      <div className="apa__metric-grid">
        <Metric label={t("aoverview.registered")} value={String(summary.catalogCount)} detail={t("aoverview.catalogGeneration", { generation: summary.catalogGeneration })} />
        <Metric label={t("aoverview.gameVersion")} value={summary.gameVersion} detail={t("aoverview.compatibility", { value: summary.compatibility })} />
        <Metric label={t("aoverview.topLevel")} value={<ObservationValue label={t("aoverview.topLevel")} observation={summary.censusCounts.topLevelObjects} />} detail={t("aoverview.topLevelDetail")} />
        <Metric label={t("aoverview.live")} value={<ObservationValue label={t("aoverview.live")} observation={summary.censusCounts.liveObjectReferences} />} detail={t("aoverview.liveDetail")} />
      </div>

      <div className="apa__metadata-line">
        <span>{summary.censusWasScanned ? t("aoverview.censusCaptured", { at: summary.censusCapturedAt ?? t("aoverview.censusUnknownTime") }) : t("aoverview.censusNotScanned")}</span>
        {summary.censusWasScanned && !summary.censusMatchesCatalog ? <span>{t("aoverview.censusGeneration", { generation: summary.censusCatalogGeneration ?? t("aoverview.unknown") })}</span> : null}
        <span>{t("aoverview.queryProfile", { version: summary.queryProfileVersion ?? t("aoverview.notAvailable") })}</span>
        <span>{t("aoverview.mod", { version: summary.modVersion })}</span>
      </div>
      {summary.capabilities.length > 0 ? (
        <ul className="apa__capability-list" aria-label={t("aoverview.capabilitiesAria")}>
          {summary.capabilities.map((capability) => (
            <li key={capability.id}>
              <span>{capability.id}</span>
              <strong>{capability.state}</strong>
            </li>
          ))}
        </ul>
      ) : <p className="apa__muted">{t("aoverview.noCapabilities")}</p>}
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
