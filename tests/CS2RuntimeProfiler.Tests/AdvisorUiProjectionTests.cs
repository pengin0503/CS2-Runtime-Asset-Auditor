using System;
using System.Collections.Generic;
using CS2RuntimeProfiler.Core.Advisor;
using CS2RuntimeProfiler.UI;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests
{
    [TestFixture]
    public sealed class AdvisorUiProjectionTests
    {
        [Test]
        public void Snapshot_exposes_current_proposed_confidence_and_apply_capability_without_mutation()
        {
            var recommendation = new SettingRecommendation("Game.Settings.GraphicsSettings::quality", "Quality",
                "High", "Medium", RecommendationDirection.LowerRecommended, RecommendationPriority.High,
                AdvisorConfidence.High, "measured", new[] { "gpu.frame.ms" },
                SettingCapabilityState.ReadOnlyForAdvisor, SettingApplyBehavior.ReadOnlyForAdvisor);
            var state = new AdvisorState(new AdvisorEvidenceSnapshot(DateTime.UtcNow, Array.Empty<CS2RuntimeProfiler.Core.NamedMetricValue>()),
                Array.Empty<BottleneckObservation>(), new[] { recommendation })
            { BaselineCaptureId = "baseline", SelectedCaptureId = "capture" };
            var input = new UiSnapshotInput { Advisor = state };

            var projection = UiSnapshotBuilder.Build(input);
            Assert.That(projection.Advisor.Recommendations[0].CurrentValue, Is.EqualTo("High"));
            Assert.That(projection.Advisor.Recommendations[0].RecommendedValue, Is.EqualTo("Medium"));
            Assert.That(projection.Advisor.Recommendations[0].Confidence, Is.EqualTo(AdvisorConfidence.High));
            Assert.That(projection.Advisor.Recommendations[0].ApplyCapability, Is.EqualTo(SettingCapabilityState.ReadOnlyForAdvisor));
            Assert.That(projection.Advisor.BaselineCaptureId, Is.EqualTo("baseline"));
        }

        [Test]
        public void Snapshot_retains_neutral_comparison_across_the_existing_profiler_projection()
        {
            var before = new AdvisorEvidenceSnapshot(DateTime.UtcNow, new[]
            { CS2RuntimeProfiler.Core.NamedMetricValue.Available("frame.p95.ms", 25,
                CS2RuntimeProfiler.Core.MetricConfidence.Full, "Milliseconds") });
            var after = new AdvisorEvidenceSnapshot(DateTime.UtcNow, new[]
            { CS2RuntimeProfiler.Core.NamedMetricValue.Available("frame.p95.ms", 20,
                CS2RuntimeProfiler.Core.MetricConfidence.Full, "Milliseconds") });
            var state = new AdvisorState(after, null, null)
            { Comparison = AdvisorComparison.Compare(before, after, Array.Empty<SettingChange>()) };
            var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput { Advisor = state });
            Assert.That(snapshot.Advisor.Comparison.Metrics[0].State, Is.EqualTo(ComparisonState.Improved));
        }
    }
}
