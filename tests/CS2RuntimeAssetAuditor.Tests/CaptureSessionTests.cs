using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class CaptureSessionTests
{
    [Test]
    public void Capture_session_tracks_marker_coverage_and_warnings()
    {
        var session = new CaptureSession("capture-1", new CaptureTrigger(CaptureTriggerKind.Manual, 1, null), maxSamplesPerSeries: 3);
        session.SetMarkerCoverage(discovered: 600, captured: 600, isBatched: true);
        session.AddWarning("Profiler overhead high");

        Assert.That(session.MarkerCoverage.Discovered, Is.EqualTo(600));
        Assert.That(session.MarkerCoverage.Captured, Is.EqualTo(600));
        Assert.That(session.MarkerCoverage.Ratio, Is.EqualTo(1.0));
        Assert.That(session.MarkerCoverage.IsBatched, Is.True);
        Assert.That(session.Warnings, Does.Contain("Profiler overhead high"));
    }

    [Test]
    public void Per_marker_samples_are_bounded()
    {
        var session = new CaptureSession("capture-1", new CaptureTrigger(CaptureTriggerKind.Manual, 1, null), maxSamplesPerSeries: 2);
        session.AddMarkerSample("m1", new MetricSample(1, 1, MetricConfidence.Full));
        session.AddMarkerSample("m1", new MetricSample(2, 2, MetricConfidence.Full));
        session.AddMarkerSample("m1", new MetricSample(3, 3, MetricConfidence.Full));

        Assert.That(session.MarkerSamples["m1"].Select(x => x.Value), Is.EqualTo(new[] { 2d, 3d }));
    }

    [Test]
    public void Marker_samples_can_be_read_without_materializing_the_entire_dictionary()
    {
        var session = new CaptureSession("capture-1", new CaptureTrigger(CaptureTriggerKind.Manual, 1, null), maxSamplesPerSeries: 3);
        session.AddMarkerSample("m1", new MetricSample(1, 10, MetricConfidence.Full));
        session.AddMarkerSample("m2", new MetricSample(1, 20, MetricConfidence.Full));

        Assert.Multiple(() =>
        {
            Assert.That(session.TryGetMarkerSamples("m1", out var samples), Is.True);
            Assert.That(samples.Select(x => x.Value), Is.EqualTo(new[] { 10d }));
            Assert.That(session.TryGetMarkerSamples("missing", out var missing), Is.False);
            Assert.That(missing, Is.Empty);
        });
    }
}
