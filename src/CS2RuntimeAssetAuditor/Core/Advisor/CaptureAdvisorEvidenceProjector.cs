using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core.Advisor
{
    public sealed class CaptureAdvisorEvidenceProjector
    {
        public AdvisorEvidenceSnapshot Project(CaptureSession capture)
        {
            if (capture == null) throw new ArgumentNullException(nameof(capture));
            var metrics = new List<NamedMetricValue>();
            var samples = capture.GlobalSamples;
            var frame = ReadTimes(samples, "Frame Time", "Frame Time (CPU)");
            metrics.Add(frame.Count == 0 ? NamedMetricValue.Unavailable("frame.p95.ms", "Frame-time marker unavailable")
                : NamedMetricValue.Available("frame.p95.ms", MetricStatistics.From(frame).P95, MetricConfidence.Full, "Milliseconds"));
            var gpu = ReadTimes(samples, "GPU Frame Time", "GPU Time");
            metrics.Add(gpu.Count == 0 ? NamedMetricValue.Unavailable("gpu.frame.ms", "GPU timer unavailable")
                : NamedMetricValue.Available("gpu.frame.ms", MetricStatistics.From(gpu).P95, MetricConfidence.Full, "Milliseconds"));

            var simulationWindow = samples
                .Where(sample => sample != null
                    && sample.TimestampSeconds >= capture.Trigger.TimestampSeconds
                    && sample.SelectedSpeed > 0d
                    && !double.IsNaN(sample.Efficiency)
                    && !double.IsInfinity(sample.Efficiency))
                .Select(sample => sample.Efficiency)
                .ToArray();
            metrics.Add(simulationWindow.Length > 0
                ? NamedMetricValue.Available("simulation.efficiency", MetricStatistics.From(simulationWindow).Median, MetricConfidence.Full)
                : NamedMetricValue.Unavailable("simulation.efficiency", "Simulation efficiency unavailable in the post-trigger capture window"));

            var triggerEfficiency = capture.TriggerEfficiency ?? capture.Trigger.Efficiency;
            metrics.Add(triggerEfficiency.HasValue
                    && !double.IsNaN(triggerEfficiency.Value)
                    && !double.IsInfinity(triggerEfficiency.Value)
                ? NamedMetricValue.Available("simulation.trigger.efficiency", triggerEfficiency.Value, MetricConfidence.Full)
                : NamedMetricValue.Unavailable("simulation.trigger.efficiency", "Trigger simulation efficiency unavailable"));

            metrics.Add(capture.MaxProfilerOverheadShare > 0
                ? NamedMetricValue.Available("profiler.overhead.share", capture.MaxProfilerOverheadShare, MetricConfidence.Full)
                : NamedMetricValue.Unavailable("profiler.overhead.share", "Profiler overhead not sampled"));
            if (capture.ProfilerMemoryDeltaBytes.HasValue)
                metrics.Add(NamedMetricValue.Available("profiler.memory.delta.bytes", capture.ProfilerMemoryDeltaBytes.Value,
                    MetricConfidence.Managed, "Bytes"));
            if (capture.SystemTiming != null)
                foreach (var system in capture.SystemTiming.Systems)
                {
                    if (system.IsAggregateContainer || string.IsNullOrWhiteSpace(system.SystemId)) continue;
                    metrics.Add(NamedMetricValue.Available("system." + system.SystemId + ".ms",
                        system.Milliseconds, system.Confidence, "Milliseconds"));
                }
            if (capture.PathfindingSnapshot != null)
                metrics.AddRange(capture.PathfindingSnapshot.Metrics.Values);
            if (capture.DomainMetricsSnapshot != null)
                metrics.AddRange(capture.DomainMetricsSnapshot.Metrics.Values);
            return new AdvisorEvidenceSnapshot(DateTime.UtcNow, metrics);
        }

        private static List<double> ReadTimes(IReadOnlyList<GlobalMetricsSnapshot> samples, params string[] names)
        {
            var values = new List<double>();
            foreach (var sample in samples)
            foreach (var reading in sample.RecorderReadings)
            {
                var marker = reading.Key.Substring(reading.Key.LastIndexOf('\u001f') + 1);
                if (!names.Any(name => string.Equals(name, marker, StringComparison.OrdinalIgnoreCase))) continue;
                if (!sample.RecorderUnits.TryGetValue(reading.Key, out var unit)) continue;
                double scale;
                if (unit == "TimeNanoseconds") scale = 1e-6;
                else if (unit == "TimeMicroseconds") scale = 1e-3;
                else if (unit == "Milliseconds") scale = 1;
                else continue;
                var time = reading.Value.Value * scale;
                if (!double.IsNaN(time) && !double.IsInfinity(time) && time >= 0) values.Add(time);
            }
            return values;
        }
    }
}
