using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

// Issues #53 and #54: rendering counters in normal monitoring, and Video Memory Bytes never read as VRAM usage.
public class RenderCounterTests
{
    private static GlobalMetricsSnapshot Sample(params (string Id, double Value, string Unit)[] readings)
        => new(1, 1, 1,
            readings.ToDictionary(r => r.Id, r => new RecorderReading(r.Value, 1)),
            readings.ToDictionary(r => r.Id, r => r.Unit));

    [Test]
    public void Normal_monitoring_activates_every_rendering_counter_once()
    {
        var names = NormalMonitoringRecorderPolicy.GetPreferredRecorderNameGroups().SelectMany(group => group).ToArray();
        foreach (var counter in RenderCounters.All)
            Assert.That(names.Count(name => name == counter.MarkerName), Is.EqualTo(1), counter.MarkerName);
        Assert.That(names, Does.Not.Contain(VideoMemoryCounter.MarkerName), "capacity is not monitored as usage");
    }

    [Test]
    public void Counters_read_from_recorders_and_missing_ones_are_unavailable_not_zero()
    {
        var metrics = RenderCounters.Read(new Dictionary<string, RecorderReading>
        {
            ["Render\u001fDraw Calls Count"] = new(6216, 1),
            ["Render\u001fBatches Count"] = new(6216, 1),
            ["Render\u001fRender Textures Bytes"] = new(2_037_676_139, 1),
            ["Render\u001fShadow Casters Count"] = new(0, 0) // No sample yet.
        });

        Assert.That(metrics.Select(m => m.Id), Is.EqualTo(RenderCounters.All.Select(c => c.Id)));
        var drawCalls = metrics.Single(m => m.Id == "render.drawCalls");
        var batches = metrics.Single(m => m.Id == "render.batches");
        Assert.That(drawCalls.Value, Is.EqualTo(6216));
        Assert.That(batches.Value, Is.EqualTo(6216));
        Assert.That(drawCalls.Id, Is.Not.EqualTo(batches.Id), "draw calls and batches stay separate counters");
        Assert.That(metrics.Single(m => m.Id == "render.renderTexturesBytes").UnitType, Is.EqualTo("Bytes"));
        var shadows = metrics.Single(m => m.Id == "render.shadowCasters");
        Assert.That(shadows.Availability, Is.EqualTo(MetricAvailability.Unavailable));
        Assert.That(shadows.Value, Is.Null);
        Assert.That(shadows.Reason, Is.EqualTo(RenderCounters.UnavailableReason));
    }

    [Test]
    public void Snapshot_shows_counters_in_their_own_list_and_the_report_carries_their_meaning()
    {
        var global = Sample(
            ("Render\u001fDraw Calls Count", 6216, "Count"),
            ("Render\u001fTriangles Count", 46_136_766, "Count"),
            ("Internal\u001fMain Thread", 24_000_000, "TimeNanoseconds"));

        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput { Global = global });
        Assert.That(snapshot.Global.RecorderMetrics.Select(m => m.Id), Is.EqualTo(new[] { "Internal\u001fMain Thread" }));
        Assert.That(snapshot.Global.RenderCounters.Count, Is.EqualTo(RenderCounters.All.Count));
        Assert.That(snapshot.Global.RenderCounters.Single(m => m.Id == "render.triangles").Value, Is.EqualTo(46_136_766));
        Assert.That(snapshot.Global.RenderCounters.Single(m => m.Id == "render.vertices").Availability, Is.EqualTo("Unavailable"));

        var report = ProfilerReportBuilder.Build(snapshot, metadata: new RuntimeReportMetadata());
        var drawCalls = report.GlobalMetrics.Single(m => m.Name == "render.drawCalls");
        Assert.That(drawCalls.Value, Is.EqualTo(6216));
        Assert.That(drawCalls.Note, Does.Contain("Draw calls submitted"));
        var vertices = report.GlobalMetrics.Single(m => m.Name == "render.vertices");
        Assert.That(vertices.Availability, Is.EqualTo("Unavailable"));
        Assert.That(vertices.Value, Is.Null);
        Assert.That(report.Warnings, Does.Contain(RenderCounters.Limitation));
    }

    [TestCase(16_962_813_952d, 16177, VideoMemoryCounterMeaning.ReportedCapacity)] // RX 7800 XT, 2026-09-29 log.
    [TestCase(16_962_813_952d, 8192, VideoMemoryCounterMeaning.Unclassified)]
    [TestCase(16_962_813_952d, null, VideoMemoryCounterMeaning.Unclassified)]
    [TestCase(0d, 16177, VideoMemoryCounterMeaning.Unavailable)]
    [TestCase(null, 16177, VideoMemoryCounterMeaning.Unavailable)]
    public void Video_memory_bytes_is_capacity_only_when_it_matches_the_reported_capacity(double? bytes, int? capacityMb, VideoMemoryCounterMeaning expected)
    {
        Assert.That(VideoMemoryCounter.Classify(bytes, capacityMb), Is.EqualTo(expected));
    }

    [Test]
    public void Report_labels_the_video_memory_marker_as_capacity_and_never_as_usage()
    {
        var snapshot = new UiSnapshot
        {
            Timeline = new[]
            {
                new TimelinePoint { TimestampSeconds = 1, Metric = "marker:Render\u001fVideo Memory Bytes", Value = 16_962_813_952d, UnitType = "Bytes", Confidence = "Full" }
            }
        };

        var matching = ProfilerReportBuilder.Build(snapshot, metadata: new RuntimeReportMetadata { GpuMemoryCapacityMegabytes = 16177 });
        var capacity = matching.GlobalMetrics.Single(m => m.Name == "render.videoMemoryBytes.capacity");
        Assert.That(capacity.Note, Does.Contain("capacity, not VRAM usage"));
        Assert.That(matching.GlobalMetrics.Single(m => m.Name == "gpuMemoryCapacityReported").Value, Is.EqualTo(16177d * 1024 * 1024));

        var other = ProfilerReportBuilder.Build(snapshot, metadata: new RuntimeReportMetadata { GpuMemoryCapacityMegabytes = 8192 });
        Assert.That(other.GlobalMetrics.Single(m => m.Name == "render.videoMemoryBytes.unclassified").Note, Does.Contain("not used as VRAM usage"));
        Assert.That(other.GlobalMetrics.Any(m => m.Name.Contains("usage", System.StringComparison.OrdinalIgnoreCase)), Is.False);
    }
}
