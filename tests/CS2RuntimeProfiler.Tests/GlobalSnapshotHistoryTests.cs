using System.Collections.Generic;
using CS2RuntimeProfiler.Core;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class GlobalSnapshotHistoryTests
{
    private static GlobalMetricsSnapshot Sample(double t, double actual = 1)
        => new(t, 4, actual, new Dictionary<string, RecorderReading>());

    [Test]
    public void History_is_bounded_and_preserves_chronological_order()
    {
        var history = new GlobalSnapshotHistory(capacity: 3);
        history.Add(Sample(1));
        history.Add(Sample(2));
        history.Add(Sample(3));
        history.Add(Sample(4));

        var all = history.Snapshot();
        Assert.That(all.Select(x => x.TimestampSeconds), Is.EqualTo(new[] { 2d, 3d, 4d }));
    }

    [Test]
    public void Recent_returns_only_requested_prebuffer_window()
    {
        var history = new GlobalSnapshotHistory(capacity: 20);
        for (var t = 0; t <= 10; t++)
            history.Add(Sample(t));

        var recent = history.Recent(nowSeconds: 10, windowSeconds: 5);
        Assert.That(recent.Select(x => x.TimestampSeconds), Is.EqualTo(new[] { 5d, 6d, 7d, 8d, 9d, 10d }));
    }

    [Test]
    public void Recent_is_safe_for_empty_history()
    {
        var history = new GlobalSnapshotHistory(capacity: 4);
        Assert.That(history.Recent(100, 5), Is.Empty);
    }
}
