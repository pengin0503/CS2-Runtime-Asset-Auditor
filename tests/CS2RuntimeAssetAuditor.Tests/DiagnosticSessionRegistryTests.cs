using CS2RuntimeAssetAuditor.Coordination;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class DiagnosticSessionRegistryTests
{
    [Test]
    public void No_session_exists_until_a_city_load_begins_one()
    {
        var registry = new DiagnosticSessionRegistry();
        Assert.That(registry.IsActive, Is.False);
        Assert.That(registry.Current, Is.Null);
        Assert.That(registry.Generation, Is.Zero);
    }

    [Test]
    public void Each_city_load_creates_a_new_session_and_advances_the_generation()
    {
        var registry = new DiagnosticSessionRegistry();
        var cityA = registry.Begin("1.6.2f1", "build", DateTimeOffset.UtcNow);
        var afterA = registry.Generation;
        registry.Close();
        var cityB = registry.Begin("1.6.2f1", "build", DateTimeOffset.UtcNow);

        Assert.Multiple(() =>
        {
            Assert.That(cityB.SessionId, Is.Not.EqualTo(cityA.SessionId));
            Assert.That(registry.Generation, Is.EqualTo(afterA + 2));
            Assert.That(registry.IsCurrent(cityB.SessionId), Is.True);
            Assert.That(registry.IsCurrent(cityA.SessionId), Is.False);
        });
    }

    [Test]
    public void Closing_without_an_active_session_does_not_advance_the_generation()
    {
        var registry = new DiagnosticSessionRegistry();
        registry.Close();
        Assert.That(registry.Generation, Is.Zero);
    }

    [Test]
    public void Evidence_from_an_earlier_city_session_is_never_linked()
    {
        var registry = new DiagnosticSessionRegistry();
        var cityA = registry.Begin("v", "b", DateTimeOffset.UtcNow);
        var cityB = registry.Begin("v", "b", DateTimeOffset.UtcNow);
        var t = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

        var link = DiagnosticEvidenceBridge.TryLink(cityA.SessionId, "capture-1", t, t.AddSeconds(10),
            cityB.SessionId, "asset-1", t.AddSeconds(20), t.AddSeconds(30));

        Assert.That(link, Is.Null);
    }
}
