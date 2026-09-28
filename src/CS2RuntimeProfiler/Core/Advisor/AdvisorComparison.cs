using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeProfiler.Core.Advisor
{
    public enum ComparisonState { Improved, Regressed, NoMaterialChange, NotComparable }

    public sealed class AdvisorMetricComparison
    {
        public string Id { get; set; }
        public double? BaselineValue { get; set; }
        public double? FollowUpValue { get; set; }
        public ComparisonState State { get; set; }
        public string Reason { get; set; }
    }

    public sealed class AdvisorComparison
    {
        private const double MaterialRelativeDifference = 0.05;
        private const double TimeToleranceMilliseconds = 1;
        private const double EfficiencyTolerance = 0.03;
        private const double OverheadTolerance = 0.02;

        private AdvisorComparison(IReadOnlyList<AdvisorMetricComparison> metrics,
            IReadOnlyList<string> changedSettings, int changeCount)
        {
            Metrics = metrics;
            ChangedSettingIds = changedSettings;
            MultipleChanges = changeCount > 1;
        }

        public IReadOnlyList<AdvisorMetricComparison> Metrics { get; }
        public IReadOnlyList<string> ChangedSettingIds { get; }
        public bool MultipleChanges { get; }

        public static AdvisorComparison Compare(AdvisorEvidenceSnapshot baseline, AdvisorEvidenceSnapshot followUp,
            IReadOnlyList<SettingChange> changes)
        {
            if (baseline == null || followUp == null) throw new ArgumentNullException(
                baseline == null ? nameof(baseline) : nameof(followUp));
            var ids = baseline.Metrics.Select(metric => metric.Id)
                .Concat(followUp.Metrics.Select(metric => metric.Id)).Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal);
            var compared = ids.Select(id => CompareMetric(id, baseline.Find(id), followUp.Find(id))).ToArray();
            var applied = (changes ?? Array.Empty<SettingChange>())
                .Where(c => c != null && c.Status != SettingChangeStatus.ApplyFailed && c.Status != SettingChangeStatus.Pending)
                .ToArray();
            return new AdvisorComparison(compared,
                applied.Select(c => c.SettingId).Distinct(StringComparer.Ordinal).ToArray(), applied.Length);
        }

        private static AdvisorMetricComparison CompareMetric(string id, NamedMetricValue baseline, NamedMetricValue followUp)
        {
            var metric = new AdvisorMetricComparison
            {
                Id = id, BaselineValue = baseline.Value, FollowUpValue = followUp.Value,
                State = ComparisonState.NotComparable
            };
            if (baseline.Availability != MetricAvailability.Available ||
                followUp.Availability != MetricAvailability.Available ||
                !baseline.Value.HasValue || !followUp.Value.HasValue)
            {
                metric.Reason = "One or both captures lack this measurement.";
                return metric;
            }
            if (baseline.Confidence != followUp.Confidence ||
                baseline.Confidence == MetricConfidence.Unavailable ||
                baseline.Confidence == MetricConfidence.Indirect ||
                !string.Equals(baseline.UnitType, followUp.UnitType, StringComparison.Ordinal))
            {
                metric.Reason = "Measurement definition or capability changed.";
                return metric;
            }
            var higherBetter = id == "simulation.efficiency" || id == "fps";
            var lowerBetter = id == "frame.p95.ms" || id == "gpu.frame.ms" ||
                id.StartsWith("system.", StringComparison.Ordinal) && id.EndsWith(".ms", StringComparison.Ordinal) ||
                id == "profiler.overhead.share" || id == "profiler.memory.delta.bytes";
            if (!higherBetter && !lowerBetter)
            {
                metric.Reason = "A beneficial direction has not been established for this metric.";
                return metric;
            }
            var before = baseline.Value.Value;
            var after = followUp.Value.Value;
            if (double.IsNaN(before) || double.IsInfinity(before) || double.IsNaN(after) || double.IsInfinity(after))
            {
                metric.Reason = "Non-finite measurement.";
                return metric;
            }
            var minimum = id == "simulation.efficiency" ? EfficiencyTolerance
                : id == "profiler.overhead.share" ? OverheadTolerance
                : id == "profiler.memory.delta.bytes" ? 64d * 1024d * 1024d : TimeToleranceMilliseconds;
            var threshold = Math.Max(minimum, Math.Abs(before) * MaterialRelativeDifference);
            var difference = (after - before) * (higherBetter ? 1 : -1);
            metric.State = Math.Abs(difference) < threshold ? ComparisonState.NoMaterialChange
                : difference > 0 ? ComparisonState.Improved : ComparisonState.Regressed;
            return metric;
        }
    }
}
