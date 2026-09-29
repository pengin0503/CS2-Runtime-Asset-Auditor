namespace CS2RuntimeAssetAuditor.Core.Frames
{
    /// <summary>
    /// Values read from the game once per rendered frame. Times are in the units the game APIs report:
    /// seconds for Unity's delta time and <c>SimulationSystem.frameDuration</c>, milliseconds for
    /// <c>FrameTimingManager</c>.
    /// </summary>
    public readonly struct RuntimeFrameSample
    {
        public RuntimeFrameSample(
            double unscaledDeltaSeconds,
            bool hasFrameTiming,
            double cpuMainThreadMs,
            double cpuRenderThreadMs,
            double gpuMs,
            double presentWaitMs,
            double selectedSpeed,
            double actualSpeed,
            uint simulationFrameIndex,
            double simulationStepSeconds,
            string? performancePreference,
            uint pathfindPendingSimulationFrame,
            int pathfindPendingRequests)
        {
            UnscaledDeltaSeconds = unscaledDeltaSeconds;
            HasFrameTiming = hasFrameTiming;
            CpuMainThreadMs = cpuMainThreadMs;
            CpuRenderThreadMs = cpuRenderThreadMs;
            GpuMs = gpuMs;
            PresentWaitMs = presentWaitMs;
            SelectedSpeed = selectedSpeed;
            ActualSpeed = actualSpeed;
            SimulationFrameIndex = simulationFrameIndex;
            SimulationStepSeconds = simulationStepSeconds;
            PerformancePreference = performancePreference;
            PathfindPendingSimulationFrame = pathfindPendingSimulationFrame;
            PathfindPendingRequests = pathfindPendingRequests;
        }

        public double UnscaledDeltaSeconds { get; }

        /// <summary>True when <c>FrameTimingManager</c> returned a timing for this frame.</summary>
        public bool HasFrameTiming { get; }
        public double CpuMainThreadMs { get; }
        public double CpuRenderThreadMs { get; }
        public double GpuMs { get; }

        /// <summary>Main-thread time spent waiting for present (vsync or a GPU that is behind).</summary>
        public double PresentWaitMs { get; }

        public double SelectedSpeed { get; }

        /// <summary><c>SimulationSystem.smoothSpeed</c>: the smoothed speed the simulation actually ran at.</summary>
        public double ActualSpeed { get; }

        public uint SimulationFrameIndex { get; }

        /// <summary>
        /// <c>SimulationSystem.frameDuration</c>: wall time per simulation step of the previous rendered frame,
        /// measured until the step jobs completed; 0 when that frame ran no step.
        /// </summary>
        public double SimulationStepSeconds { get; }

        public string? PerformancePreference { get; }

        /// <summary>
        /// <c>PathfindResultSystem.pendingSimulationFrame</c>: the simulation frame the oldest pending path
        /// result is due by; <see cref="uint.MaxValue"/> when nothing is pending.
        /// </summary>
        public uint PathfindPendingSimulationFrame { get; }

        /// <summary><c>PathfindResultSystem.pendingRequestCount</c>, or a negative value when it was not read.</summary>
        public int PathfindPendingRequests { get; }
    }
}
