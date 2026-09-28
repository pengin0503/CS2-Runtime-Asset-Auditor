import { bindValue, trigger, useValue } from "cs2/api";

export interface UiMetricRow {
  id: string;
  value: number | null;
  confidence: string;
  availability: string;
  reason: string | null;
  unitType?: string;
}

export interface GlobalUiMetrics {
  available: boolean;
  timestampSeconds: number;
  selectedSpeed: number | null;
  actualSpeed: number | null;
  efficiency: number | null;
  recorderMetrics: UiMetricRow[];
}

export interface CaptureUiState {
  state: string;
  isDeepCapture: boolean;
  completedCount: number;
  detailCaptureId: string;
  detailScope: string;
}

export interface UiHudSnapshot {
  selectedSpeed: number | null;
  actualSpeed: number | null;
  state: string;
  isDeepCapture: boolean;
}

export interface SystemUiRow {
  id: string;
  ownerAssembly: string;
  sourceKind: string;
  /** Group systems whose inclusive time already contains their children; excluded from additive totals. */
  isAggregateContainer?: boolean;
  currentMilliseconds: number;
  meanMilliseconds: number | null;
  medianMilliseconds: number | null;
  p95Milliseconds: number | null;
  p99Milliseconds: number | null;
  maxMilliseconds: number | null;
  totalMilliseconds: number | null;
  /** Total time divided by frames rendered in the measurement window; null when the frame count is unknown. */
  millisecondsPerFrame?: number | null;
  calls: number | null;
  confidence: string;
  patchOwners: string[];
}

export interface ModUiRow {
  assemblyName: string;
  directSystemMilliseconds: number;
  /** "perFrame" when every contributing system has a frame-normalized cost, otherwise "perSample". */
  directCostBasis?: string;
  directSystemCount: number;
  patchedVanillaSystemCount: number;
}

export interface TimelinePoint {
  timestampSeconds: number;
  metric: string;
  value: number;
  unitType?: string;
  confidence: string;
}

export interface CorrelatedChangeUi {
  metric: string;
  before: number;
  after: number;
  delta: number;
  relativeDelta: number | null;
  unitType?: string;
  confidence: string;
}

export interface CaptureSummaryUi {
  id: string;
  triggerKind: string;
  triggeredAtSeconds: number;
  durationSeconds: number;
  discoveredMarkers: number;
  capturedMarkers: number;
  batched: boolean;
  coverageRatio: number | null;
  warningCount: number;
  profilerOverheadShare: number;
  warnings: string[];
  correlatedChanges: CorrelatedChangeUi[];
}

export interface DiagnosticsUi {
  profilerOverheadShare: number;
  unattributedJobsMilliseconds: number | null;
  messages: string[];
  gameVersion: string;
  profilerVersion: string;
  discoveredMarkerCount: number;
  capturedMarkerCount: number;
  systemCount: number;
  markerBatchSize: number;
  samplingStride: number;
  patchMapState: string;
}

export interface AdvisorRecommendation {
  settingId: string;
  displayName: string;
  currentValue: string;
  recommendedValue: string;
  direction: string;
  priority: string;
  confidence: string;
  rationale: string;
  evidenceIds: string[];
  applyCapability: string;
  applyBehavior: string;
}

export interface AdvisorUiState {
  available: boolean;
  unavailableReason: string;
  selectedCaptureId: string;
  baselineCaptureId: string;
  catalog: Array<{ settingId: string; category: string; displayName: string; currentValue: string;
    isUserFacing: boolean; isReadable: boolean; isWritable: boolean; applyBehavior: string }>;
  observations: Array<{ category: string; severity: string; confidence: string; rationale: string; evidenceIds: string[] }>;
  recommendations: AdvisorRecommendation[];
  changes?: AdvisorChange[];
  comparison?: AdvisorComparison | null;
}

export interface AdvisorComparison {
  multipleChanges: boolean;
  changedSettingIds: string[];
  metrics: Array<{ id: string; baselineValue: number | null; followUpValue: number | null;
    state: "Improved" | "Regressed" | "NoMaterialChange" | "NotComparable"; reason: string }>;
}

export interface AdvisorChange {
  settingId: string;
  originalValue: string;
  appliedValue: string;
  currentObservedValue: string;
  status: string;
}

export const EMPTY_ADVISOR: AdvisorUiState = {
  available: false, unavailableReason: "", selectedCaptureId: "", baselineCaptureId: "",
  catalog: [], observations: [], recommendations: [], changes: [], comparison: null
};

export interface UiSnapshot {
  global: GlobalUiMetrics;
  capture: CaptureUiState;
  systems: SystemUiRow[];
  mods: ModUiRow[];
  pathfinding: { metrics: UiMetricRow[] };
  domainMetrics: UiMetricRow[];
  timeline: TimelinePoint[];
  captures: CaptureSummaryUi[];
  diagnostics: DiagnosticsUi;
  advisor?: AdvisorUiState;
}

/** Persisted panel geometry in screen pixels; `custom` is false until the user moves or resizes the panel. */
export interface PanelLayout {
  custom: boolean;
  left: number;
  top: number;
  width: number;
  height: number;
}

export const DEFAULT_PANEL_LAYOUT: PanelLayout = { custom: false, left: 0, top: 0, width: 0, height: 0 };

export const EMPTY_HUD_SNAPSHOT: UiHudSnapshot = {
  selectedSpeed: null,
  actualSpeed: null,
  state: "Monitoring",
  isDeepCapture: false
};

export const EMPTY_SNAPSHOT: UiSnapshot = {
  global: {
    available: false,
    timestampSeconds: 0,
    selectedSpeed: null,
    actualSpeed: null,
    efficiency: null,
    recorderMetrics: []
  },
  capture: {
    state: "Monitoring",
    isDeepCapture: false,
    completedCount: 0,
    detailCaptureId: "",
    detailScope: "live"
  },
  systems: [],
  mods: [],
  pathfinding: { metrics: [] },
  domainMetrics: [],
  timeline: [],
  captures: [],
  advisor: EMPTY_ADVISOR,
  diagnostics: {
    profilerOverheadShare: 0,
    unattributedJobsMilliseconds: null,
    messages: [],
    gameVersion: "",
    profilerVersion: "",
    discoveredMarkerCount: 0,
    capturedMarkerCount: 0,
    systemCount: 0,
    markerBatchSize: 0,
    samplingStride: 1,
    patchMapState: ""
  }
};

const GROUP = "CS2RuntimeAssetAuditor";

const snapshotBinding = bindValue<UiSnapshot>(GROUP, "snapshot", EMPTY_SNAPSHOT);
const hudSnapshotBinding = bindValue<UiHudSnapshot>(GROUP, "hudSnapshot", EMPTY_HUD_SNAPSHOT);
const panelVisibleBinding = bindValue<boolean>(GROUP, "panelVisible", false);
const uiScalePercentBinding = bindValue<number>(GROUP, "uiScalePercent", 100);
const selectedCaptureBinding = bindValue<string>(GROUP, "selectedCaptureId", "");
const exportResultBinding = bindValue<string>(GROUP, "exportResult", "");
const panelLayoutBinding = bindValue<PanelLayout>(GROUP, "panelLayout", DEFAULT_PANEL_LAYOUT);

export const useProfilerSnapshot = () => useValue(snapshotBinding);
export const useProfilerHudSnapshot = () => useValue(hudSnapshotBinding);
export const usePanelVisible = () => useValue(panelVisibleBinding);
export const useUiScalePercent = () => useValue(uiScalePercentBinding);
export const useSelectedCaptureId = () => useValue(selectedCaptureBinding);
export const useExportResult = () => useValue(exportResultBinding);
export const usePanelLayout = () => useValue(panelLayoutBinding);

export const togglePanel = () => trigger(GROUP, "togglePanel");
/** Idempotent close; safe when the game delivers the Back action more than once. */
export const closePanel = () => trigger(GROUP, "setPanelVisible", false);
export const savePanelLayout = (left: number, top: number, width: number, height: number) =>
  trigger(GROUP, "setPanelLayout", Math.round(left), Math.round(top), Math.round(width), Math.round(height));
export const resetPanelLayout = () => trigger(GROUP, "resetPanelLayout");
export const requestManualCapture = () => trigger(GROUP, "manualCapture");
export const selectCapture = (id: string) => trigger(GROUP, "selectCapture", id);
export const requestAdvisorDiagnosis = (id: string) => trigger(GROUP, "diagnoseAdvisor", id);
export const requestAdvisorRediagnosis = (id: string) => trigger(GROUP, "advisorRediagnose", id);
export const selectAdvisorBaseline = (id: string) => trigger(GROUP, "selectAdvisorBaseline", id);
export const advisorApply = (id: string, value: string, confirmed = false) =>
  trigger(GROUP, "advisorApply", id, value, confirmed);
export const advisorUndo = (id: string, confirmed = false) => trigger(GROUP, "advisorUndo", id, confirmed);
export const advisorUndoSession = () => trigger(GROUP, "advisorUndoSession");
export const advisorResolveConflict = (id: string, restoreOriginal: boolean) =>
  trigger(GROUP, "advisorResolveConflict", id, restoreOriginal);
export const exportReport = () => trigger(GROUP, "exportReport");
