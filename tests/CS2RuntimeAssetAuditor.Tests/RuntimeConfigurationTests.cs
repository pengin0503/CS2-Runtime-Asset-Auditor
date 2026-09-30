using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Profiling;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class RuntimeConfigurationTests
{
    [Test]
    public void Automatic_capture_can_be_disabled_without_blocking_manual_capture()
    {
        var machine = DeepCaptureStateMachine.CreateDefault();
        machine.Configure(
            efficiencyThreshold: 0.80,
            sustainSeconds: 2,
            deepSeconds: 10,
            postSeconds: 5,
            cooldownSeconds: 30,
            automaticCaptureEnabled: false);

        machine.Observe(0, selectedSpeed: 4, actualSpeed: 2);
        machine.Observe(10, selectedSpeed: 4, actualSpeed: 2);
        Assert.That(machine.State, Is.EqualTo(CaptureState.Monitoring));

        machine.RequestManualCapture(11);
        Assert.That(machine.State, Is.EqualTo(CaptureState.DeepCapture));
    }

    [Test]
    public void Runtime_duration_configuration_changes_capture_state_timing()
    {
        var machine = DeepCaptureStateMachine.CreateDefault();
        machine.Configure(
            efficiencyThreshold: 0.75,
            sustainSeconds: 1,
            deepSeconds: 3,
            postSeconds: 2,
            cooldownSeconds: 4,
            automaticCaptureEnabled: true);

        machine.RequestManualCapture(0);
        machine.Observe(3, 4, 4);
        Assert.That(machine.State, Is.EqualTo(CaptureState.PostBuffer));

        machine.Observe(5, 4, 4);
        Assert.That(machine.State, Is.EqualTo(CaptureState.Cooldown));

        machine.Observe(9, 4, 4);
        Assert.That(machine.State, Is.EqualTo(CaptureState.Monitoring));
    }

    [Test]
    public void Controller_runtime_configuration_applies_to_the_next_capture_and_trims_history()
    {
        var descriptors = Enumerable.Range(0, 8)
            .Select(index => new RecorderDescriptor($"marker-{index}", "CPU", $"Marker {index}", "TimeNanoseconds", "Int64"))
            .ToArray();
        using var manager = new RecorderManager(new FakeBackend(descriptors));
        using var controller = new DeepCaptureController(
            manager,
            CreateShortStateMachine(),
            maxConcurrent: 8,
            overheadCeiling: 0.08,
            maxCompletedSessions: 3);
        controller.Initialize();

        controller.RequestManualCapture(0);
        controller.Observe(3, global: null);
        controller.RequestManualCapture(4);
        controller.Observe(7, global: null);
        Assert.That(controller.CompletedSessions.Count, Is.EqualTo(2));

        controller.UpdateConfiguration(maxConcurrent: 4, overheadCeiling: 0.12, maxCompletedSessions: 1);
        Assert.That(controller.CompletedSessions.Count, Is.EqualTo(1));

        controller.RequestManualCapture(8);
        Assert.That(controller.CurrentBatchSize, Is.EqualTo(4));
    }

    [Test]
    public void Configuration_applied_every_frame_between_captures_keeps_the_configured_batches()
    {
        var descriptors = Enumerable.Range(0, 8)
            .Select(index => new RecorderDescriptor($"marker-{index}", "CPU", $"Marker {index}", "TimeNanoseconds", "Int64"))
            .ToArray();
        using var manager = new RecorderManager(new FakeBackend(descriptors));
        using var controller = new DeepCaptureController(manager, CreateShortStateMachine(), maxConcurrent: 8);
        controller.Initialize();
        Assert.That(manager.DescriptorCount, Is.EqualTo(manager.Descriptors.Count));

        for (var frame = 0; frame < 3; frame++)
            controller.UpdateConfiguration(maxConcurrent: 3, overheadCeiling: 0.08, maxCompletedSessions: 20);
        controller.RequestManualCapture(0);
        Assert.That(controller.CurrentBatchSize, Is.EqualTo(3));
        Assert.That(manager.ActiveIds.Count, Is.EqualTo(3));
        Assert.That(controller.CurrentSession!.MarkerCoverage.IsBatched, Is.True);
        controller.Observe(3, global: null);
        controller.Observe(5, global: null);
        Assert.That(controller.CurrentSession, Is.Null);

        controller.UpdateConfiguration(maxConcurrent: 8, overheadCeiling: 0.08, maxCompletedSessions: 20);
        controller.RequestManualCapture(6);
        Assert.That(controller.CurrentBatchSize, Is.EqualTo(8));
        Assert.That(manager.ActiveIds.Count, Is.EqualTo(8));
        Assert.That(controller.CurrentSession!.MarkerCoverage.IsBatched, Is.False);
    }

    private static DeepCaptureStateMachine CreateShortStateMachine() => new(
        efficiencyThreshold: 0.8,
        sustainSeconds: 2,
        deepSeconds: 1,
        postSeconds: 1,
        cooldownSeconds: 1);

    private sealed class FakeBackend : IRecorderBackend
    {
        private readonly IReadOnlyList<RecorderDescriptor> _descriptors;

        public FakeBackend(IReadOnlyList<RecorderDescriptor> descriptors)
        {
            _descriptors = descriptors;
        }

        public IReadOnlyList<RecorderDescriptor> Discover() => _descriptors;

        public IActiveRecorder Start(RecorderDescriptor descriptor, int capacity) => new FakeRecorder(descriptor.Id);
    }

    private sealed class FakeRecorder : IActiveRecorder
    {
        public FakeRecorder(string id)
        {
            Id = id;
        }

        public string Id { get; }
        public RecorderReading Read() => new(1d, 1);
        public void Dispose() { }
    }
}
