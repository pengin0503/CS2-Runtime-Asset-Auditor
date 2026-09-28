namespace CS2RuntimeAssetAuditor.Assets.Core.Scanning
{
    public enum ScanState
    {
        Idle,
        Running,
        CancellationRequested,
        Cancelled,
        Failed,
        Completed,
        InterruptedByRuntimeCapture
    }
}
