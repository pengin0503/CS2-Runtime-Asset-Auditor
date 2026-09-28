using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Profiling;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class RecorderManagerTests
{
    [Test]
    public void Discovery_does_not_start_recorders()
    {
        var backend = new FakeBackend(new RecorderDescriptor("cpu", "CPU", "Main Thread", "TimeNanoseconds", "Int64"));
        using var manager = new RecorderManager(backend);

        var discovered = manager.DiscoverAvailableMarkers();

        Assert.That(discovered.Count, Is.EqualTo(1));
        Assert.That(backend.StartCount, Is.Zero);
    }

    [Test]
    public void Activating_one_marker_starts_only_that_marker_once()
    {
        var backend = new FakeBackend(
            new RecorderDescriptor("cpu", "CPU", "Main Thread", "TimeNanoseconds", "Int64"),
            new RecorderDescriptor("gpu", "GPU", "GPU Time", "TimeNanoseconds", "Int64"));
        using var manager = new RecorderManager(backend);
        manager.DiscoverAvailableMarkers();

        Assert.That(manager.TryActivate("cpu", 8, out _), Is.True);
        Assert.That(manager.TryActivate("cpu", 8, out _), Is.True);

        Assert.That(backend.StartCount, Is.EqualTo(1));
        Assert.That(manager.ActiveIds, Is.EquivalentTo(new[] { "cpu" }));
    }

    [Test]
    public void Failed_activation_returns_error_instead_of_throwing()
    {
        var backend = new FakeBackend(new RecorderDescriptor("bad", "CPU", "Bad", "Count", "Int64")) { ThrowOnStart = true };
        using var manager = new RecorderManager(backend);
        manager.DiscoverAvailableMarkers();

        var activated = manager.TryActivate("bad", 4, out var error);

        Assert.That(activated, Is.False);
        Assert.That(error, Does.Contain("start failed"));
        Assert.That(manager.ActiveIds, Is.Empty);
    }

    [Test]
    public void Failed_rediscovery_preserves_previous_catalog()
    {
        var backend = new FakeBackend(new RecorderDescriptor("cpu", "CPU", "Main Thread", "TimeNanoseconds", "Int64"))
        {
            ThrowOnDiscoverAfterFirst = true
        };
        using var manager = new RecorderManager(backend);
        manager.DiscoverAvailableMarkers();

        Assert.Throws<InvalidOperationException>(() => manager.DiscoverAvailableMarkers());

        Assert.That(manager.Descriptors.Select(x => x.Id), Is.EquivalentTo(new[] { "cpu" }));
    }

    private sealed class FakeBackend : IRecorderBackend
    {
        private readonly IReadOnlyList<RecorderDescriptor> _descriptors;

        public FakeBackend(params RecorderDescriptor[] descriptors) => _descriptors = descriptors;
        public int StartCount { get; private set; }
        public int DiscoverCount { get; private set; }
        public bool ThrowOnStart { get; set; }
        public bool ThrowOnDiscoverAfterFirst { get; set; }

        public IReadOnlyList<RecorderDescriptor> Discover()
        {
            DiscoverCount++;
            if (ThrowOnDiscoverAfterFirst && DiscoverCount > 1)
                throw new InvalidOperationException("discovery failed");
            return _descriptors;
        }

        public IActiveRecorder Start(RecorderDescriptor descriptor, int capacity)
        {
            StartCount++;
            if (ThrowOnStart)
                throw new InvalidOperationException("start failed");
            return new FakeRecorder(descriptor.Id);
        }
    }

    private sealed class FakeRecorder : IActiveRecorder
    {
        public FakeRecorder(string id) => Id = id;
        public string Id { get; }
        public RecorderReading Read() => new(1.0, 1);
        public void Dispose() { }
    }
}
