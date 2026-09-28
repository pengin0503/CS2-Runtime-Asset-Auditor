using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeProfiler.Core;
using CS2RuntimeProfiler.Core.Advisor;
using CS2RuntimeProfiler.Profiling;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class Issue15To22RegressionTests
{
    private const string ProfilerUsedMemory = "Memory\u001fProfiler Used Memory";
    private const double MiB = 1024d * 1024d;

    [TestCase(false, false, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, true, false)]
    public void Automatic_capture_policy_blocks_loading_and_paused_states(bool loading, bool paused, bool expected)
        => Assert.That(AutomaticCapturePolicy.IsAllowed(loading, paused), Is.EqualTo(expected));

    [Test]
    public void Runtime_pause_reader_accepts_the_pause_members_present_in_the_game_runtime()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RuntimePauseStateReader.TryRead(new PauseMethodRuntime(true), out var methodPaused), Is.True);
            Assert.That(methodPaused, Is.True);
            Assert.That(RuntimePauseStateReader.TryRead(new PauseFieldRuntime(true), out var fieldPaused), Is.True);
            Assert.That(fieldPaused, Is.True);
            Assert.That(RuntimePauseStateReader.TryRead(new PausePropertyRuntime(false), out var propertyPaused), Is.True);
            Assert.That(propertyPaused, Is.False);
            Assert.That(RuntimePauseStateReader.TryRead(new object(), out _), Is.False);
        });
    }

    [Test]
    public void Persistent_overhead_breaches_eventually_finalize_the_capture_instead_of_degrading_forever()
    {
        using var controller = CreateController();
        controller.RequestManualCapture(0);

        for (var i = 0; i < 15; i++)
            controller.ReportProfilerOverheadShare(0.50);

        Assert.Multiple(() =>
        {
            Assert.That(controller.CurrentSession, Is.Null);
            Assert.That(controller.CompletedSessions.Count, Is.EqualTo(1));
            Assert.That(controller.CompletedSessions[0].Warnings.Any(w => w.Contains("overhead", StringComparison.OrdinalIgnoreCase)), Is.True);
        });
    }

    [Test]
    public void Profiler_memory_pressure_degrades_at_multiple_steps_and_aborts_at_512_mib_growth()
    {
        using var controller = CreateController();
        controller.RequestManualCapture(0, new[] { Global(0, 100 * MiB) });

        controller.Observe(0.25, Global(0.25, 230 * MiB));
        Assert.That(controller.CurrentBatchSize, Is.EqualTo(4));
        controller.Observe(0.50, Global(0.50, 360 * MiB));
        Assert.That(controller.CurrentBatchSize, Is.EqualTo(2));
        controller.Observe(0.75, Global(0.75, 490 * MiB));
        Assert.That(controller.CurrentBatchSize, Is.EqualTo(1));
        controller.Observe(1.00, Global(1.00, 620 * MiB));

        Assert.Multiple(() =>
        {
            Assert.That(controller.CurrentSession, Is.Null);
            Assert.That(controller.CompletedSessions.Count, Is.EqualTo(1));
            Assert.That(controller.CompletedSessions[0].ProfilerMemoryDeltaBytes, Is.GreaterThanOrEqualTo(512 * MiB));
            Assert.That(controller.CompletedSessions[0].Warnings.Any(w => w.Contains("memory", StringComparison.OrdinalIgnoreCase)), Is.True);
        });
    }

    [Test]
    public void Monotonically_rising_profiler_memory_baselines_emit_a_retention_pressure_warning_without_calling_it_a_leak()
    {
        using var controller = CreateController();
        var baselinesMiB = new[] { 100d, 200d, 300d, 400d };

        for (var i = 0; i < baselinesMiB.Length; i++)
        {
            var now = i * 10d;
            controller.RequestManualCapture(now, new[] { Global(now, baselinesMiB[i] * MiB) });
            controller.InterruptActiveCapture("test completion");
        }

        var latest = controller.CompletedSessions.Last();
        Assert.Multiple(() =>
        {
            Assert.That(controller.CompletedSessions.Count, Is.EqualTo(4));
            Assert.That(latest.Warnings, Has.Some.Contains("retention pressure"));
            Assert.That(latest.Warnings, Has.Some.Contains("not proof of a memory leak"));
        });
    }

    [TestCase(true, null, true)]
    [TestCase(true, "", true)]
    [TestCase(true, "Simulation Exact.Runtime.System", false)]
    [TestCase(false, null, false)]
    public void Strict_full_type_marker_fallback_is_only_allowed_for_verified_live_systems_without_runtime_marker_identity(
        bool systemIsLive, string? profilerMarkerName, bool expected)
        => Assert.That(RuntimeMarkerIdentityPolicy.AllowStrictFullTypeFallback(systemIsLive, profilerMarkerName), Is.EqualTo(expected));

    [Test]
    public void Advisor_uses_post_trigger_window_median_and_preserves_trigger_efficiency()
    {
        var trigger = new CaptureTrigger(CaptureTriggerKind.AutomaticLowEfficiency, 2d, 0.70d);
        var capture = new CaptureSession("advisor-window", trigger, 16);
        var triggerSample = GlobalEfficiency(2d, 0.70d);
        capture.SetTriggerSnapshot(triggerSample);
        capture.AddGlobalSample(GlobalEfficiency(0d, 0.20d)); // prebuffer must not dominate diagnosis
        capture.AddGlobalSample(triggerSample);
        capture.AddGlobalSample(GlobalEfficiency(3d, 0.95d));
        capture.AddGlobalSample(GlobalEfficiency(4d, 0.95d));

        var evidence = new CaptureAdvisorEvidenceProjector().Project(capture);

        Assert.Multiple(() =>
        {
            Assert.That(evidence.Find("simulation.efficiency").Value, Is.EqualTo(0.95d).Within(0.0001));
            Assert.That(evidence.Find("simulation.trigger.efficiency").Value, Is.EqualTo(0.70d).Within(0.0001));
        });
    }

    [Test]
    public void Advisor_terminal_only_low_sample_does_not_turn_a_healthy_window_into_high_cpu_pressure()
    {
        var capture = new CaptureSession("advisor-terminal", new CaptureTrigger(CaptureTriggerKind.Manual, 1d, null), 16);
        capture.AddGlobalSample(GlobalEfficiency(1d, 0.96d));
        capture.AddGlobalSample(GlobalEfficiency(2d, 0.95d));
        capture.AddGlobalSample(GlobalEfficiency(3d, 0.20d));

        var evidence = new CaptureAdvisorEvidenceProjector().Project(capture);
        var observations = new BottleneckClassifier().Classify(evidence);

        Assert.That(evidence.Find("simulation.efficiency").Value, Is.EqualTo(0.95d).Within(0.0001));
        Assert.That(observations.Any(x => x.Category == BottleneckCategory.SimulationCpu && x.Severity == BottleneckSeverity.High), Is.False);
    }

    [Test]
    public void Advisor_low_trigger_with_recovered_window_is_medium_not_high_cpu_observation()
    {
        var evidence = new AdvisorEvidenceSnapshot(DateTime.UtcNow, new[]
        {
            NamedMetricValue.Available("simulation.efficiency", 0.95d, MetricConfidence.Full),
            NamedMetricValue.Available("simulation.trigger.efficiency", 0.70d, MetricConfidence.Full)
        });

        var observation = new BottleneckClassifier().Classify(evidence)
            .Single(x => x.Category == BottleneckCategory.SimulationCpu);

        Assert.Multiple(() =>
        {
            Assert.That(observation.Severity, Is.EqualTo(BottleneckSeverity.Medium));
            Assert.That(observation.Confidence, Is.EqualTo(AdvisorConfidence.Medium));
        });
    }

    [Test]
    public void Capture_completion_dispatch_writes_and_flushes_without_leaking_flush_failures()
    {
        var capture = new CaptureSession("capture-log-dispatch", new CaptureTrigger(CaptureTriggerKind.Manual, 1d, null), 8);
        string? message = null;
        var flushCalls = 0;

        Assert.DoesNotThrow(() => CaptureCompletionDiagnosticsDispatcher.Dispatch(
            capture,
            value => message = value,
            () => { flushCalls++; throw new InvalidOperationException("flush unavailable"); }));

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("capture=capture-log-dispatch"));
            Assert.That(flushCalls, Is.EqualTo(1));
        });
    }

    private static DeepCaptureController CreateController()
    {
        var descriptors = Enumerable.Range(0, 8)
            .Select(index => new RecorderDescriptor($"marker-{index}", "CPU", $"Marker {index}", "TimeNanoseconds", "Int64"))
            .ToArray();
        var manager = new RecorderManager(new Backend(descriptors));
        var stateMachine = new DeepCaptureStateMachine(0.8, 2, 100, 5, 30);
        var controller = new DeepCaptureController(manager, stateMachine, maxConcurrent: 8, overheadCeiling: 0.08);
        controller.Initialize();
        return controller;
    }

    private static GlobalMetricsSnapshot Global(double timestamp, double profilerBytes)
        => new(timestamp, 4d, 4d, new Dictionary<string, RecorderReading>
        {
            [ProfilerUsedMemory] = new RecorderReading(profilerBytes, 1)
        });

    private static GlobalMetricsSnapshot GlobalEfficiency(double timestamp, double efficiency)
        => new(timestamp, 4d, 4d * efficiency, new Dictionary<string, RecorderReading>());

    private sealed class PauseMethodRuntime
    {
        private readonly bool _paused;
        public PauseMethodRuntime(bool paused) => _paused = paused;
        public bool IsPaused() => _paused;
    }

    private sealed class PauseFieldRuntime
    {
        private readonly bool m_Paused;
        public PauseFieldRuntime(bool paused) => m_Paused = paused;
    }

    private sealed class PausePropertyRuntime
    {
        public PausePropertyRuntime(bool paused) => this.paused = paused;
        public bool paused { get; }
    }

    private sealed class Backend : IRecorderBackend
    {
        private readonly IReadOnlyList<RecorderDescriptor> _descriptors;
        public Backend(IReadOnlyList<RecorderDescriptor> descriptors) => _descriptors = descriptors;
        public IReadOnlyList<RecorderDescriptor> Discover() => _descriptors;
        public IActiveRecorder Start(RecorderDescriptor descriptor, int capacity) => new Recorder(descriptor.Id);
    }

    private sealed class Recorder : IActiveRecorder
    {
        public Recorder(string id) => Id = id;
        public string Id { get; }
        public RecorderReading Read() => new(1d, 1);
        public void Dispose() { }
    }
}
