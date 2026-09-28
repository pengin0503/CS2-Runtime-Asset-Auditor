using System;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.Core.Rendering
{
    public sealed class PrefabRenderRelation
    {
        public PrefabRenderRelation(PrefabKey prefabKey, RenderAssetKey renderAssetKey, RenderRelationKind relationKind, int? lodLevel = null)
        {
            if (!prefabKey.IsValid) throw new ArgumentException("A stable Prefab key is required.", nameof(prefabKey));
            if (!renderAssetKey.IsValid) throw new ArgumentException("A stable render-asset key is required.", nameof(renderAssetKey));
            if (!Enum.IsDefined(typeof(RenderRelationKind), relationKind)) throw new ArgumentOutOfRangeException(nameof(relationKind));
            if (lodLevel.HasValue && lodLevel.Value < 0) throw new ArgumentOutOfRangeException(nameof(lodLevel));
            PrefabKey = prefabKey;
            RenderAssetKey = renderAssetKey;
            RelationKind = relationKind;
            LodLevel = lodLevel;
        }

        public PrefabKey PrefabKey { get; }
        public RenderAssetKey RenderAssetKey { get; }
        public RenderRelationKind RelationKind { get; }
        public int? LodLevel { get; }
    }
}
