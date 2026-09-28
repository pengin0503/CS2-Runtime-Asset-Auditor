namespace CS2RuntimeProfiler.Core
{
    public sealed class NamedMetricValue
    {
        private NamedMetricValue(string id, double? value, MetricAvailability availability, MetricConfidence confidence, string reason, string unitType = "")
        {
            UnitType = unitType ?? string.Empty;
            Id = id ?? string.Empty;
            Value = value;
            Availability = availability;
            Confidence = confidence;
            Reason = reason;
        }

        public string Id { get; }
        public double? Value { get; }
        public MetricAvailability Availability { get; }
        public MetricConfidence Confidence { get; }
        public string Reason { get; }

        /// <summary>Unit identifier understood by the UI formatter (for example "Bytes"); empty for plain counts.</summary>
        public string UnitType { get; }

        public static NamedMetricValue Available(string id, double value, MetricConfidence confidence, string unitType = "")
            => new NamedMetricValue(id, value, MetricAvailability.Available, confidence, null, unitType);

        public static NamedMetricValue Unavailable(string id, string reason)
            => new NamedMetricValue(id, null, MetricAvailability.Unavailable, MetricConfidence.Unavailable, reason);
    }
}
