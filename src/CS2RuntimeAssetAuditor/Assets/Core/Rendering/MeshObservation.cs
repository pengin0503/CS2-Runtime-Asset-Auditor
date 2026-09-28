using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;

namespace CS2RuntimeAssetAuditor.Assets.Core.Rendering
{
    public sealed class MeshObservation
    {
        public MeshObservation(int meshIndex, Observation<int> vertexCount, Observation<int> indexCount, Observation<string> indexFormat, IEnumerable<SubMeshObservation> subMeshes)
        {
            if (meshIndex < 0) throw new ArgumentOutOfRangeException(nameof(meshIndex));
            MeshIndex = meshIndex;
            VertexCount = vertexCount ?? throw new ArgumentNullException(nameof(vertexCount));
            IndexCount = indexCount ?? throw new ArgumentNullException(nameof(indexCount));
            IndexFormat = indexFormat ?? throw new ArgumentNullException(nameof(indexFormat));
            SubMeshes = Array.AsReadOnly((subMeshes ?? throw new ArgumentNullException(nameof(subMeshes))).ToArray());
        }
        public int MeshIndex { get; }
        public Observation<int> VertexCount { get; }
        public Observation<int> IndexCount { get; }
        public Observation<string> IndexFormat { get; }
        public IReadOnlyList<SubMeshObservation> SubMeshes { get; }
    }
}
