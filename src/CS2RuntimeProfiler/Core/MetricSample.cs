namespace CS2RuntimeProfiler.Core
{
    public readonly struct MetricSample
    {
        public MetricSample(double timestampSeconds, double value, MetricConfidence confidence, long? callCount = null)
        {
            TimestampSeconds = timestampSeconds;
            Value = value;
            Confidence = confidence;
            CallCount = callCount.HasValue && callCount.Value >= 0 ? callCount : null;
        }

        public double TimestampSeconds { get; }
        public double Value { get; }
        public MetricConfidence Confidence { get; }
        public long? CallCount { get; }
    }
}
