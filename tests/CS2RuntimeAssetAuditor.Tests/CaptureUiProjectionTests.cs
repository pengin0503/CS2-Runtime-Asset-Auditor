using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class CaptureUiProjectionTests
{
    [Test]
    public void Capture_summary_exposes_duration_max_overhead_and_correlated_changes()
    {
        var capture = new CaptureSession(
            "capture-1",
            new CaptureTrigger(CaptureTriggerKind.Manual, 10d, null),
            maxSamplesPerSeries: 64);

        capture.SetMarkerCoverage(100, 80, true);
        capture.AddGlobalSample(Global(6d, 4d, 4d, 10d));
        capture.AddGlobalSample(Global(9d, 4d, 4d, 10d));
        capture.AddGlobalSample(Global(10.5d, 4d, 2d, 20d));
        capture.AddGlobalSample(Global(12d, 4d, 2d, 20d));
        capture.ObserveProfilerOverheadShare(0.03d);
        capture.ObserveProfilerOverheadShare(0.05d);

        var timing = new SystemTimingSnapshot();
        timing.AddSystem("Example.System", 3.25d, MetricConfidence.Managed, "Example.Mod");
        capture.SetSystemTiming(timing);

        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput
        {
            Captures = new[] { capture }
        });

        var summary = snapshot.Captures.Single();
        Assert.That(summary.DurationSeconds, Is.EqualTo(6d).Within(0.0001));
        Assert.That(summary.ProfilerOverheadShare, Is.EqualTo(0.05d).Within(0.0001));
        Assert.That(summary.CorrelatedChanges.Any(x => x.Metric == "actualSpeed" && x.Delta == -2d), Is.True);
        Assert.That(summary.CorrelatedChanges.Any(x => x.Metric == "CPU\u001fMain Thread" && x.Delta == 10d), Is.True);
        Assert.That(snapshot.Timeline.Any(x => x.Metric == "recorder:CPU\u001fMain Thread"), Is.True);
        Assert.That(snapshot.Timeline.Any(x => x.Metric == "system:Example.System" && x.Value == 3.25d), Is.True);
    }

    [Test]
    public void Zero_count_recorder_readings_are_not_projected_as_numeric_zero()
    {
        var capture = new CaptureSession("zero-count", new CaptureTrigger(CaptureTriggerKind.Manual, 1d, null), 8);
        capture.AddGlobalSample(new GlobalMetricsSnapshot(1d, 1d, 1d, new Dictionary<string, RecorderReading>
        {
            ["valid"] = new RecorderReading(5d, 1),
            ["unavailable"] = new RecorderReading(0d, 0)
        }));

        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput
        {
            Global = capture.GlobalSamples.Single(),
            Captures = new[] { capture },
            SelectedCaptureId = "zero-count"
        });

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Global.RecorderMetrics.Select(x => x.Id), Does.Contain("valid"));
            Assert.That(snapshot.Global.RecorderMetrics.Select(x => x.Id), Does.Not.Contain("unavailable"));
            Assert.That(snapshot.Timeline.Select(x => x.Metric), Does.Contain("recorder:valid"));
            Assert.That(snapshot.Timeline.Select(x => x.Metric), Does.Not.Contain("recorder:unavailable"));
        });
    }

    [Test]
    public void Zero_discovered_markers_keep_coverage_unavailable_instead_of_reporting_one_hundred_percent()
    {
        var capture = new CaptureSession("no-markers", new CaptureTrigger(CaptureTriggerKind.Manual, 1d, null), 8);
        capture.SetMarkerCoverage(0, 0, false);

        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput
        {
            Captures = new[] { capture },
            SelectedCaptureId = "no-markers"
        });

        Assert.Multiple(() =>
        {
            Assert.That(capture.MarkerCoverage.Ratio, Is.Null);
            Assert.That(snapshot.Captures.Single().CoverageRatio, Is.Null);
        });
    }

    [Test]
    public void Diagnostics_uses_the_selected_capture_catalog_count_over_a_stale_live_count()
    {
        var capture = new CaptureSession("selected", new CaptureTrigger(CaptureTriggerKind.Manual, 1d, null), 8);
        capture.SetMarkerCoverage(38, 0, false);

        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput
        {
            Captures = new[] { capture },
            SelectedCaptureId = "selected",
            DiscoveredMarkerCount = 129
        });

        Assert.That(snapshot.Diagnostics.DiscoveredMarkerCount, Is.EqualTo(38));
    }

    [Test]
    public void Current_capture_is_not_counted_or_listed_as_completed()
    {
        var completed = new CaptureSession("completed", new CaptureTrigger(CaptureTriggerKind.Manual, 1d, null), 16);
        completed.AddGlobalSample(Global(1d, 1d, 1d, 1d));
        var current = new CaptureSession("current", new CaptureTrigger(CaptureTriggerKind.Manual, 2d, null), 16);
        current.AddGlobalSample(Global(2d, 1d, 1d, 2d));

        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput
        {
            Captures = new[] { completed },
            CurrentCapture = current,
            CaptureState = CaptureState.DeepCapture
        });

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Capture.CompletedCount, Is.EqualTo(1));
            Assert.That(snapshot.Captures.Select(capture => capture.Id), Is.EqualTo(new[] { "completed" }));
            Assert.That(snapshot.Captures.Any(capture => capture.Id == "current"), Is.False);
        });
    }

    [Test]
    public void Selected_completed_capture_drives_systems_and_timeline_without_mixing_other_captures()
    {
        var first = new CaptureSession("first", new CaptureTrigger(CaptureTriggerKind.Manual, 10d, null), 16);
        first.AddGlobalSample(Global(10d, 4d, 3d, 10d));
        var firstTiming = new SystemTimingSnapshot();
        firstTiming.AddSystem("First.System", 1.5d, MetricConfidence.Full, "First.Mod", sourceKind: SystemSourceKind.Mod);
        first.SetSystemTiming(firstTiming);

        var second = new CaptureSession("second", new CaptureTrigger(CaptureTriggerKind.Manual, 20d, null), 16);
        second.AddGlobalSample(Global(20d, 4d, 2d, 20d));
        var secondTiming = new SystemTimingSnapshot();
        secondTiming.AddSystem("Second.System", 9d, MetricConfidence.Full, "Second.Mod", sourceKind: SystemSourceKind.Mod);
        second.SetSystemTiming(secondTiming);

        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput
        {
            Captures = new[] { first, second },
            SelectedCaptureId = "first"
        });

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Systems.Select(row => row.Id), Is.EqualTo(new[] { "First.System" }));
            Assert.That(snapshot.Timeline.Any(point => point.Metric == "system:First.System"), Is.True);
            Assert.That(snapshot.Timeline.Any(point => point.Metric == "system:Second.System"), Is.False);
            Assert.That(snapshot.Timeline.Any(point => point.TimestampSeconds == 20d), Is.False);
        });
    }

    [Test]
    public void Historical_export_uses_capture_global_and_retained_runtime_metrics()
    {
        var capture = new CaptureSession("historical", new CaptureTrigger(CaptureTriggerKind.Manual, 10d, null), 16);
        capture.AddGlobalSample(Global(10d, 4d, 3d, 10d));
        capture.AddGlobalSample(Global(12d, 4d, 2.5d, 11d));
        capture.SetRuntimeSnapshots(
            new NamedMetricSnapshot(12d, new[]
            {
                NamedMetricValue.Available("pendingPathfindActions", 42d, MetricConfidence.Indirect)
            }),
            new NamedMetricSnapshot(12d, new[]
            {
                NamedMetricValue.Available("citizens", 40000d, MetricConfidence.Indirect)
            }));

        var liveGlobal = Global(100d, 4d, 1d, 99d);
        var snapshot = UiSnapshotBuilder.BuildForExport(new UiSnapshotInput
        {
            Global = liveGlobal,
            Pathfinding = new NamedMetricSnapshot(100d, new[] { NamedMetricValue.Available("live-path", 99d, MetricConfidence.Indirect) }),
            Domains = new NamedMetricSnapshot(100d, new[] { NamedMetricValue.Available("live-domain", 99d, MetricConfidence.Indirect) }),
            Captures = new[] { capture },
            SelectedCaptureId = "historical"
        });

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Global.TimestampSeconds, Is.EqualTo(12d));
            Assert.That(snapshot.Global.ActualSpeed, Is.EqualTo(2.5d));
            Assert.That(snapshot.Pathfinding.Metrics.Single().Id, Is.EqualTo("pendingPathfindActions"));
            Assert.That(snapshot.DomainMetrics.Single().Id, Is.EqualTo("citizens"));
            Assert.That(snapshot.Capture.DetailCaptureId, Is.EqualTo("historical"));
            Assert.That(snapshot.Capture.DetailScope, Is.EqualTo("historical-capture"));
        });
    }

    [Test]
    public void Historical_export_explains_missing_runtime_snapshots_without_fabricating_values()
    {
        var capture = new CaptureSession("historical-empty", new CaptureTrigger(CaptureTriggerKind.Manual, 10d, null), 16);
        capture.AddGlobalSample(Global(10d, 1d, 1d, 1d));

        var snapshot = UiSnapshotBuilder.BuildForExport(new UiSnapshotInput
        {
            Captures = new[] { capture },
            SelectedCaptureId = "historical-empty"
        });

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Pathfinding.Metrics, Is.Empty);
            Assert.That(snapshot.DomainMetrics, Is.Empty);
            Assert.That(snapshot.Diagnostics.Messages.Any(message => message.Contains("pathfinding", System.StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(snapshot.Diagnostics.Messages.Any(message => message.Contains("domain", System.StringComparison.OrdinalIgnoreCase)), Is.True);
        });
    }

    [Test]
    public void Capture_overhead_keeps_the_maximum_observed_share()
    {
        var capture = new CaptureSession("capture-2", new CaptureTrigger(CaptureTriggerKind.Manual, 1d, null), 8);
        capture.ObserveProfilerOverheadShare(0.07d);
        capture.ObserveProfilerOverheadShare(0.02d);

        Assert.That(capture.MaxProfilerOverheadShare, Is.EqualTo(0.07d).Within(0.0001));
    }

    private static GlobalMetricsSnapshot Global(double timestamp, double selected, double actual, double mainThread)
    {
        return new GlobalMetricsSnapshot(timestamp, selected, actual, new Dictionary<string, RecorderReading>
        {
            ["CPU\u001fMain Thread"] = new RecorderReading(mainThread, 1)
        });
    }
}
