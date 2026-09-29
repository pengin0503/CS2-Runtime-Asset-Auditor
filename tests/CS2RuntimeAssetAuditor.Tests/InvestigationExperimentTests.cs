using System;
using System.Linq;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.Advisor;
using CS2RuntimeAssetAuditor.Core.Advisor.Experiment;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests
{
    [TestFixture]
    public sealed class InvestigationExperimentTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 29, 0, 0, 0, TimeSpan.Zero);

        [Test]
        public void Start_freezes_baseline_and_rejects_second_active_experiment()
        {
            var coordinator = new InvestigationExperimentCoordinator();
            var baseline = Evidence(25);
            var started = coordinator.Start("experiment", "city", "baseline", baseline, Recommendation(), Now);
            Assert.That(started.BaselineEvidence, Is.Not.SameAs(baseline));
            Assert.That(started.BaselineEvidence.Find("frame.p95.ms").Value, Is.EqualTo(25));
            Assert.That(started.State, Is.EqualTo(InvestigationExperimentState.BaselineReady));
            Assert.Throws<InvalidOperationException>(() => coordinator.Start("next", "city", "baseline", baseline, Recommendation(), Now));
        }

        [Test]
        public void Confirmation_required_does_not_advance_until_success()
        {
            var coordinator = Started();
            coordinator.AwaitApplyConfirmation();
            Assert.That(coordinator.Current.State, Is.EqualTo(InvestigationExperimentState.AwaitingApplyConfirmation));
            coordinator.RecordApplyFailure("ConfirmationRequired");
            Assert.That(coordinator.Current.State, Is.EqualTo(InvestigationExperimentState.AwaitingApplyConfirmation));
            Assert.That(coordinator.Current.ChangeAppliedAtUtc, Is.Null);
            coordinator.RecordApplied(Now.AddSeconds(2));
            Assert.That(coordinator.Current.State, Is.EqualTo(InvestigationExperimentState.AwaitingFollowUp));
        }

        [Test]
        public void Applied_change_sets_stabilization_ready_exactly_five_seconds_later()
        {
            var coordinator = Started();
            coordinator.RecordApplied(Now);
            Assert.That(coordinator.Current.StabilizationReadyAtUtc, Is.EqualTo(Now.AddSeconds(5)));
        }

        [Test]
        public void Follow_up_completion_reuses_AdvisorComparison_states()
        {
            var coordinator = Started();
            coordinator.RecordApplied(Now);
            coordinator.RecordFollowUpStarted("follow-up");
            var changes = Changes("shadow");
            coordinator.CompleteFollowUp("follow-up", Evidence(20), changes.Changes, Now.AddSeconds(10));
            Assert.That(coordinator.Current.Comparison.Metrics.Single().State, Is.EqualTo(ComparisonState.Improved));
            Assert.That(coordinator.Current.State, Is.EqualTo(InvestigationExperimentState.Completed));
            Assert.That(coordinator.Current.CompletionOutcome, Is.EqualTo(InvestigationCompletionOutcome.None));
        }

        [Test]
        public void Interrupted_follow_up_is_compared_with_its_interruption_shown()
        {
            var coordinator = Started();
            coordinator.RecordApplied(Now);
            coordinator.RecordFollowUpStarted("follow-up");
            coordinator.CompleteFollowUp("follow-up", Evidence(20), Changes("shadow").Changes, Now.AddSeconds(10),
                CaptureInterruptionReason.SafetyLimit);
            Assert.That(coordinator.Current.State, Is.EqualTo(InvestigationExperimentState.Completed));
            Assert.That(coordinator.Current.Validity, Is.EqualTo(InvestigationExperimentValidity.Valid));
            Assert.That(coordinator.Current.Comparison.Metrics.Single().State, Is.EqualTo(ComparisonState.Improved));
            Assert.That(coordinator.Current.FollowUpInterruption, Is.EqualTo(CaptureInterruptionReason.SafetyLimit));
        }

        [Test]
        public void Follow_up_that_ran_its_full_course_has_no_interruption()
        {
            var coordinator = Started();
            coordinator.RecordApplied(Now);
            coordinator.RecordFollowUpStarted("follow-up");
            coordinator.CompleteFollowUp("follow-up", Evidence(20), Changes("shadow").Changes, Now.AddSeconds(10));
            Assert.That(coordinator.Current.FollowUpInterruption, Is.Null);
        }

        [Test]
        public void Multiple_qualifying_changes_invalidate_single_setting_experiment()
        {
            var coordinator = Started();
            coordinator.RecordApplied(Now);
            coordinator.RecordFollowUpStarted("follow-up");
            var changes = Changes("shadow", "fog");
            coordinator.CompleteFollowUp("follow-up", Evidence(20), changes.Changes, Now.AddSeconds(10));
            Assert.That(coordinator.Current.State, Is.EqualTo(InvestigationExperimentState.Invalidated));
            Assert.That(coordinator.Current.InvalidationReason, Is.EqualTo(InvestigationInvalidationReason.AdditionalAdvisorSettingChanged));
            Assert.That(coordinator.Current.Comparison, Is.Null);
        }

        [Test]
        public void Cancel_before_apply_has_no_setting_outcome()
        {
            var coordinator = Started();
            coordinator.Cancel(Now);
            Assert.That(coordinator.Current.State, Is.EqualTo(InvestigationExperimentState.Cancelled));
            Assert.That(coordinator.Current.ChangeAppliedAtUtc, Is.Null);
            Assert.That(coordinator.Current.CompletionOutcome, Is.EqualTo(InvestigationCompletionOutcome.Cancelled));
        }

        [Test]
        public void Cancel_after_apply_records_cancelled_without_claiming_undo()
        {
            var coordinator = Started();
            coordinator.RecordApplied(Now);
            coordinator.Cancel(Now.AddSeconds(1));
            Assert.That(coordinator.Current.CompletionOutcome, Is.EqualTo(InvestigationCompletionOutcome.Cancelled));
            Assert.That(coordinator.Current.ChangeAppliedAtUtc, Is.Not.Null);
            Assert.That(coordinator.Current.OriginalValue, Is.EqualTo("High"));
            Assert.That(coordinator.Current.TestedValue, Is.EqualTo("Low"));
        }

        [Test]
        public void Invalidated_experiment_rejects_progress()
        {
            var coordinator = Started();
            coordinator.Invalidate(InvestigationInvalidationReason.SessionChanged);
            Assert.That(coordinator.Current.Validity, Is.EqualTo(InvestigationExperimentValidity.Invalidated));
            Assert.Throws<InvalidOperationException>(() => coordinator.RecordApplied(Now));
            Assert.Throws<InvalidOperationException>(() => coordinator.RecordFollowUpStarted("follow-up"));
        }

        [Test]
        public void Baseline_evidence_survives_source_capture_eviction()
        {
            var coordinator = Started();
            GC.Collect();
            Assert.That(coordinator.Current.BaselineEvidence.Find("frame.p95.ms").Value, Is.EqualTo(25));
        }

        [Test]
        public void Follow_up_warnings_are_copied_into_the_observed_result()
        {
            var coordinator = Started();
            coordinator.RecordApplied(Now);
            coordinator.RecordFollowUpStarted("follow-up");
            coordinator.CompleteFollowUp("follow-up", Evidence(20), Changes("shadow").Changes, Now.AddSeconds(10));
            var warnings = new[] { "Capture finalized early after a safety stop." };
            coordinator.RecordFollowUpWarnings(warnings);
            warnings[0] = "changed";
            Assert.That(coordinator.Current.FollowUpWarnings, Is.EqualTo(new[] { "Capture finalized early after a safety stop." }));
        }

        [Test]
        public void Completed_experiment_can_be_cleared_when_its_city_session_ends()
        {
            var coordinator = Started();
            coordinator.RecordApplied(Now);
            coordinator.RecordFollowUpStarted("follow-up");
            coordinator.CompleteFollowUp("follow-up", Evidence(20), Changes("shadow").Changes, Now.AddSeconds(10));
            coordinator.Complete(InvestigationCompletionOutcome.Kept, Now.AddSeconds(11));

            coordinator.Clear();

            Assert.That(coordinator.Current, Is.Null);
        }

        private static InvestigationExperimentCoordinator Started()
        {
            var coordinator = new InvestigationExperimentCoordinator();
            coordinator.Start("experiment", "city", "baseline", Evidence(25), Recommendation(), Now);
            return coordinator;
        }

        private static AdvisorEvidenceSnapshot Evidence(double value)
            => new AdvisorEvidenceSnapshot(Now.UtcDateTime,
                new[] { NamedMetricValue.Available("frame.p95.ms", value, MetricConfidence.Full, "Milliseconds") });

        private static SettingRecommendation Recommendation()
            => new SettingRecommendation("shadow", "Shadow", "High", "Low", RecommendationDirection.LowerRecommended,
                RecommendationPriority.High, AdvisorConfidence.High, "Test", Array.Empty<string>(),
                SettingCapabilityState.Available, SettingApplyBehavior.Immediate);

        private static SettingChangeSession Changes(params string[] ids)
        {
            var changes = new SettingChangeSession();
            foreach (var id in ids) changes.RecordApplied(id, "High", "Low", Now.UtcDateTime);
            return changes;
        }
    }
}
