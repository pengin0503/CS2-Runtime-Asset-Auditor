using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core.Advisor
{
    public sealed class AdvisorEvidenceSnapshot
    {
        public AdvisorEvidenceSnapshot(DateTime timestampUtc, IEnumerable<NamedMetricValue> metrics)
        {
            TimestampUtc = timestampUtc;
            Metrics = (metrics ?? Enumerable.Empty<NamedMetricValue>()).ToArray();
        }

        public DateTime TimestampUtc { get; }
        public IReadOnlyList<NamedMetricValue> Metrics { get; }

        public NamedMetricValue Find(string id)
            => Metrics.FirstOrDefault(metric => string.Equals(metric.Id, id, StringComparison.Ordinal))
                ?? NamedMetricValue.Unavailable(id, "Not measured");
    }
}
