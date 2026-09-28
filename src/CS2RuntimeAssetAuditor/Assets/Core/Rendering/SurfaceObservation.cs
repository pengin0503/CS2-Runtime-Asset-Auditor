using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;

namespace CS2RuntimeAssetAuditor.Assets.Core.Rendering
{
    public sealed class SurfaceObservation
    {
        public SurfaceObservation(string surfaceAssetId, Observation<int> materialTemplateHash, Observation<bool> isVirtualTexturingMaterial, Observation<bool> isCurrentlyUsingVirtualTexturing, int floatPropertyCount, int intPropertyCount, int vectorPropertyCount, int colorPropertyCount, IEnumerable<string> keywords, IEnumerable<string> textureAssetIds)
        {
            if (string.IsNullOrWhiteSpace(surfaceAssetId)) throw new ArgumentException("Surface asset ID is required.", nameof(surfaceAssetId));
            SurfaceAssetId = surfaceAssetId;
            MaterialTemplateHash = materialTemplateHash ?? throw new ArgumentNullException(nameof(materialTemplateHash));
            IsVirtualTexturingMaterial = isVirtualTexturingMaterial ?? throw new ArgumentNullException(nameof(isVirtualTexturingMaterial));
            IsCurrentlyUsingVirtualTexturing = isCurrentlyUsingVirtualTexturing ?? throw new ArgumentNullException(nameof(isCurrentlyUsingVirtualTexturing));
            FloatPropertyCount = floatPropertyCount; IntPropertyCount = intPropertyCount; VectorPropertyCount = vectorPropertyCount; ColorPropertyCount = colorPropertyCount;
            Keywords = Array.AsReadOnly((keywords ?? throw new ArgumentNullException(nameof(keywords))).OrderBy(v => v, StringComparer.Ordinal).ToArray());
            TextureAssetIds = Array.AsReadOnly((textureAssetIds ?? throw new ArgumentNullException(nameof(textureAssetIds))).Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal).ToArray());
        }
        public string SurfaceAssetId { get; }
        public Observation<int> MaterialTemplateHash { get; }
        public Observation<bool> IsVirtualTexturingMaterial { get; }
        public Observation<bool> IsCurrentlyUsingVirtualTexturing { get; }
        public int FloatPropertyCount { get; }
        public int IntPropertyCount { get; }
        public int VectorPropertyCount { get; }
        public int ColorPropertyCount { get; }
        public IReadOnlyList<string> Keywords { get; }
        public IReadOnlyList<string> TextureAssetIds { get; }
    }
}
