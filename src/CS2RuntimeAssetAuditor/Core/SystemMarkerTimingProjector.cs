using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core
{
    public sealed class SystemTimingProjectionDiagnostics
    {
        public SystemTimingProjectionDiagnostics(
            int catalogSystemCount,
            int profilerMarkerCount,
            int timeMarkerCount,
            int uniqueMatchCount,
            int ambiguousMatchCount,
            int uniqueMatchesWithoutSamples)
        {
            CatalogSystemCount = Math.Max(0, catalogSystemCount);
            ProfilerMarkerCount = Math.Max(0, profilerMarkerCount);
            TimeMarkerCount = Math.Max(0, timeMarkerCount);
            UniqueMatchCount = Math.Max(0, uniqueMatchCount);
            AmbiguousMatchCount = Math.Max(0, ambiguousMatchCount);
            UniqueMatchesWithoutSamples = Math.Max(0, uniqueMatchesWithoutSamples);
        }

        public int CatalogSystemCount { get; }
        public int ProfilerMarkerCount { get; }
        public int TimeMarkerCount { get; }
        public int UniqueMatchCount { get; }
        public int AmbiguousMatchCount { get; }
        public int UniqueMatchesWithoutSamples { get; }
    }

    /// <summary>
    /// Projects captured Unity Entities profiler markers into per-system timing.
    /// Runtime descriptors use the exact marker name reported by Unity Entities when available.
    /// Legacy descriptors may retain strict full-type-name matching only when explicitly allowed.
    /// Short-name guessing and unknown units are intentionally rejected.
    /// </summary>
    public static class SystemMarkerTimingProjector
    {
        private const double NanosecondsPerMillisecond = 1_000_000d;

        public static SystemTimingSnapshot Project(
            IEnumerable<SystemDescriptor> systems,
            IEnumerable<RecorderDescriptor> recorders,
            CaptureSession capture)
        {
            var result = new SystemTimingSnapshot();
            if (capture == null)
                return result;

            var systemList = GetSystems(systems);
            var recorderList = GetTimeRecorders(recorders);

            foreach (var system in systemList)
            {
                var candidates = FindCandidates(recorderList, system);

                // Ambiguous matches are left unattributed rather than guessed.
                if (candidates.Length != 1)
                    continue;

                var recorder = candidates[0];
                if (!capture.TryGetMarkerSamples(recorder.Id, out var samples) || samples.Count == 0)
                    continue;

                var validSamples = samples
                    .Where(sample => sample.Value >= 0d && !double.IsNaN(sample.Value) && !double.IsInfinity(sample.Value))
                    .ToArray();
                if (validSamples.Length == 0)
                    continue;

                var milliseconds = validSamples
                    .Select(sample => sample.Value / NanosecondsPerMillisecond)
                    .ToArray();
                var calls = SumCallCounts(validSamples);

                var aggregate = SystemMetricAggregate.FromSamples(
                    system.FullTypeName,
                    milliseconds,
                    MetricConfidence.Full,
                    calls);
                result.AddSystemAggregate(
                    aggregate,
                    system.AssemblyName,
                    system.PatchOwners.Select(owner => owner.OwnerId),
                    system.SourceKind,
                    system.IsAggregateContainer);
            }

            return result;
        }

        public static SystemTimingProjectionDiagnostics Diagnose(
            IEnumerable<SystemDescriptor> systems,
            IEnumerable<RecorderDescriptor> recorders,
            CaptureSession capture)
        {
            var systemList = GetSystems(systems);
            var allRecorders = (recorders ?? Array.Empty<RecorderDescriptor>())
                .Where(recorder => recorder != null)
                .ToArray();
            var timeRecorders = GetTimeRecorders(allRecorders);
            var uniqueMatches = 0;
            var ambiguousMatches = 0;
            var uniqueMatchesWithoutSamples = 0;

            foreach (var system in systemList)
            {
                var candidates = FindCandidates(timeRecorders, system);
                if (candidates.Length == 1)
                {
                    uniqueMatches++;
                    if (capture == null
                        || !capture.TryGetMarkerSamples(candidates[0].Id, out var samples)
                        || samples.Count == 0)
                    {
                        uniqueMatchesWithoutSamples++;
                    }
                }
                else if (candidates.Length > 1)
                {
                    ambiguousMatches++;
                }
            }

            return new SystemTimingProjectionDiagnostics(
                systemList.Length,
                allRecorders.Length,
                timeRecorders.Length,
                uniqueMatches,
                ambiguousMatches,
                uniqueMatchesWithoutSamples);
        }

        private static SystemDescriptor[] GetSystems(IEnumerable<SystemDescriptor> systems)
        {
            return (systems ?? Array.Empty<SystemDescriptor>())
                .Where(system => system != null && !string.IsNullOrWhiteSpace(system.FullTypeName))
                .ToArray();
        }

        private static RecorderDescriptor[] GetTimeRecorders(IEnumerable<RecorderDescriptor> recorders)
        {
            return (recorders ?? Array.Empty<RecorderDescriptor>())
                .Where(recorder => recorder != null
                    && string.Equals(recorder.UnitType, "TimeNanoseconds", StringComparison.Ordinal))
                .ToArray();
        }

        private static RecorderDescriptor[] FindCandidates(
            IEnumerable<RecorderDescriptor> recorders,
            SystemDescriptor system)
        {
            return recorders
                .Where(recorder => MatchesSystemMarker(recorder.Name, system))
                .ToArray();
        }

        private static int? SumCallCounts(IReadOnlyList<MetricSample> samples)
        {
            if (samples.Count == 0 || samples.Any(sample => !sample.CallCount.HasValue))
                return null;

            long total = 0;
            foreach (var sample in samples)
            {
                var count = Math.Max(0L, sample.CallCount.GetValueOrDefault());
                if (count >= int.MaxValue - total)
                    return int.MaxValue;
                total += count;
            }

            return (int)total;
        }

        private static bool MatchesSystemMarker(string markerName, SystemDescriptor system)
        {
            if (string.IsNullOrWhiteSpace(markerName) || system == null)
                return false;

            if (!string.IsNullOrWhiteSpace(system.ProfilerMarkerName))
                return string.Equals(markerName, system.ProfilerMarkerName, StringComparison.Ordinal);

            if (!system.AllowLegacyProfilerMarkerMatching)
                return false;

            return MatchesLegacyFullSystemName(markerName, system.FullTypeName);
        }

        private static bool MatchesLegacyFullSystemName(string markerName, string fullTypeName)
        {
            if (string.IsNullOrWhiteSpace(markerName) || string.IsNullOrWhiteSpace(fullTypeName))
                return false;

            if (string.Equals(markerName, fullTypeName, StringComparison.Ordinal))
                return true;

            // Legacy compatibility for descriptors explicitly created without a runtime marker identity.
            return markerName.EndsWith(" " + fullTypeName, StringComparison.Ordinal);
        }
    }
}
