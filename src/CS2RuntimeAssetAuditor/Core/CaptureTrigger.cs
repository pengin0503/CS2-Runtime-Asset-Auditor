namespace CS2RuntimeAssetAuditor.Core
{
    public enum CaptureTriggerKind
    {
        Manual,
        AutomaticLowEfficiency
    }

    public sealed class CaptureTrigger
    {
        public CaptureTrigger(CaptureTriggerKind kind, double timestampSeconds, double? efficiency)
        {
            Kind = kind;
            TimestampSeconds = timestampSeconds;
            Efficiency = efficiency;
        }

        public CaptureTriggerKind Kind { get; }
        public double TimestampSeconds { get; }
        public double? Efficiency { get; }
    }
}
