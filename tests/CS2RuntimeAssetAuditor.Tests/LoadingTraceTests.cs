using System;
using CS2RuntimeAssetAuditor.Core.Loading;
using CS2RuntimeAssetAuditor.Export;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class LoadingTraceTests
{
    [Test]
    public void Load_marks_boundaries_and_separates_ram_from_graphics_allocation()
    {
        var recorder = new LoadingTraceRecorder();
        var start = DateTimeOffset.Parse("2026-09-29T00:00:00Z");
        recorder.MarkModStarted(start.AddSeconds(-10));
        recorder.Begin(start, "LoadGame");
        Assert.That(recorder.ShouldSample(0), Is.True);
        Assert.That(recorder.ShouldSample(1), Is.False);
        recorder.Observe(start, 100, 200, 40, 10, false);
        recorder.Mark("saveRestored", start.AddSeconds(2));
        recorder.Observe(start.AddSeconds(2), 150, 240, 45, 12, true);
        recorder.Observe(start.AddSeconds(3), 120, 220, 42, 13, true);
        recorder.Complete(start.AddSeconds(4), cityOperable: true);

        var snapshot = recorder.Snapshot()!;
        Assert.That(snapshot.ModStartedAtUtc, Is.EqualTo(start.AddSeconds(-10)));
        Assert.That(snapshot.PeakUnityAllocatedBytes, Is.EqualTo(150));
        Assert.That(snapshot.PeakProcessWorkingSetBytes, Is.EqualTo(240));
        Assert.That(snapshot.End!.ProcessWorkingSetBytes, Is.EqualTo(220));
        Assert.That(snapshot.ObservedUncachedToCachedTransition, Is.True);
        Assert.That(snapshot.Milestones, Has.Length.EqualTo(4));
        var json = RuntimeAssetAuditReportSerializer.Serialize(new RuntimeAssetAuditReport
        {
            Loading = ReportLoadingTrace.FromSnapshot(snapshot)
        });
        Assert.That(json, Does.Contain("\"processWorkingSetBytes\":240"));
        Assert.That(json, Does.Contain("\"graphicsDriverAllocatedBytes\":45"));
        Assert.That(json, Does.Contain("\"observedUncachedToCachedTransition\":true"));
        Assert.That(json, Does.Not.Contain("\"vramUsedBytes\":45"));
        Assert.That(json, Does.Not.Contain("\"cacheReady\":"));
        Assert.That(json, Does.Not.Contain("\"loadedBytes\":"));
        Assert.That(json, Does.Not.Contain("\"cacheFailed\":"));
    }

    [Test]
    public void Sampling_keeps_bounded_history_but_peak_and_end_cover_every_observation()
    {
        var recorder = new LoadingTraceRecorder();
        var start = DateTimeOffset.UtcNow;
        recorder.Begin(start, "LoadGame");
        for (var i = 0; i < 3000; i++)
            recorder.Observe(start.AddSeconds(i * 2), i == 777 ? 9000 : i, i, null, null, null);
        recorder.Complete(start.AddSeconds(6000), cityOperable: true);
        var snapshot = recorder.Snapshot()!;
        Assert.That(snapshot.Samples.Length, Is.LessThanOrEqualTo(LoadingTraceRecorder.MaximumRetainedSamples));
        Assert.That(snapshot.PeakUnityAllocatedBytes, Is.EqualTo(9000));
        Assert.That(snapshot.End!.ProcessWorkingSetBytes, Is.EqualTo(2999));
    }

    [Test]
    public void Next_load_replaces_previous_city_trace_without_losing_mod_start()
    {
        var recorder = new LoadingTraceRecorder();
        var start = DateTimeOffset.UtcNow;
        recorder.MarkModStarted(start.AddMinutes(-1));
        recorder.Begin(start, "LoadGame");
        recorder.Observe(start, 100, 200, null, null, null);
        recorder.Complete(start.AddSeconds(1), cityOperable: true);
        recorder.Begin(start.AddMinutes(10), "NewGame");
        var snapshot = recorder.Snapshot()!;
        Assert.That(snapshot.ModStartedAtUtc, Is.EqualTo(start.AddMinutes(-1)));
        Assert.That(snapshot.Samples, Is.Empty);
        Assert.That(snapshot.CompletedAtUtc, Is.Null);
        Assert.That(snapshot.PeakProcessWorkingSetBytes, Is.Null);
        recorder.Clear();
        Assert.That(recorder.Snapshot(), Is.Null);
        Assert.That(recorder.IsLoading, Is.False);
    }
}
