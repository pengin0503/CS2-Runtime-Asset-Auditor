using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Colossal.IO.AssetDatabase;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Findings;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using Game.Prefabs;
using Unity.Entities;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Rendering
{
    public sealed class AssetAnalysisCollector
    {
        private readonly World _world;
        private readonly PrefabRecord[] _catalog;
        private readonly KeyValuePair<Entity, PrefabKey>[] _runtimePrefabKeys;
        private readonly Dictionary<PrefabKey, PrefabRecord> _catalogByKey;
        private readonly CensusSnapshot? _census;
        private readonly long _worldGeneration;
        private readonly long _catalogGeneration;
        private readonly long _analysisGeneration;
        private readonly DateTimeOffset _capturedAt;
        private readonly bool _enableHeuristicFindings;
        private readonly RenderGraphAccumulator _renderGraphAccumulator;
        private readonly GeometryAssetReader _geometryReader = new GeometryAssetReader();
        private readonly SurfaceAssetReader _surfaceReader = new SurfaceAssetReader();
        private readonly TextureAssetReader _textureReader = new TextureAssetReader();
        private readonly FindingEngine _findingEngine = new FindingEngine();
        private readonly HashSet<PrefabKey> _renderProcessedKeys = new HashSet<PrefabKey>();
        private readonly Dictionary<RenderAssetKey, GeometryObservation> _geometry = new Dictionary<RenderAssetKey, GeometryObservation>();
        private readonly Dictionary<RenderAssetKey, IReadOnlyList<SurfaceObservation>> _surfaces = new Dictionary<RenderAssetKey, IReadOnlyList<SurfaceObservation>>();
        private readonly Dictionary<RenderAssetKey, IReadOnlyList<TextureObservation>> _textures = new Dictionary<RenderAssetKey, IReadOnlyList<TextureObservation>>();
        private readonly Dictionary<string, TextureObservation> _textureById = new Dictionary<string, TextureObservation>(StringComparer.Ordinal);
        private readonly Dictionary<string, SurfaceCacheEntry> _surfaceCache = new Dictionary<string, SurfaceCacheEntry>(StringComparer.Ordinal);
        private readonly HashSet<RenderAssetKey> _surfaceReadFailures = new HashSet<RenderAssetKey>();
        private readonly Dictionary<RenderAssetKey, List<string>> _textureReadFailures = new Dictionary<RenderAssetKey, List<string>>();
        private readonly List<PrefabAnalysisEntry> _prefabAnalysis = new List<PrefabAnalysisEntry>();

        private RenderGraphSnapshot? _renderGraph;
        private Dictionary<PrefabKey, PrefabRenderRelation[]>? _relationsByPrefab;
        private int _runtimePrefabIndex;
        private int _geometryIndex;
        private int _surfaceIndex;
        private int _findingIndex;

        public AssetAnalysisCollector(
            World world,
            IEnumerable<PrefabRecord> catalog,
            IReadOnlyDictionary<Entity, PrefabKey> runtimePrefabKeys,
            CensusSnapshot? census,
            long worldGeneration,
            long catalogGeneration,
            long analysisGeneration,
            DateTimeOffset capturedAt,
            bool enableHeuristicFindings)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (runtimePrefabKeys == null) throw new ArgumentNullException(nameof(runtimePrefabKeys));
            if (worldGeneration < 0) throw new ArgumentOutOfRangeException(nameof(worldGeneration));
            if (catalogGeneration < 0) throw new ArgumentOutOfRangeException(nameof(catalogGeneration));
            if (analysisGeneration < 0) throw new ArgumentOutOfRangeException(nameof(analysisGeneration));

            _catalog = catalog.ToArray();
            _runtimePrefabKeys = runtimePrefabKeys.ToArray();
            _catalogByKey = new Dictionary<PrefabKey, PrefabRecord>();
            foreach (var record in _catalog)
                if (!_catalogByKey.ContainsKey(record.Key))
                    _catalogByKey.Add(record.Key, record);
            _census = census != null && census.WorldGeneration == worldGeneration && census.CatalogGeneration == catalogGeneration ? census : null;
            _worldGeneration = worldGeneration;
            _catalogGeneration = catalogGeneration;
            _analysisGeneration = analysisGeneration;
            _capturedAt = capturedAt;
            _enableHeuristicFindings = enableHeuristicFindings;
            _renderGraphAccumulator = new RenderGraphAccumulator(new IRenderAssetResolver[] { new ObjectGeometryResolver() });
        }

        public RenderGraphSnapshot? RenderGraph => _renderGraph;
        public int RenderProcessedCount => _renderProcessedKeys.Count;
        public int RenderTargetCount => _catalog.Length;
        public int GeometryProcessedCount => _geometryIndex;
        public int GeometryTargetCount => _renderGraph?.RenderAssets.Count ?? 0;
        public int SurfaceTextureProcessedCount => _surfaceIndex;
        public int SurfaceTextureTargetCount => _renderGraph?.RenderAssets.Count ?? 0;
        public int FindingsProcessedCount => _findingIndex;
        public int FindingsTargetCount => _catalog.Length;

        public bool ProcessRenderGraphSlice(double frameBudgetMilliseconds)
        {
            EnsureWorld();
            var prefabSystem = _world.GetExistingSystemManaged<PrefabSystem>()
                ?? throw new InvalidOperationException("PrefabSystem is unavailable in the current world.");
            var entityManager = _world.EntityManager;
            var stopwatch = Stopwatch.StartNew();
            var attempted = 0;

            while (_runtimePrefabIndex < _runtimePrefabKeys.Length && (attempted == 0 || stopwatch.Elapsed.TotalMilliseconds < frameBudgetMilliseconds))
            {
                var pair = _runtimePrefabKeys[_runtimePrefabIndex++];
                if (!_renderProcessedKeys.Add(pair.Value))
                    continue;
                attempted++;
                if (!_catalogByKey.TryGetValue(pair.Value, out var record))
                    continue;

                try
                {
                    if (!entityManager.Exists(pair.Key))
                        continue;
                    var prefabData = entityManager.GetComponentData<PrefabData>(pair.Key);
                    if (!prefabSystem.TryGetPrefab<PrefabBase>(prefabData, out var prefab) || prefab == null)
                        continue;
                    _renderGraphAccumulator.Add(new RenderGraphInput(record, prefab));
                }
                catch
                {
                    // One malformed Prefab must not abort analysis of unrelated catalog entries.
                }
            }

            if (_runtimePrefabIndex < _runtimePrefabKeys.Length)
                return false;

            _renderGraph = _renderGraphAccumulator.Snapshot();
            _relationsByPrefab = _renderGraph.Relations
                .GroupBy(relation => relation.PrefabKey)
                .ToDictionary(group => group.Key, group => group.ToArray());
            return true;
        }

        public bool ProcessGeometrySlice(double frameBudgetMilliseconds)
        {
            EnsureWorld();
            var graph = RequireRenderGraph();
            var stopwatch = Stopwatch.StartNew();
            var attempted = 0;

            while (_geometryIndex < graph.RenderAssets.Count && (attempted == 0 || stopwatch.Elapsed.TotalMilliseconds < frameBudgetMilliseconds))
            {
                var record = graph.RenderAssets[_geometryIndex++];
                attempted++;
                try
                {
                    if (!graph.TryGetRuntimeAsset(record.Key, out var runtime) || !(runtime is RenderPrefab renderPrefab))
                    {
                        _geometry[record.Key] = GeometryObservation.Unavailable(
                            "geometry:" + record.Key.RenderAssetId,
                            Availability.Unsupported,
                            null,
                            _capturedAt);
                        continue;
                    }

                    if (!renderPrefab.hasGeometryAsset)
                    {
                        _geometry[record.Key] = GeometryObservation.Unavailable(
                            "geometry:" + record.Key.RenderAssetId,
                            Availability.NotApplicable,
                            null,
                            _capturedAt);
                        continue;
                    }

                    _geometry[record.Key] = _geometryReader.Read(renderPrefab, _analysisGeneration, _capturedAt);
                }
                catch
                {
                    _geometry[record.Key] = GeometryObservation.Unavailable(
                        "geometry:" + record.Key.RenderAssetId,
                        Availability.Failed,
                        "APA-GEO-003",
                        _capturedAt);
                }
            }

            return _geometryIndex >= graph.RenderAssets.Count;
        }

        public bool ProcessSurfaceTextureSlice(double frameBudgetMilliseconds)
        {
            EnsureWorld();
            var graph = RequireRenderGraph();
            var stopwatch = Stopwatch.StartNew();
            var attempted = 0;

            while (_surfaceIndex < graph.RenderAssets.Count && (attempted == 0 || stopwatch.Elapsed.TotalMilliseconds < frameBudgetMilliseconds))
            {
                var record = graph.RenderAssets[_surfaceIndex++];
                attempted++;
                var surfaceObservations = new List<SurfaceObservation>();
                var textureObservations = new List<TextureObservation>();

                try
                {
                    if (!graph.TryGetRuntimeAsset(record.Key, out var runtime) || !(runtime is RenderPrefab renderPrefab))
                    {
                        _surfaces[record.Key] = surfaceObservations.AsReadOnly();
                        _textures[record.Key] = textureObservations.AsReadOnly();
                        continue;
                    }

                    var surfaceAssets = renderPrefab.surfaceAssets;
                    if (surfaceAssets != null)
                    {
                        foreach (var surface in surfaceAssets)
                        {
                            if (surface == null)
                                continue;
                            try
                            {
                                var cacheEntry = ReadSurfaceOnce(surface);
                                surfaceObservations.Add(cacheEntry.Observation);
                                foreach (var texture in cacheEntry.Textures)
                                {
                                    if (texture == null)
                                        continue;
                                    try
                                    {
                                        var textureObservation = _textureReader.Read(texture, _analysisGeneration, _capturedAt);
                                        textureObservations.Add(textureObservation);
                                        if (!_textureById.ContainsKey(textureObservation.TextureAssetId))
                                            _textureById.Add(textureObservation.TextureAssetId, textureObservation);
                                    }
                                    catch
                                    {
                                        AddTextureFailure(record.Key, StableTextureIdOrFallback(texture));
                                    }
                                }
                            }
                            catch
                            {
                                _surfaceReadFailures.Add(record.Key);
                            }
                        }
                    }
                }
                catch
                {
                    _surfaceReadFailures.Add(record.Key);
                }

                _surfaces[record.Key] = Array.AsReadOnly(surfaceObservations
                    .GroupBy(surface => surface.SurfaceAssetId, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .OrderBy(surface => surface.SurfaceAssetId, StringComparer.Ordinal)
                    .ToArray());
                _textures[record.Key] = TextureObservation.Deduplicate(textureObservations);
            }

            return _surfaceIndex >= graph.RenderAssets.Count;
        }

        public bool ProcessFindingSlice(double frameBudgetMilliseconds)
        {
            EnsureWorld();
            var graph = RequireRenderGraph();
            var stopwatch = Stopwatch.StartNew();
            var attempted = 0;

            while (_findingIndex < _catalog.Length && (attempted == 0 || stopwatch.Elapsed.TotalMilliseconds < frameBudgetMilliseconds))
            {
                var prefab = _catalog[_findingIndex++];
                attempted++;
                _prefabAnalysis.Add(BuildPrefabAnalysis(prefab, graph));
            }

            return _findingIndex >= _catalog.Length;
        }

        public AssetAnalysisSnapshot BuildSnapshot()
        {
            var graph = RequireRenderGraph();
            if (_findingIndex < _catalog.Length)
                throw new InvalidOperationException("Asset analysis must finish before the snapshot is built.");

            var renderRecords = new List<RenderAssetAnalysisRecord>(graph.RenderAssets.Count);
            foreach (var record in graph.RenderAssets)
            {
                _geometry.TryGetValue(record.Key, out var geometry);
                _surfaces.TryGetValue(record.Key, out var surfaces);
                _textures.TryGetValue(record.Key, out var textures);
                renderRecords.Add(new RenderAssetAnalysisRecord(
                    record,
                    geometry,
                    surfaces ?? Array.Empty<SurfaceObservation>(),
                    textures ?? Array.Empty<TextureObservation>()));
            }

            return new AssetAnalysisSnapshot(
                _worldGeneration,
                _catalogGeneration,
                _analysisGeneration,
                _capturedAt,
                _prefabAnalysis,
                renderRecords);
        }

        private PrefabAnalysisEntry BuildPrefabAnalysis(PrefabRecord prefab, RenderGraphSnapshot graph)
        {
            var coverage = graph.TryGetCoverage(prefab.Key, out var resolvedCoverage)
                ? resolvedCoverage
                : RenderCoverage.Unknown;
            var relations = _relationsByPrefab != null && _relationsByPrefab.TryGetValue(prefab.Key, out var related)
                ? related
                : Array.Empty<PrefabRenderRelation>();

            var directVertices = SumVertices(relations.Where(relation => relation.RelationKind == RenderRelationKind.DirectMesh), coverage);
            var lod1Vertices = SumVertices(relations.Where(relation => relation.RelationKind == RenderRelationKind.Lod && relation.LodLevel == 1), coverage);
            var lod1Retention = CreateRetention(directVertices, lod1Vertices, relations.Any(relation => relation.RelationKind == RenderRelationKind.Lod && relation.LodLevel == 1));
            var materialCount = CountUniqueSurfaces(relations, coverage);
            var uniqueTextureCount = CountUniqueTextureReferences(relations, coverage);
            var texturePayload = SumUniqueTexturePayload(relations, coverage);

            var findings = new List<Finding>();
            var geometryFindings = _findingEngine.EvaluateGeometryLod(
                new GeometryFindingInput(
                    prefab.Key.PrefabId,
                    prefab.Key.PrefabType,
                    lowerLodPresent: relations.Any(relation => relation.RelationKind == RenderRelationKind.Lod),
                    requiredRenderReferenceBroken: coverage == RenderCoverage.Failed,
                    lod0VertexCount: directVertices.HasValue ? directVertices.Value : (long?)null,
                    lod1VertexCount: lod1Vertices.HasValue ? lod1Vertices.Value : (long?)null,
                    renderStructureResolved: coverage == RenderCoverage.Supported),
                _capturedAt);
            AddAllowedFindings(findings, geometryFindings);

            if (_census != null && _census.TryGetEntry(prefab.Key, out var censusEntry))
            {
                var live = censusEntry.Counters.LiveObjectReferences;
                var subordinate = censusEntry.Counters.SubordinateObjects;
                if (live.HasValue && subordinate.HasValue)
                    AddAllowedFindings(findings, _findingEngine.EvaluateExposure(
                        new ExposureFindingInput(prefab.Key.PrefabId, live.Value, subordinate.Value),
                        _capturedAt));
            }

            foreach (var relation in relations)
            {
                if (!_textureReadFailures.TryGetValue(relation.RenderAssetKey, out var failures))
                    continue;
                foreach (var textureId in failures.Distinct(StringComparer.Ordinal))
                    AddAllowedFindings(findings, _findingEngine.EvaluateTextureRead(
                        new TextureFindingInput(prefab.Key.PrefabId, textureId, Availability.Failed, "APA-TEX-002"),
                        _capturedAt));
            }

            return new PrefabAnalysisEntry(
                prefab.Key,
                coverage,
                relations,
                directVertices,
                lod1Retention,
                materialCount,
                uniqueTextureCount,
                texturePayload,
                findings);
        }

        private void AddAllowedFindings(List<Finding> destination, IEnumerable<Finding> source)
        {
            foreach (var finding in source)
            {
                if (!_enableHeuristicFindings && finding.Basis == FindingBasis.Heuristic)
                    continue;
                destination.Add(finding);
            }
        }

        private Observation<long> SumVertices(IEnumerable<PrefabRenderRelation> relations, RenderCoverage coverage)
        {
            var keys = relations.Select(relation => relation.RenderAssetKey).Distinct().ToArray();
            if (keys.Length == 0)
                return UnavailableLongForCoverage(coverage, ObservationOrigin.Derived);

            long total = 0;
            foreach (var key in keys)
            {
                if (!_geometry.TryGetValue(key, out var observation))
                    return Observation<long>.Unavailable(Availability.NotScanned, ObservationOrigin.Derived, _capturedAt);
                if (!observation.TotalVertexCount.HasValue)
                    return Observation<long>.Unavailable(
                        observation.TotalVertexCount.Availability,
                        ObservationOrigin.Derived,
                        _capturedAt,
                        observation.TotalVertexCount.Availability == Availability.Failed ? observation.TotalVertexCount.DiagnosticCode : null);
                total = checked(total + observation.TotalVertexCount.Value);
            }
            return Observation<long>.FromValue(total, ObservationOrigin.Derived, _capturedAt);
        }

        private Observation<double> CreateRetention(Observation<long> lod0, Observation<long> lod1, bool lowerLodPresent)
        {
            if (!lowerLodPresent)
                return Observation<double>.Unavailable(Availability.NotApplicable, ObservationOrigin.Derived, _capturedAt);
            if (!lod0.HasValue)
                return Observation<double>.Unavailable(lod0.Availability, ObservationOrigin.Derived, _capturedAt, lod0.Availability == Availability.Failed ? lod0.DiagnosticCode : null);
            if (!lod1.HasValue)
                return Observation<double>.Unavailable(lod1.Availability, ObservationOrigin.Derived, _capturedAt, lod1.Availability == Availability.Failed ? lod1.DiagnosticCode : null);
            return LodMetrics.RetentionPercent(lod0.Value, lod1.Value, _capturedAt);
        }

        private Observation<long> CountUniqueSurfaces(IEnumerable<PrefabRenderRelation> relations, RenderCoverage coverage)
        {
            var keys = relations.Select(relation => relation.RenderAssetKey).Distinct().ToArray();
            if (keys.Length == 0)
                return UnavailableLongForCoverage(coverage, ObservationOrigin.Derived);
            if (keys.Any(key => _surfaceReadFailures.Contains(key)))
                return Observation<long>.Unavailable(Availability.Failed, ObservationOrigin.Derived, _capturedAt, "APA-SRF-001");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in keys)
                if (_surfaces.TryGetValue(key, out var surfaces))
                    foreach (var surface in surfaces)
                        ids.Add(surface.SurfaceAssetId);
            return Observation<long>.FromValue(ids.Count, ObservationOrigin.Derived, _capturedAt);
        }

        private Observation<long> CountUniqueTextureReferences(IEnumerable<PrefabRenderRelation> relations, RenderCoverage coverage)
        {
            var keys = relations.Select(relation => relation.RenderAssetKey).Distinct().ToArray();
            if (keys.Length == 0)
                return UnavailableLongForCoverage(coverage, ObservationOrigin.Derived);
            if (keys.Any(key => _surfaceReadFailures.Contains(key)))
                return Observation<long>.Unavailable(Availability.Failed, ObservationOrigin.Derived, _capturedAt, "APA-SRF-001");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in keys)
                if (_surfaces.TryGetValue(key, out var surfaces))
                    foreach (var surface in surfaces)
                        foreach (var textureId in surface.TextureAssetIds)
                            ids.Add(textureId);
            return Observation<long>.FromValue(ids.Count, ObservationOrigin.Derived, _capturedAt);
        }

        private Observation<long> SumUniqueTexturePayload(IEnumerable<PrefabRenderRelation> relations, RenderCoverage coverage)
        {
            var keys = relations.Select(relation => relation.RenderAssetKey).Distinct().ToArray();
            if (keys.Length == 0)
                return UnavailableLongForCoverage(coverage, ObservationOrigin.Estimated);
            if (keys.Any(key => _surfaceReadFailures.Contains(key)))
                return Observation<long>.Unavailable(Availability.Failed, ObservationOrigin.Estimated, _capturedAt, "APA-SRF-001");

            var textureIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in keys)
                if (_surfaces.TryGetValue(key, out var surfaces))
                    foreach (var surface in surfaces)
                        foreach (var textureId in surface.TextureAssetIds)
                            textureIds.Add(textureId);

            long total = 0;
            foreach (var textureId in textureIds)
            {
                if (!_textureById.TryGetValue(textureId, out var texture))
                    return Observation<long>.Unavailable(Availability.Failed, ObservationOrigin.Estimated, _capturedAt, "APA-TEX-002");
                if (!texture.EstimatedLogicalPayload.HasValue)
                    return Observation<long>.Unavailable(
                        texture.EstimatedLogicalPayload.Availability,
                        ObservationOrigin.Estimated,
                        _capturedAt,
                        texture.EstimatedLogicalPayload.Availability == Availability.Failed ? texture.EstimatedLogicalPayload.DiagnosticCode : null);
                total = checked(total + texture.EstimatedLogicalPayload.Value);
            }
            return Observation<long>.FromValue(total, ObservationOrigin.Estimated, _capturedAt);
        }

        private Observation<long> UnavailableLongForCoverage(RenderCoverage coverage, ObservationOrigin origin)
        {
            switch (coverage)
            {
                case RenderCoverage.NotApplicable:
                    return Observation<long>.Unavailable(Availability.NotApplicable, origin, _capturedAt);
                case RenderCoverage.Failed:
                    return Observation<long>.Unavailable(Availability.Failed, origin, _capturedAt, "APA-RND-001");
                case RenderCoverage.Unknown:
                    return Observation<long>.Unavailable(Availability.Unsupported, origin, _capturedAt);
                default:
                    return Observation<long>.FromValue(0, origin, _capturedAt);
            }
        }

        private SurfaceCacheEntry ReadSurfaceOnce(SurfaceAsset surface)
        {
            var id = StableSurfaceId(surface);
            if (_surfaceCache.TryGetValue(id, out var cached))
                return cached;

            var loadedHere = !surface.isDataLoaded;
            try
            {
                if (loadedHere)
                    surface.LoadProperties(false);
                var observation = _surfaceReader.Read(surface, _analysisGeneration, _capturedAt);
                var textures = surface.textures == null
                    ? Array.Empty<TextureAsset>()
                    : surface.textures.Values.Where(texture => texture != null).ToArray();
                var entry = new SurfaceCacheEntry(observation, textures);
                _surfaceCache.Add(id, entry);
                return entry;
            }
            finally
            {
                if (loadedHere)
                    surface.UnloadProperties(false);
            }
        }

        private void AddTextureFailure(RenderAssetKey key, string textureId)
        {
            if (!_textureReadFailures.TryGetValue(key, out var items))
            {
                items = new List<string>();
                _textureReadFailures.Add(key, items);
            }
            items.Add(textureId);
        }

        private RenderGraphSnapshot RequireRenderGraph()
        {
            return _renderGraph ?? throw new InvalidOperationException("Render graph collection has not completed.");
        }

        private void EnsureWorld()
        {
            if (!_world.IsCreated)
                throw new InvalidOperationException("The analysis world is no longer available.");
        }

        private static string StableSurfaceId(SurfaceAsset surface)
        {
            if (!string.IsNullOrWhiteSpace(surface.identifier)) return surface.identifier;
            if (!string.IsNullOrWhiteSpace(surface.uniqueName)) return surface.uniqueName;
            if (!string.IsNullOrWhiteSpace(surface.name)) return surface.name;
            throw new InvalidOperationException("A stable SurfaceAsset identifier is unavailable.");
        }

        private static string StableTextureIdOrFallback(TextureAsset texture)
        {
            if (!string.IsNullOrWhiteSpace(texture.identifier)) return texture.identifier;
            if (!string.IsNullOrWhiteSpace(texture.uniqueName)) return texture.uniqueName;
            if (!string.IsNullOrWhiteSpace(texture.name)) return texture.name;
            return "texture:unidentified";
        }

        private sealed class SurfaceCacheEntry
        {
            public SurfaceCacheEntry(SurfaceObservation observation, TextureAsset[] textures)
            {
                Observation = observation;
                Textures = textures;
            }
            public SurfaceObservation Observation { get; }
            public TextureAsset[] Textures { get; }
        }
    }
}
