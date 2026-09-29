using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Core.Frames;

namespace CS2RuntimeAssetAuditor.Core.DiagnosticLog
{
    /// <summary>A normal-monitoring profiler recorder written as one diagnostic-log column.</summary>
    public sealed class DiagnosticRecorderColumn
    {
        public DiagnosticRecorderColumn(string id, string name, string unit)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Unit = unit ?? string.Empty;
        }

        public string Id { get; }
        public string Name { get; }
        public string Unit { get; }
    }

    /// <summary>Per-row values that do not come from the per-frame game reads.</summary>
    public readonly struct DiagnosticIntervalContext
    {
        public DiagnosticIntervalContext(
            string? captureState,
            string? captureTrigger,
            string? captureId,
            long? managedHeapBytes,
            int? gcCollectionCount,
            int? autoSaveStarts = null)
        {
            AutoSaveStarts = autoSaveStarts;
            CaptureState = captureState;
            CaptureTrigger = captureTrigger;
            CaptureId = captureId;
            ManagedHeapBytes = managedHeapBytes;
            GcCollectionCount = gcCollectionCount;
        }

        public string? CaptureState { get; }
        public string? CaptureTrigger { get; }
        public string? CaptureId { get; }
        public long? ManagedHeapBytes { get; }

        /// <summary>Cumulative collection count since process start; the row stores the change.</summary>
        public int? GcCollectionCount { get; }

        /// <summary>Game autosaves that started in the interval; null when the game's autosave state is unreadable.</summary>
        public int? AutoSaveStarts { get; }
    }

    /// <summary>One diagnostic-log row (normally one second). Absent values are written as empty cells.</summary>
    public sealed class DiagnosticLogRow
    {
        public DateTimeOffset UtcTime { get; set; }
        public double ElapsedSeconds { get; set; }
        public RuntimeInterval Interval { get; set; } = new RuntimeInterval();

        public int? GcCollections { get; set; }
        public double? ManagedHeapMiB { get; set; }
        public int? AutoSaveStarts { get; set; }

        public string? CaptureState { get; set; }
        public string? CaptureTrigger { get; set; }
        public string? CaptureId { get; set; }

        /// <summary>
        /// Update time of the mod's own systems per frame. The slowest system is the one that took longest in
        /// the frame where the mod spent the most time.
        /// </summary>
        public SampleSummary ModUpdateMs { get; set; }
        public string? ModUpdateSlowestSystem { get; set; }
        public double? ModUpdateSlowestSystemMs { get; set; }

        /// <summary>Last value each recorder column reported in the interval, aligned with the header.</summary>
        public IReadOnlyList<double?> RecorderValues { get; set; } = Array.Empty<double?>();
    }
}
