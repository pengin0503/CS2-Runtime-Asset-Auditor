using System.Runtime.Serialization;

namespace CS2RuntimeAssetAuditor.Assets.UI
{
    public static class UiBindingContract
    {
        public const string Group = AssetAuditBindingNames.Group;
        public const string Snapshot = "snapshot";
        public const string ExportedReport = "exportedReport";
        // Findings change only when an analysis is published, so they travel separately from the scan status.
        public const string Findings = "findings";
        public const string RequestCensus = "requestCensus";
        public const string RequestAssetAudit = "requestAssetAudit";
        public const string RequestDeepInspection = "requestDeepInspection";
        public const string CancelCensus = "cancelCensus";
        public const string QueryAssets = "queryAssets";
        public const string RequestExport = "requestExport";
        public const string UpdateSettings = "updateSettings";
    }

    [DataContract]
    public sealed class UiFindingList
    {
        [DataMember(Name = "analysisGeneration", Order = 1)] public long AnalysisGeneration { get; set; }
        [DataMember(Name = "findings", Order = 2)] public UiFinding[] Findings { get; set; } = new UiFinding[0];
    }

    [DataContract]
    public sealed class UiSnapshot
    {
        [DataMember(Name = "scanStatus", Order = 1)] public UiScanStatus ScanStatus { get; set; } = new UiScanStatus();
        [DataMember(Name = "summary", Order = 2)] public UiSummary Summary { get; set; } = new UiSummary();
        [DataMember(Name = "assetPage", Order = 3)] public UiAssetPage AssetPage { get; set; } = new UiAssetPage();
        [DataMember(Name = "settings", Order = 4)] public UiScanOptions Settings { get; set; } = new UiScanOptions();
        [DataMember(Name = "diagnostics", Order = 6)] public UiDiagnostics Diagnostics { get; set; } = new UiDiagnostics();
        [DataMember(Name = "sessionId", Order = 7, EmitDefaultValue = true)] public string? SessionId { get; set; }
    }

    [DataContract]
    public sealed class UiScanStatus
    {
        [DataMember(Name = "state", Order = 1)] public string State { get; set; } = "Idle";
        [DataMember(Name = "stage", Order = 2)] public string Stage { get; set; } = "Idle";
        [DataMember(Name = "stageNumber", Order = 3)] public int StageNumber { get; set; }
        [DataMember(Name = "totalStages", Order = 4)] public int TotalStages { get; set; } = 8;
        [DataMember(Name = "completedItems", Order = 5, EmitDefaultValue = true)] public long? CompletedItems { get; set; }
        [DataMember(Name = "totalItems", Order = 6, EmitDefaultValue = true)] public long? TotalItems { get; set; }
        [DataMember(Name = "queuedBecauseRuntimeCapture", Order = 7)] public bool QueuedBecauseRuntimeCapture { get; set; }
        [DataMember(Name = "interruptedByRuntimeCapture", Order = 8)] public bool InterruptedByRuntimeCapture { get; set; }
    }

    [DataContract]
    public sealed class UiSummary
    {
        [DataMember(Name = "gameVersion", Order = 1)] public string GameVersion { get; set; } = "Unknown";
        [DataMember(Name = "modVersion", Order = 2)] public string ModVersion { get; set; } = string.Empty;
        [DataMember(Name = "compatibility", Order = 3)] public string Compatibility { get; set; } = "Untested";
        [DataMember(Name = "capabilities", Order = 4)] public UiCapability[] Capabilities { get; set; } = new UiCapability[0];
        [DataMember(Name = "catalogCount", Order = 5)] public int CatalogCount { get; set; }
        [DataMember(Name = "catalogGeneration", Order = 6)] public long CatalogGeneration { get; set; }
        [DataMember(Name = "catalogCapturedAt", Order = 7, EmitDefaultValue = true)] public string? CatalogCapturedAt { get; set; }
        [DataMember(Name = "censusWasScanned", Order = 8)] public bool CensusWasScanned { get; set; }
        [DataMember(Name = "censusCapturedAt", Order = 9, EmitDefaultValue = true)] public string? CensusCapturedAt { get; set; }
        [DataMember(Name = "censusCatalogGeneration", Order = 10, EmitDefaultValue = true)] public long? CensusCatalogGeneration { get; set; }
        [DataMember(Name = "censusMatchesCatalog", Order = 11)] public bool CensusMatchesCatalog { get; set; }
        [DataMember(Name = "queryProfileVersion", Order = 12, EmitDefaultValue = true)] public string? QueryProfileVersion { get; set; }
        [DataMember(Name = "censusCounts", Order = 13)] public UiCensusCounts CensusCounts { get; set; } = new UiCensusCounts();
        [DataMember(Name = "latestAssetSnapshotId", Order = 14, EmitDefaultValue = true)] public string? LatestAssetSnapshotId { get; set; }
        [DataMember(Name = "latestAssetSnapshotStartedAtUtc", Order = 15, EmitDefaultValue = true)] public string? LatestAssetSnapshotStartedAtUtc { get; set; }
        [DataMember(Name = "latestAssetSnapshotCompletedAtUtc", Order = 16, EmitDefaultValue = true)] public string? LatestAssetSnapshotCompletedAtUtc { get; set; }
    }

    [DataContract]
    public sealed class UiCapability
    {
        [DataMember(Name = "id", Order = 1)] public string Id { get; set; } = string.Empty;
        [DataMember(Name = "state", Order = 2)] public string State { get; set; } = string.Empty;
        [DataMember(Name = "detail", Order = 3, EmitDefaultValue = true)] public string? Detail { get; set; }
    }

    [DataContract]
    public sealed class UiCensusCounts
    {
        [DataMember(Name = "topLevelObjects", Order = 1)] public UiObservation TopLevelObjects { get; set; } = new UiObservation();
        [DataMember(Name = "subordinateObjects", Order = 2)] public UiObservation SubordinateObjects { get; set; } = new UiObservation();
        [DataMember(Name = "liveObjectReferences", Order = 3)] public UiObservation LiveObjectReferences { get; set; } = new UiObservation();
        [DataMember(Name = "networkEdges", Order = 4)] public UiObservation NetworkEdges { get; set; } = new UiObservation();
    }

    [DataContract]
    public sealed class UiObservation
    {
        [DataMember(Name = "availability", Order = 1)] public string Availability { get; set; } = "NotScanned";
        [DataMember(Name = "value", Order = 2, EmitDefaultValue = true)] public long? Value { get; set; }
        [DataMember(Name = "origin", Order = 3, EmitDefaultValue = true)] public string? Origin { get; set; }
        [DataMember(Name = "capturedAt", Order = 4, EmitDefaultValue = true)] public string? CapturedAt { get; set; }
        [DataMember(Name = "diagnosticCode", Order = 5, EmitDefaultValue = true)] public string? DiagnosticCode { get; set; }
    }

    [DataContract]
    public sealed class UiDoubleObservation
    {
        [DataMember(Name = "availability", Order = 1)] public string Availability { get; set; } = "NotScanned";
        [DataMember(Name = "value", Order = 2, EmitDefaultValue = true)] public double? Value { get; set; }
        [DataMember(Name = "origin", Order = 3, EmitDefaultValue = true)] public string? Origin { get; set; }
        [DataMember(Name = "capturedAt", Order = 4, EmitDefaultValue = true)] public string? CapturedAt { get; set; }
        [DataMember(Name = "diagnosticCode", Order = 5, EmitDefaultValue = true)] public string? DiagnosticCode { get; set; }
    }

    [DataContract]
    public sealed class UiAssetPage
    {
        [DataMember(Name = "offset", Order = 1)] public int Offset { get; set; }
        [DataMember(Name = "limit", Order = 2)] public int Limit { get; set; }
        [DataMember(Name = "totalCount", Order = 3)] public int TotalCount { get; set; }
        [DataMember(Name = "items", Order = 4)] public UiAssetRow[] Items { get; set; } = new UiAssetRow[0];
    }

    [DataContract]
    public sealed class UiAssetRow
    {
        [DataMember(Name = "prefabId", Order = 1)] public string PrefabId { get; set; } = string.Empty;
        [DataMember(Name = "prefabType", Order = 2)] public string PrefabType { get; set; } = string.Empty;
        [DataMember(Name = "displayName", Order = 3)] public string DisplayName { get; set; } = string.Empty;
        [DataMember(Name = "sourceLabel", Order = 4)] public string SourceLabel { get; set; } = string.Empty;
        [DataMember(Name = "traits", Order = 5)] public string[] Traits { get; set; } = new string[0];
        [DataMember(Name = "countKind", Order = 6)] public string CountKind { get; set; } = "None";
        [DataMember(Name = "instances", Order = 7)] public UiObservation Instances { get; set; } = new UiObservation();
        [DataMember(Name = "presence", Order = 8)] public string Presence { get; set; } = "Unknown";
        [DataMember(Name = "counters", Order = 9, EmitDefaultValue = true)] public UiCensusCounts? Counters { get; set; }
        [DataMember(Name = "renderCoverage", Order = 10)] public string RenderCoverage { get; set; } = "NotScanned";
        [DataMember(Name = "estimatedTexturePayload", Order = 11)] public UiObservation EstimatedTexturePayload { get; set; } = new UiObservation();
        [DataMember(Name = "findingCount", Order = 12)] public int FindingCount { get; set; }
        [DataMember(Name = "lod0Vertices", Order = 13)] public UiObservation Lod0Vertices { get; set; } = new UiObservation();
        [DataMember(Name = "lod1RetentionPercent", Order = 14)] public UiDoubleObservation Lod1RetentionPercent { get; set; } = new UiDoubleObservation();
        [DataMember(Name = "materialCount", Order = 15)] public UiObservation MaterialCount { get; set; } = new UiObservation();
        [DataMember(Name = "uniqueTextureCount", Order = 16)] public UiObservation UniqueTextureCount { get; set; } = new UiObservation();
        [DataMember(Name = "renderRelations", Order = 17)] public UiRenderRelation[] RenderRelations { get; set; } = new UiRenderRelation[0];
    }

    [DataContract]
    public sealed class UiMaterialBinding
    {
        [DataMember(Name = "materialName", Order = 1)] public string MaterialName { get; set; } = string.Empty;
        [DataMember(Name = "shaderName", Order = 2)] public string ShaderName { get; set; } = string.Empty;
        [DataMember(Name = "shaderKeywords", Order = 3)] public string[] ShaderKeywords { get; set; } = new string[0];
        [DataMember(Name = "renderQueue", Order = 4)] public int RenderQueue { get; set; }
        [DataMember(Name = "passCount", Order = 5)] public int PassCount { get; set; }
        [DataMember(Name = "enableInstancing", Order = 6)] public bool EnableInstancing { get; set; }
    }

    [DataContract]
    public sealed class UiDeepInspection
    {
        [DataMember(Name = "availability", Order = 1)] public string Availability { get; set; } = "NotScanned";
        [DataMember(Name = "capturedAt", Order = 2, EmitDefaultValue = true)] public string? CapturedAt { get; set; }
        [DataMember(Name = "diagnosticCode", Order = 3, EmitDefaultValue = true)] public string? DiagnosticCode { get; set; }
        [DataMember(Name = "materials", Order = 4)] public UiMaterialBinding[] Materials { get; set; } = new UiMaterialBinding[0];
        [DataMember(Name = "surfaceAssetIds", Order = 5)] public string[] SurfaceAssetIds { get; set; } = new string[0];
    }

    [DataContract]
    public sealed class UiRenderRelation
    {
        [DataMember(Name = "kind", Order = 1)] public string Kind { get; set; } = string.Empty;
        [DataMember(Name = "from", Order = 2)] public string From { get; set; } = string.Empty;
        [DataMember(Name = "to", Order = 3)] public string To { get; set; } = string.Empty;
        [DataMember(Name = "lodLevel", Order = 4, EmitDefaultValue = true)] public int? LodLevel { get; set; }
        [DataMember(Name = "deepInspection", Order = 5, EmitDefaultValue = true)] public UiDeepInspection? DeepInspection { get; set; }
    }

    [DataContract]
    public sealed class UiFinding
    {
        [DataMember(Name = "ruleId", Order = 1)] public string RuleId { get; set; } = string.Empty;
        [DataMember(Name = "status", Order = 2)] public string Status { get; set; } = "Observed";
        [DataMember(Name = "category", Order = 3)] public string Category { get; set; } = "Integrity";
        [DataMember(Name = "title", Order = 4)] public string Title { get; set; } = string.Empty;
        [DataMember(Name = "explanation", Order = 5)] public string Explanation { get; set; } = string.Empty;
        [DataMember(Name = "evidence", Order = 6)] public string[] Evidence { get; set; } = new string[0];
        [DataMember(Name = "basis", Order = 7)] public string Basis { get; set; } = string.Empty;
        [DataMember(Name = "ruleVersion", Order = 8)] public string RuleVersion { get; set; } = string.Empty;
        [DataMember(Name = "prefabId", Order = 9, EmitDefaultValue = true)] public string? PrefabId { get; set; }
        [DataMember(Name = "prefabType", Order = 10, EmitDefaultValue = true)] public string? PrefabType { get; set; }
    }

    [DataContract]
    public sealed class UiDiagnostics
    {
        [DataMember(Name = "harmony", Order = 1)] public string Harmony { get; set; } = "not used";
        [DataMember(Name = "lastScanState", Order = 2)] public string LastScanState { get; set; } = "Idle";
        [DataMember(Name = "lastDiagnosticCode", Order = 3, EmitDefaultValue = true)] public string? LastDiagnosticCode { get; set; }
        [DataMember(Name = "catalogUnresolvedCount", Order = 4)] public int CatalogUnresolvedCount { get; set; }
        [DataMember(Name = "unmatchedPrefabReferenceCount", Order = 5)] public int UnmatchedPrefabReferenceCount { get; set; }
        [DataMember(Name = "diagnosticDistinctCount", Order = 6)] public int DiagnosticDistinctCount { get; set; }
        [DataMember(Name = "diagnosticOccurrenceCount", Order = 7)] public long DiagnosticOccurrenceCount { get; set; }
        [DataMember(Name = "telemetry", Order = 8, EmitDefaultValue = true)] public UiScanTelemetry? Telemetry { get; set; }
        [DataMember(Name = "aggregatedDiagnostics", Order = 9)] public UiDiagnosticEntry[] AggregatedDiagnostics { get; set; } = new UiDiagnosticEntry[0];
    }

    [DataContract]
    public sealed class UiScanTelemetry
    {
        [DataMember(Name = "elapsedMilliseconds", Order = 1)] public double ElapsedMilliseconds { get; set; }
        [DataMember(Name = "processedItems", Order = 2)] public long ProcessedItems { get; set; }
        [DataMember(Name = "sliceCount", Order = 3)] public long SliceCount { get; set; }
        [DataMember(Name = "sampleCount", Order = 4)] public int SampleCount { get; set; }
        [DataMember(Name = "maxSliceMilliseconds", Order = 5)] public double MaxSliceMilliseconds { get; set; }
        [DataMember(Name = "p95SliceMilliseconds", Order = 6)] public double P95SliceMilliseconds { get; set; }
    }

    [DataContract]
    public sealed class UiDiagnosticEntry
    {
        [DataMember(Name = "code", Order = 1)] public string Code { get; set; } = string.Empty;
        [DataMember(Name = "message", Order = 2)] public string Message { get; set; } = string.Empty;
        [DataMember(Name = "count", Order = 3)] public int Count { get; set; }
        [DataMember(Name = "firstSeenAt", Order = 4)] public string FirstSeenAt { get; set; } = string.Empty;
        [DataMember(Name = "lastSeenAt", Order = 5)] public string LastSeenAt { get; set; } = string.Empty;
    }

    [DataContract]
    public sealed class UiScanOptions
    {
        [DataMember(Name = "collectSubordinateObjects", Order = 1)] public bool CollectSubordinateObjects { get; set; } = true;
        [DataMember(Name = "collectNetworkEdges", Order = 2)] public bool CollectNetworkEdges { get; set; } = true;
        [DataMember(Name = "frameBudgetMs", Order = 3)] public double FrameBudgetMs { get; set; } = 1.0;
        [DataMember(Name = "progressUpdateMs", Order = 4)] public int ProgressUpdateMs { get; set; } = 200;
        [DataMember(Name = "refreshCatalogAtScanStart", Order = 5)] public bool RefreshCatalogAtScanStart { get; set; } = true;
        [DataMember(Name = "enableHeuristicFindings", Order = 6)] public bool EnableHeuristicFindings { get; set; } = true;
        [DataMember(Name = "enablePeerOutliers", Order = 7)] public bool EnablePeerOutliers { get; set; } = true;
        [DataMember(Name = "comparisonPopulation", Order = 8)] public string ComparisonPopulation { get; set; } = "SameCategory";
        [DataMember(Name = "showNoticeFindings", Order = 9)] public bool ShowNoticeFindings { get; set; } = true;
        [DataMember(Name = "pageSize", Order = 10)] public int PageSize { get; set; } = 100;
        [DataMember(Name = "metadataCacheLimit", Order = 11)] public int MetadataCacheLimit { get; set; } = 512;
        [DataMember(Name = "deepInspectionLimit", Order = 12)] public int DeepInspectionLimit { get; set; } = 1;
        [DataMember(Name = "uiScale", Order = 13)] public double UiScale { get; set; } = 1.0;
    }

    [DataContract]
    public sealed class UiAssetQueryRequest
    {
        [DataMember(Name = "searchText", Order = 1, EmitDefaultValue = true)] public string? SearchText { get; set; }
        [DataMember(Name = "traitFilter", Order = 2, EmitDefaultValue = true)] public string? TraitFilter { get; set; }
        [DataMember(Name = "sourceFilter", Order = 3, EmitDefaultValue = true)] public string? SourceFilter { get; set; }
        [DataMember(Name = "presenceFilter", Order = 4, EmitDefaultValue = true)] public string? PresenceFilter { get; set; }
        [DataMember(Name = "sort", Order = 5, EmitDefaultValue = true)] public string? Sort { get; set; }
        [DataMember(Name = "offset", Order = 6)] public int Offset { get; set; }
        [DataMember(Name = "limit", Order = 7)] public int Limit { get; set; } = 100;
    }
}
