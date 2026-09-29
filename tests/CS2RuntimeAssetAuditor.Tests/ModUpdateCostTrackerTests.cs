using CS2RuntimeAssetAuditor.Core.DiagnosticLog;
using CS2RuntimeAssetAuditor.Core.Frames;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class ModUpdateCostTrackerTests
{
    [Test]
    public void A_frame_total_is_published_once_a_later_frame_starts()
    {
        var tracker = new ModUpdateCostTracker();
        tracker.Add(10, "GlobalMetricsCollector", 0.4, 0);
        tracker.Add(10, "CaptureRuntimeSystem", 2.5, 0);
        tracker.Add(10, "ProfilerUISystem", 1.1, 0);
        Assert.That(tracker.LastCompletedFrame.HasValue, Is.False, "Frame 10 may still have systems to run.");

        tracker.Add(11, "GlobalMetricsCollector", 0.3, 0);
        var completed = tracker.LastCompletedFrame;
        Assert.Multiple(() =>
        {
            Assert.That(completed.FrameIndex, Is.EqualTo(10));
            Assert.That(completed.TotalMs, Is.EqualTo(4.0).Within(1e-9));
            Assert.That(completed.SlowestSystem, Is.EqualTo("CaptureRuntimeSystem"));
            Assert.That(completed.SlowestSystemMs, Is.EqualTo(2.5));
        });
    }

    [Test]
    public void Slow_updates_are_reported_once_per_system_and_interval_with_the_skipped_count()
    {
        var tracker = new ModUpdateCostTracker(slowUpdateMilliseconds: 100, reportIntervalSeconds: 10);
        Assert.That(tracker.Add(1, "AssetAuditSystem", 99.9, 0), Is.Null, "Below the threshold.");

        var first = tracker.Add(2, "AssetAuditSystem", 150, 1);
        Assert.That(first.HasValue, Is.True);
        Assert.That(first!.Value.SuppressedSinceLastReport, Is.EqualTo(0));

        Assert.That(tracker.Add(3, "AssetAuditSystem", 200, 5), Is.Null);
        Assert.That(tracker.Add(4, "AssetAuditSystem", 300, 8), Is.Null);
        Assert.That(tracker.Add(4, "CaptureRuntimeSystem", 1400, 8).HasValue, Is.True, "Each system has its own limit.");

        var later = tracker.Add(5, "AssetAuditSystem", 120, 11.5);
        Assert.Multiple(() =>
        {
            Assert.That(later.HasValue, Is.True);
            Assert.That(later!.Value.Milliseconds, Is.EqualTo(120));
            Assert.That(later.Value.FrameIndex, Is.EqualTo(5));
            Assert.That(later.Value.SuppressedSinceLastReport, Is.EqualTo(2));
        });
    }

    [Test]
    public void Invalid_times_are_ignored()
    {
        var tracker = new ModUpdateCostTracker();
        Assert.That(tracker.Add(1, "A", double.NaN, 0), Is.Null);
        Assert.That(tracker.Add(1, "A", -1, 0), Is.Null);
        Assert.That(tracker.Add(1, "", 500, 0), Is.Null);
        tracker.Add(2, "B", 1, 0);
        Assert.That(tracker.LastCompletedFrame.HasValue, Is.False, "Frame 1 had no valid update.");
    }

    [Test]
    public void Every_mod_system_with_update_work_is_timed()
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "CS2RuntimeAssetAuditor.sln")))
            root = root.Parent;
        Assert.That(root, Is.Not.Null);
        var untimed = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root!.FullName, "src", "CS2RuntimeAssetAuditor"), "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            if (!source.Contains("protected override void OnUpdate()") || source.Contains("protected override void OnUpdate() { }"))
                continue;
            if (!source.Contains("ModUpdateCost.Stop(nameof("))
                untimed.Add(Path.GetFileName(file));
        }
        Assert.That(untimed, Is.Empty);
    }
}

public class AutoSaveAndEventLogTests
{
    [Test]
    public void Autosave_is_counted_only_when_the_check_time_moves_forward_from_an_active_value()
    {
        var counter = new AutoSaveTriggerCounter();
        Assert.Multiple(() =>
        {
            Assert.That(counter.Observe(-1f), Is.False, "First reading.");
            Assert.That(counter.Observe(120f), Is.False, "Autosave became active when the city loaded.");
            Assert.That(counter.Observe(120f), Is.False);
            Assert.That(counter.Observe(241f), Is.True, "An autosave started.");
            Assert.That(counter.Observe(241f), Is.False);
            Assert.That(counter.Observe(-1f), Is.False, "Autosave turned off.");
            Assert.That(counter.Observe(300f), Is.False, "Turned on again.");
            Assert.That(counter.Observe(null), Is.False, "Unreadable.");
            Assert.That(counter.Observe(420f), Is.False, "No known previous value.");
            Assert.That(counter.Observe(540f), Is.True);
        });
    }

    [Test]
    public void Diagnostic_rows_write_autosave_starts_and_leave_them_empty_when_unreadable()
    {
        var accumulator = new DiagnosticLogAccumulator(null);
        var header = DiagnosticLogCsv.FormatHeader(null).Split(',');
        var index = Array.IndexOf(header, "autoSaveStarts");
        Assert.That(index, Is.GreaterThan(0));

        var saved = accumulator.Complete(DateTimeOffset.UnixEpoch, 1, new DiagnosticIntervalContext(null, null, null, null, null, 1));
        var unreadable = accumulator.Complete(DateTimeOffset.UnixEpoch, 2, new DiagnosticIntervalContext(null, null, null, null, null));
        Assert.That(DiagnosticLogCsv.FormatRow(saved).Split(',')[index], Is.EqualTo("1"));
        Assert.That(DiagnosticLogCsv.FormatRow(unreadable).Split(',')[index], Is.Empty);
    }

    [Test]
    public void Event_log_appends_lines_and_keeps_one_old_file_when_it_reaches_its_cap()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cs2raa-events-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(directory, "events.log");
            var time = new DateTime(2026, 9, 29, 19, 29, 19, 52);
            using (var log = new ModEventLogFile(path, maxBytes: 200))
            {
                log.Write(time, "INFO", "first");
                Assert.That(File.ReadAllText(path), Is.EqualTo("[2026-09-29 19:29:19,052] [INFO] first" + Environment.NewLine));
                for (var i = 0; i < 10; i++)
                    log.Write(time, "INFO", "line " + i);
            }

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(path + ".old"), Is.True);
                Assert.That(new FileInfo(path).Length, Is.LessThan(200 + 64));
                Assert.That(File.ReadAllText(path) + File.ReadAllText(path + ".old"), Does.Contain("line 9"));
                Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(2), "At most two files are kept.");
            });

            using (var reopened = new ModEventLogFile(path, maxBytes: 200))
                reopened.Write(time, "WARN", "after restart");
            Assert.That(File.ReadAllText(path), Does.EndWith("[WARN] after restart" + Environment.NewLine));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
