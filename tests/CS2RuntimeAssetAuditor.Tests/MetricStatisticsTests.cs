using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class MetricStatisticsTests
{
    [Test]
    public void Statistics_include_median_p95_p99_and_max()
    {
        var values = Enumerable.Range(1, 100).Select(x => (double)x).ToArray();
        var stats = MetricStatistics.From(values);

        Assert.That(stats.Current, Is.EqualTo(100));
        Assert.That(stats.Mean, Is.EqualTo(50.5).Within(0.001));
        Assert.That(stats.Median, Is.EqualTo(50.5).Within(0.001));
        Assert.That(stats.P95, Is.EqualTo(95).Within(0.001));
        Assert.That(stats.P99, Is.EqualTo(99).Within(0.001));
        Assert.That(stats.Max, Is.EqualTo(100));
        Assert.That(stats.Total, Is.EqualTo(5050));
        Assert.That(stats.Count, Is.EqualTo(100));
    }
}
