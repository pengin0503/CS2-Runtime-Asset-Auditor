using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core.Advisor.Experiment
{
    public sealed class InvestigationExperimentCoordinator
    {
        public InvestigationExperiment Current { get; private set; }

        public InvestigationExperiment Start(string experimentId, string sessionId, string baselineCaptureId,
            AdvisorEvidenceSnapshot baselineEvidence, SettingRecommendation recommendation, DateTimeOffset startedAtUtc)
        {
            if (Current?.IsActive == true) throw new InvalidOperationException("An experiment is active.");
            if (string.IsNullOrWhiteSpace(experimentId) || string.IsNullOrWhiteSpace(sessionId) ||
                string.IsNullOrWhiteSpace(baselineCaptureId) || baselineEvidence == null || recommendation == null ||
                recommendation.ApplyCapability != SettingCapabilityState.Available ||
                string.Equals(recommendation.CurrentValue, recommendation.RecommendedValue, StringComparison.Ordinal))
                throw new ArgumentException("A valid session, baseline and applicable change are required.");
            var frozen = Copy(baselineEvidence);
            Current = new InvestigationExperiment(experimentId, sessionId, baselineCaptureId,
                frozen, recommendation, startedAtUtc);
            return Current;
        }

        public void AwaitApplyConfirmation()
        {
            Require(InvestigationExperimentState.BaselineReady, InvestigationExperimentState.AwaitingApplyConfirmation);
            Current.State = InvestigationExperimentState.AwaitingApplyConfirmation;
        }

        public void RecordApplied(DateTimeOffset appliedAtUtc)
        {
            Require(InvestigationExperimentState.BaselineReady, InvestigationExperimentState.AwaitingApplyConfirmation);
            Current.ChangeAppliedAtUtc = appliedAtUtc;
            Current.StabilizationReadyAtUtc = appliedAtUtc.AddSeconds(5);
            Current.LastFailureReason = null;
            Current.State = InvestigationExperimentState.AwaitingFollowUp;
        }

        public void RecordApplyFailure(string machineReason)
        {
            Require(InvestigationExperimentState.BaselineReady, InvestigationExperimentState.AwaitingApplyConfirmation);
            Current.LastFailureReason = machineReason ?? string.Empty;
        }

        public void RecordFollowUpStarted(string captureId)
        {
            Require(InvestigationExperimentState.AwaitingFollowUp);
            if (string.IsNullOrWhiteSpace(captureId)) throw new ArgumentException("Capture identity is required.", nameof(captureId));
            Current.FollowUpCaptureId = captureId;
            Current.State = InvestigationExperimentState.FollowUpCapturing;
            Current.LastFailureReason = null;
        }

        public void CompleteFollowUp(string captureId, AdvisorEvidenceSnapshot followUpEvidence,
            IReadOnlyList<SettingChange> qualifyingChanges, DateTimeOffset completedAtUtc)
        {
            Require(InvestigationExperimentState.FollowUpCapturing);
            if (!string.Equals(captureId, Current.FollowUpCaptureId, StringComparison.Ordinal) || followUpEvidence == null)
            {
                Invalidate(InvestigationInvalidationReason.FollowUpCaptureInvalid);
                return;
            }
            var effective = (qualifyingChanges ?? Array.Empty<SettingChange>())
                .Where(c => c != null && c.Status != SettingChangeStatus.Pending && c.Status != SettingChangeStatus.ApplyFailed)
                .ToArray();
            if (effective.Length != 1 || !string.Equals(effective[0].SettingId, Current.SettingId, StringComparison.Ordinal))
            {
                Invalidate(InvestigationInvalidationReason.AdditionalAdvisorSettingChanged);
                return;
            }
            var comparison = AdvisorComparison.Compare(Current.BaselineEvidence, followUpEvidence, effective);
            if (comparison.MultipleChanges)
            {
                Invalidate(InvestigationInvalidationReason.AdditionalAdvisorSettingChanged);
                return;
            }
            Current.FollowUpEvidence = Copy(followUpEvidence);
            Current.Comparison = comparison;
            Current.CompletedAtUtc = completedAtUtc;
            Current.State = InvestigationExperimentState.Completed;
        }

        public void Invalidate(InvestigationInvalidationReason reason)
        {
            if (Current == null || !Current.IsActive) return;
            Current.Validity = InvestigationExperimentValidity.Invalidated;
            Current.InvalidationReason = reason;
            Current.State = InvestigationExperimentState.Invalidated;
        }

        public void RecordFollowUpWarnings(IReadOnlyList<string> warnings)
        {
            if (Current == null || Current.State != InvestigationExperimentState.Completed)
                throw new InvalidOperationException("A completed follow-up is required.");
            Current.FollowUpWarnings = (warnings ?? Array.Empty<string>()).ToArray();
        }

        public void Cancel(DateTimeOffset completedAtUtc)
        {
            if (Current == null || !Current.IsActive) throw new InvalidOperationException("No active experiment.");
            Current.State = InvestigationExperimentState.Cancelled;
            Current.CompletionOutcome = InvestigationCompletionOutcome.Cancelled;
            Current.CompletedAtUtc = completedAtUtc;
        }

        public void Complete(InvestigationCompletionOutcome outcome, DateTimeOffset completedAtUtc)
        {
            if (Current == null || Current.State != InvestigationExperimentState.Completed ||
                Current.CompletionOutcome != InvestigationCompletionOutcome.None ||
                (outcome != InvestigationCompletionOutcome.Kept && outcome != InvestigationCompletionOutcome.Undone))
                throw new InvalidOperationException("A comparison and a Keep or Undo decision are required.");
            Current.CompletionOutcome = outcome;
            Current.CompletedAtUtc = completedAtUtc;
        }

        private void Require(params InvestigationExperimentState[] states)
        {
            if (Current == null || Current.Validity != InvestigationExperimentValidity.Valid ||
                !states.Contains(Current.State))
                throw new InvalidOperationException("The experiment cannot advance in its current state.");
        }

        private static AdvisorEvidenceSnapshot Copy(AdvisorEvidenceSnapshot evidence)
            => new AdvisorEvidenceSnapshot(evidence.TimestampUtc, evidence.Metrics.ToArray());
    }
}
