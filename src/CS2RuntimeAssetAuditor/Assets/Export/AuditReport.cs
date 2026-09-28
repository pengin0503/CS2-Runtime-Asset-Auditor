using System.Runtime.Serialization;

namespace CS2RuntimeAssetAuditor.Assets.Export
{
    public enum ExportScope
    {
        Full,
        Filtered,
        Selected,
        Census,
        Findings
    }

    [DataContract]
    public sealed class AuditReport
    {
        [DataMember(Name = "schemaVersion", Order = 1)] public string SchemaVersion { get; set; } = string.Empty;
        [DataMember(Name = "ruleSetVersion", Order = 2)] public string RuleSetVersion { get; set; } = string.Empty;
        [DataMember(Name = "queryProfileVersion", Order = 3, EmitDefaultValue = true)] public string? QueryProfileVersion { get; set; }
        [DataMember(Name = "scanOptions", Order = 4)] public ReportScanOptions ScanOptions { get; set; } = new ReportScanOptions();
        [DataMember(Name = "modVersion", Order = 5)] public string ModVersion { get; set; } = string.Empty;
        [DataMember(Name = "gameVersion", Order = 6)] public string GameVersion { get; set; } = string.Empty;
        [DataMember(Name = "generatedAt", Order = 7)] public string GeneratedAt { get; set; } = string.Empty;
        [DataMember(Name = "catalogCapturedAt", Order = 8, EmitDefaultValue = true)] public string? CatalogCapturedAt { get; set; }
        [DataMember(Name = "censusCapturedAt", Order = 9, EmitDefaultValue = true)] public string? CensusCapturedAt { get; set; }
        [DataMember(Name = "catalogGeneration", Order = 10)] public long CatalogGeneration { get; set; }
        [DataMember(Name = "censusCatalogGeneration", Order = 11, EmitDefaultValue = true)] public long? CensusCatalogGeneration { get; set; }
        [DataMember(Name = "worldGeneration", Order = 12, EmitDefaultValue = true)] public long? WorldGeneration { get; set; }
        [DataMember(Name = "capabilityReport", Order = 13)] public ReportCapabilityReport CapabilityReport { get; set; } = new ReportCapabilityReport();
        [DataMember(Name = "catalog", Order = 14)] public ReportPrefab[] Catalog { get; set; } = new ReportPrefab[0];
        [DataMember(Name = "census", Order = 15)] public ReportCensusEntry[] Census { get; set; } = new ReportCensusEntry[0];
        [DataMember(Name = "diagnostics", Order = 16)] public ReportDiagnostic[] Diagnostics { get; set; } = new ReportDiagnostic[0];
        [DataMember(Name = "exportScope", Order = 17)] public string ExportScope { get; set; } = "Full";
        [DataMember(Name = "analysis", Order = 18)] public ReportAnalysis Analysis { get; set; } = new ReportAnalysis();
    }

    [DataContract]
    public sealed class ReportAnalysis
    {
        [DataMember(Name = "findings", Order = 1)] public ReportFinding[] Findings { get; set; } = new ReportFinding[0];
        [DataMember(Name = "assets", Order = 2)] public ReportAssetAnalysis[] Assets { get; set; } = new ReportAssetAnalysis[0];
        [DataMember(Name = "renderAssets", Order = 3)] public ReportRenderAssetAnalysis[] RenderAssets { get; set; } = new ReportRenderAssetAnalysis[0];
    }

    [DataContract]
    public sealed class ReportAssetAnalysis
    {
        [DataMember(Name = "prefabId", Order = 1)] public string PrefabId { get; set; } = string.Empty;
        [DataMember(Name = "prefabType", Order = 2)] public string PrefabType { get; set; } = string.Empty;
        [DataMember(Name = "renderCoverage", Order = 3)] public string RenderCoverage { get; set; } = "Unknown";
        [DataMember(Name = "lod0Vertices", Order = 4)] public ReportObservation Lod0Vertices { get; set; } = new ReportObservation();
        [DataMember(Name = "lod1RetentionPercent", Order = 5)] public ReportDoubleObservation Lod1RetentionPercent { get; set; } = new ReportDoubleObservation();
        [DataMember(Name = "materialCount", Order = 6)] public ReportObservation MaterialCount { get; set; } = new ReportObservation();
        [DataMember(Name = "uniqueTextureCount", Order = 7)] public ReportObservation UniqueTextureCount { get; set; } = new ReportObservation();
        [DataMember(Name = "estimatedTexturePayload", Order = 8)] public ReportObservation EstimatedTexturePayload { get; set; } = new ReportObservation();
        [DataMember(Name = "renderRelations", Order = 9)] public ReportRenderRelation[] RenderRelations { get; set; } = new ReportRenderRelation[0];
    }

    [DataContract]
    public sealed class ReportRenderRelation
    {
        [DataMember(Name = "kind", Order = 1)] public string Kind { get; set; } = string.Empty;
        [DataMember(Name = "renderAssetId", Order = 2)] public string RenderAssetId { get; set; } = string.Empty;
        [DataMember(Name = "renderAssetType", Order = 3)] public string RenderAssetType { get; set; } = string.Empty;
        [DataMember(Name = "lodLevel", Order = 4, EmitDefaultValue = true)] public int? LodLevel { get; set; }
    }

    [DataContract]
    public sealed class ReportRenderAssetAnalysis
    {
        [DataMember(Name = "renderAssetId", Order = 1)] public string RenderAssetId { get; set; } = string.Empty;
        [DataMember(Name = "renderAssetType", Order = 2)] public string RenderAssetType { get; set; } = string.Empty;
        [DataMember(Name = "displayName", Order = 3)] public string DisplayName { get; set; } = string.Empty;
        [DataMember(Name = "geometry", Order = 4, EmitDefaultValue = true)] public ReportGeometry? Geometry { get; set; }
        [DataMember(Name = "surfaces", Order = 5)] public ReportSurface[] Surfaces { get; set; } = new ReportSurface[0];
        [DataMember(Name = "textures", Order = 6)] public ReportTexture[] Textures { get; set; } = new ReportTexture[0];
        [DataMember(Name = "deepInspection", Order = 7, EmitDefaultValue = true)] public ReportDeepInspection? DeepInspection { get; set; }
    }

    [DataContract]
    public sealed class ReportGeometry
    {
        [DataMember(Name = "geometryAssetId", Order = 1)] public string GeometryAssetId { get; set; } = string.Empty;
        [DataMember(Name = "meshCount", Order = 2)] public ReportObservation MeshCount { get; set; } = new ReportObservation();
        [DataMember(Name = "totalVertexCount", Order = 3)] public ReportObservation TotalVertexCount { get; set; } = new ReportObservation();
        [DataMember(Name = "totalIndexCount", Order = 4)] public ReportObservation TotalIndexCount { get; set; } = new ReportObservation();
        [DataMember(Name = "subMeshCount", Order = 5)] public ReportObservation SubMeshCount { get; set; } = new ReportObservation();
        [DataMember(Name = "compressedDataSize", Order = 6)] public ReportObservation CompressedDataSize { get; set; } = new ReportObservation();
        [DataMember(Name = "meshes", Order = 7)] public ReportMesh[] Meshes { get; set; } = new ReportMesh[0];
    }

    [DataContract]
    public sealed class ReportMesh
    {
        [DataMember(Name = "meshIndex", Order = 1)] public int MeshIndex { get; set; }
        [DataMember(Name = "vertexCount", Order = 2)] public ReportObservation VertexCount { get; set; } = new ReportObservation();
        [DataMember(Name = "indexCount", Order = 3)] public ReportObservation IndexCount { get; set; } = new ReportObservation();
        [DataMember(Name = "indexFormat", Order = 4)] public ReportStringObservation IndexFormat { get; set; } = new ReportStringObservation();
        [DataMember(Name = "subMeshes", Order = 5)] public ReportSubMesh[] SubMeshes { get; set; } = new ReportSubMesh[0];
    }

    [DataContract]
    public sealed class ReportSubMesh
    {
        [DataMember(Name = "meshIndex", Order = 1)] public int MeshIndex { get; set; }
        [DataMember(Name = "subMeshIndex", Order = 2)] public int SubMeshIndex { get; set; }
        [DataMember(Name = "topology", Order = 3)] public string Topology { get; set; } = string.Empty;
        [DataMember(Name = "indexCount", Order = 4)] public ReportObservation IndexCount { get; set; } = new ReportObservation();
        [DataMember(Name = "vertexCount", Order = 5)] public ReportObservation VertexCount { get; set; } = new ReportObservation();
        [DataMember(Name = "triangleCount", Order = 6)] public ReportObservation TriangleCount { get; set; } = new ReportObservation();
        [DataMember(Name = "bounds", Order = 7, EmitDefaultValue = true)] public ReportBounds? Bounds { get; set; }
    }

    [DataContract]
    public sealed class ReportBounds
    {
        [DataMember(Name = "centerX", Order = 1)] public float CenterX { get; set; }
        [DataMember(Name = "centerY", Order = 2)] public float CenterY { get; set; }
        [DataMember(Name = "centerZ", Order = 3)] public float CenterZ { get; set; }
        [DataMember(Name = "extentsX", Order = 4)] public float ExtentsX { get; set; }
        [DataMember(Name = "extentsY", Order = 5)] public float ExtentsY { get; set; }
        [DataMember(Name = "extentsZ", Order = 6)] public float ExtentsZ { get; set; }
    }

    [DataContract]
    public sealed class ReportSurface
    {
        [DataMember(Name = "surfaceAssetId", Order = 1)] public string SurfaceAssetId { get; set; } = string.Empty;
        [DataMember(Name = "materialTemplateHash", Order = 2)] public ReportObservation MaterialTemplateHash { get; set; } = new ReportObservation();
        [DataMember(Name = "isVirtualTexturingMaterial", Order = 3)] public ReportBooleanObservation IsVirtualTexturingMaterial { get; set; } = new ReportBooleanObservation();
        [DataMember(Name = "isCurrentlyUsingVirtualTexturing", Order = 4)] public ReportBooleanObservation IsCurrentlyUsingVirtualTexturing { get; set; } = new ReportBooleanObservation();
        [DataMember(Name = "floatPropertyCount", Order = 5)] public int FloatPropertyCount { get; set; }
        [DataMember(Name = "intPropertyCount", Order = 6)] public int IntPropertyCount { get; set; }
        [DataMember(Name = "vectorPropertyCount", Order = 7)] public int VectorPropertyCount { get; set; }
        [DataMember(Name = "colorPropertyCount", Order = 8)] public int ColorPropertyCount { get; set; }
        [DataMember(Name = "keywords", Order = 9)] public string[] Keywords { get; set; } = new string[0];
        [DataMember(Name = "textureAssetIds", Order = 10)] public string[] TextureAssetIds { get; set; } = new string[0];
    }

    [DataContract]
    public sealed class ReportTexture
    {
        [DataMember(Name = "textureAssetId", Order = 1)] public string TextureAssetId { get; set; } = string.Empty;
        [DataMember(Name = "width", Order = 2)] public ReportObservation Width { get; set; } = new ReportObservation();
        [DataMember(Name = "height", Order = 3)] public ReportObservation Height { get; set; } = new ReportObservation();
        [DataMember(Name = "depth", Order = 4)] public ReportObservation Depth { get; set; } = new ReportObservation();
        [DataMember(Name = "format", Order = 5)] public ReportStringObservation Format { get; set; } = new ReportStringObservation();
        [DataMember(Name = "dimension", Order = 6)] public ReportStringObservation Dimension { get; set; } = new ReportStringObservation();
        [DataMember(Name = "mipsCount", Order = 7)] public ReportObservation MipsCount { get; set; } = new ReportObservation();
        [DataMember(Name = "filterMode", Order = 8)] public ReportStringObservation FilterMode { get; set; } = new ReportStringObservation();
        [DataMember(Name = "wrapMode", Order = 9)] public ReportStringObservation WrapMode { get; set; } = new ReportStringObservation();
        [DataMember(Name = "anisoLevel", Order = 10)] public ReportObservation AnisoLevel { get; set; } = new ReportObservation();
        [DataMember(Name = "estimatedLogicalPayload", Order = 11)] public ReportObservation EstimatedLogicalPayload { get; set; } = new ReportObservation();
    }

    [DataContract]
    public sealed class ReportDeepInspection
    {
        [DataMember(Name = "availability", Order = 1)] public string Availability { get; set; } = string.Empty;
        [DataMember(Name = "capturedAt", Order = 2)] public string CapturedAt { get; set; } = string.Empty;
        [DataMember(Name = "diagnosticCode", Order = 3, EmitDefaultValue = true)] public string? DiagnosticCode { get; set; }
        [DataMember(Name = "materials", Order = 4)] public ReportMaterialBinding[] Materials { get; set; } = new ReportMaterialBinding[0];
        [DataMember(Name = "surfaceAssetIds", Order = 5)] public string[] SurfaceAssetIds { get; set; } = new string[0];
    }

    [DataContract]
    public sealed class ReportMaterialBinding
    {
        [DataMember(Name = "materialName", Order = 1)] public string MaterialName { get; set; } = string.Empty;
        [DataMember(Name = "shaderName", Order = 2)] public string ShaderName { get; set; } = string.Empty;
        [DataMember(Name = "shaderKeywords", Order = 3)] public string[] ShaderKeywords { get; set; } = new string[0];
        [DataMember(Name = "renderQueue", Order = 4)] public int RenderQueue { get; set; }
        [DataMember(Name = "passCount", Order = 5)] public int PassCount { get; set; }
        [DataMember(Name = "enableInstancing", Order = 6)] public bool EnableInstancing { get; set; }
    }

    [DataContract]
    public sealed class ReportFinding
    {
        [DataMember(Name = "ruleId", Order = 1)] public string RuleId { get; set; } = string.Empty;
        [DataMember(Name = "status", Order = 2)] public string Status { get; set; } = string.Empty;
        [DataMember(Name = "category", Order = 3)] public string Category { get; set; } = string.Empty;
        [DataMember(Name = "title", Order = 4)] public string Title { get; set; } = string.Empty;
        [DataMember(Name = "explanation", Order = 5)] public string Explanation { get; set; } = string.Empty;
        [DataMember(Name = "evidence", Order = 6)] public string[] Evidence { get; set; } = new string[0];
        [DataMember(Name = "basis", Order = 7)] public string Basis { get; set; } = string.Empty;
        [DataMember(Name = "ruleVersion", Order = 8)] public string RuleVersion { get; set; } = string.Empty;
        // The Prefab that owns this finding; null only for findings supplied without an owning analysis entry.
        [DataMember(Name = "prefabId", Order = 9, EmitDefaultValue = true)] public string? PrefabId { get; set; }
        [DataMember(Name = "prefabType", Order = 10, EmitDefaultValue = true)] public string? PrefabType { get; set; }
    }

    [DataContract] public sealed class ReportCapabilityReport
    {
        [DataMember(Name = "compatibility", Order = 1)] public string Compatibility { get; set; } = string.Empty;
        [DataMember(Name = "capabilities", Order = 2)] public ReportCapability[] Capabilities { get; set; } = new ReportCapability[0];
    }

    [DataContract] public sealed class ReportScanOptions
    {
        [DataMember(Name = "wasCensusScanned", Order = 1)] public bool WasCensusScanned { get; set; }
        [DataMember(Name = "collectSubordinateObjects", Order = 2, EmitDefaultValue = true)] public bool? CollectSubordinateObjects { get; set; }
        [DataMember(Name = "collectNetworkEdges", Order = 3, EmitDefaultValue = true)] public bool? CollectNetworkEdges { get; set; }
    }

    [DataContract] public sealed class ReportCapability
    {
        [DataMember(Name = "id", Order = 1)] public string Id { get; set; } = string.Empty;
        [DataMember(Name = "state", Order = 2)] public string State { get; set; } = string.Empty;
        [DataMember(Name = "detail", Order = 3, EmitDefaultValue = true)] public string? Detail { get; set; }
    }

    [DataContract] public sealed class ReportPrefab
    {
        [DataMember(Name = "prefabId", Order = 1)] public string PrefabId { get; set; } = string.Empty;
        [DataMember(Name = "prefabType", Order = 2)] public string PrefabType { get; set; } = string.Empty;
        [DataMember(Name = "displayName", Order = 3)] public string DisplayName { get; set; } = string.Empty;
        [DataMember(Name = "traits", Order = 4)] public string Traits { get; set; } = string.Empty;
        [DataMember(Name = "isBuiltin", Order = 5, EmitDefaultValue = true)] public bool? IsBuiltin { get; set; }
        [DataMember(Name = "isSubscribedMod", Order = 6, EmitDefaultValue = true)] public bool? IsSubscribedMod { get; set; }
        [DataMember(Name = "isPackaged", Order = 7, EmitDefaultValue = true)] public bool? IsPackaged { get; set; }
        [DataMember(Name = "dlcPrerequisiteIds", Order = 8, EmitDefaultValue = true)] public string[]? DlcPrerequisiteIds { get; set; }
        [DataMember(Name = "assetPackMembership", Order = 9, EmitDefaultValue = true)] public string[]? AssetPackMembership { get; set; }
        [DataMember(Name = "assetDatabaseSource", Order = 10, EmitDefaultValue = true)] public string? AssetDatabaseSource { get; set; }
        [DataMember(Name = "paradoxModsPlatformId", Order = 11, EmitDefaultValue = true)] public string? ParadoxModsPlatformId { get; set; }
    }

    [DataContract]
    public sealed class ReportObservation
    {
        [DataMember(Name = "availability", Order = 1)] public string Availability { get; set; } = string.Empty;
        [DataMember(Name = "origin", Order = 2)] public string Origin { get; set; } = string.Empty;
        [DataMember(Name = "capturedAt", Order = 3)] public string CapturedAt { get; set; } = string.Empty;
        [DataMember(Name = "value", Order = 4, EmitDefaultValue = true)] public long? Value { get; set; }
        [DataMember(Name = "diagnosticCode", Order = 5, EmitDefaultValue = true)] public string? DiagnosticCode { get; set; }
    }

    [DataContract]
    public sealed class ReportDoubleObservation
    {
        [DataMember(Name = "availability", Order = 1)] public string Availability { get; set; } = string.Empty;
        [DataMember(Name = "origin", Order = 2)] public string Origin { get; set; } = string.Empty;
        [DataMember(Name = "capturedAt", Order = 3)] public string CapturedAt { get; set; } = string.Empty;
        [DataMember(Name = "value", Order = 4, EmitDefaultValue = true)] public double? Value { get; set; }
        [DataMember(Name = "diagnosticCode", Order = 5, EmitDefaultValue = true)] public string? DiagnosticCode { get; set; }
    }

    [DataContract]
    public sealed class ReportStringObservation
    {
        [DataMember(Name = "availability", Order = 1)] public string Availability { get; set; } = string.Empty;
        [DataMember(Name = "origin", Order = 2)] public string Origin { get; set; } = string.Empty;
        [DataMember(Name = "capturedAt", Order = 3)] public string CapturedAt { get; set; } = string.Empty;
        [DataMember(Name = "value", Order = 4, EmitDefaultValue = true)] public string? Value { get; set; }
        [DataMember(Name = "diagnosticCode", Order = 5, EmitDefaultValue = true)] public string? DiagnosticCode { get; set; }
    }

    [DataContract]
    public sealed class ReportBooleanObservation
    {
        [DataMember(Name = "availability", Order = 1)] public string Availability { get; set; } = string.Empty;
        [DataMember(Name = "origin", Order = 2)] public string Origin { get; set; } = string.Empty;
        [DataMember(Name = "capturedAt", Order = 3)] public string CapturedAt { get; set; } = string.Empty;
        [DataMember(Name = "value", Order = 4, EmitDefaultValue = true)] public bool? Value { get; set; }
        [DataMember(Name = "diagnosticCode", Order = 5, EmitDefaultValue = true)] public string? DiagnosticCode { get; set; }
    }

    [DataContract] public sealed class ReportCensusCounters
    {
        [DataMember(Name = "topLevelObjects", Order = 1)] public ReportObservation TopLevelObjects { get; set; } = new ReportObservation();
        [DataMember(Name = "subordinateObjects", Order = 2)] public ReportObservation SubordinateObjects { get; set; } = new ReportObservation();
        [DataMember(Name = "liveObjectReferences", Order = 3)] public ReportObservation LiveObjectReferences { get; set; } = new ReportObservation();
        [DataMember(Name = "networkEdges", Order = 4)] public ReportObservation NetworkEdges { get; set; } = new ReportObservation();
    }

    [DataContract] public sealed class ReportCensusEntry
    {
        [DataMember(Name = "prefabId", Order = 1)] public string PrefabId { get; set; } = string.Empty;
        [DataMember(Name = "prefabType", Order = 2)] public string PrefabType { get; set; } = string.Empty;
        [DataMember(Name = "presence", Order = 3)] public string Presence { get; set; } = string.Empty;
        [DataMember(Name = "counters", Order = 4)] public ReportCensusCounters Counters { get; set; } = new ReportCensusCounters();
    }

    [DataContract] public sealed class ReportDiagnostic
    {
        [DataMember(Name = "code", Order = 1)] public string Code { get; set; } = string.Empty;
        [DataMember(Name = "message", Order = 2)] public string Message { get; set; } = string.Empty;
        [DataMember(Name = "count", Order = 3)] public int Count { get; set; }
        [DataMember(Name = "firstSeenAt", Order = 4)] public string FirstSeenAt { get; set; } = string.Empty;
        [DataMember(Name = "lastSeenAt", Order = 5)] public string LastSeenAt { get; set; } = string.Empty;
    }
}
