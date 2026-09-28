namespace CS2RuntimeProfiler.Core
{
    public sealed class CapabilityInfo
    {
        public CapabilityInfo(string id, MetricAvailability availability, MetricConfidence confidence, string reason = null)
        {
            Id = id ?? string.Empty;
            Availability = availability;
            Confidence = confidence;
            Reason = reason;
        }

        public string Id { get; }
        public MetricAvailability Availability { get; }
        public MetricConfidence Confidence { get; }
        public string Reason { get; }
    }
}
