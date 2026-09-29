using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class UiCaptureDetailCacheTests
{
    private static CaptureSession Completed(string id, double systemMilliseconds)
    {
        var capture = new CaptureSession(id, new CaptureTrigger(CaptureTriggerKind.Manual, 10, null), 16);
        var timing = new SystemTimingSnapshot();
        timing.AddSystem("Game.Simulation." + id, systemMilliseconds, MetricConfidence.Managed);
        capture.SetSystemTiming(timing);
        capture.AddGlobalSample(new GlobalMetricsSnapshot(10, 1, 1, new Dictionary<string, RecorderReading>()));
        capture.AddGlobalSample(new GlobalMetricsSnapshot(12, 1, 1, new Dictionary<string, RecorderReading>()));
        return capture;
    }

    private static UiSnapshotInput Input(IReadOnlyList<CaptureSession> captures, string selected = "") =>
        new UiSnapshotInput { Captures = captures, SelectedCaptureId = selected };

    [Test]
    public void Key_holds_while_no_capture_changes()
    {
        var captures = new[] { Completed("a", 1), Completed("b", 2) };

        Assert.That(UiSnapshotBuilder.DetailKey(Input(captures)), Is.EqualTo(UiSnapshotBuilder.DetailKey(Input(captures))));
    }

    [Test]
    public void Key_changes_when_a_capture_changes_is_added_or_selected()
    {
        var a = Completed("a", 1);
        var b = Completed("b", 2);
        var before = UiSnapshotBuilder.DetailKey(Input(new[] { a, b }));

        Assert.That(UiSnapshotBuilder.DetailKey(Input(new[] { a, b }, selected: "a")), Is.Not.EqualTo(before));
        Assert.That(UiSnapshotBuilder.DetailKey(Input(new[] { a, b, Completed("c", 3) })), Is.Not.EqualTo(before));

        // A change to a capture that is listed but not shown in detail still changes the capture list.
        a.AddWarning("late warning");
        Assert.That(UiSnapshotBuilder.DetailKey(Input(new[] { a, b })), Is.Not.EqualTo(before));
    }

    [Test]
    public void Live_timing_is_compared_by_snapshot_when_no_capture_is_shown()
    {
        var live = new SystemTimingSnapshot();
        var key = UiSnapshotBuilder.DetailKey(new UiSnapshotInput { Systems = live });

        Assert.That(UiSnapshotBuilder.DetailKey(new UiSnapshotInput { Systems = live }), Is.EqualTo(key));
        Assert.That(UiSnapshotBuilder.DetailKey(new UiSnapshotInput { Systems = new SystemTimingSnapshot() }), Is.Not.EqualTo(key));
    }

    [Test]
    public void Snapshot_built_around_cached_detail_matches_a_full_build()
    {
        var captures = new[] { Completed("a", 1), Completed("b", 2) };
        var input = Input(captures, selected: "a");

        var full = UiSnapshotBuilder.Build(input);
        var cached = UiSnapshotBuilder.Build(input, UiSnapshotBuilder.BuildDetail(input));

        Assert.That(cached.Systems.Select(system => (system.Id, system.CurrentMilliseconds)),
            Is.EqualTo(full.Systems.Select(system => (system.Id, system.CurrentMilliseconds))));
        Assert.That(cached.Captures.Select(capture => (capture.Id, capture.DurationSeconds, capture.WarningCount)),
            Is.EqualTo(full.Captures.Select(capture => (capture.Id, capture.DurationSeconds, capture.WarningCount))));
        Assert.That(cached.Timeline.Count, Is.EqualTo(full.Timeline.Count));
        Assert.That(cached.Mods.Count, Is.EqualTo(full.Mods.Count));
        Assert.That(cached.Capture.CompletedCount, Is.EqualTo(2));
        Assert.That(cached.Capture.DetailCaptureId, Is.EqualTo("a"));
    }
}
