using System.Collections.Generic;
using CS2RuntimeProfiler.Core;
using CS2RuntimeProfiler.Profiling;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class ProfilerMemoryGuardTests
{
    private const string ProfilerUsedMemory = "Memory\u001fProfiler Used Memory";
    private const double MiB = 1024d * 1024d;

    [Test]
    public void Missing_profiler_memory_metric_does_not_change_capture_load()
    {
        using var controller = CreateController();
        var baseline = Global(0, null);
        controller.RequestManualCapture(0, new[] { baseline });
        controller.Observe(0.5, Global(0.5, null));

        Assert.Multiple(() =>
        {
            Assert.That(controller.CurrentBatchSize, Is.EqualTo(8));
            Assert.That(controller.CurrentSession!.ProfilerMemoryBaselineBytes, Is.Null);
            Assert.That(controller.CurrentSession.ProfilerMemoryDeltaBytes, Is.Null);
        });
    }

    [Test]
    public void Profiler_memory_growth_below_threshold_is_recorded_without_degrading()
    {
        using var controller = CreateController();
        controller.RequestManualCapture(0, new[] { Global(0, 200 * MiB) });
        controller.Observe(0.5, Global(0.5, 300 * MiB));

        Assert.Multiple(() =>
        {
            Assert.That(controller.CurrentBatchSize, Is.EqualTo(8));
            Assert.That(controller.CurrentSession!.ProfilerMemoryBaselineBytes, Is.EqualTo(200 * MiB));
            Assert.That(controller.CurrentSession.ProfilerMemoryPeakBytes, Is.EqualTo(300 * MiB));
            Assert.That(controller.CurrentSession.ProfilerMemoryDeltaBytes, Is.EqualTo(100 * MiB));
            Assert.That(controller.CurrentSession.Warnings.Any(w => w.Contains("Profiler memory", StringComparison.OrdinalIgnoreCase)), Is.False);
        });
    }

    [Test]
    public void Profiler_memory_growth_at_threshold_degrades_once_and_warns()
    {
        using var controller = CreateController();
        controller.RequestManualCapture(0, new[] { Global(0, 200 * MiB) });
        controller.Observe(0.5, Global(0.5, 328 * MiB));
        controller.Observe(0.75, Global(0.75, 400 * MiB));

        Assert.Multiple(() =>
        {
            Assert.That(controller.CurrentBatchSize, Is.EqualTo(4));
            Assert.That(controller.CurrentSession!.ProfilerMemoryDeltaBytes, Is.EqualTo(200 * MiB));
            Assert.That(controller.CurrentSession.Warnings.Count(w => w.Contains("Profiler memory", StringComparison.OrdinalIgnoreCase)), Is.EqualTo(1));
        });
    }

    private static DeepCaptureController CreateController()
    {
        var descriptors = Enumerable.Range(0, 8)
            .Select(index => new RecorderDescriptor($"marker-{index}", "CPU", $"Marker {index}", "TimeNanoseconds", "Int64"))
            .ToArray();
        var manager = new RecorderManager(new Backend(descriptors));
        var stateMachine = new DeepCaptureStateMachine(0.8, 2, 10, 5, 30);
        var controller = new DeepCaptureController(manager, stateMachine, maxConcurrent: 8, overheadCeiling: 0.08);
        controller.Initialize();
        return controller;
    }

    private static GlobalMetricsSnapshot Global(double timestamp, double? profilerBytes)
    {
        var readings = new Dictionary<string, RecorderReading>();
        if (profilerBytes.HasValue)
            readings[ProfilerUsedMemory] = new RecorderReading(profilerBytes.Value, 1);
        return new GlobalMetricsSnapshot(timestamp, 4, 4, readings);
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
