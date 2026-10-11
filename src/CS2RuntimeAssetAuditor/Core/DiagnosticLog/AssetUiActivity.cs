namespace CS2RuntimeAssetAuditor.Core.DiagnosticLog
{
    /// <summary>
    /// Counts the Asset panel's snapshot publishes and live scan-telemetry snapshots (each copies and sorts the slice
    /// samples) so the diagnostic log can show how often they happen while a scan runs and while idle (issue #49).
    /// Main thread only; the log takes and clears the counts once per row.
    /// </summary>
    public static class AssetUiActivity
    {
        private static int _publishes;
        private static int _telemetrySnapshots;

        public static void RecordPublish() => _publishes++;

        public static void RecordTelemetrySnapshot() => _telemetrySnapshots++;

        public static AssetUiActivityCounts Take()
        {
            var counts = new AssetUiActivityCounts(_publishes, _telemetrySnapshots);
            _publishes = 0;
            _telemetrySnapshots = 0;
            return counts;
        }
    }

    public readonly struct AssetUiActivityCounts
    {
        public AssetUiActivityCounts(int publishes, int telemetrySnapshots)
        {
            Publishes = publishes;
            TelemetrySnapshots = telemetrySnapshots;
        }

        public int Publishes { get; }
        public int TelemetrySnapshots { get; }
    }
}
