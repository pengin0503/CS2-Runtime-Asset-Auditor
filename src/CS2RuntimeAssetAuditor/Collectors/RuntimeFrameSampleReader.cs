using CS2RuntimeAssetAuditor.Core.Frames;
using Game.Pathfind;
using Game.Simulation;
using UnityEngine;

namespace CS2RuntimeAssetAuditor.Collectors
{
    /// <summary>
    /// Reads the per-frame values behind <see cref="RuntimeFrameSample"/> once per rendered frame. Normal monitoring
    /// and the diagnostic log share one reader so <c>FrameTimingManager.CaptureFrameTimings</c> runs once a frame.
    /// </summary>
    internal sealed class RuntimeFrameSampleReader
    {
        private readonly FrameTiming[] _frameTimings = new FrameTiming[1];
        private readonly SimulationSystem _simulation;
        private readonly PathfindResultSystem _pathfindResults;
        private int _lastFrameCount = -1;
        private RuntimeFrameSample _last;
        private int _lastPreferenceValue = int.MinValue;
        private string? _lastPreferenceName;

        public RuntimeFrameSampleReader(SimulationSystem simulation, PathfindResultSystem pathfindResults)
        {
            _simulation = simulation;
            _pathfindResults = pathfindResults;
        }

        public static bool FrameTimingFeatureEnabled => FrameTimingManager.IsFeatureEnabled();

        public RuntimeFrameSample Read()
        {
            var frameCount = UnityEngine.Time.frameCount;
            if (frameCount == _lastFrameCount)
                return _last;

            var hasTiming = false;
            FrameTiming timing = default;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1u, _frameTimings) != 0)
            {
                timing = _frameTimings[0];
                hasTiming = timing.cpuMainThreadFrameTime > 0d || timing.cpuRenderThreadFrameTime > 0d || timing.gpuFrameTime > 0d;
            }

            var preference = _simulation.performancePreference;
            if ((int)preference != _lastPreferenceValue)
            {
                _lastPreferenceValue = (int)preference;
                _lastPreferenceName = preference.ToString();
            }

            _last = new RuntimeFrameSample(
                UnityEngine.Time.unscaledDeltaTime,
                hasTiming,
                timing.cpuMainThreadFrameTime,
                timing.cpuRenderThreadFrameTime,
                timing.gpuFrameTime,
                timing.cpuMainThreadPresentWaitTime,
                _simulation.selectedSpeed,
                _simulation.smoothSpeed,
                _simulation.frameIndex,
                _simulation.frameDuration,
                _lastPreferenceName,
                _pathfindResults.pendingSimulationFrame,
                _pathfindResults.pendingRequestCount);
            _lastFrameCount = frameCount;
            return _last;
        }
    }
}
