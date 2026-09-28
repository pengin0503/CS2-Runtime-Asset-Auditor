using CS2RuntimeProfiler.Core;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class MarkerBatchPlannerTests
{
    [Test]
    public void Six_hundred_markers_are_covered_in_four_batches_of_150()
    {
        var ids = Enumerable.Range(1, 600).Select(i => $"m{i}").ToArray();
        var plan = MarkerBatchPlanner.Create(ids, maxConcurrent: 150);

        Assert.That(plan.Batches.Count, Is.EqualTo(4));
        Assert.That(plan.Batches.SelectMany(x => x).Distinct().Count(), Is.EqualTo(600));
        Assert.That(plan.CoverageRatio, Is.EqualTo(1.0));
        Assert.That(plan.IsBatched, Is.True);
    }

    [Test]
    public void Empty_marker_set_reports_complete_empty_coverage_without_divide_by_zero()
    {
        var plan = MarkerBatchPlanner.Create(Array.Empty<string>(), 150);
        Assert.That(plan.CoverageRatio, Is.EqualTo(1.0));
        Assert.That(plan.Batches, Is.Empty);
        Assert.That(plan.IsBatched, Is.False);
    }

    [Test]
    public void Duplicate_marker_ids_are_measured_once()
    {
        var plan = MarkerBatchPlanner.Create(new[] { "a", "b", "a", "c" }, 2);
        Assert.That(plan.DiscoveredCount, Is.EqualTo(3));
        Assert.That(plan.Batches.SelectMany(x => x), Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void Invalid_max_concurrency_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MarkerBatchPlanner.Create(new[] { "a" }, 0));
    }
}
