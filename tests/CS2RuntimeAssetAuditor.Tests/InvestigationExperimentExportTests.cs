using System;
using System.Linq;
using System.Text.Json;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.Advisor;
using CS2RuntimeAssetAuditor.Core.Advisor.Experiment;
using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests
{
    [TestFixture]
    public sealed class InvestigationExperimentExportTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 29, 0, 0, 0, TimeSpan.Zero);

        [Test]
        public void Completed_experiment_serializes_the_same_comparison_as_ordinary_Advisor()
        {
            var state = State();
            var coordinator = Started();
            coordinator.RecordApplied(Now.AddSeconds(1));
            coordinator.RecordFollowUpStarted("follow-up");
            var changes = new SettingChangeSession();
            changes.RecordApplied("graphics.shadow", "High", "Low", Now.AddSeconds(1).UtcDateTime);
            coordinator.CompleteFollowUp("follow-up", Evidence(20), changes.Changes, Now.AddSeconds(10));
            coordinator.Complete(InvestigationCompletionOutcome.Kept, Now.AddSeconds(11));
            state.Experiment = coordinator.Current;
            state.Comparison = coordinator.Current.Comparison;

            var report = ProfilerReportBuilder.Build(new UiSnapshot { Advisor = state });
            using var json = JsonDocument.Parse(RuntimeAssetAuditReportSerializer.Serialize(
                RuntimeAssetAuditReportBuilder.Build(report, null, null, Now)));
            var advisor = json.RootElement.GetProperty("advisor");
            var experiment = advisor.GetProperty("experiment");
            Assert.That(experiment.GetProperty("state").GetString(), Is.EqualTo("Completed"));
            Assert.That(experiment.GetProperty("completionOutcome").GetString(), Is.EqualTo("Kept"));
            Assert.That(experiment.GetProperty("baselineCaptureId").GetString(), Is.EqualTo("baseline"));
            Assert.That(experiment.GetProperty("followUpCaptureId").GetString(), Is.EqualTo("follow-up"));
            Assert.That(experiment.GetProperty("comparison").GetRawText(), Is.EqualTo(advisor.GetProperty("comparison").GetRawText()));
            Assert.That(experiment.GetProperty("comparison").GetProperty("metrics")[0].GetProperty("state").GetString(),
                Is.EqualTo("Improved"));
            Assert.That(json.RootElement.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
        }

        [Test]
        public void Invalidated_experiment_preserves_machine_reason_without_claiming_a_result()
        {
            var state = State();
            var coordinator = Started();
            coordinator.Invalidate(InvestigationInvalidationReason.SessionChanged);
            state.Experiment = coordinator.Current;
            using var json = JsonDocument.Parse(PerformanceReportSerializer.Serialize(
                ProfilerReportBuilder.Build(new UiSnapshot { Advisor = state })));
            var experiment = json.RootElement.GetProperty("advisor").GetProperty("experiment");
            Assert.That(experiment.GetProperty("validity").GetString(), Is.EqualTo("Invalidated"));
            Assert.That(experiment.GetProperty("invalidationReason").GetString(), Is.EqualTo("SessionChanged"));
            Assert.That(experiment.TryGetProperty("comparison", out var comparison) && comparison.ValueKind != JsonValueKind.Null, Is.False);
        }

        [Test]
        public void No_experiment_has_no_default_looking_observation()
        {
            using var json = JsonDocument.Parse(PerformanceReportSerializer.Serialize(
                ProfilerReportBuilder.Build(new UiSnapshot { Advisor = State() })));
            Assert.That(json.RootElement.GetProperty("advisor").TryGetProperty("experiment", out _), Is.False);
        }

        [Test]
        public void Experiment_strings_are_sanitized_and_no_game_objects_are_exported()
        {
            var state = State();
            var coordinator = Started(@"C:\Users\Alice\secret");
            state.Experiment = coordinator.Current;
            var report = ProfilerReportBuilder.Build(new UiSnapshot { Advisor = state });
            var serialized = RuntimeAssetAuditReportSerializer.Serialize(
                RuntimeAssetAuditReportBuilder.Build(report, null, null, Now));
            Assert.That(serialized, Does.Not.Contain("Alice"));
            Assert.That(serialized, Does.Contain("redacted-path"));
            Assert.That(serialized, Does.Not.Contain("BaselineEvidence"));
            Assert.That(serialized, Does.Not.Contain("IGameSettingGateway"));
            Assert.That(serialized, Does.Not.Contain("Game.Settings.InternalSettings"));
            Assert.That(report.Advisor.Experiment, Is.Not.Null);
            Assert.That(report.Advisor.Experiment.Comparison, Is.Null);
        }

        private static AdvisorState State() => new AdvisorState(Evidence(25), null, null)
        {
            SelectedCaptureId = "baseline",
            Catalog = new[] { new GameSettingDescriptor { SettingId = "graphics.shadow", DisplayName = "Shadow",
                IsUserFacing = true, IsReadable = true, IsWritable = true, CapabilityState = SettingCapabilityState.Available },
                new GameSettingDescriptor { SettingId = "Game.Settings.InternalSettings::secret", CurrentValue = "private-token",
                    IsUserFacing = false } }
        };

        private static InvestigationExperimentCoordinator Started(string displayName = "Shadow")
        {
            var coordinator = new InvestigationExperimentCoordinator();
            coordinator.Start("experiment", "city", "baseline", Evidence(25), new SettingRecommendation(
                "graphics.shadow", displayName, "High", "Low", RecommendationDirection.LowerRecommended,
                RecommendationPriority.High, AdvisorConfidence.High, "Measured", Array.Empty<string>(),
                SettingCapabilityState.Available, SettingApplyBehavior.Immediate), Now);
            return coordinator;
        }

        private static AdvisorEvidenceSnapshot Evidence(double value)
            => new AdvisorEvidenceSnapshot(Now.UtcDateTime,
                new[] { NamedMetricValue.Available("frame.p95.ms", value, MetricConfidence.Full, "Milliseconds") });
    }
}
