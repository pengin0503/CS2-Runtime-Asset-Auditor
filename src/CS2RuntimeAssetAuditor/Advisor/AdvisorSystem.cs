using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Advisor.Settings;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.Advisor;
using CS2RuntimeAssetAuditor.Core.Advisor.Experiment;
using CS2RuntimeAssetAuditor.Profiling;
using Game;

namespace CS2RuntimeAssetAuditor.Advisor
{
    public partial class AdvisorSystem : GameSystemBase
    {
        private CaptureRuntimeSystem _capture;
        private AdvisorCoordinator _coordinator;
        private IGameSettingGateway _gateway;
        private readonly SettingChangeSession _changeSession = new SettingChangeSession();
        private AdvisorSettingOperations _operations;
        private AdvisorEvidenceSnapshot _baselineEvidence;
        private DateTime _baselineSelectedAt;
        private long _observedSessionGeneration = -1;
        private readonly InvestigationExperimentCoordinator _experiment = new InvestigationExperimentCoordinator();
        private DateTimeOffset _lastExperimentSettingReadUtc = DateTimeOffset.MinValue;

        // The gateway has no UI write binding until the change-session policy is installed.
        internal IGameSettingGateway Gateway => _gateway ?? (_gateway = AdvisorSettingCatalog.CreateGateway());

        public AdvisorState CurrentState
        {
            get
            {
                var state = _coordinator?.CurrentState;
                if (state != null)
                {
                    state.Changes = _changeSession.Changes;
                    state.LastAction = _operations?.LastAction;
                    state.Experiment = _experiment.Current;
                }
                return state;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            _capture = World.GetOrCreateSystemManaged<CaptureRuntimeSystem>();
            // Recommendations come from the same catalog the gateway writes through, so a setting without a verified
            // write adapter is shown read-only instead of offering an Apply that the gateway would refuse.
            _coordinator = new AdvisorCoordinator(() => Gateway.GetCatalog());
        }

        protected override void OnUpdate()
        {
            ObserveSessionChange();
            var current = CurrentExperiment;
            if (current?.IsActive != true) return;
            var sessionReason = InvestigationExperimentGuard.EvaluateSession(current, Mod.SessionContext?.SessionId);
            if (sessionReason.HasValue)
            {
                _experiment.Invalidate(sessionReason.Value);
                return;
            }
            ObserveTestedSettingIntegrity(false);
            ObserveExperimentFollowUp();
        }

        // Diagnoses and the baseline point at captures of the previous city, which are discarded on a session
        // change. Setting changes are global game options, so the change session (and its Undo) is kept.
        private void ObserveSessionChange()
        {
            var generation = Mod.Sessions.Generation;
            if (generation == _observedSessionGeneration)
                return;
            var firstObservation = _observedSessionGeneration < 0;
            _observedSessionGeneration = generation;
            if (firstObservation)
                return;
            _experiment.Invalidate(InvestigationInvalidationReason.SessionChanged);
            _coordinator?.Reset();
            _baselineEvidence = null;
        }

        public bool DiagnoseCompletedCapture(string id)
        {
            try
            {
                if (_coordinator?.DiagnoseCompletedCapture(_capture?.CompletedSessions, id) != true) return false;
                var state = CurrentState;
                if (_baselineEvidence != null && state?.Evidence != null &&
                    !string.Equals(state.BaselineCaptureId, id, StringComparison.Ordinal))
                {
                    var sinceBaseline = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(
                        _changeSession.Changes, change => change.AppliedAt >= _baselineSelectedAt));
                    state.Comparison = AdvisorComparison.Compare(_baselineEvidence, state.Evidence, sinceBaseline);
                }
                return true;
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Advisor diagnosis failed", ex);
                return false;
            }
        }

        public bool Rediagnose(string id) => DiagnoseCompletedCapture(id);

        public bool SelectBaseline(string id)
        {
            try
            {
                var capture = System.Linq.Enumerable.FirstOrDefault(_capture?.CompletedSessions ?? Array.Empty<CS2RuntimeAssetAuditor.Core.CaptureSession>(),
                    item => item != null && string.Equals(item.Id, id, StringComparison.Ordinal));
                if (capture == null || _coordinator?.SelectBaseline(_capture.CompletedSessions, id) != true) return false;
                _baselineEvidence = new CaptureAdvisorEvidenceProjector().Project(capture);
                _baselineSelectedAt = DateTime.UtcNow;
                if (CurrentState != null) CurrentState.Comparison = null;
                return true;
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Advisor baseline selection failed", ex);
                return false;
            }
        }

        public AdvisorActionResult LastAction => Operations.LastAction;

        private AdvisorSettingOperations Operations
            => _operations ?? (_operations = new AdvisorSettingOperations(() => Gateway, _changeSession));

        public SettingApplyResult ApplySetting(string settingId, string proposedValue, bool confirmed = false)
        {
            var result = Operations.Apply(CurrentState?.Recommendations, settingId, proposedValue, confirmed);
            var reason = InvestigationExperimentGuard.EvaluateAdvisorApply(CurrentExperiment, settingId, result.Succeeded);
            if (reason.HasValue) _experiment.Invalidate(reason.Value);
            else if (result.Succeeded && CurrentExperiment?.IsActive == true &&
                string.Equals(CurrentExperiment.SettingId, settingId, StringComparison.Ordinal))
                _experiment.Invalidate(InvestigationInvalidationReason.TestedSettingNoLongerMatchesExpectedValue);
            return result;
        }

        public SettingApplyResult UndoSetting(string settingId, bool confirmed = false)
        {
            var result = Operations.Undo(settingId, confirmed);
            if (result.Succeeded) ObserveTestedSettingIntegrity(true);
            return result;
        }

        public IReadOnlyList<SettingApplyResult> UndoSession(bool confirmed = false)
        {
            var results = Operations.UndoSession(confirmed);
            ObserveTestedSettingIntegrity(true);
            return results;
        }

        public SettingApplyResult ResolveConflict(string settingId, bool restoreOriginal)
        {
            var result = Operations.ResolveConflict(settingId, restoreOriginal);
            ObserveTestedSettingIntegrity(true);
            return result;
        }

        public InvestigationExperiment CurrentExperiment => _experiment.Current;

        public bool StartExperiment(string captureId, string settingId, string proposedValue)
        {
            try
            {
                ObserveSessionChange();
                if (!Mod.Sessions.IsActive || string.IsNullOrWhiteSpace(Mod.SessionContext?.SessionId) ||
                    CurrentExperiment?.IsActive == true) return false;
                var state = CurrentState;
                if (state?.IsAvailable != true || state.Evidence == null ||
                    !string.Equals(state.SelectedCaptureId, captureId, StringComparison.Ordinal)) return false;
                var baseline = (_capture?.CompletedSessions ?? Array.Empty<CaptureSession>())
                    .FirstOrDefault(item => item != null && item.Id == captureId && item.CompletedAtUtc.HasValue &&
                        item.SessionId == Mod.SessionContext.SessionId);
                var recommendation = state.Recommendations.FirstOrDefault(item => item != null &&
                    item.SettingId == settingId && item.RecommendedValue == proposedValue &&
                    item.ApplyCapability == SettingCapabilityState.Available &&
                    !string.Equals(item.CurrentValue, proposedValue, StringComparison.Ordinal));
                if (baseline == null || recommendation == null) return false;
                _experiment.Start(Guid.NewGuid().ToString("N"), Mod.SessionContext.SessionId, captureId,
                    state.Evidence, recommendation, DateTimeOffset.UtcNow);
                return true;
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Advisor experiment start failed", ex);
                return false;
            }
        }

        public SettingApplyResult ApplyExperimentChange(bool confirmed = false)
        {
            var current = CurrentExperiment;
            if (current == null || (current.State != InvestigationExperimentState.BaselineReady &&
                current.State != InvestigationExperimentState.AwaitingApplyConfirmation) ||
                InvestigationExperimentGuard.EvaluateSession(current, Mod.SessionContext?.SessionId).HasValue)
                return new SettingApplyResult { FailureReason = "ExperimentUnavailable" };
            // The existing operation re-reads the setting, checks recommendation freshness, and handles confirmation.
            var result = Operations.Apply(CurrentState?.Recommendations, current.SettingId, current.TestedValue, confirmed);
            if (result.Succeeded)
                _experiment.RecordApplied(DateTimeOffset.UtcNow);
            else if (result.FailureReason == AdvisorSettingOperations.ConfirmationRequired)
            {
                _experiment.AwaitApplyConfirmation();
                _experiment.RecordApplyFailure(result.FailureReason);
            }
            else
            {
                _experiment.RecordApplyFailure(result.FailureReason);
                if (result.FailureReason == "StaleOrUnavailableRecommendation")
                    _experiment.Invalidate(InvestigationInvalidationReason.RecommendationBecameStaleBeforeApply);
            }
            return result;
        }

        public bool StartExperimentFollowUpCapture()
        {
            var current = CurrentExperiment;
            if (current?.State != InvestigationExperimentState.AwaitingFollowUp ||
                InvestigationExperimentGuard.EvaluateSession(current, Mod.SessionContext?.SessionId).HasValue)
                return false;
            ObserveTestedSettingIntegrity(true);
            if (current.Validity != InvestigationExperimentValidity.Valid) return false;
            try
            {
                var capture = _capture?.RequestManualCapture();
                if (capture == null)
                {
                    current.LastFailureReason = "FollowUpRequestRejected";
                    return false;
                }
                if (capture.Trigger.Kind != CaptureTriggerKind.Manual || capture.SessionId != current.SessionId ||
                    !capture.StartedAtUtc.HasValue || capture.StartedAtUtc.Value <= current.ChangeAppliedAtUtc.Value)
                {
                    _experiment.Invalidate(InvestigationInvalidationReason.FollowUpCaptureWrongSession);
                    return false;
                }
                _experiment.RecordFollowUpStarted(capture.Id);
                return true;
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Advisor follow-up capture request failed", ex);
                return false;
            }
        }

        public void CancelExperiment()
        {
            if (CurrentExperiment?.IsActive == true) _experiment.Cancel(DateTimeOffset.UtcNow);
        }

        public bool KeepExperimentChange()
        {
            var current = CurrentExperiment;
            if (current?.State != InvestigationExperimentState.Completed ||
                current.CompletionOutcome != InvestigationCompletionOutcome.None) return false;
            ObserveTestedSettingIntegrity(true);
            if (current.Validity != InvestigationExperimentValidity.Valid) return false;
            _experiment.Complete(InvestigationCompletionOutcome.Kept, DateTimeOffset.UtcNow);
            return true;
        }

        public SettingApplyResult UndoExperimentChange(bool confirmed = false)
        {
            var current = CurrentExperiment;
            if (current?.ChangeAppliedAtUtc == null || current.CompletionOutcome != InvestigationCompletionOutcome.None ||
                (current.State != InvestigationExperimentState.Completed && current.State != InvestigationExperimentState.Invalidated))
                return new SettingApplyResult { FailureReason = "ExperimentUnavailable" };
            var result = Operations.Undo(current.SettingId, confirmed);
            if (result.Succeeded && current.State == InvestigationExperimentState.Completed)
                _experiment.Complete(InvestigationCompletionOutcome.Undone, DateTimeOffset.UtcNow);
            else if (!result.Succeeded)
                current.LastFailureReason = result.FailureReason;
            return result;
        }

        private void ObserveTestedSettingIntegrity(bool force)
        {
            var current = CurrentExperiment;
            if (current?.IsActive != true || !current.ChangeAppliedAtUtc.HasValue) return;
            var now = DateTimeOffset.UtcNow;
            if (!force && now - _lastExperimentSettingReadUtc < TimeSpan.FromMilliseconds(500)) return;
            _lastExperimentSettingReadUtc = now;
            try
            {
                var reason = InvestigationExperimentGuard.EvaluateObservedSetting(current, Gateway.Read(current.SettingId));
                if (reason.HasValue) _experiment.Invalidate(reason.Value);
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Advisor experiment setting observation failed", ex);
                _experiment.Invalidate(InvestigationInvalidationReason.TestedSettingNoLongerMatchesExpectedValue);
            }
        }

        private void ObserveExperimentFollowUp()
        {
            var current = CurrentExperiment;
            if (current?.State != InvestigationExperimentState.FollowUpCapturing) return;
            var capture = (_capture?.CompletedSessions ?? Array.Empty<CaptureSession>())
                .FirstOrDefault(item => item != null && item.Id == current.FollowUpCaptureId);
            if (capture == null) return; // Automatic and unrelated captures never match the stored request ID.
            if (!capture.CompletedAtUtc.HasValue)
            {
                _experiment.Invalidate(InvestigationInvalidationReason.FollowUpCaptureInvalid);
                return;
            }
            if (!InvestigationExperimentGuard.IsExpectedFollowUp(current, capture.Id, capture.SessionId, capture.StartedAtUtc))
            {
                _experiment.Invalidate(InvestigationInvalidationReason.FollowUpCaptureWrongSession);
                return;
            }
            ObserveTestedSettingIntegrity(true);
            if (current.Validity != InvestigationExperimentValidity.Valid) return;
            try
            {
                var evidence = new CaptureAdvisorEvidenceProjector().Project(capture);
                if (evidence == null)
                {
                    _experiment.Invalidate(InvestigationInvalidationReason.FollowUpCaptureInvalid);
                    return;
                }
                var changes = InvestigationExperimentGuard.SelectQualifyingChanges(current, _changeSession.Changes);
                _experiment.CompleteFollowUp(capture.Id, evidence, changes, DateTimeOffset.UtcNow);
                if (current.State == InvestigationExperimentState.Completed)
                    _experiment.RecordFollowUpWarnings(capture.Warnings);
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Advisor experiment comparison failed", ex);
                _experiment.Invalidate(InvestigationInvalidationReason.FollowUpCaptureInvalid);
            }
        }
    }
}
