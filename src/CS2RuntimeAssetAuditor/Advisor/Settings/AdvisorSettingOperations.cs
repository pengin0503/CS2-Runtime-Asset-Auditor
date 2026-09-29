using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Core.Advisor;

namespace CS2RuntimeAssetAuditor.Advisor.Settings
{
    public enum AdvisorActionKind { Apply, Undo, UndoSession, ResolveConflict }

    /// <summary>Outcome of the latest user action, shown in the Advisor tab so no button press is silent.</summary>
    public sealed class AdvisorActionResult
    {
        public AdvisorActionKind Kind { get; set; }
        public string SettingId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool Succeeded { get; set; }
        /// <summary>Machine-readable reason, for example StaleOrUnavailableRecommendation; empty on success.</summary>
        public string FailureReason { get; set; } = string.Empty;
        public int SucceededCount { get; set; }
        public IReadOnlyList<string> FailedSettingIds { get; set; } = new string[0];
        public IReadOnlyList<string> ConfirmationRequiredSettingIds { get; set; } = new string[0];
        public DateTime AtUtc { get; set; }
    }

    /// <summary>
    /// Applies, undoes and reconciles standard-setting changes proposed by the Advisor. Every operation records
    /// its outcome in <see cref="LastAction"/>. A write that still needs user confirmation is not recorded as a
    /// failed change, and session undo can pass the user's confirmation to settings that require it.
    /// </summary>
    public sealed class AdvisorSettingOperations
    {
        public const string ConfirmationRequired = "ConfirmationRequired";
        private readonly Func<IGameSettingGateway> _gateway;
        private readonly SettingChangeSession _session;
        private readonly Func<DateTime> _clock;

        public AdvisorSettingOperations(Func<IGameSettingGateway> gateway, SettingChangeSession session, Func<DateTime> clock = null)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _clock = clock ?? (() => DateTime.UtcNow);
        }

        public AdvisorActionResult LastAction { get; private set; }

        public SettingApplyResult Apply(IReadOnlyList<SettingRecommendation> recommendations, string settingId, string proposedValue, bool confirmed)
        {
            var recommendation = (recommendations ?? Array.Empty<SettingRecommendation>())
                .FirstOrDefault(r => r != null && r.SettingId == settingId);
            var displayName = string.IsNullOrWhiteSpace(recommendation?.DisplayName) ? settingId : recommendation.DisplayName;
            SettingApplyResult result;
            try
            {
                var original = _gateway().Read(settingId);
                if (original == null)
                    result = new SettingApplyResult { Requested = proposedValue, FailureReason = "SettingReadUnavailable" };
                else if (!AdvisorApplyPolicy.IsCurrentRecommendation(recommendation, original, proposedValue))
                    result = new SettingApplyResult { Requested = proposedValue, ObservedBefore = original, FailureReason = "StaleOrUnavailableRecommendation" };
                else
                {
                    var at = _clock();
                    _session.RecordPending(settingId, original, proposedValue, at, displayName);
                    result = _gateway().Apply(settingId, proposedValue, confirmed);
                    if (result.Succeeded)
                        _session.RecordApplied(settingId, result.ObservedBefore, result.ObservedAfter, at);
                    else if (result.FailureReason == ConfirmationRequired)
                        _session.DiscardPending(settingId);
                    else
                        _session.MarkApplyFailed(settingId, result.ObservedAfter ?? result.ObservedBefore);
                }
            }
            catch (Exception ex)
            {
                _session.MarkApplyFailed(settingId, null);
                result = new SettingApplyResult { Requested = proposedValue, FailureReason = "AdvisorApplyFailure:" + ex.GetType().Name };
            }
            Record(AdvisorActionKind.Apply, settingId, displayName, result);
            return result;
        }

        public SettingApplyResult Undo(string settingId, bool confirmed)
        {
            var result = UndoCore(settingId, confirmed);
            Record(AdvisorActionKind.Undo, settingId, DisplayNameOf(settingId), result);
            return result;
        }

        public IReadOnlyList<SettingApplyResult> UndoSession(bool confirmed)
        {
            var planned = _session.PlanSessionUndo();
            var results = new List<SettingApplyResult>();
            var failed = new List<string>();
            var needsConfirmation = new List<string>();
            foreach (var change in planned)
            {
                var result = UndoCore(change.SettingId, confirmed);
                results.Add(result);
                if (result.Succeeded) continue;
                if (result.FailureReason == ConfirmationRequired) needsConfirmation.Add(change.SettingId);
                else failed.Add(change.SettingId);
            }

            LastAction = new AdvisorActionResult
            {
                Kind = AdvisorActionKind.UndoSession,
                Succeeded = failed.Count == 0 && needsConfirmation.Count == 0,
                FailureReason = needsConfirmation.Count > 0 ? ConfirmationRequired : failed.Count > 0 ? "SomeChangesNotRestored" : string.Empty,
                SucceededCount = results.Count(r => r.Succeeded),
                FailedSettingIds = failed.ToArray(),
                ConfirmationRequiredSettingIds = needsConfirmation.ToArray(),
                AtUtc = _clock()
            };
            return results;
        }

        public SettingApplyResult ResolveConflict(string settingId, bool restoreOriginal)
        {
            var change = _session.GetCurrentChange(settingId);
            SettingApplyResult result;
            if (change?.Status != SettingChangeStatus.ExternallyModified)
                result = new SettingApplyResult { FailureReason = "NoConflict" };
            else if (!restoreOriginal)
            {
                _session.KeepCurrent(settingId);
                result = new SettingApplyResult { Succeeded = true, ObservedAfter = change.CurrentObservedValue };
            }
            else
            {
                try
                {
                    result = _gateway().Restore(settingId, change.CurrentObservedValue, change.OriginalValue, confirmed: true);
                    if (result.Succeeded) _session.MarkUndone(settingId);
                    else if (result.FailureReason == "ExternallyModified")
                        _session.EvaluateUndo(settingId, _gateway().Read(settingId));
                }
                catch (Exception ex)
                {
                    result = new SettingApplyResult { FailureReason = "ConflictResolutionFailure:" + ex.GetType().Name };
                }
            }
            Record(AdvisorActionKind.ResolveConflict, settingId, DisplayNameOf(settingId), result);
            return result;
        }

        private SettingApplyResult UndoCore(string settingId, bool confirmed)
        {
            var change = _session.GetCurrentChange(settingId);
            if (change == null) return new SettingApplyResult { FailureReason = "NoUndoableChange" };
            try
            {
                var decision = _session.EvaluateUndo(settingId, _gateway().Read(settingId));
                if (decision == UndoDecision.AlreadyRestored)
                    return new SettingApplyResult { Succeeded = true, ObservedAfter = change.OriginalValue, Requested = change.OriginalValue };
                if (decision != UndoDecision.SafeRestore)
                    return new SettingApplyResult { ObservedAfter = change.CurrentObservedValue, Requested = change.OriginalValue, FailureReason = "ExternallyModified" };
                var result = _gateway().Restore(settingId, change.AppliedValue, change.OriginalValue, confirmed);
                if (result.Succeeded) _session.MarkUndone(settingId);
                else if (result.FailureReason == "ExternallyModified")
                    _session.EvaluateUndo(settingId, _gateway().Read(settingId));
                else if (result.FailureReason != ConfirmationRequired)
                    _session.MarkUndoFailed(settingId, result.ObservedAfter);
                return result;
            }
            catch (Exception ex)
            {
                _session.MarkUndoFailed(settingId, null);
                return new SettingApplyResult { FailureReason = "AdvisorUndoFailure:" + ex.GetType().Name };
            }
        }

        private string DisplayNameOf(string settingId)
        {
            var change = _session.Changes.LastOrDefault(c => c.SettingId == settingId);
            return change?.DisplayName ?? settingId ?? string.Empty;
        }

        private void Record(AdvisorActionKind kind, string settingId, string displayName, SettingApplyResult result)
        {
            LastAction = new AdvisorActionResult
            {
                Kind = kind,
                SettingId = settingId ?? string.Empty,
                DisplayName = displayName ?? string.Empty,
                Succeeded = result?.Succeeded == true,
                FailureReason = result?.Succeeded == true ? string.Empty : result?.FailureReason ?? "Unknown",
                SucceededCount = result?.Succeeded == true ? 1 : 0,
                ConfirmationRequiredSettingIds = result?.FailureReason == ConfirmationRequired ? new[] { settingId } : new string[0],
                FailedSettingIds = result?.Succeeded != true && result?.FailureReason != ConfirmationRequired ? new[] { settingId } : new string[0],
                AtUtc = _clock()
            };
        }
    }
}
