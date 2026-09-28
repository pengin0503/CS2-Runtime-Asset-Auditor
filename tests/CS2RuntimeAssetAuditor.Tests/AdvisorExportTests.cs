using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.Advisor;
using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests
{
    [TestFixture]
    public sealed class AdvisorExportTests
    {
        [Test]
        public void Export_round_trips_diagnosis_recommendation_changes_and_neutral_comparison_without_private_values()
        {
            var baseline = new AdvisorEvidenceSnapshot(DateTime.UtcNow, new[]
            {
                NamedMetricValue.Unavailable("frame.p95.ms", "Frame marker absent"),
                NamedMetricValue.Available("gpu.frame.ms", 25, MetricConfidence.Full, "Milliseconds")
            });
            var followUp = new AdvisorEvidenceSnapshot(DateTime.UtcNow, new[]
            {
                NamedMetricValue.Unavailable("frame.p95.ms", "Frame marker absent"),
                NamedMetricValue.Available("gpu.frame.ms", 20, MetricConfidence.Full, "Milliseconds")
            });
            var session = new SettingChangeSession();
            session.RecordPending("Game.Settings.GraphicsSettings::depthOfFieldMode", "Physical", "Disabled", DateTime.UtcNow);
            session.RecordApplied("Game.Settings.GraphicsSettings::depthOfFieldMode", "Physical", "Disabled", DateTime.UtcNow);
            var state = new AdvisorState(baseline, new[]
            { new BottleneckObservation(BottleneckCategory.RenderingGpu, BottleneckSeverity.High,
                AdvisorConfidence.Medium, new[] { "gpu.frame.ms" }, "Direct GPU timer elevated") },
                new[] { new SettingRecommendation("Game.Settings.GraphicsSettings::depthOfFieldMode", "Depth of field",
                    "Physical", "Disabled", RecommendationDirection.LowerRecommended, RecommendationPriority.Medium,
                    AdvisorConfidence.Medium, "Try disabling the effect; re-diagnose.", new[] { "gpu.frame.ms" },
                    SettingCapabilityState.Available, SettingApplyBehavior.ApplyRequired) });
            state.SelectedCaptureId = "follow-up";
            state.BaselineCaptureId = "baseline";
            state.Changes = session.Changes;
            state.Comparison = AdvisorComparison.Compare(baseline, followUp, session.Changes);
            state.Catalog = new[]
            {
                new GameSettingDescriptor { SettingId = "Game.Settings.GraphicsSettings::depthOfFieldMode",
                    Category = "graphics", CurrentValue = "Disabled", IsUserFacing = true, IsReadable = true,
                    IsWritable = true, CapabilityState = SettingCapabilityState.Available },
                new GameSettingDescriptor { SettingId = "Game.Settings.InternalSettings::secret",
                    Category = "internal", CurrentValue = "private-token", IsUserFacing = false }
            };

            var report = ProfilerReportBuilder.Build(new UiSnapshot { Advisor = state });
            var json = PerformanceReportSerializer.Serialize(report);
            using var document = JsonDocument.Parse(json);
            var advisor = document.RootElement.GetProperty("advisor");
            Assert.That(advisor.GetProperty("selectedCaptureId").GetString(), Is.EqualTo("follow-up"));
            Assert.That(advisor.GetProperty("baselineCaptureId").GetString(), Is.EqualTo("baseline"));
            Assert.That(advisor.GetProperty("diagnosis")[0].GetProperty("evidenceIds")[0].GetString(), Is.EqualTo("gpu.frame.ms"));
            Assert.That(advisor.GetProperty("recommendations")[0].GetProperty("capability").GetString(), Is.EqualTo("Available"));
            Assert.That(advisor.GetProperty("changes")[0].GetProperty("originalValue").GetString(), Is.EqualTo("Physical"));
            Assert.That(advisor.GetProperty("changes")[0].GetProperty("appliedValue").GetString(), Is.EqualTo("Disabled"));
            Assert.That(advisor.GetProperty("comparison").GetProperty("metrics").EnumerateArray()
                .Single(x => x.GetProperty("id").GetString() == "gpu.frame.ms")
                .GetProperty("state").GetString(), Is.EqualTo("Improved"));
            Assert.That(advisor.GetProperty("evidence").EnumerateArray()
                .Single(x => x.GetProperty("name").GetString() == "frame.p95.ms")
                .GetProperty("availability").GetString(), Is.EqualTo("Unavailable"));
            Assert.That(json, Does.Not.Contain("private-token"));
            Assert.That(json, Does.Not.Contain("InternalSettings"));
            Assert.That(json, Does.Not.Contain("caused"));

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var restored = (PerformanceReport)new DataContractJsonSerializer(typeof(PerformanceReport)).ReadObject(stream)!;
            Assert.That(restored.Advisor!.Comparison!.Metrics.Single(x => x.Id == "gpu.frame.ms").State,
                Is.EqualTo("Improved"));
        }

        [Test]
        public void Unavailable_Advisor_is_reported_without_interrupting_other_export_sections()
        {
            var snapshot = new UiSnapshot { Advisor = new AdvisorState(null, null, null, "No completed capture") };
            var report = ProfilerReportBuilder.Build(snapshot);
            using var document = JsonDocument.Parse(PerformanceReportSerializer.Serialize(report));
            Assert.That(document.RootElement.GetProperty("advisor").GetProperty("unavailableReason").GetString(),
                Is.EqualTo("No completed capture"));
            Assert.That(document.RootElement.GetProperty("captures").GetArrayLength(), Is.EqualTo(0));
        }

        [Test]
        public void Missing_Advisor_does_not_claim_diagnostics_capability()
        {
            var report = ProfilerReportBuilder.Build(new UiSnapshot { Advisor = null! });
            Assert.That(report.Capabilities.Single(x => x.Name == "advisor").Value, Is.EqualTo("unavailable"));
            Assert.That(report.Captures, Is.Empty);
        }
    }
}
