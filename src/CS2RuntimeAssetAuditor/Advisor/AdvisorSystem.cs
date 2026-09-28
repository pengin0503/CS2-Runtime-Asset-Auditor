using System;
using CS2RuntimeAssetAuditor.Advisor.Settings;
using CS2RuntimeAssetAuditor.Core.Advisor;
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
        private AdvisorEvidenceSnapshot _baselineEvidence;
        private DateTime _baselineSelectedAt;

        // The gateway has no UI write binding until the change-session policy is installed.
        internal IGameSettingGateway Gateway => _gateway ?? (_gateway = new GameSettingGateway());

        public AdvisorState CurrentState
        {
            get
            {
                var state = _coordinator?.CurrentState;
                if (state != null) state.Changes = _changeSession.Changes;
                return state;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            _capture = World.GetOrCreateSystemManaged<CaptureRuntimeSystem>();
            _coordinator = new AdvisorCoordinator(() => new GameSettingCatalogBuilder().GetCatalog());
        }

        protected override void OnUpdate() { }

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
            catch (Exception) { return false; }
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
            catch (Exception) { return false; }
        }

        public SettingApplyResult ApplySetting(string settingId, string proposedValue, bool confirmed = false)
        {
            var recommended = CurrentState?.Recommendations;
            var recommendation = System.Linq.Enumerable.FirstOrDefault(recommended ?? Array.Empty<SettingRecommendation>(),
                r => r.SettingId == settingId);
            try
            {
                var original = Gateway.Read(settingId);
                if (original == null) return new SettingApplyResult { Requested = proposedValue, FailureReason = "SettingReadUnavailable" };
                if (!AdvisorApplyPolicy.IsCurrentRecommendation(recommendation, original, proposedValue))
                    return new SettingApplyResult { Requested = proposedValue, ObservedBefore = original,
                        FailureReason = "StaleOrUnavailableRecommendation" };
                var at = DateTime.UtcNow;
                _changeSession.RecordPending(settingId, original, proposedValue, at);
                var result = Gateway.Apply(settingId, proposedValue, confirmed);
                if (result.Succeeded)
                    _changeSession.RecordApplied(settingId, result.ObservedBefore, result.ObservedAfter, at);
                else
                    _changeSession.MarkApplyFailed(settingId, result.ObservedAfter ?? result.ObservedBefore);
                return result;
            }
            catch (Exception ex)
            {
                _changeSession.MarkApplyFailed(settingId, null);
                return new SettingApplyResult { Requested = proposedValue, FailureReason = "AdvisorApplyFailure:" + ex.GetType().Name };
            }
        }

        public SettingApplyResult UndoSetting(string settingId, bool confirmed = false)
        {
            var change = _changeSession.GetCurrentChange(settingId);
            if (change == null) return new SettingApplyResult { FailureReason = "NoUndoableChange" };
            try
            {
                var decision = _changeSession.EvaluateUndo(settingId, Gateway.Read(settingId));
                if (decision == UndoDecision.AlreadyRestored) return new SettingApplyResult { Succeeded = true,
                    ObservedAfter = change.OriginalValue, Requested = change.OriginalValue };
                if (decision != UndoDecision.SafeRestore)
                    return new SettingApplyResult { ObservedAfter = change.CurrentObservedValue,
                        Requested = change.OriginalValue, FailureReason = "ExternallyModified" };
                var result = Gateway.Restore(settingId, change.AppliedValue, change.OriginalValue, confirmed);
                if (result.Succeeded) _changeSession.MarkUndone(settingId);
                else if (result.FailureReason == "ExternallyModified")
                    _changeSession.EvaluateUndo(settingId, Gateway.Read(settingId));
                else if (result.FailureReason != "ConfirmationRequired")
                    _changeSession.MarkUndoFailed(settingId, result.ObservedAfter);
                return result;
            }
            catch (Exception ex)
            {
                _changeSession.MarkUndoFailed(settingId, null);
                return new SettingApplyResult { FailureReason = "AdvisorUndoFailure:" + ex.GetType().Name };
            }
        }

        public void UndoSession()
        {
            foreach (var change in _changeSession.PlanSessionUndo()) UndoSetting(change.SettingId);
        }

        public SettingApplyResult ResolveConflict(string settingId, bool restoreOriginal)
        {
            var change = _changeSession.GetCurrentChange(settingId);
            if (change?.Status != SettingChangeStatus.ExternallyModified)
                return new SettingApplyResult { FailureReason = "NoConflict" };
            if (!restoreOriginal)
            {
                _changeSession.KeepCurrent(settingId);
                return new SettingApplyResult { Succeeded = true, ObservedAfter = change.CurrentObservedValue };
            }
            try
            {
                var result = Gateway.Restore(settingId, change.CurrentObservedValue, change.OriginalValue, confirmed: true);
                if (result.Succeeded) _changeSession.MarkUndone(settingId);
                else if (result.FailureReason == "ExternallyModified")
                    _changeSession.EvaluateUndo(settingId, Gateway.Read(settingId));
                return result;
            }
            catch (Exception ex)
            {
                return new SettingApplyResult { FailureReason = "ConflictResolutionFailure:" + ex.GetType().Name };
            }
        }
    }
}
