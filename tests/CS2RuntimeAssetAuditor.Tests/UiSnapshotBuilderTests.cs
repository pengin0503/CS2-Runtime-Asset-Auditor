using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class UiSnapshotBuilderTests
{
    [Test]
    public void Builder_projects_already_collected_metrics_without_inventing_values()
    {
        var global = new GlobalMetricsSnapshot(
            12.5,
            selectedSpeed: 4,
            actualSpeed: 2.8,
            new Dictionary<string, RecorderReading>
            {
                ["main"] = new RecorderReading(9.2, 1)
            });

        var pathfinding = new NamedMetricSnapshot(12.5, new[]
        {
            NamedMetricValue.Available("pendingPathfindActions", 42, MetricConfidence.Indirect),
            NamedMetricValue.Unavailable("requestsPerSecond", "No verified runtime request counter is available.")
        });

        var domains = new NamedMetricSnapshot(12.5, new[]
        {
            NamedMetricValue.Available("citizens", 40000, MetricConfidence.Indirect)
        });

        var systems = new SystemTimingSnapshot();
        systems.AddSystem(
            "Example.Mod.System",
            1.4,
            MetricConfidence.Managed,
            "Example.Mod",
            new[] { "patch.owner" },
            sourceKind: SystemSourceKind.Mod);
        systems.SetUnattributedJobsMilliseconds(3.1);

        var input = new UiSnapshotInput(
            global,
            CaptureState.DeepCapture,
            pathfinding,
            domains,
            systems,
            captures: new List<CaptureSession>(),
            profilerOverheadShare: 0.025,
            diagnostics: new[] { "runtime build unverified" });

        var snapshot = UiSnapshotBuilder.Build(input);

        Assert.That(snapshot.Global.SelectedSpeed, Is.EqualTo(4));
        Assert.That(snapshot.Global.ActualSpeed, Is.EqualTo(2.8));
        Assert.That(snapshot.Global.Efficiency, Is.EqualTo(0.7).Within(0.0001));
        Assert.That(snapshot.Capture.State, Is.EqualTo("DeepCapture"));
        Assert.That(snapshot.Systems.Single().OwnerAssembly, Is.EqualTo("Example.Mod"));
        Assert.That(snapshot.Systems.Single().Confidence, Is.EqualTo("Managed"));
        Assert.That(snapshot.Diagnostics.UnattributedJobsMilliseconds, Is.EqualTo(3.1));
        Assert.That(snapshot.Pathfinding.Metrics.Single(x => x.Id == "requestsPerSecond").Value, Is.Null);
        Assert.That(snapshot.Pathfinding.Metrics.Single(x => x.Id == "requestsPerSecond").Reason, Does.Contain("verified"));
        Assert.That(snapshot.DomainMetrics.Single(x => x.Id == "citizens").Value, Is.EqualTo(40000));
    }

    [Test]
    public void Mods_projection_excludes_vanilla_direct_cost_but_keeps_mod_direct_cost()
    {
        var systems = new SystemTimingSnapshot();
        systems.AddSystem(
            "Game.VanillaSystem",
            7.0,
            MetricConfidence.Full,
            ownerAssembly: "Game",
            patchOwners: new[] { "Patch.Mod" },
            sourceKind: SystemSourceKind.Vanilla);
        systems.AddSystem(
            "Example.ModSystem",
            2.5,
            MetricConfidence.Full,
            ownerAssembly: "Example.Mod",
            sourceKind: SystemSourceKind.Mod);

        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput { Systems = systems });

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Mods.Any(row => row.AssemblyName == "Game"), Is.False);
            Assert.That(snapshot.Mods.Single(row => row.AssemblyName == "Example.Mod").DirectSystemMilliseconds, Is.EqualTo(2.5));
            Assert.That(snapshot.Mods.Single(row => row.AssemblyName == "Patch.Mod").DirectSystemMilliseconds, Is.Zero);
            Assert.That(snapshot.Systems.Single(row => row.Id == "Game.VanillaSystem").SourceKind, Is.EqualTo("Vanilla"));
        });
    }

    [Test]
    public void Mods_projection_keeps_container_system_visible_but_excludes_its_inclusive_time_from_direct_totals()
    {
        var systems = new SystemTimingSnapshot();
        systems.AddSystem(
            "Example.Mod.CustomGroup",
            6.0,
            MetricConfidence.Managed,
            ownerAssembly: "Example.Mod",
            sourceKind: SystemSourceKind.Mod,
            isAggregateContainer: true);
        systems.AddSystem(
            "Example.Mod.ChildSystem",
            2.0,
            MetricConfidence.Managed,
            ownerAssembly: "Example.Mod",
            sourceKind: SystemSourceKind.Mod);

        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput { Systems = systems });
        var mod = snapshot.Mods.Single(row => row.AssemblyName == "Example.Mod");

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Systems.Single(row => row.Id == "Example.Mod.CustomGroup").IsAggregateContainer, Is.True);
            Assert.That(mod.DirectSystemMilliseconds, Is.EqualTo(2.0));
            Assert.That(mod.DirectSystemCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Global_recorder_metrics_expose_unit_metadata_for_ui_formatting()
    {
        Assert.That(typeof(GlobalMetricsSnapshot).GetProperty("RecorderUnits"), Is.Not.Null);
        Assert.That(typeof(UiMetricRow).GetProperty("UnitType"), Is.Not.Null);
    }

    [Test]
    public void Missing_input_snapshots_produce_empty_unavailable_ui_not_exceptions()
    {
        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput());

        Assert.That(snapshot.Global.Available, Is.False);
        Assert.That(snapshot.Systems, Is.Empty);
        Assert.That(snapshot.Pathfinding.Metrics, Is.Empty);
        Assert.That(snapshot.DomainMetrics, Is.Empty);
        Assert.That(snapshot.Captures, Is.Empty);
        Assert.That(snapshot.Diagnostics.UnattributedJobsMilliseconds, Is.Null);
    }

    [Test]
    public void Unattributed_job_time_distinguishes_missing_measurement_from_measured_zero()
    {
        var unavailable = UiSnapshotBuilder.Build(new UiSnapshotInput
        {
            Systems = new SystemTimingSnapshot()
        });
        var measuredZeroTiming = new SystemTimingSnapshot();
        measuredZeroTiming.SetUnattributedJobsMilliseconds(0d);
        var measuredZero = UiSnapshotBuilder.Build(new UiSnapshotInput
        {
            Systems = measuredZeroTiming
        });

        Assert.That(unavailable.Diagnostics.UnattributedJobsMilliseconds, Is.Null);
        Assert.That(measuredZero.Diagnostics.UnattributedJobsMilliseconds, Is.EqualTo(0d));
    }
}
