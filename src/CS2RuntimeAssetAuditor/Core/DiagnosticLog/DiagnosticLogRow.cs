using System;
using System.Collections.Generic;

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
            int? gcCollectionCount)
        {
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
    }

    /// <summary>One diagnostic-log interval (normally one second). Absent values are written as empty cells.</summary>
    public sealed class DiagnosticLogRow
    {
        public DateTimeOffset UtcTime { get; set; }
        public double ElapsedSeconds { get; set; }
        public double IntervalSeconds { get; set; }
        public int Frames { get; set; }

        public DiagnosticSampleSummary FrameMs { get; set; }
        public int FrameTimingSamples { get; set; }
        public DiagnosticSampleSummary CpuMainThreadMs { get; set; }
        public DiagnosticSampleSummary CpuRenderThreadMs { get; set; }
        public DiagnosticSampleSummary GpuMs { get; set; }
        public DiagnosticSampleSummary PresentWaitMs { get; set; }

        public double? SelectedSpeed { get; set; }
        public double? ActualSpeedMean { get; set; }
        public double? EfficiencyMean { get; set; }
        public int PausedFrames { get; set; }

        public int? SimulationSteps { get; set; }
        public double? SimulationStepsPerSecond { get; set; }
        public int? SimulationMaxStepsPerFrame { get; set; }
        public int SimulationFramesAtRenderCap { get; set; }
        public int SimulationFramesWithoutStep { get; set; }
        public DiagnosticSampleSummary SimulationStepMs { get; set; }
        public string? PerformancePreference { get; set; }

        public long? PathfindLeadFramesMin { get; set; }
        public int PathfindLowLeadFrames { get; set; }
        public int? PathfindPendingRequestsMax { get; set; }

        public int? GcCollections { get; set; }
        public double? ManagedHeapMiB { get; set; }

        public string? CaptureState { get; set; }
        public string? CaptureTrigger { get; set; }
        public string? CaptureId { get; set; }

        /// <summary>Last value each recorder column reported in the interval, aligned with the header.</summary>
        public IReadOnlyList<double?> RecorderValues { get; set; } = Array.Empty<double?>();
    }
}
