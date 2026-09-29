using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core.Advisor
{
    public sealed class RecommendationEngine
    {
        private readonly IReadOnlyList<PerformanceSettingRule> _rules;

        public RecommendationEngine()
            : this(new[] { new PerformanceSettingRule("rendering.ordered-quality",
                BottleneckCategory.RenderingGpu, AdvisorConfidence.Medium, true, true),
                new PerformanceSettingRule("rendering.depth-of-field-mode",
                    BottleneckCategory.RenderingGpu, AdvisorConfidence.Medium, true, true) }) { }

        public RecommendationEngine(IReadOnlyList<PerformanceSettingRule> rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public IReadOnlyList<SettingRecommendation> Build(IReadOnlyList<BottleneckObservation> bottlenecks,
            IReadOnlyList<GameSettingDescriptor> settings)
        {
            var observations = bottlenecks ?? Array.Empty<BottleneckObservation>();
            return (settings ?? Array.Empty<GameSettingDescriptor>()).Where(s => s != null)
                .Select(s => Recommend(s, observations)).ToArray();
        }

        private SettingRecommendation Recommend(GameSettingDescriptor setting,
            IReadOnlyList<BottleneckObservation> observations)
        {
            var ordered = setting.AllowedValues ?? Array.Empty<string>();
            var index = Array.IndexOf(ordered.ToArray(), setting.CurrentValue);
            var rule = _rules.FirstOrDefault(r => (setting.SemanticTags ?? Array.Empty<string>())
                .Contains(r.SemanticTag, StringComparer.Ordinal));
            var depthOfField = rule?.SemanticTag == "rendering.depth-of-field-mode"
                && setting.SettingId == "Game.Settings.GraphicsSettings::depthOfFieldMode"
                && ordered.SequenceEqual(new[] { "Disabled", "Physical", "TiltShift" }, StringComparer.Ordinal);
            var observation = rule == null ? null : observations.FirstOrDefault(o => o.Category == rule.Category);
            var sufficient = observation != null && observation.Confidence >= rule.MinimumConfidence;
            var canRecommend = setting.IsUserFacing && setting.IsReadable
                && setting.ValueKind == SettingValueKind.Enumeration && index >= 0 && sufficient;
            var lower = canRecommend && rule.CanLower && observation.Severity != BottleneckSeverity.Low && index > 0;
            var headroom = canRecommend && rule.CanSuggestHeadroom && observation.Severity == BottleneckSeverity.Low
                && (depthOfField ? index == 0 : index + 1 < ordered.Count)
                && !observations.Any(o => o.Severity == BottleneckSeverity.High);
            var direction = lower ? RecommendationDirection.LowerRecommended
                : headroom ? RecommendationDirection.HeadroomAvailable : RecommendationDirection.NoRecommendation;
            var next = lower ? depthOfField ? "Disabled" : ordered[index - 1]
                : headroom ? ordered[index + 1] : setting.CurrentValue;
            var confidence = direction == RecommendationDirection.NoRecommendation
                ? AdvisorConfidence.InsufficientEvidence : observation.Confidence;
            var priority = lower && observation.Confidence == AdvisorConfidence.High
                ? RecommendationPriority.High : direction == RecommendationDirection.NoRecommendation
                    ? RecommendationPriority.Low : RecommendationPriority.Medium;
            var rationale = lower && depthOfField
                ? "Based on the measured GPU load, it is worth disabling depth of field and measuring again."
                : lower ? "Based on the measured rendering load, it is worth lowering the quality by one step and measuring again."
                : headroom ? "Rendering has headroom under the current measurement conditions. If you raise it, measure again after the change."
                : "The current evidence alone does not justify a performance-motivated setting change.";
            return new SettingRecommendation(setting.SettingId, setting.DisplayName, setting.CurrentValue,
                next, direction, priority, confidence, rationale,
                direction == RecommendationDirection.NoRecommendation ? Array.Empty<string>() : observation.EvidenceIds,
                setting.CapabilityState, setting.ApplyBehavior);
        }
    }
}
