using CS2RuntimeProfiler.Core;

namespace CS2RuntimeProfiler.Export
{
    internal static class RuntimeCaptureConfigurationProvider
    {
        public static CaptureConfigurationSnapshot Capture()
        {
            var settings = Mod.Settings;
            if (settings == null)
                return null;

            return new CaptureConfigurationSnapshot
            {
                SamplingPeriodSeconds = settings.ResolvedSamplingPeriodSeconds,
                AutomaticCaptureEnabled = settings.EnableAutomaticCapture,
                EfficiencyThreshold = settings.ResolvedEfficiencyThreshold,
                LowEfficiencySustainSeconds = settings.ResolvedLowEfficiencySustainSeconds,
                PrebufferSeconds = settings.ResolvedPrebufferSeconds,
                DeepCaptureSeconds = settings.ResolvedDeepCaptureSeconds,
                PostbufferSeconds = settings.ResolvedPostbufferSeconds,
                CooldownSeconds = settings.ResolvedCooldownSeconds,
                MaxConcurrentMarkers = settings.ResolvedMaxConcurrentMarkers,
                ProfilerOverheadLimit = settings.ResolvedProfilerOverheadLimit,
                MaxCompletedCaptures = settings.ResolvedMaxCompletedCaptures
            };
        }
    }
}
