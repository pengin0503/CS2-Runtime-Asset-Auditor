using System;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;

namespace CS2RuntimeAssetAuditor.Assets.UI
{
    public static class UiAnalysisProjection
    {
        public static UiDeepInspection MapDeepInspection(DeepInspectionObservation observation)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            return new UiDeepInspection
            {
                Availability = observation.Availability.ToString(),
                CapturedAt = observation.CapturedAt.ToUniversalTime().ToString("O"),
                DiagnosticCode = observation.DiagnosticCode,
                SurfaceAssetIds = observation.SurfaceAssetIds.ToArray(),
                Materials = observation.Materials.Select(material => new UiMaterialBinding
                {
                    MaterialName = material.MaterialName,
                    ShaderName = material.ShaderName,
                    ShaderKeywords = material.ShaderKeywords.ToArray(),
                    RenderQueue = material.RenderQueue,
                    PassCount = material.PassCount,
                    EnableInstancing = material.EnableInstancing
                }).ToArray()
            };
        }

        public static void ApplyDeepInspections(UiSnapshot snapshot, AssetAnalysisSnapshot? analysis)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (analysis == null) return;

            foreach (var asset in snapshot.AssetPage.Items)
            {
                foreach (var relation in asset.RenderRelations)
                {
                    if (!RenderAssetKey.TryParse(relation.To, out var key)) continue;
                    if (!analysis.TryGetRenderAsset(key, out var renderRecord)) continue;
                    var observation = renderRecord.RenderAsset.DeepInspection;
                    if (observation != null)
                        relation.DeepInspection = MapDeepInspection(observation);
                }
            }
        }
    }
}
