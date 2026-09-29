using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.Frames;
using CS2RuntimeAssetAuditor.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class FrameRateDisplayTests
{
    private static GlobalMetricsSnapshot Sample(double timestamp, RuntimeInterval? interval) =>
        new GlobalMetricsSnapshot(timestamp, 2, 2, new Dictionary<string, RecorderReading>(), frameInterval: interval);

    private static RuntimeInterval Interval(int frames, double seconds, double median, double p95) => new RuntimeInterval
    {
        Frames = frames,
        IntervalSeconds = seconds,
        FrameMs = new SampleSummary(frames, median, p95, p95 * 2)
    };

    [Test]
    public void Live_snapshot_shows_the_interval_frame_rate_and_frame_times()
    {
        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput { Global = Sample(5, Interval(30, 0.5, 16.2, 25.4)) });

        Assert.That(snapshot.Global.FramesPerSecond, Is.EqualTo(60).Within(1e-9));
        Assert.That(snapshot.Global.FrameMsMedian, Is.EqualTo(16.2));
        Assert.That(snapshot.Global.FrameMsP95, Is.EqualTo(25.4));
    }

    [Test]
    public void Frame_values_that_were_not_measured_stay_unavailable()
    {
        var withoutInterval = UiSnapshotBuilder.Build(new UiSnapshotInput { Global = Sample(5, null) });
        var withoutFrames = UiSnapshotBuilder.Build(new UiSnapshotInput { Global = Sample(5, new RuntimeInterval()) });

        foreach (var snapshot in new[] { withoutInterval, withoutFrames })
        {
            Assert.That(snapshot.Global.FramesPerSecond, Is.Null);
            Assert.That(snapshot.Global.FrameMsMedian, Is.Null);
            Assert.That(snapshot.Global.FrameMsP95, Is.Null);
        }
    }

    [Test]
    public void Capture_timeline_carries_frame_rate_only_where_it_was_measured()
    {
        var capture = new CaptureSession("c", new CaptureTrigger(CaptureTriggerKind.Manual, 10, null), 16);
        capture.AddGlobalSample(Sample(10, Interval(15, 0.5, 33, 40)));
        capture.AddGlobalSample(Sample(10.5, null));

        var timeline = UiSnapshotBuilder.Build(new UiSnapshotInput { Captures = new[] { capture } }).Timeline;

        var fps = timeline.Where(point => point.Metric == "fps").ToArray();
        Assert.That(fps.Select(point => point.TimestampSeconds), Is.EqualTo(new[] { 10d }));
        Assert.That(fps[0].Value, Is.EqualTo(30).Within(1e-9));
        Assert.That(fps[0].UnitType, Is.EqualTo(MetricUnits.FramesPerSecond));
        var p95 = timeline.Single(point => point.Metric == "frameMsP95");
        Assert.That(p95.Value, Is.EqualTo(40));
        Assert.That(p95.UnitType, Is.EqualTo(MetricUnits.Milliseconds));
        Assert.That(timeline.Count(point => point.Metric == "efficiency"), Is.EqualTo(2));
    }
}
