using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.Advisor;
using CS2RuntimeAssetAuditor.Core.Frames;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

// #29 and #30, calibrated on the 2026-09-29 retail log: below 30 fps the per-frame step cap, not simulation CPU,
// limited the speed, and FrameTimingManager showed a main-thread-bound frame while GPU time tracked frame time.
public class FrameRateBottleneckTests
{
    [Test]
    public void Main_thread_bound_frames_that_cap_the_simulation_are_not_reported_as_gpu_or_simulation_load()
    {
        var result = Classify(
            Available("frame.p95.ms", 36), Available("frame.median.ms", 27), Available("cpu.main.ms", 27),
            Available("present.wait.ms", 0.002), Available("gpu.frame.ms", 26.9),
            Available("simulation.efficiency", 0.75), Available("simulation.frame.ceiling", 0.72),
            Available("simulation.render.cap.share", 0.9), Available("simulation.preference.limits.steps", 0));

        Assert.Multiple(() =>
        {
            Assert.That(result.Select(x => x.Category), Is.EquivalentTo(new[] { BottleneckCategory.MainThreadCpu, BottleneckCategory.FrameRateLimit }));
            var main = result.Single(x => x.Category == BottleneckCategory.MainThreadCpu);
            Assert.That(main.Confidence, Is.EqualTo(AdvisorConfidence.High));
            Assert.That(main.EvidenceIds, Is.EqualTo(new[] { "frame.p95.ms", "cpu.main.ms", "present.wait.ms" }));
            var limit = result.Single(x => x.Category == BottleneckCategory.FrameRateLimit);
            Assert.That(limit.Severity, Is.EqualTo(BottleneckSeverity.High));
            Assert.That(limit.Confidence, Is.EqualTo(AdvisorConfidence.High));
            Assert.That(limit.EvidenceIds, Is.EqualTo(new[] { "simulation.efficiency", "simulation.frame.ceiling", "simulation.render.cap.share" }));
        });
    }

    [Test]
    public void Waiting_for_present_identifies_a_gpu_bound_frame()
    {
        var result = Classify(
            Available("frame.p95.ms", 34), Available("frame.median.ms", 30), Available("cpu.main.ms", 29),
            Available("present.wait.ms", 12), Available("gpu.frame.ms", 29.5), Available("simulation.efficiency", 1));

        var gpu = result.Single();
        Assert.Multiple(() =>
        {
            Assert.That(gpu.Category, Is.EqualTo(BottleneckCategory.RenderingGpu));
            Assert.That(gpu.Confidence, Is.EqualTo(AdvisorConfidence.High));
            Assert.That(gpu.EvidenceIds, Does.Contain("present.wait.ms"));
        });
    }

    [Test]
    public void Main_thread_classification_does_not_recommend_lowering_graphics_quality()
    {
        var observations = Classify(
            Available("frame.p95.ms", 36), Available("frame.median.ms", 27), Available("cpu.main.ms", 27),
            Available("present.wait.ms", 0), Available("gpu.frame.ms", 26.9));

        Assert.That(observations.Any(x => x.Category == BottleneckCategory.RenderingGpu), Is.False);
    }

    [Test]
    public void Low_efficiency_well_below_the_frame_rate_ceiling_stays_simulation_cpu()
    {
        var result = Classify(
            Available("simulation.efficiency", 0.6), Available("simulation.frame.ceiling", 1),
            Available("simulation.render.cap.share", 0.1), Available("simulation.preference.limits.steps", 0),
            Available("pathfinding.low.lead.share", 0));

        var simulation = result.Single();
        Assert.Multiple(() =>
        {
            Assert.That(simulation.Category, Is.EqualTo(BottleneckCategory.SimulationCpu));
            Assert.That(simulation.Confidence, Is.EqualTo(AdvisorConfidence.High));
        });
    }

    [Test]
    public void Performance_preference_that_limits_steps_lowers_simulation_confidence()
    {
        var result = Classify(
            Available("simulation.efficiency", 0.6), Available("simulation.frame.ceiling", 1),
            Available("simulation.render.cap.share", 0.1), Available("simulation.preference.limits.steps", 1));

        var simulation = result.Single();
        Assert.Multiple(() =>
        {
            Assert.That(simulation.Category, Is.EqualTo(BottleneckCategory.SimulationCpu));
            Assert.That(simulation.Confidence, Is.EqualTo(AdvisorConfidence.Medium));
            Assert.That(simulation.EvidenceIds, Does.Contain("simulation.preference.limits.steps"));
            Assert.That(simulation.Rationale, Does.Contain("Performance Preference"));
        });
    }

    [Test]
    public void Frequent_low_pathfinding_lead_points_to_pathfinding()
    {
        var result = Classify(
            Available("simulation.efficiency", 0.6), Available("simulation.frame.ceiling", 1),
            Available("simulation.render.cap.share", 0), Available("pathfinding.low.lead.share", 0.4));

        Assert.That(result.Single().Category, Is.EqualTo(BottleneckCategory.Pathfinding));
    }

    [Test]
    public void Trigger_only_slowdown_uses_the_samples_before_the_trigger()
    {
        var result = Classify(
            Available("simulation.efficiency", 0.93), Available("simulation.trigger.efficiency", 0.7),
            Available("simulation.trigger.frame.ceiling", 0.7), Available("simulation.trigger.render.cap.share", 0.95),
            Available("simulation.frame.ceiling", 1));

        var limit = result.Single();
        Assert.Multiple(() =>
        {
            Assert.That(limit.Category, Is.EqualTo(BottleneckCategory.FrameRateLimit));
            Assert.That(limit.Severity, Is.EqualTo(BottleneckSeverity.Medium));
            Assert.That(limit.EvidenceIds, Is.EqualTo(new[] { "simulation.trigger.efficiency", "simulation.efficiency",
                "simulation.trigger.frame.ceiling", "simulation.trigger.render.cap.share" }));
        });
    }

    [Test]
    public void Projector_uses_the_mods_own_frame_measurements()
    {
        var capture = new CaptureSession("capture", new CaptureTrigger(CaptureTriggerKind.AutomaticLowEfficiency, 10, 0.7), 16);
        capture.AddGlobalSample(Sample(8, fps: 22, efficiency: 0.72, capShare: 1));   // Before the trigger.
        capture.AddGlobalSample(Sample(10, fps: 24, efficiency: 0.78, capShare: 0.9));
        capture.AddGlobalSample(Sample(10.5, fps: 26, efficiency: 0.8, capShare: 0.8));

        var evidence = new CaptureAdvisorEvidenceProjector().Project(capture);

        Assert.Multiple(() =>
        {
            Assert.That(evidence.Find("frame.p95.ms").Value, Is.EqualTo(1000d / 24 * 1.3).Within(1e-9));
            Assert.That(evidence.Find("frame.median.ms").Value, Is.EqualTo(1000d / 24).Within(1e-9));
            Assert.That(evidence.Find("cpu.main.ms").Value, Is.EqualTo(1000d / 24).Within(1e-9));
            Assert.That(evidence.Find("present.wait.ms").Value, Is.EqualTo(0.01));
            Assert.That(evidence.Find("gpu.frame.ms").Value, Is.EqualTo(1000d / 24 - 1).Within(1e-9));
            Assert.That(evidence.Find("simulation.frame.ceiling").Value, Is.EqualTo((24d / 30 + 26d / 30) / 2).Within(1e-9));
            Assert.That(evidence.Find("simulation.render.cap.share").Value, Is.EqualTo((0.9 * 20 + 0.8 * 20) / 40).Within(1e-9));
            Assert.That(evidence.Find("simulation.trigger.frame.ceiling").Value, Is.EqualTo(22d / 30).Within(1e-9));
            Assert.That(evidence.Find("simulation.step.ms").Value, Is.EqualTo(1.2));
            Assert.That(evidence.Find("simulation.preference.limits.steps").Value, Is.EqualTo(0));
            Assert.That(evidence.Find("pathfinding.low.lead.share").Value, Is.EqualTo(0));
        });

        var diagnosis = new BottleneckClassifier().Classify(evidence);
        Assert.That(diagnosis.Select(x => x.Category), Is.EquivalentTo(new[] { BottleneckCategory.MainThreadCpu, BottleneckCategory.FrameRateLimit }));
    }

    [Test]
    public void Projector_falls_back_to_the_retail_total_frame_time_recorder()
    {
        var capture = new CaptureSession("capture", new CaptureTrigger(CaptureTriggerKind.Manual, 1, null), 8);
        capture.AddGlobalSample(new GlobalMetricsSnapshot(1, 1, 1,
            new Dictionary<string, RecorderReading> { ["Render\u001fCPU Total Frame Time"] = new RecorderReading(25_000_000, 1) },
            new Dictionary<string, string> { ["Render\u001fCPU Total Frame Time"] = "TimeNanoseconds" }));

        var evidence = new CaptureAdvisorEvidenceProjector().Project(capture);

        Assert.That(evidence.Find("frame.p95.ms").Value, Is.EqualTo(25d).Within(1e-9));
        Assert.That(evidence.Find("frame.median.ms").Availability, Is.EqualTo(MetricAvailability.Unavailable));
    }

    [TestCase(1d, 60d, 1d)]
    [TestCase(1d, 20d, 2d / 3d)]
    [TestCase(4d, 24d, 0.8)]
    [TestCase(2d, 15d, 0.5)]
    [TestCase(0.5d, 20d, 2d / 3d)]
    public void Frame_rate_ceiling_follows_the_step_cap(double speed, double fps, double expected)
    {
        Assert.That(SimulationStepLimits.FrameRateEfficiencyCeiling(speed, fps), Is.EqualTo(expected).Within(1e-9));
    }

    [Test]
    public void Frame_rate_ceiling_is_absent_while_paused_or_unmeasured()
    {
        Assert.That(SimulationStepLimits.FrameRateEfficiencyCeiling(0, 60), Is.Null);
        Assert.That(SimulationStepLimits.FrameRateEfficiencyCeiling(1, 0), Is.Null);
    }

    [Test]
    public void Automatic_capture_does_not_start_when_the_frame_rate_explains_the_slowdown()
    {
        var machine = new DeepCaptureStateMachine(0.8, 2, 10, 5, 30);
        for (var t = 0d; t <= 5; t += 0.5)
            machine.Observe(t, 1, 0.6, automaticTriggerAllowed: true, slowdownExplainedByFrameRate: true);
        Assert.Multiple(() =>
        {
            Assert.That(machine.State, Is.EqualTo(CaptureState.Monitoring));
            Assert.That(machine.FrameRateSkips, Is.EqualTo(1), "One run of skipped samples counts once.");
            Assert.That(machine.LastFrameRateSkipEfficiency, Is.EqualTo(0.6).Within(1e-9));
        });

        machine.Observe(5.5, 1, 1, automaticTriggerAllowed: true, slowdownExplainedByFrameRate: false);
        machine.Observe(6, 1, 0.6, automaticTriggerAllowed: true, slowdownExplainedByFrameRate: true);
        Assert.That(machine.FrameRateSkips, Is.EqualTo(2), "A new run after recovery counts again.");

        // A slowdown the frame rate does not explain still starts a capture after the sustain time.
        machine.Observe(6.5, 1, 0.6, automaticTriggerAllowed: true, slowdownExplainedByFrameRate: false);
        machine.Observe(8.5, 1, 0.6, automaticTriggerAllowed: true, slowdownExplainedByFrameRate: false);
        Assert.That(machine.State, Is.EqualTo(CaptureState.DeepCapture));
    }

    [Test]
    public void Capture_controller_skips_the_automatic_capture_for_a_frame_rate_limited_sample()
    {
        using var limited = CreateController();
        // 20 fps at 1x: the ceiling is 2/3 and the efficiency of 0.62 reaches most of it.
        for (var t = 0d; t <= 4; t += 0.5)
            limited.Observe(t, Sample(t, 20, 0.62, 1.0), automaticTriggerAllowed: true, prebuffer: null);
        Assert.That(limited.State, Is.EqualTo(CaptureState.Monitoring));
        Assert.That(limited.FrameRateSkips, Is.EqualTo(1));

        using var slow = CreateController();
        // 60 fps: the frame rate allows full speed, so the slowdown is the simulation's own.
        for (var t = 0d; t <= 4; t += 0.5)
            slow.Observe(t, Sample(t, 60, 0.62, 0.2), automaticTriggerAllowed: true, prebuffer: null);
        Assert.That(slow.State, Is.EqualTo(CaptureState.DeepCapture));
        Assert.That(slow.FrameRateSkips, Is.EqualTo(0));
    }

    private static CS2RuntimeAssetAuditor.Profiling.DeepCaptureController CreateController()
    {
        var descriptors = Enumerable.Range(0, 4)
            .Select(index => new RecorderDescriptor($"marker-{index}", "CPU", $"Marker {index}", "TimeNanoseconds", "Int64"))
            .ToArray();
        var manager = new CS2RuntimeAssetAuditor.Profiling.RecorderManager(new NullBackend(descriptors));
        var controller = new CS2RuntimeAssetAuditor.Profiling.DeepCaptureController(manager, new DeepCaptureStateMachine(0.8, 2, 10, 5, 30), maxConcurrent: 4, overheadCeiling: 0.08);
        controller.Initialize();
        return controller;
    }

    private static GlobalMetricsSnapshot Sample(double timestamp, double fps, double efficiency, double capShare)
    {
        var frameMs = 1000d / fps;
        var interval = new RuntimeInterval
        {
            IntervalSeconds = 20 / fps,
            Frames = 20,
            FrameMs = new SampleSummary(20, frameMs, frameMs * 1.3, frameMs * 2),
            FrameTimingSamples = 20,
            CpuMainThreadMs = new SampleSummary(20, frameMs, frameMs, frameMs),
            PresentWaitMs = new SampleSummary(20, 0.01, 0.02, 0.1),
            GpuMs = new SampleSummary(20, frameMs - 1, frameMs, frameMs),
            SelectedSpeed = 1,
            RunningFrames = 20,
            SimulationStepCountedFrames = 20,
            SimulationFramesAtRenderCap = (int)Math.Round(capShare * 20),
            SimulationStepMs = new SampleSummary(20, 1.2, 1.5, 2),
            PerformancePreference = "SimulationSpeed",
            FrameRateEfficiencyCeiling = SimulationStepLimits.FrameRateEfficiencyCeiling(1, fps)
        };
        return new GlobalMetricsSnapshot(timestamp, 1, efficiency, null, null, interval);
    }

    private static NamedMetricValue Available(string id, double value) => NamedMetricValue.Available(id, value, MetricConfidence.Full);

    private static IReadOnlyList<BottleneckObservation> Classify(params NamedMetricValue[] metrics)
        => new BottleneckClassifier().Classify(new AdvisorEvidenceSnapshot(DateTime.UtcNow, metrics));
}

internal sealed class NullBackend : CS2RuntimeAssetAuditor.Profiling.IRecorderBackend
{
    private readonly IReadOnlyList<RecorderDescriptor> _descriptors;
    public NullBackend(IReadOnlyList<RecorderDescriptor> descriptors) => _descriptors = descriptors;
    public IReadOnlyList<RecorderDescriptor> Discover() => _descriptors;
    public CS2RuntimeAssetAuditor.Profiling.IActiveRecorder Start(RecorderDescriptor descriptor, int capacity) => new NullRecorder(descriptor.Id);

    private sealed class NullRecorder : CS2RuntimeAssetAuditor.Profiling.IActiveRecorder
    {
        public NullRecorder(string id) => Id = id;
        public string Id { get; }
        public RecorderReading Read() => new(1d, 1);
        public void Dispose() { }
    }
}
