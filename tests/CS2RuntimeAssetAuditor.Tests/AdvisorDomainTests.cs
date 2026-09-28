using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.Advisor;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests
{
    [TestFixture]
    public sealed class AdvisorDomainTests
    {
        [Test]
        public void Unavailable_evidence_preserves_its_reason_and_has_no_numeric_value()
        {
            var snapshot = new AdvisorEvidenceSnapshot(DateTime.UnixEpoch, new[]
            {
                NamedMetricValue.Unavailable("gpu.frame.ms", "GPU timer unavailable")
            });

            Assert.That(snapshot.Metrics.Single().Availability, Is.EqualTo(MetricAvailability.Unavailable));
            Assert.That(snapshot.Metrics.Single().Value, Is.Null);
            Assert.That(snapshot.Metrics.Single().Reason, Is.EqualTo("GPU timer unavailable"));
        }

        [TestCase(RecommendationDirection.LowerRecommended)]
        [TestCase(RecommendationDirection.KeepCurrent)]
        [TestCase(RecommendationDirection.HeadroomAvailable)]
        [TestCase(RecommendationDirection.NoRecommendation)]
        public void Recommendation_retains_direction_capability_and_evidence(RecommendationDirection direction)
        {
            var recommendation = new SettingRecommendation("Game.Settings.GraphicsSettings::vSync", "vSync",
                "true", "false", direction, RecommendationPriority.Medium, AdvisorConfidence.Low,
                "Measure again after an explicit change", new[] { "frame.p95.ms" },
                SettingCapabilityState.ReadOnlyForAdvisor, SettingApplyBehavior.ReadOnlyForAdvisor);

            Assert.That(recommendation.Direction, Is.EqualTo(direction));
            Assert.That(recommendation.EvidenceIds, Is.EqualTo(new[] { "frame.p95.ms" }));
            Assert.That(recommendation.ApplyCapability, Is.EqualTo(SettingCapabilityState.ReadOnlyForAdvisor));
            Assert.That(recommendation.ApplyBehavior, Is.EqualTo(SettingApplyBehavior.ReadOnlyForAdvisor));
        }
    }
}
