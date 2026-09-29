namespace CS2RuntimeAssetAuditor.Core.DiagnosticLog
{
    /// <summary>
    /// Detects the game's autosave from <c>AutoSaveSystem.m_LastAutoSaveCheck</c> (game 1.6.2f1): the field is -1
    /// while autosave is inactive, is set to the current time when autosave becomes active (city loaded, setting
    /// turned on), and moves forward each time an autosave starts. So only a rise from a non-negative value is an
    /// autosave. The real-play logs of 2026-09-29 showed a 1 to 1.5 s frame at every two-minute autosave.
    /// </summary>
    public sealed class AutoSaveTriggerCounter
    {
        private float? _last;

        /// <returns>True when this reading shows a new autosave.</returns>
        public bool Observe(float? lastAutoSaveCheck)
        {
            var previous = _last;
            _last = lastAutoSaveCheck;
            return previous.HasValue && lastAutoSaveCheck.HasValue
                && previous.Value >= 0f && lastAutoSaveCheck.Value > previous.Value;
        }

        public void Reset() => _last = null;
    }
}
