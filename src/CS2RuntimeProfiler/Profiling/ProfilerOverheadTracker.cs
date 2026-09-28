using System;
using System.Collections.Generic;
using System.Diagnostics;
using CS2RuntimeProfiler.Core;

namespace CS2RuntimeProfiler.Profiling
{
    public sealed class ProfilerOverheadTracker
    {
        private readonly RollingMetricSeries _history;

        public ProfilerOverheadTracker(int historyCapacity = 240)
        {
            _history = new RollingMetricSeries(historyCapacity);
        }

        public double LastMilliseconds { get; private set; }
        public IReadOnlyList<MetricSample> Snapshot() => _history.Snapshot();

        public void Measure(double timestampSeconds, Action action)
        {
            var start = Stopwatch.GetTimestamp();
            try
            {
                action();
            }
            finally
            {
                var elapsedTicks = Stopwatch.GetTimestamp() - start;
                LastMilliseconds = elapsedTicks * 1000d / Stopwatch.Frequency;
                _history.Add(new MetricSample(timestampSeconds, LastMilliseconds, MetricConfidence.Full));
            }
        }
    }
}
