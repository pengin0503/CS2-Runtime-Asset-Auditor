namespace CS2RuntimeAssetAuditor.Core.Frames
{
    /// <summary>
    /// Frame, simulation and pathfinding values the mod measured itself over one interval, without profiler
    /// markers. Absent values are null or an empty <see cref="SampleSummary"/>, never 0.
    /// </summary>
    public sealed class RuntimeInterval
    {
        /// <summary>Sum of the frames' unscaled delta times.</summary>
        public double IntervalSeconds { get; set; }
        public int Frames { get; set; }
        public double? FramesPerSecond => IntervalSeconds > 0d && Frames > 0 ? Frames / IntervalSeconds : (double?)null;

        public SampleSummary FrameMs { get; set; }
        public int FrameTimingSamples { get; set; }
        public SampleSummary CpuMainThreadMs { get; set; }
        public SampleSummary CpuRenderThreadMs { get; set; }
        public SampleSummary GpuMs { get; set; }
        public SampleSummary PresentWaitMs { get; set; }

        /// <summary>Selected speed of the interval's last frame.</summary>
        public double? SelectedSpeed { get; set; }
        public double? ActualSpeedMean { get; set; }
        public double? EfficiencyMean { get; set; }
        public int RunningFrames { get; set; }
        public int PausedFrames { get; set; }

        public int? SimulationSteps { get; set; }
        public double? SimulationStepsPerSecond { get; set; }
        public int? SimulationMaxStepsPerFrame { get; set; }

        /// <summary>Running frames whose step count could be measured (the denominator of the two counts below).</summary>
        public int SimulationStepCountedFrames { get; set; }
        public int SimulationFramesAtRenderCap { get; set; }
        public int SimulationFramesWithoutStep { get; set; }
        public SampleSummary SimulationStepMs { get; set; }
        public string? PerformancePreference { get; set; }

        /// <summary>
        /// <see cref="SimulationStepLimits.FrameRateEfficiencyCeiling"/> for the interval's selected speed and
        /// frame rate; null while paused.
        /// </summary>
        public double? FrameRateEfficiencyCeiling { get; set; }

        public long? PathfindLeadFramesMin { get; set; }
        public int PathfindLowLeadFrames { get; set; }
        public int? PathfindPendingRequestsMax { get; set; }
    }
}
