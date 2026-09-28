namespace CS2RuntimeAssetAuditor.Core
{
    /// <summary>
    /// Effective runtime settings retained with an exported profiler report.
    /// Values are already resolved/clamped by the game-facing settings layer.
    /// </summary>
    public sealed class CaptureConfigurationSnapshot
    {
        public double SamplingPeriodSeconds { get; set; }
        public bool AutomaticCaptureEnabled { get; set; }
        public double EfficiencyThreshold { get; set; }
        public double LowEfficiencySustainSeconds { get; set; }
        public double PrebufferSeconds { get; set; }
        public double DeepCaptureSeconds { get; set; }
        public double PostbufferSeconds { get; set; }
        public double CooldownSeconds { get; set; }
        public int MaxConcurrentMarkers { get; set; }
        public double ProfilerOverheadLimit { get; set; }
        public int MaxCompletedCaptures { get; set; }

        public CaptureConfigurationSnapshot Clone()
        {
            return new CaptureConfigurationSnapshot
            {
                SamplingPeriodSeconds = SamplingPeriodSeconds,
                AutomaticCaptureEnabled = AutomaticCaptureEnabled,
                EfficiencyThreshold = EfficiencyThreshold,
                LowEfficiencySustainSeconds = LowEfficiencySustainSeconds,
                PrebufferSeconds = PrebufferSeconds,
                DeepCaptureSeconds = DeepCaptureSeconds,
                PostbufferSeconds = PostbufferSeconds,
                CooldownSeconds = CooldownSeconds,
                MaxConcurrentMarkers = MaxConcurrentMarkers,
                ProfilerOverheadLimit = ProfilerOverheadLimit,
                MaxCompletedCaptures = MaxCompletedCaptures
            };
        }
    }
}
