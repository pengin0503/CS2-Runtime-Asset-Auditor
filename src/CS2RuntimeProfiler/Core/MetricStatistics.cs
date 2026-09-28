using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeProfiler.Core
{
    public readonly struct MetricStatistics
    {
        private MetricStatistics(double current, double mean, double median, double p95, double p99, double max, double total, int count)
        {
            Current = current;
            Mean = mean;
            Median = median;
            P95 = p95;
            P99 = p99;
            Max = max;
            Total = total;
            Count = count;
        }

        public double Current { get; }
        public double Mean { get; }
        public double Median { get; }
        public double P95 { get; }
        public double P99 { get; }
        public double Max { get; }
        public double Total { get; }
        public int Count { get; }

        public static MetricStatistics From(IReadOnlyCollection<double> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Count == 0)
                return default;

            var original = values.ToArray();
            var sorted = original.OrderBy(x => x).ToArray();
            var total = original.Sum();
            var middle = sorted.Length / 2;
            var median = sorted.Length % 2 == 0
                ? (sorted[middle - 1] + sorted[middle]) / 2d
                : sorted[middle];

            return new MetricStatistics(
                current: original[original.Length - 1],
                mean: total / original.Length,
                median: median,
                p95: NearestRank(sorted, 0.95),
                p99: NearestRank(sorted, 0.99),
                max: sorted[sorted.Length - 1],
                total: total,
                count: original.Length);
        }

        internal static MetricStatistics FromSummary(
            double current,
            double mean,
            IReadOnlyCollection<double> distributionValues,
            double max,
            double total,
            int count)
        {
            if (distributionValues == null)
                throw new ArgumentNullException(nameof(distributionValues));
            if (count <= 0 || distributionValues.Count == 0)
                return default;

            var distribution = From(distributionValues);
            return new MetricStatistics(
                current: current,
                mean: mean,
                median: distribution.Median,
                p95: distribution.P95,
                p99: distribution.P99,
                max: max,
                total: total,
                count: count);
        }

        private static double NearestRank(IReadOnlyList<double> sorted, double percentile)
        {
            var rank = (int)Math.Ceiling(percentile * sorted.Count);
            rank = Math.Max(1, Math.Min(rank, sorted.Count));
            return sorted[rank - 1];
        }
    }
}
