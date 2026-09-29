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

        [Test]
        public void Undo_of_preexisting_other_setting_contaminates_only_when_it_writes_a_new_value()
        {
            var coordinator = Started();
            var session = new SettingChangeSession();
            session.RecordApplied("fog", "High", "Low", Now.AddSeconds(-1).UtcDateTime);
            coordinator.RecordApplied(Now.AddSeconds(1));
            session.RecordApplied("shadow", "High", "Low", Now.AddSeconds(1).UtcDateTime);
            Assert.That(InvestigationExperimentGuard.SelectQualifyingChanges(coordinator.Current, session.Changes)
                .Select(change => change.SettingId), Is.EqualTo(new[] { "shadow" }));

            Assert.That(InvestigationExperimentGuard.EvaluateAdvisorMutation(coordinator.Current, "fog", false, "Low", "High"), Is.Null);
            Assert.That(InvestigationExperimentGuard.EvaluateAdvisorMutation(coordinator.Current, "fog", true, null, "High"), Is.Null);
            Assert.That(InvestigationExperimentGuard.EvaluateAdvisorMutation(coordinator.Current, "fog", true, "High", "High"), Is.Null);
            var reason = InvestigationExperimentGuard.EvaluateAdvisorMutation(coordinator.Current, "fog", true, "Low", "High");
            Assert.That(reason, Is.EqualTo(InvestigationInvalidationReason.AdditionalAdvisorSettingChanged));
            coordinator.Invalidate(reason.Value);
            Assert.That(coordinator.Current.State, Is.EqualTo(InvestigationExperimentState.Invalidated));
        }

        [Test]
        public void Follow_up_cut_short_by_a_city_change_is_never_compared()
        {
            Assert.That(InvestigationExperimentGuard.EvaluateFollowUpInterruption(null), Is.Null);
            Assert.That(InvestigationExperimentGuard.EvaluateFollowUpInterruption(CaptureInterruptionReason.SafetyLimit), Is.Null);
            Assert.That(InvestigationExperimentGuard.EvaluateFollowUpInterruption(CaptureInterruptionReason.MonitoringDisabled), Is.Null);
            Assert.That(InvestigationExperimentGuard.EvaluateFollowUpInterruption(CaptureInterruptionReason.SessionChanged),
                Is.EqualTo(InvestigationInvalidationReason.FollowUpCaptureInterrupted));
        }

        [Test]
        public void Unusable_follow_up_reports_interruption_when_the_capture_ended_early()
        {
            Assert.That(InvestigationExperimentGuard.EvaluateUnusableFollowUp(null),
                Is.EqualTo(InvestigationInvalidationReason.FollowUpCaptureInvalid));
            Assert.That(InvestigationExperimentGuard.EvaluateUnusableFollowUp(CaptureInterruptionReason.SafetyLimit),
                Is.EqualTo(InvestigationInvalidationReason.FollowUpCaptureInterrupted));
        }

        [Test]
        public void Advisor_passes_the_capture_interruption_into_the_experiment()
        {
            var directory = new System.IO.DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !System.IO.File.Exists(System.IO.Path.Combine(directory.FullName, "CS2RuntimeAssetAuditor.sln")))
                directory = directory.Parent;
            Assert.That(directory, Is.Not.Null);
            var source = System.IO.File.ReadAllText(System.IO.Path.Combine(directory!.FullName, "src/CS2RuntimeAssetAuditor/Advisor/AdvisorSystem.cs"));
            var start = source.IndexOf("private void ObserveExperimentFollowUp()", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            var method = source.Substring(start);
            Assert.That(method, Does.Contain("InvestigationExperimentGuard.EvaluateFollowUpInterruption(capture.InterruptionReason)"));
            Assert.That(method, Does.Contain("InvestigationExperimentGuard.EvaluateUnusableFollowUp(capture.InterruptionReason)"));
            Assert.That(method, Does.Contain("DateTimeOffset.UtcNow, capture.InterruptionReason)"));
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
