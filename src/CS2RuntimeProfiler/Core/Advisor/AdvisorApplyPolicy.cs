using System;

namespace CS2RuntimeProfiler.Core.Advisor
{
    public static class AdvisorApplyPolicy
    {
        public static bool IsCurrentRecommendation(SettingRecommendation recommendation,
            string observedCurrent, string requested)
            => recommendation != null && recommendation.ApplyCapability == SettingCapabilityState.Available
                && (recommendation.Direction == RecommendationDirection.LowerRecommended
                    || recommendation.Direction == RecommendationDirection.HeadroomAvailable)
                && string.Equals(recommendation.CurrentValue, observedCurrent, StringComparison.Ordinal)
                && string.Equals(recommendation.RecommendedValue, requested, StringComparison.Ordinal);
    }
}
