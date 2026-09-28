using System;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;

namespace CS2RuntimeAssetAuditor.Assets.Core.Rendering
{
    public sealed class BoundsObservation
    {
        private BoundsObservation(bool hasValue, float centerX, float centerY, float centerZ, float extentsX, float extentsY, float extentsZ)
        {
            HasValue = hasValue;
            CenterX = centerX;
            CenterY = centerY;
            CenterZ = centerZ;
            ExtentsX = extentsX;
            ExtentsY = extentsY;
            ExtentsZ = extentsZ;
        }
        public static BoundsObservation Empty { get; } = new BoundsObservation(false, 0, 0, 0, 0, 0, 0);
        public bool HasValue { get; }
        public float CenterX { get; }
        public float CenterY { get; }
        public float CenterZ { get; }
        public float ExtentsX { get; }
        public float ExtentsY { get; }
        public float ExtentsZ { get; }
        public static BoundsObservation FromValues(float centerX, float centerY, float centerZ, float extentsX, float extentsY, float extentsZ) => new BoundsObservation(true, centerX, centerY, centerZ, extentsX, extentsY, extentsZ);
    }

    public sealed class SubMeshObservation
    {
        private SubMeshObservation(int meshIndex, int subMeshIndex, string topology, Observation<int> indexCount, Observation<int> vertexCount, Observation<long> triangleCount, BoundsObservation bounds)
        {
            MeshIndex = meshIndex; SubMeshIndex = subMeshIndex; Topology = topology; IndexCount = indexCount; VertexCount = vertexCount; TriangleCount = triangleCount; Bounds = bounds;
        }
        public int MeshIndex { get; }
        public int SubMeshIndex { get; }
        public string Topology { get; }
        public Observation<int> IndexCount { get; }
        public Observation<int> VertexCount { get; }
        public Observation<long> TriangleCount { get; }
        public BoundsObservation Bounds { get; }

        public static SubMeshObservation Create(int meshIndex, int subMeshIndex, string topology, int indexCount, int vertexCount, BoundsObservation bounds, DateTimeOffset capturedAt)
        {
            if (meshIndex < 0) throw new ArgumentOutOfRangeException(nameof(meshIndex));
            if (subMeshIndex < 0) throw new ArgumentOutOfRangeException(nameof(subMeshIndex));
            if (string.IsNullOrWhiteSpace(topology)) throw new ArgumentException("Topology is required.", nameof(topology));
            if (indexCount < 0) throw new ArgumentOutOfRangeException(nameof(indexCount));
            if (vertexCount < 0) throw new ArgumentOutOfRangeException(nameof(vertexCount));
            if (bounds == null) throw new ArgumentNullException(nameof(bounds));
            var triangles = string.Equals(topology, "Triangles", StringComparison.Ordinal)
                ? Observation<long>.FromValue(indexCount / 3L, ObservationOrigin.Derived, capturedAt)
                : Observation<long>.Unavailable(Availability.NotApplicable, ObservationOrigin.Derived, capturedAt);
            return new SubMeshObservation(meshIndex, subMeshIndex, topology,
                Observation<int>.FromValue(indexCount, ObservationOrigin.AssetDatabase, capturedAt),
                Observation<int>.FromValue(vertexCount, ObservationOrigin.AssetDatabase, capturedAt), triangles, bounds);
        }
    }
}
