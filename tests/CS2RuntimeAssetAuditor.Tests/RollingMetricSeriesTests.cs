using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class RollingMetricSeriesTests
{
    [Test]
    public void Rolling_series_discards_oldest_sample_at_capacity()
    {
        var series = new RollingMetricSeries(3);
        series.Add(new MetricSample(1, 10, MetricConfidence.Full));
        series.Add(new MetricSample(2, 20, MetricConfidence.Full));
        series.Add(new MetricSample(3, 30, MetricConfidence.Full));
        series.Add(new MetricSample(4, 40, MetricConfidence.Full));

        Assert.That(series.Snapshot().Select(x => x.Value), Is.EqualTo(new[] { 20d, 30d, 40d }));
    }

    [Test]
    public void Capacity_below_one_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RollingMetricSeries(0));
    }
}
