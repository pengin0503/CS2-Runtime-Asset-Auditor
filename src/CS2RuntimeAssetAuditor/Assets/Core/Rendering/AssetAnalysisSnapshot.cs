using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Findings;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.Core.Rendering
{
    public sealed class RenderAssetAnalysisRecord
    {
        public RenderAssetAnalysisRecord(
            RenderAssetRecord renderAsset,
            GeometryObservation? geometry,
            IEnumerable<SurfaceObservation>? surfaces = null,
            IEnumerable<TextureObservation>? textures = null)
        {
            RenderAsset = renderAsset ?? throw new ArgumentNullException(nameof(renderAsset));
            Geometry = geometry;
            Surfaces = Array.AsReadOnly((surfaces ?? Array.Empty<SurfaceObservation>()).ToArray());
            Textures = Array.AsReadOnly((textures ?? Array.Empty<TextureObservation>()).ToArray());
        }

        public RenderAssetRecord RenderAsset { get; }
        public GeometryObservation? Geometry { get; }
        public IReadOnlyList<SurfaceObservation> Surfaces { get; }
        public IReadOnlyList<TextureObservation> Textures { get; }

        public RenderAssetAnalysisRecord WithDeepInspection(DeepInspectionObservation observation)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            return new RenderAssetAnalysisRecord(RenderAsset.WithDeepInspection(observation), Geometry, Surfaces, Textures);
        }
    }

    public sealed class PrefabAnalysisEntry
    {
        public PrefabAnalysisEntry(
            PrefabKey key,
            RenderCoverage renderCoverage,
            IEnumerable<PrefabRenderRelation> relations,
            Observation<long> lod0Vertices,
            Observation<double> lod1RetentionPercent,
            Observation<long> materialCount,
            Observation<long> uniqueTextureCount,
            Observation<long> estimatedTexturePayload,
            IEnumerable<Finding>? findings = null)
        {
            if (!key.IsValid) throw new ArgumentException("A stable Prefab key is required.", nameof(key));
            if (!Enum.IsDefined(typeof(RenderCoverage), renderCoverage)) throw new ArgumentOutOfRangeException(nameof(renderCoverage));
            Key = key;
            RenderCoverage = renderCoverage;
            Relations = Array.AsReadOnly((relations ?? throw new ArgumentNullException(nameof(relations))).ToArray());
            Lod0Vertices = lod0Vertices ?? throw new ArgumentNullException(nameof(lod0Vertices));
            Lod1RetentionPercent = lod1RetentionPercent ?? throw new ArgumentNullException(nameof(lod1RetentionPercent));
            MaterialCount = materialCount ?? throw new ArgumentNullException(nameof(materialCount));
            UniqueTextureCount = uniqueTextureCount ?? throw new ArgumentNullException(nameof(uniqueTextureCount));
            EstimatedTexturePayload = estimatedTexturePayload ?? throw new ArgumentNullException(nameof(estimatedTexturePayload));
            Findings = Array.AsReadOnly((findings ?? Array.Empty<Finding>()).ToArray());
        }

        public PrefabKey Key { get; }
        public RenderCoverage RenderCoverage { get; }
        public IReadOnlyList<PrefabRenderRelation> Relations { get; }
        public Observation<long> Lod0Vertices { get; }
        public Observation<double> Lod1RetentionPercent { get; }
        public Observation<long> MaterialCount { get; }
        public Observation<long> UniqueTextureCount { get; }
        public Observation<long> EstimatedTexturePayload { get; }
        public IReadOnlyList<Finding> Findings { get; }
    }

    public sealed class AssetAnalysisSnapshot
    {
        private readonly IReadOnlyDictionary<PrefabKey, PrefabAnalysisEntry> _prefabs;
        private readonly IReadOnlyDictionary<RenderAssetKey, RenderAssetAnalysisRecord> _renderAssets;

        public AssetAnalysisSnapshot(
            long worldGeneration,
            long catalogGeneration,
            long analysisGeneration,
            DateTimeOffset capturedAt,
            IEnumerable<PrefabAnalysisEntry> prefabs,
            IEnumerable<RenderAssetAnalysisRecord> renderAssets)
        {
            if (worldGeneration < 0) throw new ArgumentOutOfRangeException(nameof(worldGeneration));
            if (catalogGeneration < 0) throw new ArgumentOutOfRangeException(nameof(catalogGeneration));
            if (analysisGeneration < 0) throw new ArgumentOutOfRangeException(nameof(analysisGeneration));
            WorldGeneration = worldGeneration;
            CatalogGeneration = catalogGeneration;
            AnalysisGeneration = analysisGeneration;
            CapturedAt = capturedAt;

            var prefabMap = new Dictionary<PrefabKey, PrefabAnalysisEntry>();
            foreach (var entry in prefabs ?? throw new ArgumentNullException(nameof(prefabs)))
            {
                if (entry == null) throw new ArgumentException("Prefab analysis entries cannot contain null.", nameof(prefabs));
                if (prefabMap.ContainsKey(entry.Key))
                    throw new ArgumentException("Prefab analysis keys must be unique.", nameof(prefabs));
                prefabMap.Add(entry.Key, entry);
            }

            var renderMap = new Dictionary<RenderAssetKey, RenderAssetAnalysisRecord>();
            foreach (var record in renderAssets ?? throw new ArgumentNullException(nameof(renderAssets)))
            {
                if (record == null) throw new ArgumentException("Render analysis records cannot contain null.", nameof(renderAssets));
                if (renderMap.ContainsKey(record.RenderAsset.Key))
                    throw new ArgumentException("Render analysis keys must be unique.", nameof(renderAssets));
                renderMap.Add(record.RenderAsset.Key, record);
            }

            _prefabs = new ReadOnlyDictionary<PrefabKey, PrefabAnalysisEntry>(prefabMap);
            _renderAssets = new ReadOnlyDictionary<RenderAssetKey, RenderAssetAnalysisRecord>(renderMap);
            Prefabs = Array.AsReadOnly(prefabMap.Values
                .OrderBy(entry => entry.Key.PrefabType, StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.PrefabId, StringComparer.Ordinal)
                .ToArray());
            RenderAssets = Array.AsReadOnly(renderMap.Values
                .OrderBy(record => record.RenderAsset.Key.RenderAssetType, StringComparer.Ordinal)
                .ThenBy(record => record.RenderAsset.Key.RenderAssetId, StringComparer.Ordinal)
                .ToArray());
            Findings = Array.AsReadOnly(Prefabs.SelectMany(entry => entry.Findings).ToArray());
        }

        public long WorldGeneration { get; }
        public long CatalogGeneration { get; }
        public long AnalysisGeneration { get; }
        public DateTimeOffset CapturedAt { get; }
        public IReadOnlyList<PrefabAnalysisEntry> Prefabs { get; }
        public IReadOnlyList<RenderAssetAnalysisRecord> RenderAssets { get; }
        public IReadOnlyList<Finding> Findings { get; }

        public bool TryGetPrefab(PrefabKey key, out PrefabAnalysisEntry entry) => _prefabs.TryGetValue(key, out entry!);
        public bool TryGetRenderAsset(RenderAssetKey key, out RenderAssetAnalysisRecord record) => _renderAssets.TryGetValue(key, out record!);

        public AssetAnalysisSnapshot WithDeepInspection(RenderAssetKey key, DeepInspectionObservation observation, long analysisGeneration)
        {
            if (!key.IsValid) throw new ArgumentException("A stable render-asset key is required.", nameof(key));
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            if (analysisGeneration <= AnalysisGeneration) throw new ArgumentOutOfRangeException(nameof(analysisGeneration), "Enrichment must advance the analysis generation.");
            if (!_renderAssets.ContainsKey(key))
                throw new InvalidOperationException("The selected render asset is not part of this analysis snapshot.");

            var updated = new List<RenderAssetAnalysisRecord>(RenderAssets.Count);
            foreach (var record in RenderAssets)
                updated.Add(record.RenderAsset.Key == key ? record.WithDeepInspection(observation) : record);
            return new AssetAnalysisSnapshot(WorldGeneration, CatalogGeneration, analysisGeneration, observation.CapturedAt, Prefabs, updated);
        }
    }
}
