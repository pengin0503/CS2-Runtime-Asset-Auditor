using System;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using CS2RuntimeAssetAuditor.Assets.GameIntegration.Rendering;
using Game.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration
{
    public static class AssetAuditDeepInspectionExtensions
    {
        public static DeepInspectionObservation InspectSelectedRenderAsset(this AssetAuditSystem system, RenderAssetKey selectedKey, RenderPrefab renderPrefab)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (renderPrefab == null) throw new ArgumentNullException(nameof(renderPrefab));
            return new DeepInspectionReader().Read(selectedKey, renderPrefab, DateTimeOffset.UtcNow);
        }
    }
}
