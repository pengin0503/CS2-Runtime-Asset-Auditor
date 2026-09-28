using CS2RuntimeProfiler.Core;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class DynamicTimingProjectionTests
{
    [Test]
    public void Completion_processor_uses_descriptors_added_after_construction()
    {
        const string systemName = "Simulation Example.System";
        var systems = new[]
        {
            new SystemDescriptor(
                systemName,
                "Game",
                SystemSourceKind.Vanilla,
                null,
                MetricConfidence.Unavailable,
                Array.Empty<PatchOwnerInfo>())
        };
        var recorders = new List<RecorderDescriptor>();
        var processor = new CaptureCompletionTimingProcessor(systems, () => recorders);
        var recorder = new RecorderDescriptor("system-marker", "CPU", "World " + systemName, "TimeNanoseconds", "Int64");
        recorders.Add(recorder);

        var capture = new CaptureSession(
            "capture-dynamic",
            new CaptureTrigger(CaptureTriggerKind.Manual, 1d, null),
            maxSamplesPerSeries: 16);
        capture.AddMarkerSample(
            recorder.Id,
            new MetricSample(1d, 2_000_000d, MetricConfidence.Full, 1));

        processor.ProcessNew(new[] { capture });

        Assert.That(capture.SystemTiming, Is.Not.Null);
        Assert.That(capture.SystemTiming!.Systems.Select(x => x.SystemId), Does.Contain(systemName));
    }
}
