using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Core.Advisor.Experiment
{
    public sealed class InvestigationExperiment
    {
        internal InvestigationExperiment(string experimentId, string sessionId, string baselineCaptureId,
            AdvisorEvidenceSnapshot baselineEvidence, SettingRecommendation recommendation, DateTimeOffset startedAtUtc)
        {
            ExperimentId = experimentId;
            SessionId = sessionId;
            BaselineCaptureId = baselineCaptureId;
            BaselineEvidence = baselineEvidence;
            SettingId = recommendation.SettingId;
            SettingDisplayName = recommendation.DisplayName;
            OriginalValue = recommendation.CurrentValue;
            TestedValue = recommendation.RecommendedValue;
            ApplyBehavior = recommendation.ApplyBehavior;
            StartedAtUtc = startedAtUtc;
            State = InvestigationExperimentState.BaselineReady;
        }

        public string ExperimentId { get; }
        public string SessionId { get; }
        public DateTimeOffset StartedAtUtc { get; }
        public DateTimeOffset? CompletedAtUtc { get; internal set; }
        public string BaselineCaptureId { get; }
        public AdvisorEvidenceSnapshot BaselineEvidence { get; }
        public string SettingId { get; }
        public string SettingDisplayName { get; }
        public string OriginalValue { get; }
        public string TestedValue { get; }
        public SettingApplyBehavior ApplyBehavior { get; }
        public DateTimeOffset? ChangeAppliedAtUtc { get; internal set; }
        public DateTimeOffset? StabilizationReadyAtUtc { get; internal set; }
        public string FollowUpCaptureId { get; internal set; }
        public AdvisorEvidenceSnapshot FollowUpEvidence { get; internal set; }
        public IReadOnlyList<string> FollowUpWarnings { get; internal set; } = Array.Empty<string>();
        /// <summary>
        /// Why the follow-up capture ended early, when it did. The comparison is still shown, but it covers only
        /// what was measured before the interruption.
        /// </summary>
        public CaptureInterruptionReason? FollowUpInterruption { get; internal set; }
        public InvestigationExperimentState State { get; internal set; }
        public InvestigationExperimentValidity Validity { get; internal set; } = InvestigationExperimentValidity.Valid;
        public InvestigationInvalidationReason InvalidationReason { get; internal set; }
        public InvestigationCompletionOutcome CompletionOutcome { get; internal set; }
        public AdvisorComparison Comparison { get; internal set; }
        public string LastFailureReason { get; internal set; }

        public bool IsActive => State == InvestigationExperimentState.BaselineReady ||
            State == InvestigationExperimentState.AwaitingApplyConfirmation ||
            State == InvestigationExperimentState.AwaitingFollowUp ||
            State == InvestigationExperimentState.FollowUpCapturing ||
            State == InvestigationExperimentState.Completed && CompletionOutcome == InvestigationCompletionOutcome.None;
    }
}
