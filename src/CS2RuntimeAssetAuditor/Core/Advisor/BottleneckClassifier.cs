using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Core.Frames;

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
                ClassifyThreadTimings(evidence, frameMedian, cpuMain, presentWait, gpu, overheadHigh, result);
                return;
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

        // Shares of the median frame, after Unity's BottleneckHistory.DetermineBottleneck (HDRP debug frame timing):
        // a thread above 80% of the frame limits it. Unlike Unity's rule, a high GPU time does not count as GPU-bound
        // here, because on D3D11 it includes time the GPU waits for the CPU; the main thread's wait for present does.
        // FrameTimingManager ends the render thread's time when it calls Present, so a render thread that blocks in
        // Present for the GPU does not look busy, while one that is busy issuing draw calls makes the main thread wait
        // for present (Gfx.WaitForPresentOnGfxThread) just as a GPU-bound frame does: the render-thread time is what
        // tells the two apart.
        private static void ClassifyThreadTimings(AdvisorEvidenceSnapshot evidence, NamedMetricValue frameMedian,
            NamedMetricValue cpuMain, NamedMetricValue presentWait, NamedMetricValue gpu,
            bool overheadHigh, List<BottleneckObservation> result)
        {
            var cpuRender = evidence.Find("cpu.render.ms");
            var frameCap = evidence.Find("display.frame.cap");
            var busy = MainThreadBoundShare * frameMedian.Value;
            var renderKnown = Available(cpuRender);
            var renderBusy = renderKnown && cpuRender.Value >= busy;
            var gpuBusy = Available(gpu) && gpu.Value >= busy;
            var waitsForPresent = presentWait.Value >= GpuBoundPresentWaitShare * frameMedian.Value;

            // Typical frames are not slow, only the P95 is: the timings describe the typical frames, so they do not
            // tell what made the slow ones slow.
            var spikesOnly = frameMedian.Value < SlowFrameMilliseconds;
            var severity = spikesOnly ? BottleneckSeverity.Medium : BottleneckSeverity.High;

            void Add(BottleneckCategory category, AdvisorConfidence confidence, IEnumerable<string> ids, string rationale)
            {
                if (overheadHigh || spikesOnly) confidence = AdvisorConfidence.Low;
                result.Add(new BottleneckObservation(category, severity, confidence, ids,
                    spikesOnly ? rationale + IntermittentSuffix : rationale));
            }

            if (renderBusy)
            {
                var ids = new List<string> { "frame.p95.ms", "cpu.render.ms", "present.wait.ms" };
                if (gpuBusy) ids.Add("gpu.frame.ms");
                Add(BottleneckCategory.RenderThreadCpu, gpuBusy ? AdvisorConfidence.Medium : AdvisorConfidence.High, ids,
                    gpuBusy
                        ? "Frame time is high and the render thread is busy for almost the whole frame, so issuing draw calls limits the frame rate. GPU time is also close to the frame time; it includes time the GPU waits for the CPU, so whether the GPU is also at its limit cannot be told."
                        : "Frame time is high and the render thread is busy for almost the whole frame while the GPU is not, so issuing draw calls (the number of objects, materials and shadow casters drawn) limits the frame rate, not GPU rendering.");
                return;
            }

            if (waitsForPresent)
            {
                if (renderKnown && Available(gpu) && !gpuBusy && cpuMain.Value - presentWait.Value < busy)
                {
                    // Nothing is busy, yet the main thread waits: a frame-rate cap paces the frames.
                    var capped = Available(frameCap) && frameCap.Value > 0;
                    var ids = new List<string> { "frame.p95.ms", "present.wait.ms", "gpu.frame.ms", "cpu.render.ms" };
                    if (capped) ids.Add("display.frame.cap");
                    Add(BottleneckCategory.PresentLimited, capped ? AdvisorConfidence.High : AdvisorConfidence.Medium, ids,
                        "The main thread waits for present while the GPU and the render thread have time to spare, so a frame-rate cap (VSync, a target frame rate or a driver limit) sets the frame time. Lowering graphics settings will not raise the frame rate.");
                    return;
                }

                var gpuIds = new List<string> { "frame.p95.ms", "present.wait.ms", "gpu.frame.ms" };
                if (renderKnown) gpuIds.Add("cpu.render.ms");
                // High only when the render thread is known not to be the limit and the GPU is busy; without GPU time a
                // frame-rate cap is not ruled out either.
                Add(BottleneckCategory.RenderingGpu, renderKnown && gpuBusy ? AdvisorConfidence.High : AdvisorConfidence.Medium, gpuIds,
                    renderKnown
                        ? "Frame time is high and the main thread waits for the GPU to present each frame, so rendering on the GPU limits the frame rate."
                        : "Frame time is high and the main thread waits for present. Render-thread time was not measured, so a render thread busy issuing draw calls cannot be ruled out.");
                return;
            }

            if (cpuMain.Value >= busy)
            {
                Add(BottleneckCategory.MainThreadCpu, AdvisorConfidence.High,
                    new[] { "frame.p95.ms", "cpu.main.ms", "present.wait.ms" },
                    "Frame time is high and the main thread is busy for almost the whole frame without waiting for the GPU, so CPU work on the main thread (including waiting for worker jobs) limits the frame rate. GPU time is not used here because it also counts time the GPU waits for the CPU.");
                return;
            }

            var mixedIds = new List<string> { "frame.p95.ms", "cpu.main.ms", "present.wait.ms" };
            if (renderKnown) mixedIds.Add("cpu.render.ms");
            if (Available(gpu)) mixedIds.Add("gpu.frame.ms");
            result.Add(new BottleneckObservation(BottleneckCategory.Unknown, severity, AdvisorConfidence.Low, mixedIds,
                "Frame time is high, but neither the GPU nor a single CPU thread accounts for most of it; the load is mixed or changed during the capture."));
        }

        internal const string IntermittentSuffix =
            " Only some frames are slow; the thread timings describe typical frames, so they may not explain the slow ones.";

        private static void ClassifySimulationSlowdown(AdvisorEvidenceSnapshot evidence, NamedMetricValue efficiency, string scope,
            BottleneckSeverity severity, bool overheadHigh, List<BottleneckObservation> result, string lowEfficiencyRationale,
            string? additionalEvidenceId = null)
        {
            var ceiling = evidence.Find("simulation." + scope + "frame.ceiling");
            var capShare = evidence.Find("simulation." + scope + "render.cap.share");
            var ceilingValue = Available(ceiling) ? ceiling.Value : null;
            var ceilingExplains = FrameRateLimit.CeilingExplains(efficiency.Value!.Value, ceilingValue);
            var capExplains = FrameRateLimit.RenderCapExplains(ceilingValue, Available(capShare) ? capShare.Value : null);
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
