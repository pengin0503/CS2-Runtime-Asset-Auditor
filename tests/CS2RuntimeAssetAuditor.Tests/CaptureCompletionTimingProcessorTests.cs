using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class CaptureCompletionTimingProcessorTests
{
    [Test]
    public void ProcessNew_finalizes_each_completed_capture_once()
    {
        var processor = Processor();
        var completed = new List<CaptureSession>();

        var first = Capture("first", 2_000_000d);
        completed.Add(first);
        processor.ProcessNew(completed);

        Assert.That(first.SystemTiming, Is.Not.Null);
        Assert.That(first.SystemTiming.Systems[0].Milliseconds, Is.EqualTo(2d).Within(0.0001d));
        var firstTiming = first.SystemTiming;

        processor.ProcessNew(completed);
        Assert.That(first.SystemTiming, Is.SameAs(firstTiming));

        var second = Capture("second", 5_000_000d);
        completed.Add(second);
        processor.ProcessNew(completed);

        Assert.That(first.SystemTiming, Is.SameAs(firstTiming));
        Assert.That(second.SystemTiming, Is.Not.Null);
        Assert.That(second.SystemTiming.Systems[0].Milliseconds, Is.EqualTo(5d).Within(0.0001d));
        Assert.That(processor.ProcessedCount, Is.EqualTo(2));
    }

    [Test]
    public void ProcessNew_does_not_skip_new_capture_after_front_eviction()
    {
        var processor = Processor();
        var first = Capture("first", 1_000_000d);
        var second = Capture("second", 2_000_000d);
        processor.ProcessNew(new[] { first, second });

        var third = Capture("third", 3_000_000d);
        processor.ProcessNew(new[] { second, third });

        Assert.Multiple(() =>
        {
            Assert.That(second.SystemTiming, Is.Not.Null);
            Assert.That(third.SystemTiming, Is.Not.Null);
            Assert.That(third.SystemTiming.Systems[0].Milliseconds, Is.EqualTo(3d).Within(0.0001d));
            Assert.That(processor.ProcessedCount, Is.EqualTo(3));
            Assert.That(processor.LastProcessedCapture, Is.SameAs(third));
        });
    }

    private static CaptureCompletionTimingProcessor Processor()
    {
        var systems = new[]
        {
            new SystemDescriptor(
                "Game.Simulation.ExampleSystem",
                "Game",
                SystemSourceKind.Vanilla,
                null,
                MetricConfidence.Unavailable)
        };
        var recorders = new[]
        {
            new RecorderDescriptor(
                "ecs-system",
                "Scripts",
                "Main World Game.Simulation.ExampleSystem",
                "TimeNanoseconds",
                "Int64")
        };
        return new CaptureCompletionTimingProcessor(systems, recorders);
    }

    private static CaptureSession Capture(string id, double nanoseconds)
    {
        var capture = new CaptureSession(
            id,
            new CaptureTrigger(CaptureTriggerKind.Manual, 0d, null),
            maxSamplesPerSeries: 8);
        capture.AddMarkerSample("ecs-system", new MetricSample(1d, nanoseconds, MetricConfidence.Full));
        return capture;
    }
}
