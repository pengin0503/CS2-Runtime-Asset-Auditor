using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Core
{
    /// <summary>
    /// Normal-monitoring samples tagged with the city session (<see cref="Coordination.DiagnosticSessionRegistry.Generation"/>)
    /// they were taken in. Reads for any other generation return nothing, so the latest sample and the capture
    /// prebuffer of one city can never be shown or captured as evidence for the next one, whatever order the
    /// systems that read them run in.
    /// </summary>
    public sealed class SessionScopedSnapshots
    {
        private readonly GlobalSnapshotHistory _history;
        private GlobalMetricsSnapshot? _latest;
        private long _generation = long.MinValue;

        public SessionScopedSnapshots(int capacity)
        {
            _history = new GlobalSnapshotHistory(capacity);
        }

        public void Add(long generation, GlobalMetricsSnapshot sample)
        {
            if (sample == null)
                return;
            if (generation != _generation)
            {
                Clear();
                _generation = generation;
            }
            _latest = sample;
            _history.Add(sample);
        }

        public GlobalMetricsSnapshot? Latest(long generation)
            => generation == _generation ? _latest : null;

        public IReadOnlyList<GlobalMetricsSnapshot> Recent(long generation, double windowSeconds)
        {
            var latest = Latest(generation);
            return latest == null
                ? Array.Empty<GlobalMetricsSnapshot>()
                : _history.Recent(latest.TimestampSeconds, windowSeconds);
        }

        public void Clear()
        {
            _latest = null;
            _history.Clear();
        }
    }
}
