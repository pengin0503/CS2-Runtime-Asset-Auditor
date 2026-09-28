using CS2RuntimeProfiler.Core;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class ManagedSystemTimingFallbackTests
{
    [Test]
    public void Managed_accumulator_projects_bounded_system_statistics_with_managed_confidence()
    {
        var descriptor = new SystemDescriptor(
            "Game.Simulation.ExampleSystem",
            "Game",
            SystemSourceKind.Vanilla,
            null,
            MetricConfidence.Unavailable,
            new[] { new PatchOwnerInfo("patch.owner", "Patch Owner") });
        var accumulator = new ManagedSystemTimingAccumulator(maxSamplesPerSystem: 8);

        accumulator.Record(descriptor.FullTypeName, 0.25);
        accumulator.Record(descriptor.FullTypeName, 0.75);

        var snapshot = accumulator.BuildSnapshot(new[] { descriptor });
        var system = snapshot.Systems.Single();

        Assert.Multiple(() =>
        {
            Assert.That(system.SystemId, Is.EqualTo(descriptor.FullTypeName));
            Assert.That(system.Confidence, Is.EqualTo(MetricConfidence.Managed));
            Assert.That(system.OwnerAssembly, Is.EqualTo("Game"));
            Assert.That(system.SourceKind, Is.EqualTo(SystemSourceKind.Vanilla));
            Assert.That(system.MeanMilliseconds, Is.EqualTo(0.5).Within(0.0001));
            Assert.That(system.TotalMilliseconds, Is.EqualTo(1.0).Within(0.0001));
            Assert.That(system.Calls, Is.EqualTo(2));
            Assert.That(system.PatchOwners, Does.Contain("patch.owner"));
        });
    }

    [Test]
    public void Managed_accumulator_keeps_exact_whole_capture_counters_after_distribution_samples_roll_over()
    {
        var descriptor = new SystemDescriptor(
            "Game.Simulation.HighFrequencySystem",
            "Game",
            SystemSourceKind.Vanilla,
            null,
            MetricConfidence.Unavailable,
            Array.Empty<PatchOwnerInfo>());
        var accumulator = new ManagedSystemTimingAccumulator(maxSamplesPerSystem: 3);

        accumulator.Record(descriptor.FullTypeName, 10.0);
        accumulator.Record(descriptor.FullTypeName, 2.0);
        accumulator.Record(descriptor.FullTypeName, 3.0);
        accumulator.Record(descriptor.FullTypeName, 4.0);

        var system = accumulator.BuildSnapshot(new[] { descriptor }).Systems.Single();

        Assert.Multiple(() =>
        {
            Assert.That(system.Calls, Is.EqualTo(4), "bounded percentile storage must not truncate whole-capture call count");
            Assert.That(system.TotalMilliseconds, Is.EqualTo(19.0).Within(0.0001), "whole-capture total must include evicted distribution samples");
            Assert.That(system.MeanMilliseconds, Is.EqualTo(4.75).Within(0.0001), "whole-capture mean must use exact count and total");
            Assert.That(system.MaxMilliseconds, Is.EqualTo(10.0).Within(0.0001), "the evicted maximum must remain exact");
            Assert.That(system.Milliseconds, Is.EqualTo(4.0).Within(0.0001), "current must be the last recorded call");
            Assert.That(system.MedianMilliseconds, Is.EqualTo(3.0).Within(0.0001), "median uses only retained values 2, 3, and 4");
            Assert.That(system.P95Milliseconds, Is.EqualTo(4.0).Within(0.0001), "P95 uses only retained values 2, 3, and 4");
            Assert.That(system.P99Milliseconds, Is.EqualTo(4.0).Within(0.0001), "P99 uses only retained values 2, 3, and 4");
        });
    }

    [Test]
    public void Full_marker_timing_wins_and_managed_fallback_only_fills_missing_systems()
    {
        var full = new SystemTimingSnapshot();
        full.AddSystem("System.A", 1.5, MetricConfidence.Full, "Game");

        var managed = new SystemTimingSnapshot();
        managed.AddSystem("System.A", 9.0, MetricConfidence.Managed, "Game");
        managed.AddSystem("System.B", 2.0, MetricConfidence.Managed, "Mod.B", sourceKind: SystemSourceKind.Mod);

        var merged = SystemTimingSnapshotMerger.Merge(full, managed);

        Assert.Multiple(() =>
        {
            Assert.That(merged.Systems.Count, Is.EqualTo(2));
            Assert.That(merged.Systems.Single(x => x.SystemId == "System.A").Milliseconds, Is.EqualTo(1.5));
            Assert.That(merged.Systems.Single(x => x.SystemId == "System.A").Confidence, Is.EqualTo(MetricConfidence.Full));
            Assert.That(merged.Systems.Single(x => x.SystemId == "System.B").Confidence, Is.EqualTo(MetricConfidence.Managed));
        });
    }
}
