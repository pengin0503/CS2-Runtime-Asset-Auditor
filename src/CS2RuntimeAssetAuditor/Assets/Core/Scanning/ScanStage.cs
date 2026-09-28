namespace CS2RuntimeAssetAuditor.Assets.Core.Scanning
{
    public enum ScanStage
    {
        Idle,
        Preparing,
        CapturingCatalog,
        ProcessingCatalog,
        CapturingObjectCensus,
        ReducingObjectCensus,
        CapturingNetworkCensus,
        ReducingNetworkCensus,
        ResolvingRenderGraph,
        CollectingGeometry,
        CollectingSurfaceTexture,
        EvaluatingFindings,
        DeepInspecting,
        Finalizing,
        Completed
    }
}
