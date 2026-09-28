import { useMemo } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import {
  DEFAULT_ASSET_QUERY_STATE,
  DEFAULT_EXPORT_REQUEST,
  DEFAULT_SCAN_OPTIONS,
  EMPTY_UI_SNAPSHOT,
  MAX_ASSET_PAGE_SIZE,
  type AssetAuditorBindings,
  type AssetQueryRequest,
  type AssetQueryState,
  type CountKind,
  type ExportRequest,
  type NormalizedUiScanOptions,
  type UiObservation,
  type UiScanOptions,
  type UiSnapshot,
} from "./types";

export const UI_BINDING_GROUP = "CS2RuntimeAssetAuditor.assets";

const snapshotBinding = bindValue<string>(UI_BINDING_GROUP, "snapshot", "{}");
const exportedReportBinding = bindValue<string>(UI_BINDING_GROUP, "exportedReport", "");

export function useAuditorSnapshot(): UiSnapshot {
  const raw = useValue(snapshotBinding);
  // Parse once per published binding value so unchanged snapshots keep stable object identity across renders.
  return useMemo(() => parseSnapshot(raw), [raw]);
}

export function useExportedReport(): string {
  return useValue(exportedReportBinding);
}

export const nativeBindings: AssetAuditorBindings = {
  requestCensus(options: UiScanOptions): void {
    trigger(UI_BINDING_GROUP, "requestCensus", JSON.stringify(normalizeUiSettings(options)));
  },
  requestAssetAudit(options: UiScanOptions): void {
    trigger(UI_BINDING_GROUP, "requestAssetAudit", JSON.stringify(normalizeUiSettings(options)));
  },
  requestDeepInspection(renderKey: string): void {
    trigger(UI_BINDING_GROUP, "requestDeepInspection", renderKey);
  },
  cancelCensus(): void {
    trigger(UI_BINDING_GROUP, "cancelCensus");
  },
  requestAssetsPage(query: AssetQueryRequest): void {
    trigger(UI_BINDING_GROUP, "queryAssets", JSON.stringify(query));
  },
  requestExport(request?: ExportRequest): void {
    trigger(UI_BINDING_GROUP, "requestExport", JSON.stringify(request ?? DEFAULT_EXPORT_REQUEST));
  },
  updateSettings(options: UiScanOptions): void {
    trigger(UI_BINDING_GROUP, "updateSettings", JSON.stringify(normalizeUiSettings(options)));
  },
};

export function createAssetQuery(state: AssetQueryState): AssetQueryRequest {
  return {
    searchText: state.searchText.trim(),
    traitFilter: state.traitFilter,
    sourceFilter: state.sourceFilter,
    presenceFilter: state.presenceFilter,
    sort: state.sort,
    offset: Math.max(0, Math.floor(state.offset)),
    limit: Math.min(MAX_ASSET_PAGE_SIZE, Math.max(1, Math.floor(state.pageSize))),
  };
}

export function updateAssetQueryState(
  current: AssetQueryState,
  patch: Partial<AssetQueryState>,
): AssetQueryState {
  const changesFilter = Object.keys(patch).some(
    (key) => key !== "offset" && key !== "pageSize",
  );
  return {
    ...current,
    ...patch,
    offset: patch.offset ?? (changesFilter ? 0 : current.offset),
  };
}

export function normalizeUiSettings(settings: Partial<UiScanOptions>): NormalizedUiScanOptions {
  const merged = { ...DEFAULT_SCAN_OPTIONS, ...settings };
  return {
    collectSubordinateObjects: merged.collectSubordinateObjects ?? DEFAULT_SCAN_OPTIONS.collectSubordinateObjects,
    collectNetworkEdges: merged.collectNetworkEdges ?? DEFAULT_SCAN_OPTIONS.collectNetworkEdges,
    frameBudgetMs: clampFinite(merged.frameBudgetMs, 0.25, 8, DEFAULT_SCAN_OPTIONS.frameBudgetMs),
    progressUpdateMs: clampFinite(merged.progressUpdateMs, 50, 2000, DEFAULT_SCAN_OPTIONS.progressUpdateMs),
    refreshCatalogAtScanStart: merged.refreshCatalogAtScanStart ?? DEFAULT_SCAN_OPTIONS.refreshCatalogAtScanStart,
    enableHeuristicFindings: merged.enableHeuristicFindings ?? DEFAULT_SCAN_OPTIONS.enableHeuristicFindings,
    enablePeerOutliers: merged.enablePeerOutliers ?? DEFAULT_SCAN_OPTIONS.enablePeerOutliers,
    comparisonPopulation: merged.comparisonPopulation ?? DEFAULT_SCAN_OPTIONS.comparisonPopulation,
    showNoticeFindings: merged.showNoticeFindings ?? DEFAULT_SCAN_OPTIONS.showNoticeFindings,
    pageSize: Math.round(clampFinite(merged.pageSize, 25, MAX_ASSET_PAGE_SIZE, DEFAULT_SCAN_OPTIONS.pageSize)),
    metadataCacheLimit: Math.round(clampFinite(merged.metadataCacheLimit, 64, 4096, DEFAULT_SCAN_OPTIONS.metadataCacheLimit)),
    deepInspectionLimit: Math.round(clampFinite(merged.deepInspectionLimit, 1, 16, DEFAULT_SCAN_OPTIONS.deepInspectionLimit)),
    uiScale: clampFinite(merged.uiScale, 0.75, 1.5, DEFAULT_SCAN_OPTIONS.uiScale),
  };
}

function clampFinite(value: number | undefined, minimum: number, maximum: number, fallback: number): number {
  if (value === undefined || !Number.isFinite(value)) return fallback;
  return Math.min(maximum, Math.max(minimum, value));
}

export function formatObservation(observation: UiObservation<number>): string {
  switch (observation.availability) {
    case "Available":
      return observation.value === null || observation.value === undefined ? "Unknown" : String(observation.value);
    case "NotScanned": return "Not scanned";
    case "NotApplicable": return "N/A";
    case "Unsupported": return "Unsupported";
    case "Failed": return "Failed";
    default: return "Unknown";
  }
}

export function formatCountKind(countKind: CountKind): string {
  switch (countKind) {
    case "TopLevelObjects": return "Top-level objects";
    case "SubordinateObjects": return "Subordinate objects";
    case "LiveObjectReferences": return "Live object references";
    case "NetworkEdges": return "Network edges";
    default: return "Not applicable";
  }
}

export function createEscapeCloseHandler(onClose: () => void) {
  return (event: Pick<KeyboardEvent, "key" | "preventDefault">): void => {
    if (event.key !== "Escape") return;
    event.preventDefault();
    onClose();
  };
}

export function parseSnapshot(raw: string): UiSnapshot {
  try {
    const value: unknown = JSON.parse(raw);
    if (value && typeof value === "object" && "scanStatus" in value) {
      const snapshot = value as UiSnapshot;
      return {
        ...snapshot,
        settings: normalizeUiSettings(snapshot.settings ?? DEFAULT_SCAN_OPTIONS),
        findings: snapshot.findings ?? [],
      };
    }
  } catch {
    // A malformed/missing binding keeps the documented empty view available.
  }
  return EMPTY_UI_SNAPSHOT;
}

export { DEFAULT_ASSET_QUERY_STATE };
