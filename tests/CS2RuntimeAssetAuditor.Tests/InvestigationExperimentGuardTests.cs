using System;
using System.Linq;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.Advisor;
using CS2RuntimeAssetAuditor.Core.Advisor.Experiment;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests
{
    [TestFixture]
    public sealed class InvestigationExperimentGuardTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 29, 0, 0, 0, TimeSpan.Zero);

        [Test]
        public void Successful_other_setting_apply_contaminates_but_failed_apply_does_not()
        {
            var experiment = Started().Current;
            Assert.That(InvestigationExperimentGuard.EvaluateAdvisorApply(experiment, "fog", false), Is.Null);
            Assert.That(InvestigationExperimentGuard.EvaluateAdvisorApply(experiment, "shadow", true), Is.Null);
            Assert.That(InvestigationExperimentGuard.EvaluateAdvisorApply(experiment, "fog", true),
                Is.EqualTo(InvestigationInvalidationReason.AdditionalAdvisorSettingChanged));
        }

        [Test]
        public void Only_a_changed_tested_value_after_apply_invalidates()
        {
            var coordinator = Started();
            Assert.That(InvestigationExperimentGuard.EvaluateObservedSetting(coordinator.Current, "other"), Is.Null);
            coordinator.RecordApplied(Now.AddSeconds(1));
            Assert.That(InvestigationExperimentGuard.EvaluateObservedSetting(coordinator.Current, "Low"), Is.Null);
            Assert.That(InvestigationExperimentGuard.EvaluateObservedSetting(coordinator.Current, "High"),
                Is.EqualTo(InvestigationInvalidationReason.TestedSettingExternallyModified));
        }

        [Test]
        public void City_session_identity_is_required()
        {
            var experiment = Started().Current;
            Assert.That(InvestigationExperimentGuard.EvaluateSession(experiment, "city"), Is.Null);
            Assert.That(InvestigationExperimentGuard.EvaluateSession(experiment, "another"),
                Is.EqualTo(InvestigationInvalidationReason.SessionChanged));
        }

        [Test]
        public void Only_requested_capture_after_apply_in_same_session_qualifies()
        {
            var coordinator = Started();
            coordinator.RecordApplied(Now.AddSeconds(1));
            coordinator.RecordFollowUpStarted("requested");
            var experiment = coordinator.Current;
            Assert.That(InvestigationExperimentGuard.IsExpectedFollowUp(experiment, "requested", "city", Now.AddSeconds(2)), Is.True);
            Assert.That(InvestigationExperimentGuard.IsExpectedFollowUp(experiment, "automatic", "city", Now.AddSeconds(2)), Is.False);
            Assert.That(InvestigationExperimentGuard.IsExpectedFollowUp(experiment, "requested", "another", Now.AddSeconds(2)), Is.False);
            Assert.That(InvestigationExperimentGuard.IsExpectedFollowUp(experiment, "requested", "city", Now), Is.False);
            Assert.That(InvestigationExperimentGuard.IsExpectedFollowUp(experiment, "requested", "city", null), Is.False);
        }

        [Test]
        public void Qualifying_changes_exclude_earlier_pending_and_failed_mutations()
        {
            var experiment = Started().Current;
            var session = new SettingChangeSession();
            session.RecordApplied("before", "High", "Low", Now.AddSeconds(-1).UtcDateTime);
            session.RecordPending("pending", "High", "Low", Now.UtcDateTime);
            session.RecordPending("failed", "High", "Low", Now.UtcDateTime);
            session.MarkApplyFailed("failed", "High");
            session.RecordApplied("shadow", "High", "Low", Now.AddSeconds(1).UtcDateTime);
            Assert.That(InvestigationExperimentGuard.SelectQualifyingChanges(experiment, session.Changes)
                .Select(change => change.SettingId), Is.EqualTo(new[] { "shadow" }));
        }

        private static InvestigationExperimentCoordinator Started()
        {
            var coordinator = new InvestigationExperimentCoordinator();
            coordinator.Start("experiment", "city", "baseline", new AdvisorEvidenceSnapshot(Now.UtcDateTime,
                    new[] { NamedMetricValue.Available("frame.p95.ms", 25, MetricConfidence.Full, "Milliseconds") }),
                new SettingRecommendation("shadow", "Shadow", "High", "Low", RecommendationDirection.LowerRecommended,
                    RecommendationPriority.High, AdvisorConfidence.High, "Test", Array.Empty<string>(),
                    SettingCapabilityState.Available, SettingApplyBehavior.Immediate), Now);
            return coordinator;
        }
    }
}
