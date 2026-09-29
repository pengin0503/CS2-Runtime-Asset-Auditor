import { useMemo } from "react";
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
  /** Rendered frames per second over the last sampling interval; null when it was not measured. */
  framesPerSecond: number | null;
  /** Median and 95th-percentile frame time of the same interval. */
  frameMsMedian: number | null;
  frameMsP95: number | null;
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
  framesPerSecond: number | null;
  frameMsP95: number | null;
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
  sessionId?: string | null;
  startedAtUtc?: string | null;
  completedAtUtc?: string | null;
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
  experiment?: AdvisorExperiment | null;
  lastAction?: AdvisorAction | null;
}

export interface AdvisorExperiment {
  experimentId: string;
  state: "BaselineReady" | "AwaitingApplyConfirmation" | "AwaitingFollowUp" | "FollowUpCapturing" | "Completed" | "Cancelled" | "Invalidated";
  validity: "Valid" | "Invalidated";
  invalidationReason: string;
  baselineCaptureId: string;
  followUpCaptureId: string;
  settingId: string;
  settingDisplayName: string;
  originalValue: string;
  testedValue: string;
  changeAppliedAtUtc: string | null;
  stabilizationReadyAtUtc: string | null;
  completionOutcome: "None" | "Kept" | "Undone" | "Cancelled";
  lastFailureReason: string;
  followUpWarnings: string[];
  /** Why the compared follow-up capture ended early; empty when it ran its full course. */
  followUpInterruption?: string;
  comparison: AdvisorComparison | null;
}

/** Outcome of the latest Apply/Undo/conflict action, so no button press is silent. */
export interface AdvisorAction {
  kind: "Apply" | "Undo" | "UndoSession" | "ResolveConflict";
  settingId: string;
  displayName: string;
  succeeded: boolean;
  failureReason: string;
  succeededCount: number;
  failedSettingIds: string[];
  confirmationRequiredSettingIds: string[];
  atUtc: string;
}

export interface AdvisorComparison {
  multipleChanges: boolean;
  changedSettingIds: string[];
  metrics: Array<{ id: string; baselineValue: number | null; followUpValue: number | null;
    state: "Improved" | "Regressed" | "NoMaterialChange" | "NotComparable"; reason: string }>;
}

export interface AdvisorChange {
  settingId: string;
  displayName?: string;
  originalValue: string;
  appliedValue: string;
  currentObservedValue: string;
  status: string;
}

export const EMPTY_ADVISOR: AdvisorUiState = {
  available: false, unavailableReason: "", selectedCaptureId: "", baselineCaptureId: "",
  catalog: [], observations: [], recommendations: [], changes: [], comparison: null, experiment: null, lastAction: null
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

/**
 * The capture detail the game sends separately from the live snapshot: it changes only when a capture changes,
 * so the live values can refresh twice a second without resending about a thousand system rows.
 */
export interface UiCaptureDetail {
  systems: SystemUiRow[];
  mods: ModUiRow[];
  timeline: TimelinePoint[];
  captures: CaptureSummaryUi[];
}

/** The live snapshot binding: the full snapshot without the capture detail. */
export type UiLiveSnapshot = Omit<UiSnapshot, keyof UiCaptureDetail> & Partial<UiCaptureDetail>;

/** Joins the two bindings into one snapshot. The detail keeps its references while it is unchanged. */
export function mergeProfilerSnapshot(live: UiLiveSnapshot, detail: UiCaptureDetail | null): UiSnapshot {
  return {
    ...live,
    systems: detail?.systems ?? live.systems ?? [],
    mods: detail?.mods ?? live.mods ?? [],
    timeline: detail?.timeline ?? live.timeline ?? [],
    captures: detail?.captures ?? live.captures ?? []
  };
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
  isDeepCapture: false,
  framesPerSecond: null,
  frameMsP95: null
};

export const EMPTY_SNAPSHOT: UiSnapshot = {
  global: {
    available: false,
    timestampSeconds: 0,
    selectedSpeed: null,
    actualSpeed: null,
    efficiency: null,
    framesPerSecond: null,
    frameMsMedian: null,
    frameMsP95: null,
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

const snapshotBinding = bindValue<UiLiveSnapshot>(GROUP, "snapshot", EMPTY_SNAPSHOT);
// Null until the game has sent it, so a live snapshot that still carries the detail is shown as it is.
const captureDetailBinding = bindValue<UiCaptureDetail | null>(GROUP, "captureDetail", null);
const hudSnapshotBinding = bindValue<UiHudSnapshot>(GROUP, "hudSnapshot", EMPTY_HUD_SNAPSHOT);
const panelVisibleBinding = bindValue<boolean>(GROUP, "panelVisible", false);
const uiScalePercentBinding = bindValue<number>(GROUP, "uiScalePercent", 100);
const selectedCaptureBinding = bindValue<string>(GROUP, "selectedCaptureId", "");
const exportResultBinding = bindValue<string>(GROUP, "exportResult", "");
const panelLayoutBinding = bindValue<PanelLayout>(GROUP, "panelLayout", DEFAULT_PANEL_LAYOUT);

export const useProfilerSnapshot = (): UiSnapshot => {
  const live = useValue(snapshotBinding);
  const detail = useValue(captureDetailBinding);
  return useMemo(() => mergeProfilerSnapshot(live, detail), [live, detail]);
};
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
/** Undoes every applied change; `confirmed` also restores settings that require confirmation. */
export const advisorUndoSession = (confirmed = false) => trigger(GROUP, "advisorUndoSession", confirmed);
export const advisorResolveConflict = (id: string, restoreOriginal: boolean) =>
  trigger(GROUP, "advisorResolveConflict", id, restoreOriginal);
export const startAdvisorExperiment = (captureId: string, settingId: string, proposedValue: string): void =>
  trigger(GROUP, "advisorStartExperiment", captureId, settingId, proposedValue);
export const applyAdvisorExperiment = (confirmed = false): void => trigger(GROUP, "advisorApplyExperiment", confirmed);
export const startAdvisorExperimentFollowUp = (): void => trigger(GROUP, "advisorStartExperimentFollowUp");
export const cancelAdvisorExperiment = (): void => trigger(GROUP, "advisorCancelExperiment");
export const keepAdvisorExperiment = (): void => trigger(GROUP, "advisorKeepExperiment");
export const undoAdvisorExperiment = (confirmed = false): void => trigger(GROUP, "advisorUndoExperiment", confirmed);
export const exportReport = () => trigger(GROUP, "exportReport");
