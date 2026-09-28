using CS2RuntimeProfiler.Core;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class SystemCatalogCacheTests
{
    [Test]
    public void Successful_refresh_replaces_the_published_catalog_snapshot()
    {
        var current = SystemCatalogDiscoveryResult.Complete(new[] { Descriptor("System.A") });
        var cache = new SystemCatalogCache(() => current);

        Assert.That(cache.TryRefresh(out var firstError), Is.True);
        Assert.That(firstError, Is.Null);
        Assert.That(cache.Snapshot.Select(system => system.FullTypeName), Is.EqualTo(new[] { "System.A" }));

        current = SystemCatalogDiscoveryResult.Complete(new[] { Descriptor("System.B"), Descriptor("System.C") });

        Assert.That(cache.TryRefresh(out var secondError), Is.True);
        Assert.That(secondError, Is.Null);
        Assert.That(cache.Snapshot.Select(system => system.FullTypeName), Is.EqualTo(new[] { "System.B", "System.C" }));
    }

    [Test]
    public void Failed_refresh_preserves_the_last_known_good_catalog_snapshot()
    {
        var fail = false;
        var cache = new SystemCatalogCache(() =>
        {
            if (fail)
                throw new InvalidOperationException("catalog refresh failed");
            return SystemCatalogDiscoveryResult.Complete(new[] { Descriptor("System.A") });
        });

        Assert.That(cache.TryRefresh(out _), Is.True);
        fail = true;

        Assert.Multiple(() =>
        {
            Assert.That(cache.TryRefresh(out var error), Is.False);
            Assert.That(error, Does.Contain("catalog refresh failed"));
            Assert.That(cache.Snapshot.Select(system => system.FullTypeName), Is.EqualTo(new[] { "System.A" }));
        });
    }

    [Test]
    public void Partial_refresh_preserves_last_known_good_catalog_and_reports_failure()
    {
        var current = SystemCatalogDiscoveryResult.Complete(new[] { Descriptor("System.A") });
        var cache = new SystemCatalogCache(() => current);
        Assert.That(cache.TryRefresh(out _), Is.True);

        current = SystemCatalogDiscoveryResult.Incomplete(
            new[] { Descriptor("System.B") },
            "one assembly could not be enumerated");

        Assert.Multiple(() =>
        {
            Assert.That(cache.TryRefresh(out var error), Is.False);
            Assert.That(error, Does.Contain("one assembly could not be enumerated"));
            Assert.That(cache.Snapshot.Select(system => system.FullTypeName), Is.EqualTo(new[] { "System.A" }));
        });
    }

    [Test]
    public void Initial_partial_discovery_keeps_available_descriptors_instead_of_publishing_an_empty_catalog()
    {
        var cache = new SystemCatalogCache(() => SystemCatalogDiscoveryResult.Incomplete(
            new[] { Descriptor("System.A") },
            "one assembly could not be enumerated"));

        Assert.Multiple(() =>
        {
            Assert.That(cache.TryRefresh(out var error), Is.False);
            Assert.That(error, Does.Contain("one assembly could not be enumerated"));
            Assert.That(cache.Snapshot.Select(system => system.FullTypeName), Is.EqualTo(new[] { "System.A" }));
        });
    }

    private static SystemDescriptor Descriptor(string name) => new(
        name,
        "Game",
        SystemSourceKind.Vanilla,
        null,
        MetricConfidence.Unavailable,
        Array.Empty<PatchOwnerInfo>());
}
