using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeProfiler.Core
{
    public sealed class SystemTimingEntry
    {
        public SystemTimingEntry(
            string systemId,
            double milliseconds,
            MetricConfidence confidence,
            string ownerAssembly,
            IEnumerable<string> patchOwners,
            double? meanMilliseconds = null,
            double? medianMilliseconds = null,
            double? p95Milliseconds = null,
            double? p99Milliseconds = null,
            double? maxMilliseconds = null,
            double? totalMilliseconds = null,
            int? calls = null,
            SystemSourceKind sourceKind = SystemSourceKind.Unknown,
            bool isAggregateContainer = false,
            double? millisecondsPerFrame = null)
        {
            SystemId = systemId ?? string.Empty;
            Milliseconds = Math.Max(0d, milliseconds);
            Confidence = confidence;
            OwnerAssembly = ownerAssembly ?? string.Empty;
            PatchOwners = (patchOwners ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
            MeanMilliseconds = Normalize(meanMilliseconds);
            MedianMilliseconds = Normalize(medianMilliseconds);
            P95Milliseconds = Normalize(p95Milliseconds);
            P99Milliseconds = Normalize(p99Milliseconds);
            MaxMilliseconds = Normalize(maxMilliseconds);
            TotalMilliseconds = Normalize(totalMilliseconds);
            Calls = calls.HasValue ? Math.Max(0, calls.Value) : (int?)null;
            SourceKind = sourceKind;
            IsAggregateContainer = isAggregateContainer;
            MillisecondsPerFrame = Normalize(millisecondsPerFrame);
        }

        public string SystemId { get; }
        public double Milliseconds { get; }
        public MetricConfidence Confidence { get; }
        public string OwnerAssembly { get; }
        public IReadOnlyList<string> PatchOwners { get; }
        public double? MeanMilliseconds { get; }
        public double? MedianMilliseconds { get; }
        public double? P95Milliseconds { get; }
        public double? P99Milliseconds { get; }
        public double? MaxMilliseconds { get; }
        public double? TotalMilliseconds { get; }
        public int? Calls { get; }
        public SystemSourceKind SourceKind { get; }
        public bool IsAggregateContainer { get; }

        /// <summary>
        /// Total measured time divided by the number of rendered frames in the measurement window.
        /// Unlike per-call statistics this is additive across systems with different update intervals.
        /// Null when the window frame count is unknown.
        /// </summary>
        public double? MillisecondsPerFrame { get; }

        private static double? Normalize(double? value)
        {
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
                return null;
            return Math.Max(0d, value.Value);
        }
    }

    public sealed class SystemTimingSnapshot
    {
        private readonly List<SystemTimingEntry> _systems = new List<SystemTimingEntry>();

        public IReadOnlyList<SystemTimingEntry> Systems => _systems;
        public double? UnattributedJobsMilliseconds { get; private set; }

        public void AddSystem(
            string systemId,
            double milliseconds,
            MetricConfidence confidence,
            string ownerAssembly = "",
            IEnumerable<string> patchOwners = null,
            SystemSourceKind sourceKind = SystemSourceKind.Unknown,
            bool isAggregateContainer = false)
        {
            _systems.Add(new SystemTimingEntry(
                systemId,
                milliseconds,
                confidence,
                ownerAssembly,
                patchOwners,
                sourceKind: sourceKind,
                isAggregateContainer: isAggregateContainer));
        }

        public void AddSystemAggregate(
            SystemMetricAggregate aggregate,
            string ownerAssembly = "",
            IEnumerable<string> patchOwners = null,
            SystemSourceKind sourceKind = SystemSourceKind.Unknown,
            bool isAggregateContainer = false,
            double? millisecondsPerFrame = null)
        {
            if (aggregate == null)
                return;

            _systems.Add(new SystemTimingEntry(
                aggregate.SystemId,
                aggregate.CurrentMilliseconds,
                aggregate.Confidence,
                ownerAssembly,
                patchOwners,
                aggregate.MeanMilliseconds,
                aggregate.MedianMilliseconds,
                aggregate.P95Milliseconds,
                aggregate.P99Milliseconds,
                aggregate.MaxMilliseconds,
                aggregate.TotalMilliseconds,
                aggregate.Calls,
                sourceKind,
                isAggregateContainer,
                millisecondsPerFrame));
        }

        public void AddEntry(SystemTimingEntry entry)
        {
            if (entry == null)
                return;

            _systems.Add(new SystemTimingEntry(
                entry.SystemId,
                entry.Milliseconds,
                entry.Confidence,
                entry.OwnerAssembly,
                entry.PatchOwners,
                entry.MeanMilliseconds,
                entry.MedianMilliseconds,
                entry.P95Milliseconds,
                entry.P99Milliseconds,
                entry.MaxMilliseconds,
                entry.TotalMilliseconds,
                entry.Calls,
                entry.SourceKind,
                entry.IsAggregateContainer,
                entry.MillisecondsPerFrame));
        }

        public void SetUnattributedJobsMilliseconds(double milliseconds)
        {
            UnattributedJobsMilliseconds = Math.Max(0d, milliseconds);
        }

        public double GetDirectAssemblyTotal(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return 0d;
            return _systems
                .Where(system => !system.IsAggregateContainer
                    && string.Equals(system.OwnerAssembly, assemblyName, StringComparison.Ordinal))
                .Sum(system => system.Milliseconds);
        }
    }
}
