using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;

namespace CS2RuntimeAssetAuditor.Assets.Core.Rendering
{
    public sealed class GeometryObservation
    {
        public GeometryObservation(string geometryAssetId, Observation<int> meshCount, Observation<long> totalVertexCount, Observation<long> totalIndexCount, Observation<int> subMeshCount, Observation<long> compressedDataSize, IEnumerable<MeshObservation> meshes)
        {
            if (string.IsNullOrWhiteSpace(geometryAssetId)) throw new ArgumentException("Geometry asset ID is required.", nameof(geometryAssetId));
            GeometryAssetId = geometryAssetId;
            MeshCount = meshCount ?? throw new ArgumentNullException(nameof(meshCount));
            TotalVertexCount = totalVertexCount ?? throw new ArgumentNullException(nameof(totalVertexCount));
            TotalIndexCount = totalIndexCount ?? throw new ArgumentNullException(nameof(totalIndexCount));
            SubMeshCount = subMeshCount ?? throw new ArgumentNullException(nameof(subMeshCount));
            CompressedDataSize = compressedDataSize ?? throw new ArgumentNullException(nameof(compressedDataSize));
            Meshes = Array.AsReadOnly((meshes ?? throw new ArgumentNullException(nameof(meshes))).ToArray());
        }
        public string GeometryAssetId { get; }
        public Observation<int> MeshCount { get; }
        public Observation<long> TotalVertexCount { get; }
        public Observation<long> TotalIndexCount { get; }
        public Observation<int> SubMeshCount { get; }
        public Observation<long> CompressedDataSize { get; }
        public IReadOnlyList<MeshObservation> Meshes { get; }

        public static GeometryObservation Unavailable(string geometryAssetId, Availability availability, string? diagnosticCode, DateTimeOffset capturedAt)
        {
            if (availability == Availability.Available) throw new ArgumentException("Unavailable geometry must not use Available.", nameof(availability));
            var code = availability == Availability.Failed ? diagnosticCode : null;
            return new GeometryObservation(
                geometryAssetId,
                Observation<int>.Unavailable(availability, ObservationOrigin.AssetDatabase, capturedAt, code),
                Observation<long>.Unavailable(availability, ObservationOrigin.AssetDatabase, capturedAt, code),
                Observation<long>.Unavailable(availability, ObservationOrigin.AssetDatabase, capturedAt, code),
                Observation<int>.Unavailable(availability, ObservationOrigin.AssetDatabase, capturedAt, code),
                Observation<long>.Unavailable(availability, ObservationOrigin.AssetDatabase, capturedAt, code),
                Array.Empty<MeshObservation>());
        }
    }

    public sealed class GenerationCache<TKey, TValue> where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _values = new Dictionary<TKey, TValue>();
        private long? _generation;
        public TValue GetOrAdd(long generation, TKey key, Func<TValue> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (!_generation.HasValue || _generation.Value != generation) { _values.Clear(); _generation = generation; }
            if (_values.TryGetValue(key, out var existing)) return existing;
            var value = factory();
            _values.Add(key, value);
            return value;
        }
        public void Clear() { _values.Clear(); _generation = null; }
    }
}
