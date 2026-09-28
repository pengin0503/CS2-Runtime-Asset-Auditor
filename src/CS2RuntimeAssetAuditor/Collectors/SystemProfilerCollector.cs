using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Core;

namespace CS2RuntimeAssetAuditor.Collectors
{
    /// <summary>
    /// Aggregates already-observed system timing samples. Managed boundary samples remain Managed;
    /// worker/job time that cannot be tied to a system remains explicitly unattributed.
    /// </summary>
    public sealed class SystemProfilerCollector
    {
        private readonly int _maxSamplesPerSystem;
        private readonly Dictionary<string, RollingMetricSeries> _samples = new Dictionary<string, RollingMetricSeries>(StringComparer.Ordinal);
        private readonly Dictionary<string, SystemDescriptor> _descriptors = new Dictionary<string, SystemDescriptor>(StringComparer.Ordinal);
        private double _unattributedJobsMilliseconds;

        public SystemProfilerCollector(int maxSamplesPerSystem = 240)
        {
            if (maxSamplesPerSystem < 1)
                throw new ArgumentOutOfRangeException(nameof(maxSamplesPerSystem));
            _maxSamplesPerSystem = maxSamplesPerSystem;
        }

        public void AddManagedSample(SystemDescriptor descriptor, double timestampSeconds, double milliseconds)
        {
            AddSample(descriptor, timestampSeconds, milliseconds, MetricConfidence.Managed);
        }

        public void AddSample(SystemDescriptor descriptor, double timestampSeconds, double milliseconds, MetricConfidence confidence)
        {
            if (descriptor == null || milliseconds < 0 || double.IsNaN(milliseconds) || double.IsInfinity(milliseconds))
                return;

            var id = descriptor.FullTypeName;
            if (!_samples.TryGetValue(id, out var series))
            {
                series = new RollingMetricSeries(_maxSamplesPerSystem);
                _samples[id] = series;
            }

            _descriptors[id] = descriptor;
            series.Add(new MetricSample(timestampSeconds, milliseconds, confidence));
        }

        public void SetUnattributedJobsMilliseconds(double milliseconds)
        {
            _unattributedJobsMilliseconds = Math.Max(0d, milliseconds);
        }

        public IReadOnlyList<SystemMetricAggregate> GetAggregates()
        {
            var result = new List<SystemMetricAggregate>();
            foreach (var pair in _samples)
            {
                var samples = pair.Value.Snapshot();
                if (samples.Count == 0)
                    continue;
                var confidence = samples.Select(sample => sample.Confidence).OrderByDescending(ConfidenceRank).First();
                result.Add(SystemMetricAggregate.FromSamples(pair.Key, samples.Select(sample => sample.Value), confidence));
            }
            return result;
        }

        public SystemTimingSnapshot BuildSnapshot()
        {
            var snapshot = new SystemTimingSnapshot();
            foreach (var aggregate in GetAggregates())
            {
                _descriptors.TryGetValue(aggregate.SystemId, out var descriptor);
                snapshot.AddSystem(
                    aggregate.SystemId,
                    aggregate.CurrentMilliseconds,
                    aggregate.Confidence,
                    descriptor?.AssemblyName ?? string.Empty,
                    descriptor?.PatchOwners.Select(owner => owner.OwnerId));
            }
            snapshot.SetUnattributedJobsMilliseconds(_unattributedJobsMilliseconds);
            return snapshot;
        }

        private static int ConfidenceRank(MetricConfidence confidence)
        {
            switch (confidence)
            {
                case MetricConfidence.Unavailable: return 0;
                case MetricConfidence.Indirect: return 1;
                case MetricConfidence.Managed: return 2;
                case MetricConfidence.Full: return 3;
                default: return 0;
            }
        }
    }
}
