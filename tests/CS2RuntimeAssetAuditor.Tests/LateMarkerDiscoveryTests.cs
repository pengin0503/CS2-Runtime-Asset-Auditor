using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Profiling;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class LateMarkerDiscoveryTests
{
    [Test]
    public void Deep_capture_rediscovers_markers_registered_after_initialization()
    {
        var initial = new RecorderDescriptor("initial", "CPU", "Main Thread", "TimeNanoseconds", "Int64");
        var late = new RecorderDescriptor("late", "CPU", "Simulation Example.System", "TimeNanoseconds", "Int64");
        var backend = new MutableBackend(initial);
        using var manager = new RecorderManager(backend);
        using var controller = new DeepCaptureController(
            manager,
            new DeepCaptureStateMachine(
                efficiencyThreshold: 0.8,
                sustainSeconds: 2,
                deepSeconds: 1,
                postSeconds: 1,
                cooldownSeconds: 100),
            maxConcurrent: 8);

        controller.Initialize();
        backend.Add(late);

        controller.RequestManualCapture(0);
        controller.Observe(0.5, global: null);

        Assert.Multiple(() =>
        {
            Assert.That(controller.CurrentSession, Is.Not.Null);
            Assert.That(controller.CurrentSession!.MarkerCoverage.Discovered, Is.EqualTo(2));
            Assert.That(controller.CurrentSession.TryGetMarkerSamples(late.Id, out var samples), Is.True);
            Assert.That(samples, Is.Not.Empty);
        });
    }

    private sealed class MutableBackend : IRecorderBackend
    {
        private readonly List<RecorderDescriptor> _descriptors;

        public MutableBackend(params RecorderDescriptor[] descriptors)
        {
            _descriptors = descriptors.ToList();
        }

        public void Add(RecorderDescriptor descriptor) => _descriptors.Add(descriptor);

        public IReadOnlyList<RecorderDescriptor> Discover() => _descriptors.ToArray();

        public IActiveRecorder Start(RecorderDescriptor descriptor, int capacity) => new FakeRecorder(descriptor.Id);
    }

    private sealed class FakeRecorder : IActiveRecorder
    {
        public FakeRecorder(string id) => Id = id;
        public string Id { get; }
        public RecorderReading Read() => new(1d, 1);
        public void Dispose() { }
    }
}
