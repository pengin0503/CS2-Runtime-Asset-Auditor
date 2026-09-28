using CS2RuntimeProfiler.Core;
using CS2RuntimeProfiler.Profiling;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class CaptureCompletionEventTests
{
    [Test]
    public void Finalized_capture_raises_completion_event_exactly_once()
    {
        var descriptor = new RecorderDescriptor("cpu", "CPU", "Main Thread", "TimeNanoseconds", "Int64");
        using var manager = new RecorderManager(new Backend(descriptor));
        var stateMachine = new DeepCaptureStateMachine(0.8, 2, 1, 1, 100);
        using var controller = new DeepCaptureController(manager, stateMachine, maxConcurrent: 8);
        controller.Initialize();

        var completed = new List<CaptureSession>();
        controller.CaptureCompleted += completed.Add;

        controller.RequestManualCapture(0);
        var session = controller.CurrentSession;
        controller.Observe(3, global: null);
        controller.Observe(4, global: null);

        Assert.Multiple(() =>
        {
            Assert.That(completed.Count, Is.EqualTo(1));
            Assert.That(completed[0], Is.SameAs(session));
            Assert.That(controller.CompletedSessions, Does.Contain(session));
        });
    }

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
