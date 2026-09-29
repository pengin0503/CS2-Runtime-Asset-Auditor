using CS2RuntimeAssetAuditor.Core;
using Game.SceneFlow;
using Game.Simulation;

namespace CS2RuntimeAssetAuditor.Profiling
{
    internal static class RuntimeGameStateProbe
    {
        public static bool IsAutomaticCaptureAllowed(SimulationSystem? simulationSystem)
        {
            var gameManager = GameManager.instance;
            if (gameManager == null)
                return false;

            var paused = simulationSystem != null && AutomaticCapturePolicy.IsPaused(simulationSystem.selectedSpeed);
            return AutomaticCapturePolicy.IsAllowed(gameManager.isGameLoading, paused);
        }
    }
}
