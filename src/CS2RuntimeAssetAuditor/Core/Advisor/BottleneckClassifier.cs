using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Core.Advisor
{
    public sealed class BottleneckClassifier
    {
        private const double SlowFrameMilliseconds = 20;
        private const double SlowGpuMilliseconds = 18;
        private const double LowSimulationEfficiency = 0.8;
        private const double HighProfilerOverheadShare = 0.1;

        public IReadOnlyList<BottleneckObservation> Classify(AdvisorEvidenceSnapshot evidence)
        {
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            var result = new List<BottleneckObservation>();
            var gpu = evidence.Find("gpu.frame.ms");
            var frame = evidence.Find("frame.p95.ms");
            var simulation = evidence.Find("simulation.efficiency");
            var triggerSimulation = evidence.Find("simulation.trigger.efficiency");
            var overhead = evidence.Find("profiler.overhead.share");
            var overheadHigh = overhead.Availability == MetricAvailability.Available
                && overhead.Value >= HighProfilerOverheadShare;

            if (gpu.Availability == MetricAvailability.Available && gpu.Value >= SlowGpuMilliseconds
                && frame.Availability == MetricAvailability.Available && frame.Value >= SlowFrameMilliseconds)
            {
                var confidence = gpu.Confidence == MetricConfidence.Indirect ? AdvisorConfidence.Low
                    : gpu.Confidence == MetricConfidence.Full && frame.Confidence == MetricConfidence.Full
                        ? AdvisorConfidence.High : AdvisorConfidence.Medium;
                if (overheadHigh) confidence = AdvisorConfidence.Low;
                result.Add(new BottleneckObservation(BottleneckCategory.RenderingGpu, BottleneckSeverity.High,
                    confidence, new[] { "frame.p95.ms", "gpu.frame.ms" }, "Both frame time and GPU time show elevated rendering load."));
            }
            else if (gpu.Availability == MetricAvailability.Available && gpu.Value >= SlowGpuMilliseconds
                && gpu.Confidence == MetricConfidence.Full && frame.Availability == MetricAvailability.Unavailable)
            {
                result.Add(new BottleneckObservation(BottleneckCategory.RenderingGpu, BottleneckSeverity.High,
                    overheadHigh ? AdvisorConfidence.Low : AdvisorConfidence.Medium,
                    new[] { "gpu.frame.ms" }, "Direct GPU time is high. Total frame time is unavailable, so confidence is reduced."));
            }

            if (simulation.Availability == MetricAvailability.Available && simulation.Value < LowSimulationEfficiency)
            {
                var confidence = overheadHigh ? AdvisorConfidence.Low : simulation.Confidence == MetricConfidence.Full
                    ? AdvisorConfidence.High : AdvisorConfidence.Medium;
                result.Add(new BottleneckObservation(BottleneckCategory.SimulationCpu, BottleneckSeverity.High,
                    confidence, new[] { "simulation.efficiency" }, "Simulation efficiency stays low across the measurement window, including the later part of the capture."));
            }
            else if (triggerSimulation.Availability == MetricAvailability.Available
                && triggerSimulation.Value < LowSimulationEfficiency)
            {
                var evidenceIds = simulation.Availability == MetricAvailability.Available
                    ? new[] { "simulation.trigger.efficiency", "simulation.efficiency" }
                    : new[] { "simulation.trigger.efficiency" };
                result.Add(new BottleneckObservation(BottleneckCategory.SimulationCpu, BottleneckSeverity.Medium,
                    overheadHigh ? AdvisorConfidence.Low : AdvisorConfidence.Medium,
                    evidenceIds,
                    "Simulation efficiency was low at the trigger, but no sustained drop was confirmed in the measurement window."));
            }

            // Low pressure is evidence of potential headroom, not proof that increasing quality is safe.
            if (result.Count == 0 && gpu.Availability == MetricAvailability.Available && gpu.Value < 10
                && frame.Availability == MetricAvailability.Available && frame.Value < 12
                && simulation.Availability == MetricAvailability.Available && simulation.Value >= 0.95)
            {
                result.Add(new BottleneckObservation(BottleneckCategory.RenderingGpu, BottleneckSeverity.Low,
                    overheadHigh ? AdvisorConfidence.Low : AdvisorConfidence.Medium,
                    new[] { "gpu.frame.ms", "frame.p95.ms", "simulation.efficiency" },
                    "Rendering has headroom under the current measurement conditions. Diagnose again after changing settings."));
            }
            return result;
        }
    }
}
