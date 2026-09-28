using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Profiling;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class DeepCaptureControllerTests
{
    [Test]
    public void Observe_finalizes_when_time_jump_skips_over_postbuffer()
    {
        var backend = new FakeBackend(new RecorderDescriptor("cpu", "CPU", "Main Thread", "TimeNanoseconds", "Int64"));
        using var manager = new RecorderManager(backend);
        var stateMachine = CreateStateMachine();
        using var controller = new DeepCaptureController(manager, stateMachine, maxConcurrent: 8);
        controller.Initialize();

        controller.RequestManualCapture(0);
        Assert.That(controller.CurrentSession, Is.Not.Null);

        controller.Observe(3, global: null);

        Assert.Multiple(() =>
        {
            Assert.That(controller.State, Is.EqualTo(CaptureState.Cooldown));
            Assert.That(controller.CurrentSession, Is.Null);
            Assert.That(controller.CompletedSessions.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void Automatic_capture_does_not_duplicate_trigger_sample_from_prebuffer()
    {
        using var controller = CreateController(maxConcurrent: 8);
        var before = CreateGlobalSample(0);
        var trigger = CreateGlobalSample(2);

        controller.Observe(0, before, new[] { before });
        controller.Observe(2, trigger, new[] { before, trigger });

        Assert.That(
            controller.CurrentSession!.GlobalSamples.Select(sample => sample.TimestampSeconds).ToArray(),
            Is.EqualTo(new[] { 0d, 2d }));
    }

    [Test]
    public void Zero_count_profiler_read_does_not_create_marker_sample_or_coverage()
    {
        var descriptor = new RecorderDescriptor("cpu", "CPU", "Simulation Example.System", "TimeNanoseconds", "Int64");
        var backend = new FakeBackend(new RecorderReading(0d, 0), descriptor);
        using var manager = new RecorderManager(backend);
        using var controller = new DeepCaptureController(manager, CreateStateMachine(), maxConcurrent: 8);
        controller.Initialize();
        controller.RequestManualCapture(0);

        controller.Observe(0.5, global: null);

        Assert.Multiple(() =>
        {
            Assert.That(controller.CurrentSession, Is.Not.Null);
            Assert.That(controller.CurrentSession!.MarkerCoverage.Captured, Is.Zero);
            Assert.That(controller.CurrentSession.TryGetMarkerSamples(descriptor.Id, out var samples), Is.False);
            Assert.That(samples, Is.Empty);
        });
    }

    [Test]
    public void Coverage_distinguishes_attempted_activated_and_sampled_markers()
    {
        var active = new RecorderDescriptor("active", "CPU", "Active", "TimeNanoseconds", "Int64");
        var failing = new RecorderDescriptor("failing", "CPU", "Failing", "TimeNanoseconds", "Int64");
        using var manager = new RecorderManager(new SelectiveBackend(active, failing));
        using var controller = new DeepCaptureController(manager, CreateStateMachine(), maxConcurrent: 8);
        controller.Initialize();

        controller.RequestManualCapture(0);
        controller.Observe(0.5, global: null);

        var coverage = controller.CurrentSession!.MarkerCoverage;
        Assert.Multiple(() =>
        {
            Assert.That(coverage.Discovered, Is.EqualTo(2));
            Assert.That(coverage.Attempted, Is.EqualTo(2));
            Assert.That(coverage.Activated, Is.EqualTo(1));
            Assert.That(coverage.Sampled, Is.EqualTo(1));
            Assert.That(coverage.Captured, Is.EqualTo(1));
            Assert.That(coverage.AttemptedRatio, Is.EqualTo(1d));
            Assert.That(coverage.ActivatedRatio, Is.EqualTo(0.5d));
            Assert.That(coverage.SampledRatio, Is.EqualTo(0.5d));
        });
    }

    [Test]
    public void Interrupt_active_capture_finalizes_partial_session_and_returns_to_monitoring()
    {
        var descriptor = new RecorderDescriptor("cpu", "CPU", "Simulation Example.System", "TimeNanoseconds", "Int64");
        using var manager = new RecorderManager(new FakeBackend(descriptor));
        using var controller = new DeepCaptureController(manager, CreateStateMachine(), maxConcurrent: 8);
        controller.Initialize();
        controller.RequestManualCapture(0);
        controller.Observe(0.5, global: null);

        Assert.That(manager.ActiveIds, Is.Not.Empty);

        controller.InterruptActiveCapture("Monitoring disabled during Deep Capture.");

        Assert.Multiple(() =>
        {
            Assert.That(controller.State, Is.EqualTo(CaptureState.Monitoring));
            Assert.That(controller.CurrentSession, Is.Null);
            Assert.That(controller.CompletedSessions.Count, Is.EqualTo(1));
            Assert.That(controller.CompletedSessions[0].Warnings, Does.Contain("Monitoring disabled during Deep Capture."));
            Assert.That(manager.ActiveIds, Is.Empty);
        });
    }

    [Test]
    public void Overhead_outside_a_capture_does_not_degrade_batching()
    {
        using var controller = CreateController(maxConcurrent: 8);

        controller.ReportProfilerOverheadShare(0.50);
        controller.ReportProfilerOverheadShare(0.50);
        controller.ReportProfilerOverheadShare(0.50);

        Assert.Multiple(() =>
        {
            Assert.That(controller.CurrentBatchSize, Is.EqualTo(8));
            Assert.That(controller.SamplingStride, Is.EqualTo(1));
        });
    }

    [Test]
    public void Single_overhead_spike_does_not_degrade_active_capture()
    {
        using var controller = CreateController(maxConcurrent: 8);
        controller.RequestManualCapture(0);

        controller.ReportProfilerOverheadShare(0.50);

        Assert.That(controller.CurrentBatchSize, Is.EqualTo(8));
    }

    [Test]
    public void Consecutive_overhead_spikes_degrade_active_capture_once()
    {
        using var controller = CreateController(maxConcurrent: 8);
        controller.RequestManualCapture(0);

        controller.ReportProfilerOverheadShare(0.50);
        controller.ReportProfilerOverheadShare(0.50);
        controller.ReportProfilerOverheadShare(0.50);

        Assert.That(controller.CurrentBatchSize, Is.EqualTo(4));
    }

    [Test]
    public void New_capture_restores_configured_batch_size_and_sampling_stride()
    {
        using var controller = CreateController(maxConcurrent: 8);
        controller.RequestManualCapture(0);

        for (var i = 0; i < 12; i++)
            controller.ReportProfilerOverheadShare(0.50);

        Assert.That(controller.CurrentBatchSize, Is.LessThan(8));
        controller.Observe(3, global: null);
        controller.RequestManualCapture(4);

        Assert.Multiple(() =>
        {
            Assert.That(controller.CurrentSession, Is.Not.Null);
            Assert.That(controller.CurrentBatchSize, Is.EqualTo(8));
            Assert.That(controller.SamplingStride, Is.EqualTo(1));
        });
    }

    [Test]
    public void Completed_capture_history_keeps_only_the_newest_sessions()
    {
        using var controller = CreateController(maxConcurrent: 2, maxCompletedSessions: 2);

        controller.RequestManualCapture(0);
        var first = controller.CurrentSession;
        controller.Observe(3, global: null);

        controller.RequestManualCapture(4);
        var second = controller.CurrentSession;
        controller.Observe(7, global: null);

        controller.RequestManualCapture(8);
        var third = controller.CurrentSession;
        controller.Observe(11, global: null);

        Assert.Multiple(() =>
        {
            Assert.That(controller.CompletedSessions.Count, Is.EqualTo(2));
            Assert.That(controller.CompletedSessions, Does.Not.Contain(first));
            Assert.That(controller.CompletedSessions[0], Is.SameAs(second));
            Assert.That(controller.CompletedSessions[1], Is.SameAs(third));
        });
    }

    private static DeepCaptureController CreateController(int maxConcurrent, int maxCompletedSessions = 20)
    {
        var descriptors = Enumerable.Range(0, maxConcurrent)
            .Select(index => new RecorderDescriptor($"marker-{index}", "CPU", $"Marker {index}", "TimeNanoseconds", "Int64"))
            .ToArray();
        var manager = new RecorderManager(new FakeBackend(descriptors));
        var controller = new DeepCaptureController(
            manager,
            CreateStateMachine(),
            maxConcurrent: maxConcurrent,
            overheadCeiling: 0.08,
            maxCompletedSessions: maxCompletedSessions);
        controller.Initialize();
        return controller;
    }

    private static DeepCaptureStateMachine CreateStateMachine() => new(
        efficiencyThreshold: 0.8,
        sustainSeconds: 2,
        deepSeconds: 1,
        postSeconds: 1,
        cooldownSeconds: 100);

    private static GlobalMetricsSnapshot CreateGlobalSample(double timestampSeconds)
        => new(timestampSeconds, 4d, 1d, new Dictionary<string, RecorderReading>());

    private sealed class FakeBackend : IRecorderBackend
    {
        private readonly IReadOnlyList<RecorderDescriptor> _descriptors;
        private readonly RecorderReading _reading;

        public FakeBackend(params RecorderDescriptor[] descriptors)
            : this(new RecorderReading(1d, 1), descriptors)
        {
        }

        public FakeBackend(RecorderReading reading, params RecorderDescriptor[] descriptors)
        {
            _reading = reading;
            _descriptors = descriptors;
        }

        public IReadOnlyList<RecorderDescriptor> Discover() => _descriptors;

        public IActiveRecorder Start(RecorderDescriptor descriptor, int capacity) => new FakeRecorder(descriptor.Id, _reading);
    }

    private sealed class SelectiveBackend : IRecorderBackend
    {
        private readonly RecorderDescriptor _active;
        private readonly RecorderDescriptor _failing;

        public SelectiveBackend(RecorderDescriptor active, RecorderDescriptor failing)
        {
            _active = active;
            _failing = failing;
        }

        public IReadOnlyList<RecorderDescriptor> Discover() => new[] { _active, _failing };

        public IActiveRecorder Start(RecorderDescriptor descriptor, int capacity)
        {
            if (descriptor.Id == _failing.Id)
                throw new InvalidOperationException("activation failed");
            return new FakeRecorder(descriptor.Id, new RecorderReading(1d, 1));
        }
    }

    private sealed class FakeRecorder : IActiveRecorder
    {
        private readonly RecorderReading _reading;

        public FakeRecorder(string id, RecorderReading reading)
        {
            Id = id;
            _reading = reading;
        }

        public string Id { get; }
        public RecorderReading Read() => _reading;
        public void Dispose() { }
    }
}
