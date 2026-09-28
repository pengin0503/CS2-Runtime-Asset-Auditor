using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core
{
    public sealed class NamedMetricSnapshot
    {
        private readonly IReadOnlyDictionary<string, NamedMetricValue> _metrics;

        public NamedMetricSnapshot(double timestampSeconds, IEnumerable<NamedMetricValue> metrics)
        {
            TimestampSeconds = timestampSeconds;
            _metrics = (metrics ?? Enumerable.Empty<NamedMetricValue>())
                .Where(metric => metric != null && !string.IsNullOrWhiteSpace(metric.Id))
                .GroupBy(metric => metric.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
        }

        public double TimestampSeconds { get; }
        public IReadOnlyDictionary<string, NamedMetricValue> Metrics => _metrics;

        public NamedMetricValue Get(string id)
        {
            if (!string.IsNullOrWhiteSpace(id) && _metrics.TryGetValue(id, out var metric))
                return metric;
            return NamedMetricValue.Unavailable(id ?? string.Empty, $"Metric '{id}' is unavailable.");
        }
    }
}
