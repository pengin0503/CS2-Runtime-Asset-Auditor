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
