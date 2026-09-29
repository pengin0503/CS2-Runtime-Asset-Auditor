namespace CS2RuntimeAssetAuditor.Core
{
    /// <summary>
    /// Unit identifiers shared by C# snapshots and the UI formatter. Unity recorder units
    /// ("TimeNanoseconds", "Bytes", "Count") pass through unchanged; the remaining values
    /// describe values the profiler derives itself.
    /// </summary>
    public static class MetricUnits
    {
        public const string Bytes = "Bytes";
        public const string TimeNanoseconds = "TimeNanoseconds";
        public const string Milliseconds = "Milliseconds";
        public const string Ratio = "Ratio";
        public const string Speed = "Speed";
        public const string FramesPerSecond = "FramesPerSecond";
    }
}
