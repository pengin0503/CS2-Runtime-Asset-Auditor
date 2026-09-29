import React, { useEffect, useState } from "react";
import { createAssetQuery, nativeBindings, normalizeUiSettings, updateAssetQueryState, useAuditorFindings, useAuditorSnapshot, useExportedReport } from "./bindings";
import { ExportControls } from "./components/ExportControls";
import { ScanStatus } from "./components/ScanStatus";
import { AssetsTab } from "./tabs/AssetsTab";
import { CensusTab } from "./tabs/CensusTab";
import { CompareTab } from "./tabs/CompareTab";
import { OverviewTab } from "./tabs/OverviewTab";
import { SettingsTab } from "./tabs/SettingsTab";
import { WarningsTab } from "./tabs/WarningsTab";
import type { AssetQueryState, ExportAssetKey } from "./types";
import type { CaptureSummaryUi } from "../profiler/bindings";
import { RuntimeContextCard } from "./RuntimeContextCard";
import { DEFAULT_ASSET_QUERY_STATE } from "./types";
import type { AssetSectionName } from "../shell/navigation";
import { exportResultLabel } from "../profiler/text";
import { useText } from "../i18n/locale";
import styles from "./assetAuditor.module.scss";

export function AssetSection({ view, active, capture, onOpenRuntimeCaptures }: { view: AssetSectionName | "overview"; active: boolean; capture?: CaptureSummaryUi | null; onOpenRuntimeCaptures?: () => void }) {
  const { locale, t } = useText();
  const snapshot = useAuditorSnapshot();
  const published = useAuditorFindings();
  const exportedReport = useExportedReport();
  const [query, setQuery] = useState<AssetQueryState>(DEFAULT_ASSET_QUERY_STATE);
  const [selectedAsset, setSelectedAsset] = useState<ExportAssetKey | null>(null);
  const settings = normalizeUiSettings(snapshot.settings);
  // Findings arrive on their own binding once an analysis is published; older snapshots may still embed them.
  const allFindings = published.analysisGeneration > 0 ? published.findings : snapshot.findings ?? [];
  const findings = settings.showNoticeFindings ? allFindings : allFindings.filter(item => item.status !== "Notice");

  useEffect(() => {
    if (!active) return;
    const timer = window.setTimeout(() => nativeBindings.requestAssetsPage(createAssetQuery(query)), 180);
    return () => window.clearTimeout(timer);
  }, [active, query]);

  useEffect(() => {
    setQuery(current => current.pageSize === settings.pageSize ? current : { ...current, pageSize: settings.pageSize, offset: 0 });
  }, [settings.pageSize]);

  const updateQuery = (patch: Partial<AssetQueryState>) => setQuery(current => updateAssetQueryState(current, patch));

  return (
    <section className={styles.assetSection} aria-label={t("assets.sectionAria")}>
      <ScanStatus scan={snapshot.scanStatus} onCancel={nativeBindings.cancelCensus} />
      {capture && <RuntimeContextCard capture={capture} sessionId={snapshot.sessionId} summary={snapshot.summary}
        onRunAudit={() => nativeBindings.requestAssetAudit(settings)} />}
      {view === "overview" && <OverviewTab snapshot={snapshot} bindings={nativeBindings} />}
      {view === "catalog" && <AssetsTab page={snapshot.assetPage} query={query} onQueryChange={updateQuery}
        findings={findings} onSelectedAssetChange={setSelectedAsset} onDeepInspect={nativeBindings.requestDeepInspection} onOpenRuntimeCaptures={onOpenRuntimeCaptures} />}
      {view === "census" && <CensusTab snapshot={snapshot} />}
      {view === "findings" && <WarningsTab findings={findings} />}
      {view === "compare" && <CompareTab assets={snapshot.assetPage.items.slice(0, 4)} findings={findings} />}
      {view === "settings" && <SettingsTab settings={settings} onChange={nativeBindings.updateSettings} />}
      <ExportControls selectedAsset={selectedAsset} onExport={nativeBindings.requestExport} />
      {exportedReport && <section className={styles.exportResult} aria-label={t("assets.exportResult")}>
        <strong>{t("assets.exportResult")}</strong><p role="status">{exportResultLabel(exportedReport, locale)}</p>
      </section>}
    </section>
  );
}
