namespace CS2RuntimeAssetAuditor.Core.Frames
{
    /// <summary>
    /// Decides whether a simulation slowdown is explained by the frame rate: the frame-rate ceiling
    /// (<see cref="SimulationStepLimits.FrameRateEfficiencyCeiling"/>) is below full speed and either the measured
    /// efficiency reaches most of it or most running frames ran the per-frame maximum of steps. The advisor and
    /// automatic capture use the same rule.
    /// </summary>
    public static class FrameRateLimit
    {
        public const double FullSpeedCeiling = 0.95;
        public const double CeilingReachedRatio = 0.85;
        public const double RenderCapLimitedShare = 0.5;

        /// <summary>The ceiling is low enough to slow the simulation.</summary>
        public static bool LowersCeiling(double? ceiling) => ceiling.HasValue && ceiling.Value < FullSpeedCeiling;

        /// <summary>The efficiency is close to what the frame rate allows.</summary>
        public static bool CeilingExplains(double efficiency, double? ceiling)
            => LowersCeiling(ceiling) && efficiency >= CeilingReachedRatio * ceiling!.Value;

        /// <summary>
        /// Most frames ran the per-frame maximum of steps. Between 30 and 60 fps that happens without limiting the
        /// speed, so it counts only while the ceiling is below full speed.
        /// </summary>
        public static bool RenderCapExplains(double? ceiling, double? renderCapShare)
            => LowersCeiling(ceiling) && renderCapShare.HasValue && renderCapShare.Value >= RenderCapLimitedShare;

        public static bool Explains(double efficiency, double? ceiling, double? renderCapShare)
            => CeilingExplains(efficiency, ceiling) || RenderCapExplains(ceiling, renderCapShare);

        /// <summary>The share of running frames with a counted step delta that ran the per-frame maximum.</summary>
        public static double? RenderCapShare(RuntimeInterval? interval)
            => interval == null || interval.SimulationStepCountedFrames <= 0
                ? (double?)null
                : interval.SimulationFramesAtRenderCap / (double)interval.SimulationStepCountedFrames;
    }
}
