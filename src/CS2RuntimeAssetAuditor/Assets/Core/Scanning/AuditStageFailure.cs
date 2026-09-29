using CS2RuntimeAssetAuditor.Assets.Core.Capabilities;

namespace CS2RuntimeAssetAuditor.Assets.Core.Scanning
{
    /// <summary>
    /// Maps the Asset Audit stage that failed to the game-data capability it depends on. Findings evaluation
    /// runs on already collected evidence, so its failure is a mod defect and degrades no game capability.
    /// </summary>
    public static class AuditStageFailure
    {
        public static CapabilityId? CapabilityFor(ScanStage stage)
        {
            switch (stage)
            {
                case ScanStage.ResolvingRenderGraph:
                case ScanStage.CollectingGeometry:
                    return CapabilityId.GeometryMetadata;
                case ScanStage.CollectingSurfaceTexture:
                    return CapabilityId.SurfaceMetadata;
                default:
                    return null;
            }
        }

        public static string DetailFor(ScanStage stage)
        {
            switch (stage)
            {
                case ScanStage.ResolvingRenderGraph: return "asset_render_graph_stage_failed";
                case ScanStage.CollectingGeometry: return "asset_geometry_stage_failed";
                case ScanStage.CollectingSurfaceTexture: return "asset_surface_texture_stage_failed";
                case ScanStage.EvaluatingFindings: return "asset_findings_stage_failed";
                default: return "asset_analysis_stage_failed";
            }
        }
    }
}
