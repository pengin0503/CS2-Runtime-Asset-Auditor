using NUnit.Framework;
using CS2RuntimeAssetAuditor.Core;

namespace CS2RuntimeAssetAuditor.Tests;

public class CoreSmokeTests
{
    [Test]
    public void Unavailable_is_not_equivalent_to_zero_measurement()
    {
        Assert.That(MetricAvailability.Unavailable, Is.Not.EqualTo(MetricAvailability.Available));
        Assert.That(MetricConfidence.Unavailable, Is.Not.EqualTo(MetricConfidence.Full));
    }
}
