namespace CS2RuntimeAssetAuditor.Core
{
    public static class AutomaticCapturePolicy
    {
        public static bool IsAllowed(bool loading, bool simulationPaused)
        {
            return !loading && !simulationPaused;
        }

        // The game pauses the simulation by setting SimulationSystem.selectedSpeed to 0; there is no separate flag.
        public static bool IsPaused(double selectedSpeed) => selectedSpeed <= 0d;
    }
}
