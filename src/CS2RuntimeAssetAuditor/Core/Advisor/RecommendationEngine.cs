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
            IReadOnlyList<GameSettingDescriptor> settings, AdvisorEvidenceSnapshot? evidence = null)
        {
            var observations = bottlenecks ?? Array.Empty<BottleneckObservation>();
            var measured = evidence ?? new AdvisorEvidenceSnapshot(DateTime.UtcNow, Array.Empty<NamedMetricValue>());
            return (settings ?? Array.Empty<GameSettingDescriptor>()).Where(s => s != null)
                .Select(s => QualityGroupRules.ForTags(s.SemanticTags) is QualityGroupRule group
                    ? RecommendGroup(s, group, observations, measured)
                    : Recommend(s, observations)).ToArray();
        }

        private const string NoChangeRationale = "The current evidence alone does not justify a performance-motivated setting change.";

        // A detailed quality group is a trial candidate: one step down, only for a bottleneck it can relieve, with at
        // most medium confidence. A group the Options screen locks, a custom level (no level restores it) and the
        // lowest level get no proposal; raising a group is never proposed.
        private static SettingRecommendation RecommendGroup(GameSettingDescriptor setting, QualityGroupRule rule,
            IReadOnlyList<BottleneckObservation> observations, AdvisorEvidenceSnapshot evidence)
        {
            var ordered = (setting.AllowedValues ?? Array.Empty<string>()).ToArray();
            var index = Array.IndexOf(ordered, setting.CurrentValue);
            var usable = setting.IsUserFacing && setting.IsReadable && setting.ValueKind == SettingValueKind.Enumeration
                && setting.IsCurrentlyEnabled != false && setting.IsCurrentlyVisible != false && index > 0;
            // The frame-time observation decides; a mixed or unclassified frame matches no group.
            var observation = observations.FirstOrDefault(o => rule.Categories.Contains(o.Category)
                && o.Severity != BottleneckSeverity.Low && o.Confidence >= AdvisorConfidence.Medium);
            var required = rule.RequiredEvidenceId.Length == 0 ? null : evidence.Find(rule.RequiredEvidenceId);
            var requirementMet = required == null || (required.Availability == MetricAvailability.Available
                && required.Value.HasValue && required.Value.Value > QualityGroupRules.NeutralValue(rule.RequiredEvidenceId));

            if (!usable || observation == null || !requirementMet)
                return new SettingRecommendation(setting.SettingId, setting.DisplayName, setting.CurrentValue, setting.CurrentValue,
                    RecommendationDirection.NoRecommendation, RecommendationPriority.Low, AdvisorConfidence.InsufficientEvidence,
                    NoChangeRationale, Array.Empty<string>(), setting.CapabilityState, setting.ApplyBehavior);

            var confidence = observation.Confidence < rule.MaximumConfidence ? observation.Confidence : rule.MaximumConfidence;
            var evidenceIds = observation.EvidenceIds.ToList();
            if (required != null) evidenceIds.Add(required.Id);
            foreach (var id in rule.SupportingEvidenceIds)
            {
                var supporting = evidence.Find(id);
                if (supporting.Availability == MetricAvailability.Available && !evidenceIds.Contains(id)) evidenceIds.Add(id);
            }
            return new SettingRecommendation(setting.SettingId, setting.DisplayName, setting.CurrentValue, ordered[index - 1],
                RecommendationDirection.LowerRecommended,
                confidence >= AdvisorConfidence.Medium ? RecommendationPriority.Medium : RecommendationPriority.Low,
                confidence, rule.Rationale, evidenceIds, setting.CapabilityState, setting.ApplyBehavior);
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
