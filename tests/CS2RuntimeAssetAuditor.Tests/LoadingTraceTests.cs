using System;
using CS2RuntimeAssetAuditor.Core.Loading;
using CS2RuntimeAssetAuditor.Export;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class LoadingTraceTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-29T00:00:00Z");

    [Test]
    public void Load_marks_boundaries_and_separates_ram_from_graphics_allocation()
    {
        var recorder = new LoadingTraceRecorder();
        recorder.MarkModStarted(Start.AddSeconds(-10));
        recorder.Begin(Start, "LoadGame");
        Assert.That(recorder.ShouldSample(0), Is.True);
        Assert.That(recorder.ShouldSample(1), Is.False);
        recorder.Observe(Start, 100, 200, 40, 10, false);
        recorder.Mark("saveRestored", Start.AddSeconds(2));
        recorder.Observe(Start.AddSeconds(2), 150, 240, 45, 12, true);
        recorder.Observe(Start.AddSeconds(3), 120, 220, 42, 13, true);
        recorder.Complete(Start.AddSeconds(4));

        var snapshot = recorder.Snapshot()!;
        Assert.That(snapshot.ModStartedAtUtc, Is.EqualTo(Start.AddSeconds(-10)));
        Assert.That(snapshot.Outcome, Is.EqualTo(LoadingTraceOutcome.Completed));
        Assert.That(snapshot.CompletedAtUtc, Is.EqualTo(Start.AddSeconds(4)));
        Assert.That(snapshot.InterruptedAtUtc, Is.Null);
        Assert.That(snapshot.PeakUnityAllocatedBytes, Is.EqualTo(150));
        Assert.That(snapshot.PeakProcessWorkingSetBytes, Is.EqualTo(240));
        Assert.That(snapshot.End!.ProcessWorkingSetBytes, Is.EqualTo(220));
        Assert.That(snapshot.ObservedUncachedToCachedTransition, Is.True);
        Assert.That(snapshot.ObservationCount, Is.EqualTo(3));
        Assert.That(Array.ConvertAll(snapshot.Milestones, milestone => milestone.Name), Is.EqualTo(new[]
        {
            "loadStarted", "saveRestored", "assetRegistrationChanged", "assetCacheStateChanged", "gameLoadingComplete"
        }));
        var json = Serialize(snapshot);
        Assert.That(json, Does.Contain("\"outcome\":\"completed\""));
        Assert.That(json, Does.Contain("\"processWorkingSetBytes\":240"));
        Assert.That(json, Does.Contain("\"graphicsDriverAllocatedBytes\":45"));
        Assert.That(json, Does.Contain("\"observedUncachedToCachedTransition\":true"));
        Assert.That(json, Does.Not.Contain("\"loadInterruptedAtUtc\""));
        Assert.That(json, Does.Not.Contain("\"cacheReady\":"));
    }

    [Test]
    public void Graphics_driver_zero_from_a_release_player_is_unavailable_not_zero_bytes()
    {
        var recorder = new LoadingTraceRecorder();
        recorder.Begin(Start, "LoadGame");
        recorder.Observe(Start, 100, 200, 0, null, null);
        recorder.Observe(Start.AddSeconds(2), 110, 210, -1, null, null);
        recorder.Complete(Start.AddSeconds(3));

        var snapshot = recorder.Snapshot()!;
        Assert.That(snapshot.Samples, Has.All.Matches<LoadingMemorySample>(sample => sample.GraphicsDriverAllocatedBytes == null));
        Assert.That(snapshot.End!.GraphicsDriverAllocatedBytes, Is.Null);
        Assert.That(snapshot.PeakGraphicsDriverAllocated.Bytes, Is.Null);
        Assert.That(snapshot.PeakGraphicsDriverAllocated.AtUtc, Is.Null);
        var json = Serialize(snapshot);
        Assert.That(json, Does.Not.Contain("\"graphicsDriverAllocatedBytes\""));
        Assert.That(json, Does.Contain("release players such as the shipped game report 0"));
        // Unity allocated memory and the working set keep their values; only the driver allocation treats 0 as absent.
        Assert.That(json, Does.Contain("\"unityAllocatedBytes\":110"));
    }

    [Test]
    public void Asset_database_milestones_mark_the_first_sampled_change_not_the_first_observation()
    {
        var recorder = new LoadingTraceRecorder();
        recorder.Begin(Start, "LoadGame");
        recorder.Observe(Start, 1, 1, null, null, null);
        recorder.Observe(Start.AddSeconds(2), 1, 1, null, 500, false);
        recorder.Observe(Start.AddSeconds(4), 1, 1, null, 500, false);
        Assert.That(Array.ConvertAll(recorder.Snapshot()!.Milestones, milestone => milestone.Name),
            Is.EqualTo(new[] { "loadStarted" }), "The baseline observation is taken as the load starts and is not an event.");

        recorder.Observe(Start.AddSeconds(6), 1, 1, null, 520, false);
        recorder.Observe(Start.AddSeconds(8), 1, 1, null, 540, true);
        recorder.Observe(Start.AddSeconds(10), 1, 1, null, 560, false);
        var milestones = recorder.Snapshot()!.Milestones;
        Assert.That(Array.ConvertAll(milestones, milestone => milestone.Name),
            Is.EqualTo(new[] { "loadStarted", "assetRegistrationChanged", "assetCacheStateChanged" }), "Each change is marked once.");
        Assert.That(milestones[1].AtUtc, Is.EqualTo(Start.AddSeconds(6)));
        Assert.That(milestones[2].AtUtc, Is.EqualTo(Start.AddSeconds(8)));
        Assert.That(Serialize(recorder.Snapshot()!), Does.Not.Contain("assetDatabaseObserved"));
    }

    [Test]
    public void Memory_peak_records_when_each_metric_first_reached_its_peak()
    {
        var recorder = new LoadingTraceRecorder();
        recorder.Begin(Start, "LoadGame");
        recorder.Observe(Start, 100, 900, 30, null, null);
        recorder.Observe(Start.AddSeconds(2), 300, 800, 50, null, null);
        recorder.Observe(Start.AddSeconds(4), 300, 700, 40, null, null);
        recorder.Observe(Start.AddSeconds(6), 200, 900, 20, null, null);
        recorder.Complete(Start.AddSeconds(7));

        var snapshot = recorder.Snapshot()!;
        Assert.That(snapshot.PeakUnityAllocated.Bytes, Is.EqualTo(300));
        Assert.That(snapshot.PeakUnityAllocated.AtUtc, Is.EqualTo(Start.AddSeconds(2)), "Ties keep the first time the peak was reached.");
        Assert.That(snapshot.PeakProcessWorkingSet.AtUtc, Is.EqualTo(Start));
        Assert.That(snapshot.PeakGraphicsDriverAllocated.AtUtc, Is.EqualTo(Start.AddSeconds(2)));
        var peak = ReportLoadingTrace.FromSnapshot(snapshot)!.MemoryPeak;
        Assert.That(peak.UnityAllocatedAtUtc, Is.EqualTo(Start.AddSeconds(2).ToString("O")));
        Assert.That(peak.ProcessWorkingSetAtUtc, Is.EqualTo(Start.ToString("O")));
        Assert.That(peak.GraphicsDriverAllocatedAtUtc, Is.EqualTo(Start.AddSeconds(2).ToString("O")));
        Assert.That(Serialize(snapshot), Does.Contain("\"memoryPeak\":{\"unityAllocatedBytes\":300,\"unityAllocatedAtUtc\":"));
    }

    [Test]
    public void Sampling_keeps_bounded_history_but_peak_and_end_cover_every_observation()
    {
        var recorder = new LoadingTraceRecorder();
        recorder.Begin(Start, "LoadGame");
        for (var i = 0; i < 3000; i++)
            recorder.Observe(Start.AddSeconds(i * 2), i == 777 ? 9000 : i, i, null, null, null);
        recorder.Complete(Start.AddSeconds(6000));
        var snapshot = recorder.Snapshot()!;
        Assert.That(snapshot.Samples.Length, Is.LessThanOrEqualTo(LoadingTraceRecorder.MaximumRetainedSamples));
        Assert.That(snapshot.ObservationCount, Is.EqualTo(3000));
        Assert.That(snapshot.PeakUnityAllocatedBytes, Is.EqualTo(9000));
        Assert.That(snapshot.PeakUnityAllocated.AtUtc, Is.EqualTo(Start.AddSeconds(777 * 2)), "The peak time survives thinning.");
        Assert.That(snapshot.End!.ProcessWorkingSetBytes, Is.EqualTo(2999));
    }

    [Test]
    public void Thinned_history_stays_evenly_spaced_from_the_first_observation()
    {
        var recorder = new LoadingTraceRecorder();
        recorder.Begin(Start, "LoadGame");
        for (var i = 0; i < 3000; i++)
            recorder.Observe(Start.AddSeconds(i * 2), i, i, null, null, null);
        var samples = recorder.Snapshot()!.Samples;

        // 3000 observations overflow 512 three times, so every eighth observation remains: 0, 8, ..., 2992.
        Assert.That(samples.Length, Is.EqualTo(375));
        Assert.That(samples[0].AtUtc, Is.EqualTo(Start));
        for (var i = 0; i < samples.Length; i++)
            Assert.That(samples[i].ProcessWorkingSetBytes, Is.EqualTo(i * 8L), $"sample {i}");
    }

    [Test]
    public void Ended_trace_ignores_late_marks_observations_and_sampling()
    {
        var recorder = new LoadingTraceRecorder();
        recorder.Begin(Start, "LoadGame");
        recorder.Observe(Start, 100, 200, null, 10, false);
        recorder.Complete(Start.AddSeconds(1));

        recorder.Mark("late", Start.AddSeconds(2));
        recorder.Observe(Start.AddSeconds(2), 999, 999, 999, 99, true);
        Assert.That(recorder.ShouldSample(100), Is.False);
        recorder.Complete(Start.AddSeconds(3));
        recorder.Interrupt(Start.AddSeconds(4), "ignored");

        var snapshot = recorder.Snapshot()!;
        Assert.That(recorder.IsLoading, Is.False);
        Assert.That(snapshot.Outcome, Is.EqualTo(LoadingTraceOutcome.Completed));
        Assert.That(snapshot.CompletedAtUtc, Is.EqualTo(Start.AddSeconds(1)));
        Assert.That(snapshot.InterruptionReason, Is.Null);
        Assert.That(snapshot.ObservationCount, Is.EqualTo(1));
        Assert.That(snapshot.PeakUnityAllocatedBytes, Is.EqualTo(100));
        Assert.That(snapshot.End!.RegisteredAssetCount, Is.EqualTo(10));
        Assert.That(Array.ConvertAll(snapshot.Milestones, milestone => milestone.Name),
            Is.EqualTo(new[] { "loadStarted", "gameLoadingComplete" }));
    }

    [Test]
    public void Interrupted_load_keeps_its_trace_and_reports_why_it_ended()
    {
        var recorder = new LoadingTraceRecorder();
        recorder.Begin(Start, "LoadGame");
        recorder.Observe(Start, 100, 200, null, 10, false);
        recorder.Observe(Start.AddSeconds(2), 300, 400, null, 10, false);
        recorder.Interrupt(Start.AddSeconds(5), "nextLoadStarted:MainMenu/NewGame");

        var snapshot = recorder.Snapshot()!;
        Assert.That(recorder.IsLoading, Is.False);
        Assert.That(snapshot.Outcome, Is.EqualTo(LoadingTraceOutcome.Interrupted));
        Assert.That(snapshot.InterruptedAtUtc, Is.EqualTo(Start.AddSeconds(5)));
        Assert.That(snapshot.CompletedAtUtc, Is.Null);
        Assert.That(snapshot.Samples, Has.Length.EqualTo(2));
        Assert.That(snapshot.PeakProcessWorkingSetBytes, Is.EqualTo(400));
        Assert.That(snapshot.Milestones[snapshot.Milestones.Length - 1].Name, Is.EqualTo("loadInterrupted"));

        var json = Serialize(snapshot);
        Assert.That(json, Does.Contain("\"outcome\":\"interrupted\""));
        Assert.That(json, Does.Contain("\"interruptionReason\":\"nextLoadStarted:MainMenu\\/NewGame\""));
        Assert.That(json, Does.Contain("\"loadInterruptedAtUtc\":"));
        Assert.That(json, Does.Not.Contain("\"loadCompletedAtUtc\""));
    }

    [Test]
    public void Next_load_keeps_the_interrupted_load_before_it_for_export()
    {
        // Reports are exported only during gameplay, so an interrupted load can first be exported during the next one.
        var recorder = new LoadingTraceRecorder();
        recorder.Begin(Start, "LoadGame");
        recorder.Observe(Start, 100, 5000, null, 10, false);
        recorder.Interrupt(Start.AddSeconds(30), "nextLoadStarted:MainMenu/NewGame");
        recorder.Begin(Start.AddMinutes(2), "LoadGame");
        recorder.Observe(Start.AddMinutes(2), 100, 3000, null, 10, false);
        recorder.Complete(Start.AddMinutes(3));

        var snapshot = recorder.Snapshot()!;
        Assert.That(snapshot.Outcome, Is.EqualTo(LoadingTraceOutcome.Completed));
        Assert.That(snapshot.PreviousInterrupted, Is.Not.Null);
        Assert.That(snapshot.PreviousInterrupted!.Outcome, Is.EqualTo(LoadingTraceOutcome.Interrupted));
        Assert.That(snapshot.PreviousInterrupted.PeakProcessWorkingSetBytes, Is.EqualTo(5000));
        Assert.That(snapshot.PreviousInterrupted.PreviousInterrupted, Is.Null);

        var report = ReportLoadingTrace.FromSnapshot(snapshot)!;
        Assert.That(report.PreviousInterruptedLoad!.Outcome, Is.EqualTo("interrupted"));
        Assert.That(report.PreviousInterruptedLoad.InterruptionReason, Is.EqualTo("nextLoadStarted:MainMenu/NewGame"));
        Assert.That(report.PreviousInterruptedLoad.Limitations, Is.Null, "Limitations are exported once, on the enclosing trace.");
        Assert.That(report.Limitations, Is.Not.Empty);
        Assert.That(Serialize(snapshot), Does.Contain("\"previousInterruptedLoad\":{"));

        // Only the load directly before is kept: a completed load in between drops it.
        recorder.Begin(Start.AddMinutes(10), "LoadGame");
        Assert.That(recorder.Snapshot()!.PreviousInterrupted, Is.Null);
        Assert.That(Serialize(recorder.Snapshot()!), Does.Not.Contain("previousInterruptedLoad"));
    }

    [Test]
    public void Load_in_progress_is_exported_as_in_progress_without_an_end_time()
    {
        var recorder = new LoadingTraceRecorder();
        recorder.Begin(Start, "NewGame");
        recorder.Observe(Start, 100, 200, null, null, null);
        var report = ReportLoadingTrace.FromSnapshot(recorder.Snapshot())!;
        Assert.That(report.Outcome, Is.EqualTo("inProgress"));
        Assert.That(report.LoadCompletedAtUtc, Is.Null);
        Assert.That(report.LoadInterruptedAtUtc, Is.Null);
        Assert.That(report.InterruptionReason, Is.Null);
    }

    [Test]
    public void Next_load_replaces_previous_city_trace_without_losing_mod_start()
    {
        var recorder = new LoadingTraceRecorder();
        Assert.That(recorder.Snapshot(), Is.Null);
        Assert.That(ReportLoadingTrace.FromSnapshot(null), Is.Null);
        recorder.MarkModStarted(Start.AddMinutes(-1));
        recorder.MarkModStarted(Start.AddMinutes(5));
        recorder.Begin(Start, "LoadGame");
        recorder.Observe(Start, 100, 200, null, null, null);
        recorder.Interrupt(Start.AddSeconds(1), "nextLoadStarted:Game/LoadGame");
        recorder.Begin(Start.AddMinutes(10), "NewGame");
        var snapshot = recorder.Snapshot()!;
        Assert.That(snapshot.ModStartedAtUtc, Is.EqualTo(Start.AddMinutes(-1)));
        Assert.That(snapshot.Samples, Is.Empty);
        Assert.That(snapshot.Outcome, Is.EqualTo(LoadingTraceOutcome.InProgress));
        Assert.That(snapshot.EndedAtUtc, Is.Null);
        Assert.That(snapshot.InterruptionReason, Is.Null);
        Assert.That(snapshot.PeakProcessWorkingSetBytes, Is.Null);
        Assert.That(snapshot.ObservationCount, Is.Zero);
        Assert.That(recorder.IsLoading, Is.True);
    }

    [Test]
    public void Export_omits_fields_the_trace_never_measures()
    {
        var recorder = new LoadingTraceRecorder();
        recorder.Begin(Start, "LoadGame");
        recorder.Observe(Start, 100, 200, 45, 10, true);
        recorder.Complete(Start.AddSeconds(1));
        var json = Serialize(recorder.Snapshot()!);
        foreach (var removed in new[] { "vramUsedBytes", "loadedBytes", "cacheRebuilt", "cacheFailed" })
            Assert.That(json, Does.Not.Contain("\"" + removed + "\""), removed);
        Assert.That(json, Does.Contain("VRAM usage is unavailable"));
        Assert.That(json, Does.Contain("Loaded byte count and cache rebuild\\/failure events are unavailable"));
    }

    [Test]
    public void Log_lines_carry_stage_duration_and_peak_times()
    {
        var recorder = new LoadingTraceRecorder();
        recorder.Begin(Start, "LoadGame");
        recorder.Observe(Start, 100L * 1024 * 1024, 2048L * 1024 * 1024, 0, 1234, false);
        recorder.Mark("saveRestored", Start.AddSeconds(12.5));
        recorder.Observe(Start.AddSeconds(14), 300L * 1024 * 1024, 1024L * 1024 * 1024, 0, 1234, true);

        var progress = LoadingTraceLogFormatter.FormatProgress(recorder.Snapshot()!, "saveRestored");
        Assert.That(progress, Is.EqualTo(
            "Loading trace: milestone=saveRestored purpose=LoadGame elapsed=12.5s " +
            "peakUnityMiB=300@14.0s peakWorkingSetMiB=2048@0.0s peakGraphicsDriverMiB=unavailable"));

        recorder.Interrupt(Start.AddSeconds(20), "nextLoadStarted:MainMenu/NewGame");
        var summary = LoadingTraceLogFormatter.FormatSummary(recorder.Snapshot()!);
        Assert.That(summary, Is.EqualTo(
            "Loading trace finished: outcome=Interrupted purpose=LoadGame duration=20.0s observations=2 " +
            "peakUnityMiB=300@14.0s peakWorkingSetMiB=2048@0.0s peakGraphicsDriverMiB=unavailable " +
            "registeredAssets=1234 cacheTransitionObserved=true reason=nextLoadStarted:MainMenu/NewGame"));
        Assert.That(LoadingTraceLogFormatter.FormatSummary(null!), Is.EqualTo("Loading trace: unavailable"));
    }

    [Test]
    public void Lifecycle_keeps_unfinished_loads_as_interrupted_and_logs_them()
    {
        var root = FindRoot();
        var lifecycle = File.ReadAllText(Path.Combine(root, "src", "CS2RuntimeAssetAuditor", "Lifecycle", "DiagnosticSessionSystem.cs"));
        Assert.That(lifecycle, Does.Not.Contain("LoadingTrace.Clear"), "An unfinished load must not be discarded.");
        var preload = Between(lifecycle, "OnGamePreload(", "OnGameLoaded(");
        Assert.That(preload.IndexOf("LoadingTrace.Interrupt(", StringComparison.Ordinal),
            Is.GreaterThan(-1).And.LessThan(preload.IndexOf("LoadingTrace.Begin(", StringComparison.Ordinal)),
            "A load still in progress is interrupted before the next gameplay load replaces it.");
        Assert.That(preload, Does.Contain("LogSummary()"));
        Assert.That(preload, Does.Contain("LogProgress(LoadingTraceRecorder.LoadStartedMilestone)"));
        Assert.That(Between(lifecycle, "OnGameLoaded(", "OnGameLoadingComplete("), Does.Contain("LogProgress(milestone)"));
        var complete = Between(lifecycle, "OnGameLoadingComplete(", "LogProgress(string");
        Assert.That(complete, Does.Contain("LoadingTrace.Complete("));
        Assert.That(complete, Does.Contain("LogSummary()"));
    }

    private static string Serialize(LoadingTraceSnapshot snapshot) =>
        RuntimeAssetAuditReportSerializer.Serialize(new RuntimeAssetAuditReport { Loading = ReportLoadingTrace.FromSnapshot(snapshot) });

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        var to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.That(from, Is.GreaterThan(-1), start);
        Assert.That(to, Is.GreaterThan(from), end);
        return text.Substring(from, to - from);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CS2RuntimeAssetAuditor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
