using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class RecorderHandleTableTests
{
    private static RecorderDescriptor? Describe(string handle) => handle switch
    {
        "invalid" => null,
        "throws" => throw new InvalidOperationException("stripped marker"),
        _ => new RecorderDescriptor(handle.Split('#')[0], "CPU", handle, "TimeNanoseconds", "Int64")
    };

    [Test]
    public void A_failed_rediscovery_keeps_the_previous_handles_usable()
    {
        var table = new RecorderHandleTable<string>();
        table.Rediscover(() => new[] { "main", "render" }, Describe);

        Assert.Throws<InvalidOperationException>(() =>
            table.Rediscover(() => throw new InvalidOperationException("GetAvailable failed"), Describe));

        Assert.Multiple(() =>
        {
            Assert.That(table.Count, Is.EqualTo(2));
            Assert.That(table.TryGet("main", out var handle), Is.True);
            Assert.That(handle, Is.EqualTo("main"));
        });
    }

    [Test]
    public void A_successful_rediscovery_replaces_the_table()
    {
        var table = new RecorderHandleTable<string>();
        table.Rediscover(() => new[] { "main", "render" }, Describe);

        var descriptors = table.Rediscover(() => new[] { "render", "gpu" }, Describe);

        Assert.Multiple(() =>
        {
            Assert.That(descriptors.Select(d => d.Id), Is.EqualTo(new[] { "render", "gpu" }));
            Assert.That(table.TryGet("main", out _), Is.False);
            Assert.That(table.TryGet("gpu", out _), Is.True);
        });
    }

    [Test]
    public void Invalid_failing_and_duplicate_handles_are_skipped_and_the_first_duplicate_wins()
    {
        var table = new RecorderHandleTable<string>();

        var descriptors = table.Rediscover(() => new[] { "invalid", "throws", "main#1", "main#2" }, Describe);

        Assert.Multiple(() =>
        {
            Assert.That(descriptors.Select(d => d.Id), Is.EqualTo(new[] { "main" }));
            Assert.That(table.TryGet("main", out var handle), Is.True);
            Assert.That(handle, Is.EqualTo("main#1"));
        });
    }
}
