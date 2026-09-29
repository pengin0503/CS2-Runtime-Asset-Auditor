using System;
using System.Linq;
using CS2RuntimeAssetAuditor.Core.Advisor;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests
{
    [TestFixture]
    public sealed class RecommendationEngineTests
    {
        [Test]
        public void High_confidence_gpu_pressure_lowers_one_verified_quality_step()
        {
            var result = Build(Observe(BottleneckCategory.RenderingGpu, BottleneckSeverity.High,
                AdvisorConfidence.High), Graphics("High"));
            Assert.That(result.Direction, Is.EqualTo(RecommendationDirection.LowerRecommended));
            Assert.That(result.RecommendedValue, Is.EqualTo("Medium"));
            Assert.That(result.Priority, Is.EqualTo(RecommendationPriority.High));
            Assert.That(result.Confidence, Is.EqualTo(AdvisorConfidence.High));
            Assert.That(result.EvidenceIds, Is.EqualTo(new[] { "gpu.frame.ms" }));
        }

        [Test]
        public void Simulation_only_pressure_does_not_recommend_lowering_gpu_quality()
        {
            var result = Build(Observe(BottleneckCategory.SimulationCpu, BottleneckSeverity.High,
                AdvisorConfidence.High), Graphics("High"));
            Assert.That(result.Direction, Is.EqualTo(RecommendationDirection.NoRecommendation));
        }

        [Test]
        public void Low_gpu_pressure_reports_headroom_and_recommends_remeasurement()
        {
            var result = Build(Observe(BottleneckCategory.RenderingGpu, BottleneckSeverity.Low,
                AdvisorConfidence.Medium), Graphics("Medium"));
            Assert.That(result.Direction, Is.EqualTo(RecommendationDirection.HeadroomAvailable));
            Assert.That(result.RecommendedValue, Is.EqualTo("High"));
            Assert.That(result.Rationale, Does.Contain("measure again"));
        }

        [Test]
        public void Indirect_low_confidence_evidence_suppresses_quality_changes()
        {
            var result = Build(Observe(BottleneckCategory.RenderingGpu, BottleneckSeverity.High,
                AdvisorConfidence.Low), Graphics("High"));
            Assert.That(result.Direction, Is.EqualTo(RecommendationDirection.NoRecommendation));
            Assert.That(result.Confidence, Is.EqualTo(AdvisorConfidence.InsufficientEvidence));
        }

        [Test]
        public void Volume_and_keybinding_remain_catalogued_without_performance_recommendations()
        {
            var other = Graphics("High");
            other.Category = "audio";
            other.SettingId = "Game.Settings.AudioSettings::masterVolume";
            other.SemanticTags = new string[0];
            var binding = Graphics("High");
            binding.Category = "keybinding";
            binding.ValueKind = SettingValueKind.Keybinding;
            binding.SemanticTags = new string[0];
            var results = new RecommendationEngine().Build(new[]
            { Observe(BottleneckCategory.RenderingGpu, BottleneckSeverity.High, AdvisorConfidence.High) },
                new[] { other, binding });
            Assert.That(results.Select(r => r.Direction), Is.EqualTo(new[]
            { RecommendationDirection.NoRecommendation, RecommendationDirection.NoRecommendation }));
        }

        [TestCase("Physical", BottleneckSeverity.High, RecommendationDirection.LowerRecommended, "Disabled")]
        [TestCase("TiltShift", BottleneckSeverity.High, RecommendationDirection.LowerRecommended, "Disabled")]
        [TestCase("Disabled", BottleneckSeverity.Low, RecommendationDirection.HeadroomAvailable, "Physical")]
        public void Verified_depth_of_field_modes_are_mapped_by_explicit_effect_not_enum_order(
            string current, BottleneckSeverity severity, RecommendationDirection expectedDirection, string expectedValue)
        {
            var setting = Graphics(current);
            setting.SettingId = "Game.Settings.GraphicsSettings::depthOfFieldMode";
            setting.AllowedValues = new[] { "Disabled", "Physical", "TiltShift" };
            setting.SemanticTags = new[] { "rendering.depth-of-field-mode" };

            var result = Build(Observe(BottleneckCategory.RenderingGpu, severity, AdvisorConfidence.Medium), setting);

            Assert.That(result.Direction, Is.EqualTo(expectedDirection));
            Assert.That(result.RecommendedValue, Is.EqualTo(expectedValue));
            Assert.That(result.EvidenceIds, Is.EqualTo(new[] { "gpu.frame.ms" }));
        }

        private static GameSettingDescriptor Graphics(string value) => new GameSettingDescriptor
        {
            SettingId = "Game.Settings.GraphicsSettings::quality",
            Category = "graphics",
            DisplayName = "Quality",
            ValueKind = SettingValueKind.Enumeration,
            CurrentValue = value,
            AllowedValues = new[] { "Low", "Medium", "High" },
            SemanticTags = new[] { "rendering.ordered-quality" },
            IsReadable = true,
            IsUserFacing = true,
            IsWritable = true,
            CapabilityState = SettingCapabilityState.Available,
            ApplyBehavior = SettingApplyBehavior.ApplyRequired
        };

        private static BottleneckObservation Observe(BottleneckCategory category,
            BottleneckSeverity severity, AdvisorConfidence confidence)
            => new BottleneckObservation(category, severity, confidence, new[] { "gpu.frame.ms" }, "measured");

        private static SettingRecommendation Build(BottleneckObservation observation, GameSettingDescriptor setting)
            => new RecommendationEngine().Build(new[] { observation }, new[] { setting }).Single();
    }
}
