using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeProfiler.Core;
using CS2RuntimeProfiler.Core.Advisor;

namespace CS2RuntimeProfiler.UI
{
    public static class UiSnapshotBuilder
    {
        private const double CorrelationWindowSeconds = 5d;
        private const int MaxCorrelatedChanges = 8;

        public static UiSnapshot Build(UiSnapshotInput input) => BuildCore(input, forExport: false);
        public static UiSnapshot BuildForExport(UiSnapshotInput input) => BuildCore(input, forExport: true);

        private static UiSnapshot BuildCore(UiSnapshotInput input, bool forExport)
        {
            input = input ?? new UiSnapshotInput();
            var completedSessions = (input.Captures ?? Array.Empty<CaptureSession>()).Where(capture => capture != null).ToArray();
            var detailCapture = SelectDetailCapture(input, completedSessions);
            var detailIsCurrent = detailCapture != null && ReferenceEquals(detailCapture, input.CurrentCapture);
            var historicalExport = forExport && detailCapture != null && !detailIsCurrent;
            var detailTiming = detailCapture != null ? detailCapture.SystemTiming : input.Systems;
            var systems = BuildSystems(detailTiming);
            var captures = completedSessions.Select(BuildCapture).ToArray();
            var diagnostics = (input.Diagnostics ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var global = historicalExport ? GetLatestGlobalSample(detailCapture) : input.Global;
            var pathfinding = historicalExport ? detailCapture?.PathfindingSnapshot : input.Pathfinding;
            var domains = historicalExport ? detailCapture?.DomainMetricsSnapshot : input.Domains;
            if (historicalExport && pathfinding == null) diagnostics.Add("Historical capture has no retained pathfinding snapshot; this metric group is unavailable for this capture.");
            if (historicalExport && domains == null) diagnostics.Add("Historical capture has no retained domain-metrics snapshot; this metric group is unavailable for this capture.");
            var overheadShare = historicalExport ? Math.Max(0d, detailCapture.MaxProfilerOverheadShare) : Math.Max(0d, input.ProfilerOverheadShare);

            return new UiSnapshot
            {
                Global = BuildGlobal(global),
                Capture = new CaptureUiState
                {
                    State = input.CaptureState.ToString(), IsDeepCapture = input.CaptureState == CaptureState.DeepCapture,
                    CompletedCount = captures.Length, DetailCaptureId = detailCapture?.Id ?? string.Empty,
                    DetailScope = ResolveDetailScope(detailCapture, detailIsCurrent, historicalExport),
                    DetailConfiguration = detailCapture?.Configuration
                },
                Systems = systems, Mods = BuildMods(systems),
                Pathfinding = new PathfindingUiMetrics { Metrics = BuildMetrics(pathfinding) }, DomainMetrics = BuildMetrics(domains),
                Timeline = BuildTimeline(detailCapture), Captures = captures,
                Diagnostics = new DiagnosticsUi
                {
                    ProfilerOverheadShare = overheadShare, UnattributedJobsMilliseconds = detailTiming?.UnattributedJobsMilliseconds,
                    Messages = diagnostics.ToArray(), GameVersion = input.GameVersion ?? string.Empty, ProfilerVersion = input.ProfilerVersion ?? string.Empty,
                    DiscoveredMarkerCount = Math.Max(0, detailCapture?.MarkerCoverage.Discovered ?? input.DiscoveredMarkerCount), CapturedMarkerCount = Math.Max(0, input.CapturedMarkerCount),
                    SystemCount = systems.Count, MarkerBatchSize = Math.Max(0, input.MarkerBatchSize), SamplingStride = Math.Max(1, input.SamplingStride),
                    PatchMapState = input.PatchMapState ?? string.Empty
                },
                Advisor = input.Advisor ?? new AdvisorState(null, null, null, "Select a completed capture for Advisor diagnosis.")
            };
        }

        private static CaptureSession SelectDetailCapture(UiSnapshotInput input, IReadOnlyList<CaptureSession> completed)
        {
            if (!string.IsNullOrWhiteSpace(input.SelectedCaptureId))
            {
                var selected = completed.FirstOrDefault(capture => string.Equals(capture.Id, input.SelectedCaptureId, StringComparison.Ordinal));
                if (selected != null) return selected;
            }
            if (input.CurrentCapture != null) return input.CurrentCapture;
            return completed.Count == 0 ? null : completed[completed.Count - 1];
        }

        private static string ResolveDetailScope(CaptureSession detailCapture, bool detailIsCurrent, bool historicalExport)
        {
            if (detailCapture == null) return "live";
            if (detailIsCurrent) return "active-capture";
            return historicalExport ? "historical-capture" : "completed-capture-detail";
        }

        private static GlobalMetricsSnapshot GetLatestGlobalSample(CaptureSession capture)
        {
            if (capture?.GlobalSamples == null || capture.GlobalSamples.Count == 0) return null;
            return capture.GlobalSamples.OrderBy(sample => sample.TimestampSeconds).LastOrDefault();
        }

        private static GlobalUiMetrics BuildGlobal(GlobalMetricsSnapshot global)
        {
            if (global == null) return new GlobalUiMetrics { Available = false };
            return new GlobalUiMetrics
            {
                Available = true, TimestampSeconds = global.TimestampSeconds, SelectedSpeed = global.SelectedSpeed,
                ActualSpeed = global.ActualSpeed, Efficiency = global.Efficiency,
                RecorderMetrics = global.RecorderReadings.Where(pair => pair.Value.Count > 0).OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new UiMetricRow
                    {
                        Id = pair.Key, Value = pair.Value.Value,
                        UnitType = global.RecorderUnits.TryGetValue(pair.Key, out var unitType) ? unitType : string.Empty,
                        Availability = MetricAvailability.Available.ToString(), Confidence = MetricConfidence.Full.ToString()
                    }).ToArray()
            };
        }

        private static IReadOnlyList<UiMetricRow> BuildMetrics(NamedMetricSnapshot snapshot)
        {
            if (snapshot == null) return Array.Empty<UiMetricRow>();
            return snapshot.Metrics.Values.OrderBy(metric => metric.Id, StringComparer.Ordinal)
                .Select(metric => new UiMetricRow { Id = metric.Id, Value = metric.Value, UnitType = metric.UnitType, Confidence = metric.Confidence.ToString(), Availability = metric.Availability.ToString(), Reason = metric.Reason }).ToArray();
        }

        private static IReadOnlyList<SystemUiRow> BuildSystems(SystemTimingSnapshot snapshot)
        {
            if (snapshot == null) return Array.Empty<SystemUiRow>();
            return snapshot.Systems.Select(system => new SystemUiRow
            {
                Id = system.SystemId, OwnerAssembly = system.OwnerAssembly, SourceKind = system.SourceKind.ToString(), IsAggregateContainer = system.IsAggregateContainer,
                CurrentMilliseconds = system.Milliseconds, MeanMilliseconds = system.MeanMilliseconds, MedianMilliseconds = system.MedianMilliseconds,
                P95Milliseconds = system.P95Milliseconds, P99Milliseconds = system.P99Milliseconds, MaxMilliseconds = system.MaxMilliseconds,
                TotalMilliseconds = system.TotalMilliseconds, MillisecondsPerFrame = system.MillisecondsPerFrame, Calls = system.Calls, Confidence = system.Confidence.ToString(), PatchOwners = system.PatchOwners.ToArray()
            }).OrderByDescending(AdditiveCost).ThenBy(system => system.Id, StringComparer.Ordinal).ToArray();
        }

        private static IReadOnlyList<ModUiRow> BuildMods(IReadOnlyList<SystemUiRow> systems)
        {
            if (systems == null || systems.Count == 0) return Array.Empty<ModUiRow>();
            var direct = systems.Where(system => !system.IsAggregateContainer && string.Equals(system.SourceKind, SystemSourceKind.Mod.ToString(), StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(system.OwnerAssembly))
                .GroupBy(system => system.OwnerAssembly, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => new ModUiRow
                {
                    AssemblyName = group.Key,
                    DirectSystemMilliseconds = group.Sum(AdditiveCost),
                    DirectCostBasis = group.All(system => system.MillisecondsPerFrame.HasValue) ? ModCostBasis.PerFrame : ModCostBasis.PerSample,
                    DirectSystemCount = group.Count()
                }, StringComparer.Ordinal);
            foreach (var system in systems)
            foreach (var patchOwner in system.PatchOwners ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(patchOwner)) continue;
                if (!direct.TryGetValue(patchOwner, out var row)) { row = new ModUiRow { AssemblyName = patchOwner }; direct[patchOwner] = row; }
                if (!string.Equals(system.OwnerAssembly, patchOwner, StringComparison.Ordinal)) row.PatchedVanillaSystemCount++;
            }
            return direct.Values.OrderByDescending(row => row.DirectSystemMilliseconds).ThenBy(row => row.AssemblyName, StringComparer.Ordinal).ToArray();
        }

        // The last observed call ("current") is not additive: summing it lets a single rare call, such as an
        // autosave serializer, dominate a mod total. Prefer total time per frame, then the per-sample mean.
        private static double AdditiveCost(SystemUiRow system) => system.MillisecondsPerFrame ?? system.MeanMilliseconds ?? system.CurrentMilliseconds;

        private static CaptureSummaryUi BuildCapture(CaptureSession capture)
        {
            var samples = capture.GlobalSamples ?? Array.Empty<GlobalMetricsSnapshot>();
            var duration = samples.Count < 2 ? 0d : Math.Max(0d, samples.Max(sample => sample.TimestampSeconds) - samples.Min(sample => sample.TimestampSeconds));
            return new CaptureSummaryUi
            {
                Id = capture.Id, TriggerKind = capture.Trigger.Kind.ToString(), TriggeredAtSeconds = capture.Trigger.TimestampSeconds,
                TriggerSelectedSpeed = capture.TriggerSelectedSpeed, TriggerActualSpeed = capture.TriggerActualSpeed, TriggerEfficiency = capture.TriggerEfficiency,
                DurationSeconds = duration, DiscoveredMarkers = capture.MarkerCoverage.Discovered, AttemptedMarkers = capture.MarkerCoverage.Attempted,
                ActivatedMarkers = capture.MarkerCoverage.Activated, SampledMarkers = capture.MarkerCoverage.Sampled, CapturedMarkers = capture.MarkerCoverage.Captured,
                Batched = capture.MarkerCoverage.IsBatched, AttemptedRatio = capture.MarkerCoverage.AttemptedRatio,
                ActivatedRatio = capture.MarkerCoverage.ActivatedRatio, SampledRatio = capture.MarkerCoverage.SampledRatio, CoverageRatio = capture.MarkerCoverage.Ratio,
                ProfilerMemoryBaselineBytes = capture.ProfilerMemoryBaselineBytes, ProfilerMemoryPeakBytes = capture.ProfilerMemoryPeakBytes,
                ProfilerMemoryDeltaBytes = capture.ProfilerMemoryDeltaBytes,
                WarningCount = capture.Warnings.Count, ProfilerOverheadShare = capture.MaxProfilerOverheadShare, Warnings = capture.Warnings.ToArray(),
                CorrelatedChanges = BuildCorrelatedChanges(capture)
            };
        }

        private static IReadOnlyList<CorrelatedChangeUi> BuildCorrelatedChanges(CaptureSession capture)
        {
            var trigger = capture.Trigger.TimestampSeconds;
            var pre = capture.GlobalSamples.Where(sample => sample.TimestampSeconds >= trigger - CorrelationWindowSeconds && sample.TimestampSeconds < trigger).ToArray();
            var post = capture.GlobalSamples.Where(sample => sample.TimestampSeconds >= trigger && sample.TimestampSeconds <= trigger + CorrelationWindowSeconds).ToArray();
            if (pre.Length == 0 || post.Length == 0) return Array.Empty<CorrelatedChangeUi>();
            var changes = new List<CorrelatedChangeUi>();
            AddChange(changes, "actualSpeed", pre.Average(sample => sample.ActualSpeed), post.Average(sample => sample.ActualSpeed), MetricConfidence.Full, MetricUnits.Speed);
            AddChange(changes, "efficiency", pre.Average(sample => sample.Efficiency), post.Average(sample => sample.Efficiency), MetricConfidence.Full, MetricUnits.Ratio);
            var sharedRecorderIds = pre.SelectMany(sample => sample.RecorderReadings.Where(pair => pair.Value.Count > 0).Select(pair => pair.Key))
                .Intersect(post.SelectMany(sample => sample.RecorderReadings.Where(pair => pair.Value.Count > 0).Select(pair => pair.Key)), StringComparer.Ordinal).Distinct(StringComparer.Ordinal);
            foreach (var id in sharedRecorderIds)
            {
                var beforeValues = pre.Where(sample => sample.RecorderReadings.TryGetValue(id, out var reading) && reading.Count > 0).Select(sample => sample.RecorderReadings[id].Value).ToArray();
                var afterValues = post.Where(sample => sample.RecorderReadings.TryGetValue(id, out var reading) && reading.Count > 0).Select(sample => sample.RecorderReadings[id].Value).ToArray();
                if (beforeValues.Length == 0 || afterValues.Length == 0) continue;
                var unit = post.Concat(pre).Select(sample => UnitOf(sample.RecorderUnits, id)).FirstOrDefault(value => !string.IsNullOrEmpty(value)) ?? string.Empty;
                AddChange(changes, id, beforeValues.Average(), afterValues.Average(), MetricConfidence.Full, unit);
            }
            return changes.Where(change => Math.Abs(change.Delta) > double.Epsilon).OrderByDescending(change => Math.Abs(change.RelativeDelta ?? 0d)).ThenByDescending(change => Math.Abs(change.Delta)).Take(MaxCorrelatedChanges).ToArray();
        }

        private static void AddChange(List<CorrelatedChangeUi> changes, string metric, double before, double after, MetricConfidence confidence, string unitType)
        {
            if (double.IsNaN(before) || double.IsInfinity(before) || double.IsNaN(after) || double.IsInfinity(after)) return;
            var delta = after - before;
            changes.Add(new CorrelatedChangeUi { Metric = metric, Before = before, After = after, Delta = delta, RelativeDelta = Math.Abs(before) <= double.Epsilon ? (double?)null : delta / Math.Abs(before), UnitType = unitType ?? string.Empty, Confidence = confidence.ToString() });
        }

        private static IReadOnlyList<TimelinePoint> BuildTimeline(CaptureSession capture)
        {
            if (capture == null) return Array.Empty<TimelinePoint>();
            var points = new List<TimelinePoint>();
            var units = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var sample in capture.GlobalSamples)
            {
                foreach (var unit in sample.RecorderUnits) units[unit.Key] = unit.Value ?? string.Empty;
                points.Add(new TimelinePoint { TimestampSeconds = sample.TimestampSeconds, Metric = "selectedSpeed", Value = sample.SelectedSpeed, UnitType = MetricUnits.Speed, Confidence = MetricConfidence.Full.ToString() });
                points.Add(new TimelinePoint { TimestampSeconds = sample.TimestampSeconds, Metric = "actualSpeed", Value = sample.ActualSpeed, UnitType = MetricUnits.Speed, Confidence = MetricConfidence.Full.ToString() });
                points.Add(new TimelinePoint { TimestampSeconds = sample.TimestampSeconds, Metric = "efficiency", Value = sample.Efficiency, UnitType = MetricUnits.Ratio, Confidence = MetricConfidence.Full.ToString() });
                foreach (var recorder in sample.RecorderReadings.Where(pair => pair.Value.Count > 0))
                    points.Add(new TimelinePoint { TimestampSeconds = sample.TimestampSeconds, Metric = "recorder:" + recorder.Key, Value = recorder.Value.Value, UnitType = UnitOf(sample.RecorderUnits, recorder.Key), Confidence = MetricConfidence.Full.ToString() });
            }
            foreach (var marker in capture.MarkerSamples)
            foreach (var sample in marker.Value)
                points.Add(new TimelinePoint { TimestampSeconds = sample.TimestampSeconds, Metric = "marker:" + marker.Key, Value = sample.Value, UnitType = UnitOf(units, marker.Key), Confidence = sample.Confidence.ToString() });
            if (capture.SystemTiming != null)
            foreach (var system in capture.SystemTiming.Systems)
                points.Add(new TimelinePoint { TimestampSeconds = capture.Trigger.TimestampSeconds, Metric = "system:" + system.SystemId, Value = system.Milliseconds, UnitType = MetricUnits.Milliseconds, Confidence = system.Confidence.ToString() });
            return points.OrderBy(point => point.TimestampSeconds).ThenBy(point => point.Metric, StringComparer.Ordinal).ToArray();
        }

        private static string UnitOf(IReadOnlyDictionary<string, string> units, string id) =>
            units != null && units.TryGetValue(id, out var unit) ? unit ?? string.Empty : string.Empty;
    }
}
