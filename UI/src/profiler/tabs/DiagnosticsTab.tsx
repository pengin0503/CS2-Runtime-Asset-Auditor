import React from "react";
import type { CaptureSummaryUi, DiagnosticsUi } from "../bindings";
import { formatMilliseconds, formatPercent } from "../format";
import { diagnosticMessageLabel } from "../text";
import { useText } from "../../i18n/locale";
import type { MessageKey } from "../../i18n/messages";
import styles from "../profiler.module.scss";

export function DiagnosticsTab({ diagnostics, captures }: { diagnostics: DiagnosticsUi; captures: CaptureSummaryUi[] }) {
  const { locale, t } = useText();
  const latest = captures?.length ? captures[captures.length - 1] : null;
  const values: Array<[MessageKey, string | number]> = [
    ["diagnostics.gameVersion", diagnostics.gameVersion || t("common.unavailable")],
    ["diagnostics.profilerVersion", diagnostics.profilerVersion || t("common.unavailable")],
    ["diagnostics.discoveredMarkers", diagnostics.discoveredMarkerCount],
    ["diagnostics.capturedMarkers", diagnostics.capturedMarkerCount],
    ["diagnostics.systemRows", diagnostics.systemCount],
    ["diagnostics.batchSize", diagnostics.markerBatchSize],
    ["diagnostics.stride", diagnostics.samplingStride],
    ["diagnostics.overhead", formatPercent(diagnostics.profilerOverheadShare)],
    ["diagnostics.unattributedJobs", diagnostics.unattributedJobsMilliseconds == null ? t("diagnostics.notMeasured") : formatMilliseconds(diagnostics.unattributedJobsMilliseconds)],
    ["diagnostics.patchMap", diagnostics.patchMapState ? diagnosticMessageLabel(diagnostics.patchMapState, locale) : t("common.unavailable")]
  ];

  return (
    <div className={styles.tabBody}>
      <div className={styles.diagnosticsGrid}>
        {values.map(([name, value]) => <div className={styles.diagnosticItem} key={name}><span>{t(name)}</span><strong>{value}</strong></div>)}
      </div>
      <p className={styles.diagnosticMessage}>{t("diagnostics.overheadNote")}</p>
      {latest && (
        <section>
          <h3>{t("diagnostics.latestCoverage")}</h3>
          <div className={styles.captureFacts}>
            <span>{t("diagnostics.coverage")} <b>{formatPercent(latest.coverageRatio)}</b></span>
            <span>{t("diagnostics.mode")} <b>{latest.batched ? t("captures.batched") : t("captures.simultaneous")}</b></span>
            <span>{t("diagnostics.maxOverhead")} <b>{formatPercent(latest.profilerOverheadShare)}</b></span>
          </div>
        </section>
      )}
      <section>
        <h3>{t("diagnostics.notes")}</h3>
        {diagnostics.messages?.length
          ? diagnostics.messages.map(message => <p className={styles.diagnosticMessage} key={message}>{diagnosticMessageLabel(message, locale)}</p>)
          : <p className={styles.empty}>{t("diagnostics.noMessages")}</p>}
      </section>
    </div>
  );
}
