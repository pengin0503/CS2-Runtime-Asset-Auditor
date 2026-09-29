using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Core.Frames;

namespace CS2RuntimeAssetAuditor.Core.Advisor
{
    public sealed class CaptureAdvisorEvidenceProjector
    {
        public AdvisorEvidenceSnapshot Project(CaptureSession capture)
        {
            if (capture == null) throw new ArgumentNullException(nameof(capture));
            var metrics = new List<NamedMetricValue>();
            var samples = capture.GlobalSamples;
            var intervals = samples.Where(sample => sample?.FrameInterval != null).Select(sample => sample.FrameInterval).ToArray();
            AddFrameEvidence(metrics, samples, intervals);

            var simulationSamples = samples
                .Where(sample => sample != null
                    && sample.TimestampSeconds >= capture.Trigger.TimestampSeconds
                    && sample.SelectedSpeed > 0d
                    && !double.IsNaN(sample.Efficiency)
                    && !double.IsInfinity(sample.Efficiency))
                .ToArray();
            var simulationWindow = simulationSamples.Select(sample => sample.Efficiency).ToArray();
            metrics.Add(simulationWindow.Length > 0
                ? NamedMetricValue.Available("simulation.efficiency", MetricStatistics.From(simulationWindow).Median, MetricConfidence.Full)
                : NamedMetricValue.Unavailable("simulation.efficiency", "Simulation efficiency unavailable in the post-trigger capture window"));
            AddSimulationLimitEvidence(metrics, simulationSamples, string.Empty);

            // The samples before the trigger are the ones that started an automatic capture.
            var triggerSamples = samples
                .Where(sample => sample != null
                    && sample.TimestampSeconds < capture.Trigger.TimestampSeconds
                    && sample.SelectedSpeed > 0d)
                .ToArray();
            AddSimulationLimitEvidence(metrics, triggerSamples, "trigger.");

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

        public const string FrameTimeRecorderUnavailableReason = "Frame time was not measured";

        private static void AddFrameEvidence(List<NamedMetricValue> metrics, IReadOnlyList<GlobalMetricsSnapshot> samples, RuntimeInterval[] intervals)
        {
            // The mod measures frame time itself (Unity's unscaled delta time), so it does not depend on a profiler
            // marker. The retail build has no "Frame Time" marker; "CPU Total Frame Time" is the fallback.
            var frameP95 = intervals.Where(x => x.FrameMs.HasValue).Select(x => x.FrameMs.P95).ToArray();
            if (frameP95.Length > 0)
            {
                metrics.Add(NamedMetricValue.Available("frame.p95.ms", MetricStatistics.From(frameP95).Median, MetricConfidence.Full, "Milliseconds"));
                metrics.Add(NamedMetricValue.Available("frame.median.ms",
                    MetricStatistics.From(intervals.Where(x => x.FrameMs.HasValue).Select(x => x.FrameMs.Median).ToArray()).Median,
                    MetricConfidence.Full, "Milliseconds"));
            }
            else
            {
                var frame = ReadTimes(samples, "CPU Total Frame Time", "Frame Time", "Frame Time (CPU)");
                metrics.Add(frame.Count == 0 ? NamedMetricValue.Unavailable("frame.p95.ms", FrameTimeRecorderUnavailableReason)
                    : NamedMetricValue.Available("frame.p95.ms", MetricStatistics.From(frame).P95, MetricConfidence.Full, "Milliseconds"));
                metrics.Add(NamedMetricValue.Unavailable("frame.median.ms", FrameTimeRecorderUnavailableReason));
            }

            // FrameTimingManager: per-frame main-thread, GPU and present-wait times. A GPU-bound frame shows up as the
            // main thread waiting for present; D3D11 GPU time also counts time the GPU idles waiting for the CPU.
            var timed = intervals.Where(x => x.FrameTimingSamples > 0).ToArray();
            metrics.Add(MedianOf("cpu.main.ms", timed.Where(x => x.CpuMainThreadMs.HasValue).Select(x => x.CpuMainThreadMs.Median), "Main-thread frame timing unavailable"));
            metrics.Add(MedianOf("present.wait.ms", timed.Where(x => x.PresentWaitMs.HasValue).Select(x => x.PresentWaitMs.Median), "Present-wait frame timing unavailable"));
            var gpuTimings = timed.Where(x => x.GpuMs.HasValue).Select(x => x.GpuMs.Median).ToArray();
            if (gpuTimings.Length > 0)
            {
                metrics.Add(NamedMetricValue.Available("gpu.frame.ms", MetricStatistics.From(gpuTimings).Median, MetricConfidence.Full, "Milliseconds"));
            }
            else
            {
                var gpu = ReadTimes(samples, "GPU Frame Time", "GPU Time");
                metrics.Add(gpu.Count == 0 ? NamedMetricValue.Unavailable("gpu.frame.ms", "GPU timer unavailable")
                    : NamedMetricValue.Available("gpu.frame.ms", MetricStatistics.From(gpu).P95, MetricConfidence.Full, "Milliseconds"));
            }
        }

        // Values that tell why the simulation ran slower than selected: the frame-rate step cap, pathfinding waits,
        // the PerformancePreference budget, or the cost of the steps themselves.
        private static void AddSimulationLimitEvidence(List<NamedMetricValue> metrics, GlobalMetricsSnapshot[] samples, string scope)
        {
            var intervals = samples.Where(sample => sample.FrameInterval != null).Select(sample => sample.FrameInterval).ToArray();
            var prefix = "simulation." + scope;
            metrics.Add(MedianOf(prefix + "frame.ceiling", intervals.Where(x => x.FrameRateEfficiencyCeiling.HasValue).Select(x => x.FrameRateEfficiencyCeiling.Value),
                "Frame rate was not measured while the simulation ran", string.Empty));

            var counted = intervals.Sum(x => x.SimulationStepCountedFrames);
            metrics.Add(counted > 0
                ? NamedMetricValue.Available(prefix + "render.cap.share", intervals.Sum(x => x.SimulationFramesAtRenderCap) / (double)counted, MetricConfidence.Full)
                : NamedMetricValue.Unavailable(prefix + "render.cap.share", "Simulation steps were not measured while the simulation ran"));

            if (scope.Length > 0)
                return;

            metrics.Add(MedianOf("simulation.step.ms", intervals.Where(x => x.SimulationStepMs.HasValue).Select(x => x.SimulationStepMs.Median), "No simulation step was timed"));
            var running = intervals.Sum(x => x.RunningFrames);
            metrics.Add(running > 0
                ? NamedMetricValue.Available("pathfinding.low.lead.share", intervals.Sum(x => x.PathfindLowLeadFrames) / (double)running, MetricConfidence.Full)
                : NamedMetricValue.Unavailable("pathfinding.low.lead.share", "Pathfinding lead was not measured while the simulation ran"));
            var preference = intervals.Select(x => x.PerformancePreference).LastOrDefault(x => !string.IsNullOrEmpty(x));
            metrics.Add(preference == null
                ? NamedMetricValue.Unavailable("simulation.preference.limits.steps", "PerformancePreference was not read")
                : NamedMetricValue.Available("simulation.preference.limits.steps",
                    string.Equals(preference, "SimulationSpeed", StringComparison.Ordinal) ? 0d : 1d, MetricConfidence.Full));
        }

        private static NamedMetricValue MedianOf(string id, IEnumerable<double> values, string unavailableReason, string unitType = "Milliseconds")
        {
            var array = values.Where(x => !double.IsNaN(x) && !double.IsInfinity(x)).ToArray();
            return array.Length == 0
                ? NamedMetricValue.Unavailable(id, unavailableReason)
                : NamedMetricValue.Available(id, MetricStatistics.From(array).Median, MetricConfidence.Full, unitType);
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
