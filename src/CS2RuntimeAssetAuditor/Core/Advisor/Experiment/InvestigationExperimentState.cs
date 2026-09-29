namespace CS2RuntimeAssetAuditor.Core.Advisor.Experiment
{
    public enum InvestigationExperimentState
    {
        BaselineReady, AwaitingApplyConfirmation, AwaitingFollowUp,
        FollowUpCapturing, Completed, Cancelled, Invalidated
    }

    public enum InvestigationExperimentValidity { Valid, Invalidated }

    public enum InvestigationInvalidationReason
    {
        None, SessionChanged, AdditionalAdvisorSettingChanged,
        TestedSettingExternallyModified, TestedSettingNoLongerMatchesExpectedValue,
        RecommendationBecameStaleBeforeApply, FollowUpCaptureInvalid,
        FollowUpCaptureInterrupted, FollowUpCaptureWrongSession
    }

    public enum InvestigationCompletionOutcome { None, Kept, Undone, Cancelled }
}
