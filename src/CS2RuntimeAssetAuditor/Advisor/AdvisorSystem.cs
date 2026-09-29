using System;
using System.Collections.Generic;
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
        private AdvisorSettingOperations _operations;
        private AdvisorEvidenceSnapshot _baselineEvidence;
        private DateTime _baselineSelectedAt;
        private long _observedSessionGeneration = -1;

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

        protected override void OnUpdate() => ObserveSessionChange();

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
            => Operations.Apply(CurrentState?.Recommendations, settingId, proposedValue, confirmed);

        public SettingApplyResult UndoSetting(string settingId, bool confirmed = false)
            => Operations.Undo(settingId, confirmed);

        public IReadOnlyList<SettingApplyResult> UndoSession(bool confirmed = false)
            => Operations.UndoSession(confirmed);

        public SettingApplyResult ResolveConflict(string settingId, bool restoreOriginal)
            => Operations.ResolveConflict(settingId, restoreOriginal);
    }
}
