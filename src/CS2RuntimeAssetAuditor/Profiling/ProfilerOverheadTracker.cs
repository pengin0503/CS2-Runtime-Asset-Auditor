using System;
using System.Diagnostics;

namespace CS2RuntimeAssetAuditor.Profiling
{
    public sealed class ProfilerOverheadTracker
    {
        public double LastMilliseconds { get; private set; }

        public void Measure(Action action)
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
            }
        }
    }
}
