namespace CS2RuntimeAssetAuditor.Core.Advisor
{
    public sealed class PerformanceSettingRule
    {
        public PerformanceSettingRule(string semanticTag, BottleneckCategory category,
            AdvisorConfidence minimumConfidence, bool canLower, bool canSuggestHeadroom)
        {
            SemanticTag = semanticTag;
            Category = category;
            MinimumConfidence = minimumConfidence;
            CanLower = canLower;
            CanSuggestHeadroom = canSuggestHeadroom;
        }

        public string SemanticTag { get; }
        public BottleneckCategory Category { get; }
        public AdvisorConfidence MinimumConfidence { get; }
        public bool CanLower { get; }
        public bool CanSuggestHeadroom { get; }
    }
}
