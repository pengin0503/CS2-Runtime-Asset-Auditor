namespace CS2RuntimeAssetAuditor.Core
{
    public static class AutomaticCapturePolicy
    {
        public static bool IsAllowed(bool loading, bool simulationPaused)
        {
            return !loading && !simulationPaused;
        }
    }
}
