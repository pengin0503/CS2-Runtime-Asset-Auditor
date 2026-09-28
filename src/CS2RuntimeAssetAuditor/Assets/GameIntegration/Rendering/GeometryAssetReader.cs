using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using Game.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Rendering
{
    // Geometry totals come from the RenderPrefab's serialized metadata (meshCount/vertexCount/indexCount), which
    // is resident whenever the Prefab is. GeometryAsset mesh data is streamed on demand: GeometryAsset.meshCount
    // throws while it is not loaded, and its buffers are written by async reads while loading. Per-mesh and
    // submesh detail is therefore read only when that data is already resident and idle; the audit never loads
    // geometry itself, and non-resident detail is reported as NotScanned rather than as zero.
    public sealed class GeometryAssetReader
    {
        private readonly GenerationCache<string, GeometryObservation> _cache = new GenerationCache<string, GeometryObservation>();

        public GeometryObservation Read(RenderPrefab renderPrefab, long analysisGeneration, DateTimeOffset capturedAt)
        {
            if (renderPrefab == null) throw new ArgumentNullException(nameof(renderPrefab));
            var geometryAsset = renderPrefab.geometryAsset;
            if (geometryAsset == null)
                return GeometryObservation.Unavailable("geometry:missing", Availability.Failed, "APA-GEO-001", capturedAt);
            string id;
            try { id = StableId(geometryAsset); }
            catch { return GeometryObservation.Unavailable("geometry:unidentified", Availability.Failed, "APA-GEO-001", capturedAt); }
            return _cache.GetOrAdd(analysisGeneration, id, () => ReadCore(renderPrefab, geometryAsset, id, capturedAt));
        }

        public void ClearCache() => _cache.Clear();

        private static GeometryObservation ReadCore(RenderPrefab renderPrefab, GeometryAsset geometryAsset, string id, DateTimeOffset capturedAt)
        {
            var prefabMeshCount = renderPrefab.meshCount;
            if (prefabMeshCount <= 0)
                return GeometryObservation.Unavailable(id, Availability.Unsupported, null, capturedAt);

            var meshCount = Observation<int>.FromValue(prefabMeshCount, ObservationOrigin.GameAssembly, capturedAt);
            var totalVertices = Observation<long>.FromValue(renderPrefab.vertexCount, ObservationOrigin.GameAssembly, capturedAt);
            var totalIndices = Observation<long>.FromValue(renderPrefab.indexCount, ObservationOrigin.GameAssembly, capturedAt);

            if (!IsResidentAndIdle(geometryAsset))
            {
                return new GeometryObservation(id, meshCount, totalVertices, totalIndices,
                    Observation<int>.Unavailable(Availability.NotScanned, ObservationOrigin.AssetDatabase, capturedAt),
                    Observation<long>.Unavailable(Availability.NotScanned, ObservationOrigin.AssetDatabase, capturedAt),
                    Array.Empty<MeshObservation>());
            }

            try
            {
                var residentMeshCount = geometryAsset.meshCount;
                var meshes = new List<MeshObservation>(Math.Max(0, residentMeshCount));
                var totalSubMeshes = 0;
                for (var meshIndex = 0; meshIndex < residentMeshCount; meshIndex++)
                {
                    var subMeshCount = geometryAsset.GetSubMeshCount(meshIndex);
                    var subMeshes = new List<SubMeshObservation>(Math.Max(0, subMeshCount));
                    for (var subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                    {
                        var descriptor = geometryAsset.GetSubMeshDesc(meshIndex, subMeshIndex);
                        var bounds = descriptor.bounds;
                        subMeshes.Add(SubMeshObservation.Create(meshIndex, subMeshIndex, descriptor.topology.ToString(), descriptor.indexCount, descriptor.vertexCount,
                            BoundsObservation.FromValues(bounds.center.x, bounds.center.y, bounds.center.z, bounds.extents.x, bounds.extents.y, bounds.extents.z), capturedAt));
                    }
                    totalSubMeshes = checked(totalSubMeshes + subMeshCount);
                    meshes.Add(new MeshObservation(meshIndex,
                        Observation<int>.FromValue(geometryAsset.GetVertexCount(meshIndex), ObservationOrigin.AssetDatabase, capturedAt),
                        Observation<int>.FromValue(geometryAsset.GetIndicesCount(meshIndex), ObservationOrigin.AssetDatabase, capturedAt),
                        Observation<string>.FromValue(geometryAsset.GetIndexFormat(meshIndex).ToString(), ObservationOrigin.AssetDatabase, capturedAt),
                        subMeshes));
                }
                return new GeometryObservation(id, meshCount, totalVertices, totalIndices,
                    Observation<int>.FromValue(totalSubMeshes, ObservationOrigin.Derived, capturedAt),
                    Observation<long>.FromValue(geometryAsset.compressedDataSize, ObservationOrigin.AssetDatabase, capturedAt), meshes);
            }
            catch
            {
                // A resident-detail read failure is scoped to the detail; the Prefab metadata totals remain valid.
                return new GeometryObservation(id, meshCount, totalVertices, totalIndices,
                    Observation<int>.Unavailable(Availability.Failed, ObservationOrigin.AssetDatabase, capturedAt, "APA-GEO-002"),
                    Observation<long>.Unavailable(Availability.Failed, ObservationOrigin.AssetDatabase, capturedAt, "APA-GEO-002"),
                    Array.Empty<MeshObservation>());
            }
        }

        private static bool IsResidentAndIdle(GeometryAsset geometryAsset)
        {
            ref var loading = ref geometryAsset.loading;
            if (loading.m_AsyncLoadingScheduled || loading.m_AsyncLoadingStarted)
                return false;
            ref var data = ref geometryAsset.data;
            return data.IsValid && data.meshInfos.IsCreated && data.meshOffsets.IsCreated && data.subMeshInfos.IsCreated;
        }

        private static string StableId(GeometryAsset asset)
        {
            if (!string.IsNullOrWhiteSpace(asset.identifier)) return asset.identifier;
            if (!string.IsNullOrWhiteSpace(asset.uniqueName)) return asset.uniqueName;
            if (!string.IsNullOrWhiteSpace(asset.name)) return asset.name;
            throw new InvalidOperationException("A stable GeometryAsset identifier is unavailable.");
        }
    }
}
