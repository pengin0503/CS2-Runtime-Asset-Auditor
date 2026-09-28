using CS2RuntimeProfiler.Core;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class SystemTimingSnapshotMergerTests
{
    [Test]
    public void Missing_measurements_stay_unavailable_when_both_snapshots_are_unmeasured()
    {
        var merged = SystemTimingSnapshotMerger.Merge(new SystemTimingSnapshot(), new SystemTimingSnapshot());

        Assert.That(merged.UnattributedJobsMilliseconds, Is.Null);
    }

    [Test]
    public void Fallback_measurement_fills_an_unavailable_primary_value()
    {
        var fallback = new SystemTimingSnapshot();
        fallback.SetUnattributedJobsMilliseconds(6.25d);

        var merged = SystemTimingSnapshotMerger.Merge(new SystemTimingSnapshot(), fallback);

        Assert.That(merged.UnattributedJobsMilliseconds, Is.EqualTo(6.25d));
    }

    [Test]
    public void Measured_primary_zero_takes_precedence_over_fallback_measurement()
    {
        var primary = new SystemTimingSnapshot();
        primary.SetUnattributedJobsMilliseconds(0d);
        var fallback = new SystemTimingSnapshot();
        fallback.SetUnattributedJobsMilliseconds(6.25d);

        var merged = SystemTimingSnapshotMerger.Merge(primary, fallback);

        Assert.That(merged.UnattributedJobsMilliseconds, Is.EqualTo(0d));
    }
}
