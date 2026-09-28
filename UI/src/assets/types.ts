export type Availability =
  | "Available"
  | "NotScanned"
  | "NotApplicable"
  | "Unsupported"
  | "Failed";

export type CountKind =
  | "None"
  | "TopLevelObjects"
  | "SubordinateObjects"
  | "LiveObjectReferences"
  | "NetworkEdges";

export type CensusPresence =
  | "Present"
  | "NotPresentAtSnapshot"
  | "NotApplicable"
  | "Unknown";

export type FindingStatus = "Warning" | "PotentialIssue" | "Notice" | "Observed" | "Unknown";
export type FindingCategory = "Geometry" | "Lod" | "Material" | "Texture" | "Exposure" | "Integrity";
export type RenderCoverage = Availability | "Unknown";
export type ExportFormat = "Json" | "Csv";
export type ExportScope = "Full" | "Filtered" | "Selected" | "Census" | "Findings";

export interface UiObservation<T = number> {
  availability: Availability;
  value?: T | null;
  origin?: string;
  capturedAt?: string;
  diagnosticCode?: string | null;
}

export interface UiFinding {
  ruleId: string;
  status: FindingStatus;
  category: FindingCategory;
  title: string;
  explanation: string;
  evidence: string[];
  basis: string;
  ruleVersion: string;
  prefabId?: string | null;
  prefabType?: string | null;
}

export interface UiMaterialBinding {
  materialName: string;
  shaderName: string;
  shaderKeywords: string[];
  renderQueue: number;
  passCount: number;
  enableInstancing: boolean;
}

export interface UiDeepInspection {
  availability: Availability;
  capturedAt?: string | null;
  diagnosticCode?: string | null;
  materials: UiMaterialBinding[];
  surfaceAssetIds: string[];
}

export interface UiRenderRelation {
  kind: string;
  from: string;
  to: string;
  lodLevel?: number | null;
  deepInspection?: UiDeepInspection | null;
}

export interface AssetRow {
  prefabId: string;
  prefabType: string;
  displayName: string;
  sourceLabel: string;
  traits: string[];
  countKind: CountKind;
  instances: UiObservation<number>;
  presence: CensusPresence;
  counters?: {
    topLevelObjects: UiObservation<number>;
    subordinateObjects: UiObservation<number>;
    liveObjectReferences: UiObservation<number>;
    networkEdges: UiObservation<number>;
  };
  renderCoverage?: RenderCoverage;
  estimatedTexturePayload?: UiObservation<number>;
  findingCount?: number;
  lod0Vertices?: UiObservation<number>;
  lod1RetentionPercent?: UiObservation<number>;
  materialCount?: UiObservation<number>;
  uniqueTextureCount?: UiObservation<number>;
  renderRelations?: UiRenderRelation[];
}

export interface AssetPage {
  offset: number;
  limit: number;
  totalCount: number;
  items: AssetRow[];
}

export type SourceFilter =
  | "Any"
  | "Builtin"
  | "SubscribedMod"
  | "Packaged"
  | "UserProvided"
  | "Unknown";

export type AssetSort =
  | "DisplayNameAscending"
  | "DisplayNameDescending"
  | "PrefabIdAscending"
  | "InstancesDescending";

export interface AssetQueryState {
  searchText: string;
  traitFilter: string | null;
  sourceFilter: SourceFilter;
  presenceFilter: CensusPresence | null;
  sort: AssetSort;
  offset: number;
  pageSize: number;
}

export interface AssetQueryRequest {
  searchText: string;
  traitFilter: string | null;
  sourceFilter: SourceFilter;
  presenceFilter: CensusPresence | null;
  sort: AssetSort;
  offset: number;
  limit: number;
}

export interface ExportAssetKey {
  prefabId: string;
  prefabType: string;
}

export interface ExportRequest {
  format: ExportFormat;
  scope: ExportScope;
  selectedKeys: ExportAssetKey[];
}

export interface ScanStatusData {
  state: "Idle" | "Running" | "CancellationRequested" | "Cancelled" | "Failed" | "Completed" | "WaitingForRuntimeCapture" | "InterruptedByRuntimeCapture";
  queuedBecauseRuntimeCapture?: boolean;
  interruptedByRuntimeCapture?: boolean;
  stage: string;
  stageNumber: number;
  totalStages: number;
  completedItems: number | null;
  totalItems: number | null;
}

export interface UiScanOptions {
  collectSubordinateObjects: boolean;
  collectNetworkEdges: boolean;
  frameBudgetMs?: number;
  progressUpdateMs?: number;
  refreshCatalogAtScanStart?: boolean;
  enableHeuristicFindings?: boolean;
  enablePeerOutliers?: boolean;
  comparisonPopulation?: "SameCategory" | "BuiltinDlc" | "Custom" | "SameSourcePack";
  showNoticeFindings?: boolean;
  pageSize?: number;
  metadataCacheLimit?: number;
  deepInspectionLimit?: number;
  uiScale?: number;
}

export type NormalizedUiScanOptions = Required<UiScanOptions>;

export interface UiCensusCounts {
  topLevelObjects: UiObservation<number>;
  subordinateObjects: UiObservation<number>;
  liveObjectReferences: UiObservation<number>;
  networkEdges: UiObservation<number>;
}

export interface UiSummary {
  gameVersion: string;
  modVersion: string;
  compatibility: string;
  capabilities: Array<{ id: string; state: string; detail?: string | null }>;
  catalogCount: number;
  catalogGeneration: number;
  catalogCapturedAt: string | null;
  censusWasScanned: boolean;
  censusCapturedAt: string | null;
  censusCatalogGeneration: number | null;
  censusMatchesCatalog: boolean;
  queryProfileVersion: string | null;
  censusCounts: UiCensusCounts;
  latestAssetSnapshotId?: string | null;
  latestAssetSnapshotStartedAtUtc?: string | null;
  latestAssetSnapshotCompletedAtUtc?: string | null;
}

export interface UiSnapshot {
  sessionId?: string | null;
  scanStatus: ScanStatusData;
  summary: UiSummary;
  assetPage: AssetPage;
  settings: UiScanOptions;
  findings?: UiFinding[];
}

export interface AssetAuditorBindings {
  requestCensus(options: UiScanOptions): void;
  requestAssetAudit(options: UiScanOptions): void;
  requestDeepInspection(renderKey: string): void;
  cancelCensus(): void;
  requestAssetsPage(query: AssetQueryRequest): void;
  requestExport(request?: ExportRequest): void;
  updateSettings(options: UiScanOptions): void;
}

export const MAX_ASSET_PAGE_SIZE = 200;

export const DEFAULT_ASSET_QUERY_STATE: AssetQueryState = {
  searchText: "",
  traitFilter: null,
  sourceFilter: "Any",
  presenceFilter: null,
  sort: "DisplayNameAscending",
  offset: 0,
  pageSize: 100,
};

export const DEFAULT_SCAN_OPTIONS: NormalizedUiScanOptions = {
  collectSubordinateObjects: true,
  collectNetworkEdges: true,
  frameBudgetMs: 1,
  progressUpdateMs: 200,
  refreshCatalogAtScanStart: true,
  enableHeuristicFindings: true,
  enablePeerOutliers: true,
  comparisonPopulation: "SameCategory",
  showNoticeFindings: true,
  pageSize: 100,
  metadataCacheLimit: 512,
  deepInspectionLimit: 1,
  uiScale: 1,
};

export const DEFAULT_EXPORT_REQUEST: ExportRequest = {
  format: "Json",
  scope: "Full",
  selectedKeys: [],
};

export const EMPTY_OBSERVATION: UiObservation<number> = {
  availability: "NotScanned",
  value: null,
};

export const EMPTY_UI_SNAPSHOT: UiSnapshot = {
  scanStatus: {
    state: "Idle",
    stage: "Idle",
    stageNumber: 0,
    totalStages: 8,
    completedItems: null,
    totalItems: null,
  },
  summary: {
    gameVersion: "Unknown",
    modVersion: "0.1.0",
    compatibility: "Untested",
    capabilities: [],
    catalogCount: 0,
    catalogGeneration: 0,
    catalogCapturedAt: null,
    censusWasScanned: false,
    censusCapturedAt: null,
    censusCatalogGeneration: null,
    censusMatchesCatalog: false,
    queryProfileVersion: null,
    censusCounts: {
      topLevelObjects: EMPTY_OBSERVATION,
      subordinateObjects: EMPTY_OBSERVATION,
      liveObjectReferences: EMPTY_OBSERVATION,
      networkEdges: EMPTY_OBSERVATION,
    },
  },
  assetPage: { offset: 0, limit: 100, totalCount: 0, items: [] },
  settings: DEFAULT_SCAN_OPTIONS,
  findings: [],
};
