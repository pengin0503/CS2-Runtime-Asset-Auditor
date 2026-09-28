using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeProfiler.Core
{
    public sealed class SystemMetricAggregate
    {
        private SystemMetricAggregate(string systemId, MetricStatistics statistics, MetricConfidence confidence, int? calls)
        {
            SystemId = systemId ?? string.Empty;
            Statistics = statistics;
            Confidence = confidence;
            Calls = calls.HasValue ? Math.Max(0, calls.Value) : (int?)null;
        }

        public string SystemId { get; }
        public MetricStatistics Statistics { get; }
        public MetricConfidence Confidence { get; }
        public double CurrentMilliseconds => Statistics.Current;
        public double MeanMilliseconds => Statistics.Mean;
        public double MedianMilliseconds => Statistics.Median;
        public double P95Milliseconds => Statistics.P95;
        public double P99Milliseconds => Statistics.P99;
        public double MaxMilliseconds => Statistics.Max;
        public double TotalMilliseconds => Statistics.Total;
        public int? Calls { get; }

        public static SystemMetricAggregate FromManagedSamples(string systemId, IEnumerable<double> milliseconds)
        {
            if (milliseconds == null)
                throw new ArgumentNullException(nameof(milliseconds));
            var values = milliseconds.Where(value => value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value)).ToArray();
            return new SystemMetricAggregate(systemId, MetricStatistics.From(values), MetricConfidence.Managed, calls: null);
        }

        public static SystemMetricAggregate FromSamples(
            string systemId,
            IEnumerable<double> milliseconds,
            MetricConfidence confidence,
            int? calls = null)
        {
            if (milliseconds == null)
                throw new ArgumentNullException(nameof(milliseconds));
            var values = milliseconds.Where(value => value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value)).ToArray();
            return new SystemMetricAggregate(systemId, MetricStatistics.From(values), confidence, calls);
        }

        internal static SystemMetricAggregate FromStreamingSummary(
            string systemId,
            IReadOnlyCollection<double> distributionMilliseconds,
            double currentMilliseconds,
            double meanMilliseconds,
            double maxMilliseconds,
            double totalMilliseconds,
            int calls,
            MetricConfidence confidence)
        {
            if (distributionMilliseconds == null)
                throw new ArgumentNullException(nameof(distributionMilliseconds));

            var values = distributionMilliseconds
                .Where(value => value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value))
                .ToArray();
            var statistics = MetricStatistics.FromSummary(
                currentMilliseconds,
                meanMilliseconds,
                values,
                maxMilliseconds,
                totalMilliseconds,
                calls);
            return new SystemMetricAggregate(systemId, statistics, confidence, calls);
        }
    }
}
