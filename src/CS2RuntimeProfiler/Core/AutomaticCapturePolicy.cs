namespace CS2RuntimeProfiler.Core
{
    public static class AutomaticCapturePolicy
    {
        public static bool IsAllowed(bool loading, bool simulationPaused)
        {
            return !loading && !simulationPaused;
        }
    }
}
