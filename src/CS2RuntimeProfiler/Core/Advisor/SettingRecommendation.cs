using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeProfiler.Core.Advisor
{
    public enum RecommendationDirection { LowerRecommended, KeepCurrent, HeadroomAvailable, NoRecommendation }
    public enum RecommendationPriority { High, Medium, Low }

    public sealed class SettingRecommendation
    {
        public SettingRecommendation(string settingId, string displayName, string currentValue,
            string recommendedValue, RecommendationDirection direction, RecommendationPriority priority,
            AdvisorConfidence confidence, string rationale, IEnumerable<string> evidenceIds,
            SettingCapabilityState applyCapability, SettingApplyBehavior applyBehavior)
        {
            SettingId = settingId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            CurrentValue = currentValue ?? string.Empty;
            RecommendedValue = recommendedValue ?? string.Empty;
            Direction = direction;
            Priority = priority;
            Confidence = confidence;
            Rationale = rationale ?? string.Empty;
            EvidenceIds = (evidenceIds ?? Enumerable.Empty<string>()).ToArray();
            ApplyCapability = applyCapability;
            ApplyBehavior = applyBehavior;
        }

        public string SettingId { get; }
        public string DisplayName { get; }
        public string CurrentValue { get; }
        public string RecommendedValue { get; }
        public RecommendationDirection Direction { get; }
        public RecommendationPriority Priority { get; }
        public AdvisorConfidence Confidence { get; }
        public string Rationale { get; }
        public IReadOnlyList<string> EvidenceIds { get; }
        public SettingCapabilityState ApplyCapability { get; }
        public SettingApplyBehavior ApplyBehavior { get; }
    }
}
