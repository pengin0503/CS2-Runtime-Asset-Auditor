using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class FrameNormalizedCostTests
{
    private static SystemDescriptor Mod(string id, string assembly) =>
        new SystemDescriptor(id, assembly, SystemSourceKind.Mod, assembly, MetricConfidence.Managed);

    [Test]
    public void Rare_single_call_does_not_dominate_mod_totals_when_frame_count_is_known()
    {
        // Mirrors an in-game report: an autosave serializer ran once for ~10.5 ms inside a 144-frame
        // window, while a per-frame system averaged ~7 ms every frame.
        var accumulator = new ManagedSystemTimingAccumulator();
        accumulator.Record("Rare.SerializeSystem", 10.5);
        for (var frame = 0; frame < 144; frame++)
            accumulator.Record("Busy.PerFrameSystem", 7.0);

        var timing = accumulator.BuildSnapshot(
            new[] { Mod("Rare.SerializeSystem", "Rare"), Mod("Busy.PerFrameSystem", "Busy") },
            windowFrames: 144);
        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput { Systems = timing });

        var rare = snapshot.Mods.Single(row => row.AssemblyName == "Rare");
        var busy = snapshot.Mods.Single(row => row.AssemblyName == "Busy");
        Assert.Multiple(() =>
        {
            Assert.That(rare.DirectSystemMilliseconds, Is.EqualTo(10.5 / 144).Within(1e-9));
            Assert.That(busy.DirectSystemMilliseconds, Is.EqualTo(7.0).Within(1e-9));
            Assert.That(rare.DirectCostBasis, Is.EqualTo(ModCostBasis.PerFrame));
            Assert.That(snapshot.Mods.First().AssemblyName, Is.EqualTo("Busy"));
            Assert.That(snapshot.Systems.First().Id, Is.EqualTo("Busy.PerFrameSystem"));
            // Per-call statistics stay raw: the spike is still visible as current/max.
            Assert.That(snapshot.Systems.Single(row => row.Id == "Rare.SerializeSystem").MaxMilliseconds, Is.EqualTo(10.5));
        });
    }

    [Test]
    public void Unknown_frame_count_falls_back_to_per_sample_mean_and_marks_basis()
    {
        var accumulator = new ManagedSystemTimingAccumulator();
        accumulator.Record("Rare.SerializeSystem", 9.0);
        accumulator.Record("Rare.SerializeSystem", 1.0);

        var timing = accumulator.BuildSnapshot(new[] { Mod("Rare.SerializeSystem", "Rare") });
        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput { Systems = timing });
        var mod = snapshot.Mods.Single();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Systems.Single().MillisecondsPerFrame, Is.Null);
            Assert.That(mod.DirectSystemMilliseconds, Is.EqualTo(5.0));
            Assert.That(mod.DirectCostBasis, Is.EqualTo(ModCostBasis.PerSample));
        });
    }

    [Test]
    public void Per_frame_cost_survives_marker_and_managed_merge_and_export()
    {
        var accumulator = new ManagedSystemTimingAccumulator();
        accumulator.Record("Busy.PerFrameSystem", 4.0);
        accumulator.Record("Busy.PerFrameSystem", 2.0);
        var managed = accumulator.BuildSnapshot(new[] { Mod("Busy.PerFrameSystem", "Busy") }, windowFrames: 4);

        var merged = SystemTimingSnapshotMerger.Merge(new SystemTimingSnapshot(), managed);
        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput { Systems = merged });
        var report = ProfilerReportBuilder.Build(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(merged.Systems.Single().MillisecondsPerFrame, Is.EqualTo(1.5));
            Assert.That(report.Systems.Single().MillisecondsPerFrame, Is.EqualTo(1.5));
            Assert.That(report.ModAttribution.Single().Value, Does.Contain("costBasis=perFrame"));
        });
    }

    [Test]
    public void Pathfinding_memory_metrics_carry_byte_units_into_ui_rows()
    {
        var metrics = new NamedMetricSnapshot(1, new[]
        {
            NamedMetricValue.Available("graphMemoryUsed", 123339232, MetricConfidence.Full, MetricUnits.Bytes),
            NamedMetricValue.Available("graphSize", 391316, MetricConfidence.Full)
        });

        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput { Pathfinding = metrics });

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Pathfinding.Metrics.Single(row => row.Id == "graphMemoryUsed").UnitType, Is.EqualTo("Bytes"));
            Assert.That(snapshot.Pathfinding.Metrics.Single(row => row.Id == "graphSize").UnitType, Is.Empty);
        });
    }

    [Test]
    public void Harmony_owner_ids_resolve_to_the_patching_assembly()
    {
        var owners = PatchOwnerResolver.Resolve(new (string, string)[]
        {
            ("Mods_Yenyang_Anarchy", "Anarchy"),
            ("Mods_Yenyang_Anarchy.Second", "Anarchy"),
            ("legacy.owner", null)
        });

        Assert.Multiple(() =>
        {
            Assert.That(owners.Select(owner => owner.OwnerId), Is.EqualTo(new[] { "Anarchy", "legacy.owner" }));
            Assert.That(owners[0].DisplayName, Is.EqualTo("Mods_Yenyang_Anarchy"));
        });
    }
}
