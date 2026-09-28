using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core
{
    public sealed class MarkerBatchPlan
    {
        internal MarkerBatchPlan(IReadOnlyList<IReadOnlyList<string>> batches, int discoveredCount, int capturedCount)
        {
            Batches = batches;
            DiscoveredCount = discoveredCount;
            CapturedCount = capturedCount;
        }

        public IReadOnlyList<IReadOnlyList<string>> Batches { get; }
        public int DiscoveredCount { get; }
        public int CapturedCount { get; }
        public bool IsBatched => Batches.Count > 1;
        public double CoverageRatio => DiscoveredCount == 0 ? 1d : (double)CapturedCount / DiscoveredCount;
    }

    public static class MarkerBatchPlanner
    {
        public static MarkerBatchPlan Create(IEnumerable<string> markerIds, int maxConcurrent)
        {
            if (markerIds == null)
                throw new ArgumentNullException(nameof(markerIds));
            if (maxConcurrent < 1)
                throw new ArgumentOutOfRangeException(nameof(maxConcurrent));

            var unique = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in markerIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                    continue;
                unique.Add(id);
            }

            var batches = new List<IReadOnlyList<string>>();
            for (var offset = 0; offset < unique.Count; offset += maxConcurrent)
                batches.Add(unique.Skip(offset).Take(maxConcurrent).ToArray());

            return new MarkerBatchPlan(batches, unique.Count, unique.Count);
        }
    }
}
