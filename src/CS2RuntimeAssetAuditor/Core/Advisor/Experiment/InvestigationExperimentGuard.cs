using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core.Advisor.Experiment
{
    public static class InvestigationExperimentGuard
    {
        public static InvestigationInvalidationReason? EvaluateAdvisorApply(
            InvestigationExperiment experiment, string changedSettingId, bool applySucceeded)
            => experiment?.IsActive == true && applySucceeded &&
               !string.Equals(experiment.SettingId, changedSettingId, StringComparison.Ordinal)
                ? InvestigationInvalidationReason.AdditionalAdvisorSettingChanged : (InvestigationInvalidationReason?)null;

        public static InvestigationInvalidationReason? EvaluateObservedSetting(
            InvestigationExperiment experiment, string observedValue)
            => experiment?.IsActive == true && experiment.ChangeAppliedAtUtc.HasValue &&
               !string.Equals(experiment.TestedValue, observedValue, StringComparison.Ordinal)
                ? InvestigationInvalidationReason.TestedSettingExternallyModified : (InvestigationInvalidationReason?)null;

        public static InvestigationInvalidationReason? EvaluateSession(
            InvestigationExperiment experiment, string currentSessionId)
            => experiment?.IsActive == true &&
               !string.Equals(experiment.SessionId, currentSessionId, StringComparison.Ordinal)
                ? InvestigationInvalidationReason.SessionChanged : (InvestigationInvalidationReason?)null;

        public static bool IsExpectedFollowUp(InvestigationExperiment experiment, string captureId,
            string captureSessionId, DateTimeOffset? captureStartedAtUtc)
            => experiment != null && experiment.Validity == InvestigationExperimentValidity.Valid &&
               experiment.State == InvestigationExperimentState.FollowUpCapturing &&
               !string.IsNullOrEmpty(experiment.FollowUpCaptureId) &&
               string.Equals(experiment.FollowUpCaptureId, captureId, StringComparison.Ordinal) &&
               string.Equals(experiment.SessionId, captureSessionId, StringComparison.Ordinal) &&
               experiment.ChangeAppliedAtUtc.HasValue && captureStartedAtUtc.HasValue &&
               captureStartedAtUtc.Value > experiment.ChangeAppliedAtUtc.Value;

        public static IReadOnlyList<SettingChange> SelectQualifyingChanges(
            InvestigationExperiment experiment, IReadOnlyList<SettingChange> changes)
        {
            if (experiment == null) return Array.Empty<SettingChange>();
            return (changes ?? Array.Empty<SettingChange>())
                .Where(change => change != null && change.AppliedAt >= experiment.StartedAtUtc.UtcDateTime &&
                    change.Status != SettingChangeStatus.Pending && change.Status != SettingChangeStatus.ApplyFailed)
                .ToArray();
        }
    }
}
