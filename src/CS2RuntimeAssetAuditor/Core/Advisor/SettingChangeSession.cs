using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core.Advisor
{
    public enum SettingChangeStatus
    {
        Pending, Applied, Undone, ExternallyModified, KeptExternalValue, ApplyFailed, UndoFailed, RestartPending
    }

    public enum UndoDecision { SafeRestore, AlreadyRestored, Conflict, NotUndoable }

    public sealed class SettingChange
    {
        internal SettingChange(string settingId, string original, string applied, DateTime timestamp)
        {
            SettingId = settingId;
            DisplayName = settingId;
            OriginalValue = original;
            AppliedValue = applied;
            CurrentObservedValue = original;
            AppliedAt = timestamp;
            Status = SettingChangeStatus.Pending;
        }

        public string SettingId { get; }
        // Human-readable name from the recommendation that produced the change; falls back to the ID.
        public string DisplayName { get; internal set; }
        public string OriginalValue { get; internal set; }
        public string AppliedValue { get; }
        public string CurrentObservedValue { get; internal set; }
        public DateTime AppliedAt { get; }
        public SettingChangeStatus Status { get; internal set; }
    }

    public sealed class SettingChangeSession
    {
        private readonly List<SettingChange> _changes = new List<SettingChange>();
        public IReadOnlyList<SettingChange> Changes => _changes.ToArray();

        public void RecordPending(string settingId, string originalValue, string requestedValue, DateTime appliedAt, string displayName = null)
        {
            if (string.IsNullOrWhiteSpace(settingId)) throw new ArgumentException("A setting ID is required.", nameof(settingId));
            var change = new SettingChange(settingId, originalValue, requestedValue, appliedAt);
            if (!string.IsNullOrWhiteSpace(displayName)) change.DisplayName = displayName;
            _changes.Add(change);
        }

        /// <summary>Removes a pending record whose write never started (for example, it still needs confirmation).</summary>
        public void DiscardPending(string settingId)
        {
            var pending = _changes.LastOrDefault(c => c.SettingId == settingId && c.Status == SettingChangeStatus.Pending);
            if (pending != null) _changes.Remove(pending);
        }

        public void RecordApplied(string settingId, string originalValue, string appliedValue, DateTime appliedAt)
        {
            var pending = _changes.LastOrDefault(c => c.SettingId == settingId &&
                c.AppliedValue == appliedValue && c.Status == SettingChangeStatus.Pending);
            var change = pending ?? new SettingChange(settingId, originalValue, appliedValue, appliedAt);
            if (pending == null) _changes.Add(change);
            change.OriginalValue = originalValue;
            change.CurrentObservedValue = appliedValue;
            change.Status = SettingChangeStatus.Applied;
        }

        public void MarkApplyFailed(string settingId, string observed)
        {
            var pending = _changes.LastOrDefault(c => c.SettingId == settingId && c.Status == SettingChangeStatus.Pending);
            if (pending == null) return;
            pending.CurrentObservedValue = observed;
            pending.Status = SettingChangeStatus.ApplyFailed;
        }

        public UndoDecision EvaluateUndo(string settingId, string currentValue)
        {
            var change = _changes.LastOrDefault(c => c.SettingId == settingId &&
                (c.Status == SettingChangeStatus.Applied || c.Status == SettingChangeStatus.ExternallyModified));
            if (change == null) return UndoDecision.NotUndoable;
            change.CurrentObservedValue = currentValue;
            if (string.Equals(currentValue, change.OriginalValue, StringComparison.Ordinal))
            {
                change.Status = SettingChangeStatus.Undone;
                return UndoDecision.AlreadyRestored;
            }
            if (change.Status == SettingChangeStatus.Applied &&
                string.Equals(currentValue, change.AppliedValue, StringComparison.Ordinal)) return UndoDecision.SafeRestore;
            change.Status = SettingChangeStatus.ExternallyModified;
            return UndoDecision.Conflict;
        }

        public IReadOnlyList<SettingChange> PlanSessionUndo()
            => _changes.Where(c => c.Status == SettingChangeStatus.Applied).Reverse().ToArray();

        public void MarkUndone(string settingId)
        {
            var change = LastUndoable(settingId);
            if (change == null) return;
            change.CurrentObservedValue = change.OriginalValue;
            change.Status = SettingChangeStatus.Undone;
        }

        public void MarkUndoFailed(string settingId, string currentValue)
        {
            var change = LastUndoable(settingId);
            if (change == null) return;
            change.CurrentObservedValue = currentValue;
            change.Status = SettingChangeStatus.UndoFailed;
        }

        public void KeepCurrent(string settingId)
        {
            var change = _changes.LastOrDefault(c => c.SettingId == settingId && c.Status == SettingChangeStatus.ExternallyModified);
            if (change != null) change.Status = SettingChangeStatus.KeptExternalValue;
        }

        public SettingChange GetCurrentChange(string settingId) => LastUndoable(settingId);

        private SettingChange LastUndoable(string settingId)
            => _changes.LastOrDefault(c => c.SettingId == settingId &&
                (c.Status == SettingChangeStatus.Applied || c.Status == SettingChangeStatus.ExternallyModified));
    }
}
