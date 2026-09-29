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

        // FrameTimingManager shares of the median frame. A GPU-bound frame makes the main thread wait for present;
        // a main-thread-bound frame keeps the main thread busy for the whole frame without that wait.
        private const double GpuBoundPresentWaitShare = 0.2;
        private const double MainThreadBoundShare = 0.8;

        // The simulation is limited by the frame rate when the frame-rate ceiling is below full speed and either the
        // measured efficiency reaches most of it or most running frames ran the per-frame maximum of steps.
        private const double FullSpeedCeiling = 0.95;
        private const double CeilingReachedRatio = 0.85;
        private const double RenderCapLimitedShare = 0.5;
        private const double PathfindingLimitedShare = 0.25;

        public IReadOnlyList<BottleneckObservation> Classify(AdvisorEvidenceSnapshot evidence)
        {
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            var result = new List<BottleneckObservation>();
            var gpu = evidence.Find("gpu.frame.ms");
            var frame = evidence.Find("frame.p95.ms");
            var simulation = evidence.Find("simulation.efficiency");
            var triggerSimulation = evidence.Find("simulation.trigger.efficiency");
            var overhead = evidence.Find("profiler.overhead.share");
            var overheadHigh = Available(overhead) && overhead.Value >= HighProfilerOverheadShare;

            ClassifyFrameTime(evidence, frame, gpu, overheadHigh, result);

            if (Available(simulation) && simulation.Value < LowSimulationEfficiency)
            {
                ClassifySimulationSlowdown(evidence, simulation, string.Empty, BottleneckSeverity.High, overheadHigh, result,
                    "Simulation efficiency stays low across the measurement window, including the later part of the capture.");
            }
            else if (Available(triggerSimulation) && triggerSimulation.Value < LowSimulationEfficiency)
            {
                ClassifySimulationSlowdown(evidence, triggerSimulation, "trigger.", BottleneckSeverity.Medium, overheadHigh, result,
                    "Simulation efficiency was low at the trigger, but no sustained drop was confirmed in the measurement window.",
                    Available(simulation) ? "simulation.efficiency" : null);
            }

            // Low pressure is evidence of potential headroom, not proof that increasing quality is safe.
            if (result.Count == 0 && Available(gpu) && gpu.Value < 10
                && Available(frame) && frame.Value < 12
                && Available(simulation) && simulation.Value >= 0.95)
            {
                result.Add(new BottleneckObservation(BottleneckCategory.RenderingGpu, BottleneckSeverity.Low,
                    overheadHigh ? AdvisorConfidence.Low : AdvisorConfidence.Medium,
                    new[] { "gpu.frame.ms", "frame.p95.ms", "simulation.efficiency" },
                    "Rendering has headroom under the current measurement conditions. Diagnose again after changing settings."));
            }
            return result;
        }

        private static void ClassifyFrameTime(AdvisorEvidenceSnapshot evidence, NamedMetricValue frame, NamedMetricValue gpu,
            bool overheadHigh, List<BottleneckObservation> result)
        {
            var frameMedian = evidence.Find("frame.median.ms");
            var cpuMain = evidence.Find("cpu.main.ms");
            var presentWait = evidence.Find("present.wait.ms");
            var frameSlow = Available(frame) && frame.Value >= SlowFrameMilliseconds;

            // With per-frame thread timings the wait for present separates a GPU-bound frame from a main-thread-bound
            // one. GPU time alone cannot: on D3D11 it includes time the GPU waits for the CPU, so it tracks the frame
            // time in both cases.
            if (frameSlow && Available(frameMedian) && frameMedian.Value > 0 && Available(cpuMain) && Available(presentWait))
            {
                // Slow typical frames are a sustained limit; slow P95 alone means intermittent spikes.
                var severity = frameMedian.Value >= SlowFrameMilliseconds ? BottleneckSeverity.High : BottleneckSeverity.Medium;
                if (presentWait.Value >= GpuBoundPresentWaitShare * frameMedian.Value)
                {
                    result.Add(new BottleneckObservation(BottleneckCategory.RenderingGpu, severity,
                        overheadHigh ? AdvisorConfidence.Low : AdvisorConfidence.High,
                        new[] { "frame.p95.ms", "present.wait.ms", "gpu.frame.ms" },
                        "Frame time is high and the main thread waits for the GPU to present each frame, so rendering on the GPU limits the frame rate."));
                    return;
                }
                if (cpuMain.Value >= MainThreadBoundShare * frameMedian.Value)
                {
                    result.Add(new BottleneckObservation(BottleneckCategory.MainThreadCpu, severity,
                        overheadHigh ? AdvisorConfidence.Low : AdvisorConfidence.High,
                        new[] { "frame.p95.ms", "cpu.main.ms", "present.wait.ms" },
                        "Frame time is high and the main thread is busy for almost the whole frame without waiting for the GPU, so CPU work on the main thread (including waiting for worker jobs) limits the frame rate. GPU time is not used here because it also counts time the GPU waits for the CPU."));
                    return;
                }
            }

            if (Available(gpu) && gpu.Value >= SlowGpuMilliseconds && frameSlow)
            {
                var confidence = gpu.Confidence == MetricConfidence.Indirect ? AdvisorConfidence.Low
                    : gpu.Confidence == MetricConfidence.Full && frame.Confidence == MetricConfidence.Full
                        ? AdvisorConfidence.High : AdvisorConfidence.Medium;
                // Without the main-thread and present-wait split, GPU time cannot rule out a CPU-bound frame.
                if (!Available(presentWait) && confidence == AdvisorConfidence.High) confidence = AdvisorConfidence.Medium;
                if (overheadHigh) confidence = AdvisorConfidence.Low;
                result.Add(new BottleneckObservation(BottleneckCategory.RenderingGpu, BottleneckSeverity.High,
                    confidence, new[] { "frame.p95.ms", "gpu.frame.ms" }, "Both frame time and GPU time show elevated rendering load."));
            }
            else if (Available(gpu) && gpu.Value >= SlowGpuMilliseconds
                && gpu.Confidence == MetricConfidence.Full && frame.Availability == MetricAvailability.Unavailable)
            {
                result.Add(new BottleneckObservation(BottleneckCategory.RenderingGpu, BottleneckSeverity.High,
                    overheadHigh ? AdvisorConfidence.Low : AdvisorConfidence.Medium,
                    new[] { "gpu.frame.ms" }, "Direct GPU time is high. Total frame time is unavailable, so confidence is reduced."));
            }
        }

        private static void ClassifySimulationSlowdown(AdvisorEvidenceSnapshot evidence, NamedMetricValue efficiency, string scope,
            BottleneckSeverity severity, bool overheadHigh, List<BottleneckObservation> result, string lowEfficiencyRationale,
            string? additionalEvidenceId = null)
        {
            var ceiling = evidence.Find("simulation." + scope + "frame.ceiling");
            var capShare = evidence.Find("simulation." + scope + "render.cap.share");
            // Between 30 and 60 fps many frames run the per-frame maximum of steps without limiting the speed, so the
            // step-cap share only counts when the frame rate is low enough to lower the ceiling.
            var belowCeiling = Available(ceiling) && ceiling.Value < FullSpeedCeiling;
            var ceilingExplains = belowCeiling && efficiency.Value >= CeilingReachedRatio * ceiling.Value;
            var capExplains = belowCeiling && Available(capShare) && capShare.Value >= RenderCapLimitedShare;
            var efficiencyIds = additionalEvidenceId == null
                ? new List<string> { efficiency.Id }
                : new List<string> { efficiency.Id, additionalEvidenceId };

            if (ceilingExplains || capExplains)
            {
                var ids = new List<string>(efficiencyIds);
                if (Available(ceiling)) ids.Add(ceiling.Id);
                if (Available(capShare)) ids.Add(capShare.Id);
                var confidence = overheadHigh ? AdvisorConfidence.Low
                    : ceilingExplains && capExplains ? AdvisorConfidence.High : AdvisorConfidence.Medium;
                result.Add(new BottleneckObservation(BottleneckCategory.FrameRateLimit, severity, confidence, ids,
                    "The simulation runs as fast as the frame rate allows: below 30 fps the game cannot run enough simulation steps per rendered frame (at most two per frame for each 1x of speed). Lowering frame time, not simulation load, raises the speed."));
                return;
            }

            if (scope.Length == 0)
            {
                var pathfinding = evidence.Find("pathfinding.low.lead.share");
                if (Available(pathfinding) && pathfinding.Value >= PathfindingLimitedShare)
                {
                    var ids = new List<string>(efficiencyIds) { pathfinding.Id };
                    result.Add(new BottleneckObservation(BottleneckCategory.Pathfinding, severity,
                        overheadHigh ? AdvisorConfidence.Low : AdvisorConfidence.Medium, ids,
                        "The simulation often waited for pathfinding results: the game slows the simulation when fewer than 48 frames of pathfinding lead remain."));
                    return;
                }
            }

            var preference = evidence.Find("simulation.preference.limits.steps");
            var preferenceLimits = Available(preference) && preference.Value > 0;
            var simulationConfidence = overheadHigh ? AdvisorConfidence.Low
                : severity == BottleneckSeverity.High && efficiency.Confidence == MetricConfidence.Full && !preferenceLimits
                    ? AdvisorConfidence.High : AdvisorConfidence.Medium;
            var simulationIds = new List<string>(efficiencyIds);
            if (preferenceLimits) simulationIds.Add(preference.Id);
            result.Add(new BottleneckObservation(BottleneckCategory.SimulationCpu, severity, simulationConfidence, simulationIds,
                preferenceLimits
                    ? lowEfficiencyRationale + " The Performance Preference option also limits simulation steps to the time left in each frame, so part of the slowdown may come from that setting."
                    : lowEfficiencyRationale));
        }

        private static bool Available(NamedMetricValue value)
            => value.Availability == MetricAvailability.Available && value.Value.HasValue;
    }
}
