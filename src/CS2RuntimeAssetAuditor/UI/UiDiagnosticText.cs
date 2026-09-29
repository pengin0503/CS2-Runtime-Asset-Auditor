namespace CS2RuntimeAssetAuditor.UI
{
    /// <summary>
    /// Canonical (English) diagnostic sentences sent to the panel and written to exports. The UI translates
    /// them for the active interface language, so they must stay exact.
    /// </summary>
    public static class UiDiagnosticText
    {
        public const string SystemTimingPending = "Per-system timing becomes available once a matching Deep Capture completes.";
        public const string TimelineRetainedOnly = "The timeline shows only the history retained by the selected capture; unavailable series are never synthesized.";
        public const string PatchMapUnavailable = "Unavailable: no system timing snapshot.";
        public const string PatchMapDetected = "Patch information was detected in the current system timing snapshot.";
        public const string PatchMapNone = "No patch owners were detected in the current system timing snapshot.";
    }
}
