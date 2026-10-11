using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Diagnostics;
using CS2RuntimeAssetAuditor.Assets.Core.Scanning;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets;

// Issue #51: the surface/texture stage must stop between reads inside one render asset, not only between assets.
public class NestedReadCursorTests
{
    private sealed class Surface
    {
        public Surface(string id, params string[] textures) { Id = id; Textures = textures; }
        public string Id { get; }
        public string[] Textures { get; }
    }

    private sealed class Texture
    {
        public Texture(string id) => Id = id;
        public string Id { get; }
    }

    // Every read advances a fake clock by 1 ms, so a budget of N ms allows about N reads per slice.
    private sealed class Visitor : INestedReadVisitor<string, Surface, Texture>
    {
        private readonly Dictionary<string, Surface[]> _surfaces;
        public double Clock;
        public readonly Dictionary<string, List<string>> Read = new();
        public readonly List<string> Completed = new();
        public readonly List<string> Failures = new();
        public string? FailTexture;
        public string? FailSurface;
        public string? FailOwner;
        public double SlowReadMs = 1;
        public string? SlowTexture;

        public Visitor(Dictionary<string, Surface[]> surfaces) => _surfaces = surfaces;

        public IReadOnlyList<Surface>? BeginOwner(string owner)
        {
            Clock += 1;
            if (owner == FailOwner) throw new InvalidOperationException("owner");
            Read[owner] = new List<string>();
            return _surfaces.TryGetValue(owner, out var surfaces) ? surfaces : null;
        }

        public IReadOnlyList<Texture>? ReadSurface(string owner, Surface surface)
        {
            Clock += 1;
            if (surface.Id == FailSurface) throw new InvalidOperationException("surface");
            Read[owner].Add(surface.Id);
            return surface.Textures.Select(id => new Texture(id)).ToArray();
        }

        public void ReadTexture(string owner, Texture texture)
        {
            Clock += texture.Id == SlowTexture ? SlowReadMs : 1;
            if (texture.Id == FailTexture) throw new InvalidOperationException("texture");
            Read[owner].Add(texture.Id);
        }

        public void OwnerFailed(string owner, Exception error) => Failures.Add("owner:" + owner);
        public void SurfaceFailed(string owner, Surface surface, Exception error) => Failures.Add("surface:" + surface.Id);
        public void TextureFailed(string owner, Texture texture, Exception error) => Failures.Add("texture:" + texture.Id);
        public void CompleteOwner(string owner) => Completed.Add(owner);
    }

    private static Surface[] ManySurfaces(int surfaces, int texturesPerSurface)
        => Enumerable.Range(0, surfaces)
            .Select(s => new Surface("s" + s, Enumerable.Range(0, texturesPerSurface).Select(t => $"s{s}t{t}").ToArray()))
            .ToArray();

    [Test]
    public void One_asset_with_many_textures_is_spread_over_several_slices_within_the_budget()
    {
        var visitor = new Visitor(new Dictionary<string, Surface[]> { ["big"] = ManySurfaces(10, 10), ["small"] = ManySurfaces(1, 1) });
        var cursor = new NestedReadCursor<string, Surface, Texture>(new[] { "big", "small" }, visitor, () => visitor.Clock);

        var slices = 0;
        var completed = false;
        while (!completed)
        {
            var before = visitor.Clock;
            completed = cursor.Advance(budgetMilliseconds: 4);
            slices++;
            Assert.That(visitor.Clock - before, Is.LessThanOrEqualTo(4), "a slice ran past its budget");
            Assert.That(slices, Is.LessThan(1000));
        }

        // 1 begin + 10 surfaces + 100 textures + 1 end for "big": the old loop did all of it in one slice.
        Assert.That(slices, Is.GreaterThan(25));
        Assert.That(visitor.Completed, Is.EqualTo(new[] { "big", "small" }));
        Assert.That(visitor.Read["big"].Count, Is.EqualTo(110));
        Assert.That(visitor.Read["big"].Distinct().Count(), Is.EqualTo(110), "a read was repeated after resuming");
        Assert.That(cursor.CompletedOwners, Is.EqualTo(2));
    }

    [Test]
    public void Completed_count_moves_only_when_an_asset_is_finished()
    {
        var visitor = new Visitor(new Dictionary<string, Surface[]> { ["a"] = ManySurfaces(3, 3) });
        var cursor = new NestedReadCursor<string, Surface, Texture>(new[] { "a" }, visitor, () => visitor.Clock);

        cursor.Advance(budgetMilliseconds: 3);
        Assert.That(cursor.CompletedOwners, Is.EqualTo(0));
        Assert.That(visitor.Completed, Is.Empty);

        while (!cursor.Advance(budgetMilliseconds: 3)) { }
        Assert.That(cursor.CompletedOwners, Is.EqualTo(1));
        Assert.That(visitor.Completed, Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public void Each_slice_makes_at_least_one_read_and_records_an_indivisible_slow_read()
    {
        var visitor = new Visitor(new Dictionary<string, Surface[]> { ["a"] = new[] { new Surface("s", "slow", "fast") } })
        {
            SlowTexture = "slow",
            SlowReadMs = 12
        };
        var cursor = new NestedReadCursor<string, Surface, Texture>(new[] { "a" }, visitor, () => visitor.Clock);

        var slices = 0;
        while (!cursor.Advance(budgetMilliseconds: 0)) slices++;

        Assert.That(slices, Is.GreaterThan(3), "a zero budget still progresses one read per slice");
        Assert.That(cursor.MaxStepMilliseconds, Is.EqualTo(12));
        Assert.That(visitor.Read["a"], Is.EqualTo(new[] { "s", "slow", "fast" }));
    }

    [Test]
    public void Failed_reads_are_reported_and_the_walk_continues()
    {
        var visitor = new Visitor(new Dictionary<string, Surface[]>
        {
            ["bad"] = ManySurfaces(1, 1),
            ["a"] = new[] { new Surface("broken", "x"), new Surface("ok", "t1", "t2") }
        })
        {
            FailOwner = "bad",
            FailSurface = "broken",
            FailTexture = "t1"
        };
        var cursor = new NestedReadCursor<string, Surface, Texture>(new[] { "bad", "a" }, visitor, () => visitor.Clock);

        while (!cursor.Advance(budgetMilliseconds: 2)) { }

        Assert.That(visitor.Failures, Is.EqualTo(new[] { "owner:bad", "surface:broken", "texture:t1" }));
        Assert.That(visitor.Completed, Is.EqualTo(new[] { "bad", "a" }));
        Assert.That(visitor.Read["a"], Is.EqualTo(new[] { "ok", "t2" }));
    }

    [Test]
    public void Telemetry_counts_over_budget_slices_and_the_longest_indivisible_read()
    {
        var telemetry = new ScanTelemetry(DateTimeOffset.UnixEpoch);
        telemetry.RecordManagedSlice(TimeSpan.FromMilliseconds(0.5), 1, budgetMilliseconds: 1);
        telemetry.RecordManagedSlice(TimeSpan.FromMilliseconds(3), 1, budgetMilliseconds: 1);
        telemetry.RecordIndivisibleRead(2.5);
        telemetry.RecordIndivisibleRead(1);

        var snapshot = telemetry.Snapshot(DateTimeOffset.UnixEpoch.AddSeconds(1));
        Assert.That(snapshot.OverBudgetSliceCount, Is.EqualTo(1));
        Assert.That(snapshot.MaxIndivisibleReadMilliseconds, Is.EqualTo(2.5));
    }
}
