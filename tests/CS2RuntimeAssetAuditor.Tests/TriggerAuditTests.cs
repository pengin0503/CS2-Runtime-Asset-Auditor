using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Profiling;
using CS2RuntimeAssetAuditor.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class TriggerAuditTests
{
    [Test]
    public void Automatic_capture_records_speed_snapshot_at_trigger_time()
    {
        var descriptor = new RecorderDescriptor("cpu", "CPU", "Marker", "TimeNanoseconds", "Int64");
        using var manager = new RecorderManager(new Backend(descriptor));
        var stateMachine = new DeepCaptureStateMachine(0.8, 2, 10, 5, 30);
        using var controller = new DeepCaptureController(manager, stateMachine, maxConcurrent: 8);
        controller.Initialize();

        var before = Global(0, 4, 1);
        var trigger = Global(2, 4, 1);
        controller.Observe(0, before, new[] { before });
        controller.Observe(2, trigger, new[] { before, trigger });

        Assert.Multiple(() =>
        {
            Assert.That(controller.CurrentSession, Is.Not.Null);
            Assert.That(controller.CurrentSession!.TriggerSelectedSpeed, Is.EqualTo(4d));
            Assert.That(controller.CurrentSession.TriggerActualSpeed, Is.EqualTo(1d));
            Assert.That(controller.CurrentSession.TriggerEfficiency, Is.EqualTo(0.25d));
        });
    }

    [Test]
    public void Capture_projection_exports_trigger_snapshot_and_selected_speed_timeline()
    {
        var capture = new CaptureSession("capture-audit", new CaptureTrigger(CaptureTriggerKind.AutomaticLowEfficiency, 10d, 0.5d), 16);
        var trigger = Global(10, 4, 2);
        capture.SetTriggerSnapshot(trigger);
        capture.AddGlobalSample(trigger);

        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput
        {
            Captures = new[] { capture },
            SelectedCaptureId = capture.Id
        });

        var summary = snapshot.Captures.Single();
        Assert.Multiple(() =>
        {
            Assert.That(summary.TriggerSelectedSpeed, Is.EqualTo(4d));
            Assert.That(summary.TriggerActualSpeed, Is.EqualTo(2d));
            Assert.That(summary.TriggerEfficiency, Is.EqualTo(0.5d));
            Assert.That(snapshot.Timeline.Any(point => point.Metric == "selectedSpeed" && point.Value == 4d), Is.True);
        });
    }

    private static GlobalMetricsSnapshot Global(double timestamp, double selected, double actual)
        => new(timestamp, selected, actual, new Dictionary<string, RecorderReading>());

    private sealed class Backend : IRecorderBackend
    {
        private readonly RecorderDescriptor _descriptor;
        public Backend(RecorderDescriptor descriptor) => _descriptor = descriptor;
        public IReadOnlyList<RecorderDescriptor> Discover() => new[] { _descriptor };
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
