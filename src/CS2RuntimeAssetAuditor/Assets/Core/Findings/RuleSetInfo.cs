namespace CS2RuntimeAssetAuditor.Assets.Core.Findings
{
    public static class RuleSetInfo
    {
        public const string Version = "1.1";
        public const int MinimumPeerSampleSize = 5;
        // A peer outlier must exceed the population P95 and be at least this multiple of the median.
        public const double PeerOutlierMedianMultiplier = 2d;
        public const double WeakLodRetentionPercent = 85d;
        public const long HighExposureReferenceCount = 10000;
    }
}
