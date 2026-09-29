using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.Advisor;

namespace CS2RuntimeAssetAuditor.UI
{
    public sealed class GlobalUiMetrics
    {
        public bool Available { get; set; }
        public double TimestampSeconds { get; set; }
        public double? SelectedSpeed { get; set; }
        public double? ActualSpeed { get; set; }
        public double? Efficiency { get; set; }
        public IReadOnlyList<UiMetricRow> RecorderMetrics { get; set; } = Array.Empty<UiMetricRow>();
    }

    public sealed class CaptureUiState
    {
        public string State { get; set; } = CaptureState.Monitoring.ToString();
        public bool IsDeepCapture { get; set; }
        public int CompletedCount { get; set; }
        public string DetailCaptureId { get; set; } = string.Empty;
        public string DetailScope { get; set; } = "live";
        public CaptureConfigurationSnapshot DetailConfiguration { get; set; }
    }

    public sealed class UiMetricRow
    {
        public string Id { get; set; } = string.Empty;
        public double? Value { get; set; }
        public string UnitType { get; set; } = string.Empty;
        public string Confidence { get; set; } = MetricConfidence.Unavailable.ToString();
        public string Availability { get; set; } = MetricAvailability.Unavailable.ToString();
        public string Reason { get; set; }
    }

    public sealed class SystemUiRow
    {
        public string Id { get; set; } = string.Empty;
        public string OwnerAssembly { get; set; } = string.Empty;
        public string SourceKind { get; set; } = SystemSourceKind.Unknown.ToString();
        public bool IsAggregateContainer { get; set; }
        public double CurrentMilliseconds { get; set; }
        public double? MeanMilliseconds { get; set; }
        public double? MedianMilliseconds { get; set; }
        public double? P95Milliseconds { get; set; }
        public double? P99Milliseconds { get; set; }
        public double? MaxMilliseconds { get; set; }
        public double? TotalMilliseconds { get; set; }
        public double? MillisecondsPerFrame { get; set; }
        public int? Calls { get; set; }
        public string Confidence { get; set; } = MetricConfidence.Unavailable.ToString();
        public IReadOnlyList<string> PatchOwners { get; set; } = Array.Empty<string>();
    }

    public sealed class ModUiRow
    {
        public string AssemblyName { get; set; } = string.Empty;
        /// <summary>
        /// Additive direct cost of the systems owned by this assembly. Uses total time per frame when the
        /// measurement window frame count is known, otherwise the per-sample mean (see <see cref="DirectCostBasis"/>).
        /// </summary>
        public double DirectSystemMilliseconds { get; set; }
        public string DirectCostBasis { get; set; } = ModCostBasis.PerFrame;
        public int DirectSystemCount { get; set; }
        public int PatchedVanillaSystemCount { get; set; }
    }

    public static class ModCostBasis
    {
        /// <summary>Every contributing system reports total time divided by rendered frames.</summary>
        public const string PerFrame = "perFrame";
        /// <summary>At least one system lacks a frame count; its per-sample mean (or current value) was used.</summary>
        public const string PerSample = "perSample";
    }

    public sealed class PathfindingUiMetrics
    {
        public IReadOnlyList<UiMetricRow> Metrics { get; set; } = Array.Empty<UiMetricRow>();
    }

    public sealed class TimelinePoint
    {
        public double TimestampSeconds { get; set; }
        public string Metric { get; set; } = string.Empty;
        public double Value { get; set; }
        public string UnitType { get; set; } = string.Empty;
        public string Confidence { get; set; } = MetricConfidence.Unavailable.ToString();
    }

    public sealed class CorrelatedChangeUi
    {
        public string Metric { get; set; } = string.Empty;
        public double Before { get; set; }
        public double After { get; set; }
        public double Delta { get; set; }
        public double? RelativeDelta { get; set; }
        public string UnitType { get; set; } = string.Empty;
        public string Confidence { get; set; } = MetricConfidence.Unavailable.ToString();
    }

    public sealed class CaptureSummaryUi
    {
        public string Id { get; set; } = string.Empty;
        public string SessionId { get; set; }
        public string StartedAtUtc { get; set; }
        public string CompletedAtUtc { get; set; }
        /// <summary>Why the capture ended early (a <see cref="Core.CaptureInterruptionReason"/> name); null when it ran its full course.</summary>
        public string InterruptionReason { get; set; }
        public string TriggerKind { get; set; } = string.Empty;
        public double TriggeredAtSeconds { get; set; }
        public double? TriggerSelectedSpeed { get; set; }
        public double? TriggerActualSpeed { get; set; }
        public double? TriggerEfficiency { get; set; }
        public double DurationSeconds { get; set; }
        public int DiscoveredMarkers { get; set; }
        public int AttemptedMarkers { get; set; }
        public int ActivatedMarkers { get; set; }
        public int SampledMarkers { get; set; }
        public int CapturedMarkers { get; set; }
        public bool Batched { get; set; }
        public double? AttemptedRatio { get; set; }
        public double? ActivatedRatio { get; set; }
        public double? SampledRatio { get; set; }
        public double? CoverageRatio { get; set; }
        public double? ProfilerMemoryBaselineBytes { get; set; }
        public double? ProfilerMemoryPeakBytes { get; set; }
        public double? ProfilerMemoryDeltaBytes { get; set; }
        public int WarningCount { get; set; }
        public double ProfilerOverheadShare { get; set; }
        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
        public IReadOnlyList<CorrelatedChangeUi> CorrelatedChanges { get; set; } = Array.Empty<CorrelatedChangeUi>();
    }

    public sealed class DiagnosticsUi
    {
        public double ProfilerOverheadShare { get; set; }
        public double? UnattributedJobsMilliseconds { get; set; }
        public IReadOnlyList<string> Messages { get; set; } = Array.Empty<string>();
        public string GameVersion { get; set; } = string.Empty;
        public string ProfilerVersion { get; set; } = string.Empty;
        public int DiscoveredMarkerCount { get; set; }
        public int CapturedMarkerCount { get; set; }
        public int SystemCount { get; set; }
        public int MarkerBatchSize { get; set; }
        public int SamplingStride { get; set; } = 1;
        public string PatchMapState { get; set; } = string.Empty;
    }

    public sealed class UiSnapshot
    {
        public GlobalUiMetrics Global { get; set; } = new GlobalUiMetrics();
        public CaptureUiState Capture { get; set; } = new CaptureUiState();
        public IReadOnlyList<SystemUiRow> Systems { get; set; } = Array.Empty<SystemUiRow>();
        public IReadOnlyList<ModUiRow> Mods { get; set; } = Array.Empty<ModUiRow>();
        public PathfindingUiMetrics Pathfinding { get; set; } = new PathfindingUiMetrics();
        public IReadOnlyList<UiMetricRow> DomainMetrics { get; set; } = Array.Empty<UiMetricRow>();
        public IReadOnlyList<TimelinePoint> Timeline { get; set; } = Array.Empty<TimelinePoint>();
        public IReadOnlyList<CaptureSummaryUi> Captures { get; set; } = Array.Empty<CaptureSummaryUi>();
        public DiagnosticsUi Diagnostics { get; set; } = new DiagnosticsUi();
        public AdvisorState Advisor { get; set; } = new AdvisorState(null, null, null, "Select a completed capture for Advisor diagnosis.");
    }

    /// <summary>The part of the panel snapshot that changes only when a capture changes.</summary>
    public sealed class UiCaptureDetail
    {
        public IReadOnlyList<SystemUiRow> Systems { get; set; } = Array.Empty<SystemUiRow>();
        public IReadOnlyList<ModUiRow> Mods { get; set; } = Array.Empty<ModUiRow>();
        public IReadOnlyList<TimelinePoint> Timeline { get; set; } = Array.Empty<TimelinePoint>();
        public IReadOnlyList<CaptureSummaryUi> Captures { get; set; } = Array.Empty<CaptureSummaryUi>();
    }

    /// <summary>
    /// What <see cref="UiCaptureDetail"/> was built from, compared by reference and revision; equal keys mean the
    /// detail would be built the same.
    /// </summary>
    public sealed class UiCaptureDetailKey : IEquatable<UiCaptureDetailKey>
    {
        private readonly CaptureSession _detail;
        private readonly long _detailRevision;
        private readonly SystemTimingSnapshot _liveSystems;
        private readonly (CaptureSession Capture, long Revision)[] _completed;

        public UiCaptureDetailKey(CaptureSession detail, long detailRevision, SystemTimingSnapshot liveSystems, (CaptureSession Capture, long Revision)[] completed)
        {
            _detail = detail;
            _detailRevision = detailRevision;
            _liveSystems = liveSystems;
            _completed = completed ?? Array.Empty<(CaptureSession, long)>();
        }

        public bool Equals(UiCaptureDetailKey other)
        {
            if (other is null) return false;
            if (!ReferenceEquals(_detail, other._detail) || _detailRevision != other._detailRevision
                || !ReferenceEquals(_liveSystems, other._liveSystems) || _completed.Length != other._completed.Length)
                return false;
            for (var index = 0; index < _completed.Length; index++)
            {
                if (!ReferenceEquals(_completed[index].Capture, other._completed[index].Capture)
                    || _completed[index].Revision != other._completed[index].Revision)
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as UiCaptureDetailKey);

        public override int GetHashCode() => _completed.Length ^ _detailRevision.GetHashCode();
    }

    public sealed class UiSnapshotInput
    {
        public UiSnapshotInput() { }
        public UiSnapshotInput(GlobalMetricsSnapshot global, CaptureState captureState, NamedMetricSnapshot pathfinding, NamedMetricSnapshot domains, SystemTimingSnapshot systems, IReadOnlyList<CaptureSession> captures, double profilerOverheadShare, IReadOnlyList<string> diagnostics)
        {
            Global = global; CaptureState = captureState; Pathfinding = pathfinding; Domains = domains; Systems = systems; Captures = captures; ProfilerOverheadShare = profilerOverheadShare; Diagnostics = diagnostics;
        }

        public GlobalMetricsSnapshot Global { get; set; }
        public CaptureState CaptureState { get; set; } = CaptureState.Monitoring;
        public NamedMetricSnapshot Pathfinding { get; set; }
        public NamedMetricSnapshot Domains { get; set; }
        public SystemTimingSnapshot Systems { get; set; }
        public IReadOnlyList<CaptureSession> Captures { get; set; } = Array.Empty<CaptureSession>();
        public CaptureSession CurrentCapture { get; set; }
        public string SelectedCaptureId { get; set; } = string.Empty;
        public double ProfilerOverheadShare { get; set; }
        public IReadOnlyList<string> Diagnostics { get; set; } = Array.Empty<string>();
        public string GameVersion { get; set; } = string.Empty;
        public string ProfilerVersion { get; set; } = string.Empty;
        public int DiscoveredMarkerCount { get; set; }
        public int CapturedMarkerCount { get; set; }
        public int MarkerBatchSize { get; set; }
        public int SamplingStride { get; set; } = 1;
        public string PatchMapState { get; set; } = string.Empty;
        public AdvisorState Advisor { get; set; }
    }
}
