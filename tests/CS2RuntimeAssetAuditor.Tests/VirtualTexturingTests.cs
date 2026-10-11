using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.DiagnosticLog;
using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

// Issue #56: the game's virtual-texturing streaming state, read only, never shown as VRAM usage.
public class VirtualTexturingTests
{
    [Test]
    public void Values_the_game_has_not_produced_yet_are_unavailable_not_zero()
    {
        // TextureStreamingSystem before Initialize: workingSetLodBias starts at 0 and mipBias at -1.
        var metrics = VirtualTexturingMetrics.ToMetrics(VirtualTexturingReading.FromSystem(0f, -1, 0, 0, 0, 0));

        Assert.Multiple(() =>
        {
            Assert.That(metrics.Single(m => m.Id == VirtualTexturingMetrics.WorkingSetLodBias).Availability, Is.EqualTo(MetricAvailability.Unavailable));
            Assert.That(metrics.Single(m => m.Id == VirtualTexturingMetrics.WorkingSetLodBias).Reason, Does.Contain("readback"));
            Assert.That(metrics.Single(m => m.Id == VirtualTexturingMetrics.WorkingSetLodBiasLevels).Value, Is.Null);
            Assert.That(metrics.Single(m => m.Id == VirtualTexturingMetrics.MipBias).Availability, Is.EqualTo(MetricAvailability.Unavailable));
            Assert.That(metrics.Single(m => m.Id == VirtualTexturingMetrics.BusyRequests).Value, Is.EqualTo(0));
        });
    }

    [Test]
    public void The_working_set_bias_is_also_given_in_mip_levels()
    {
        var reading = VirtualTexturingReading.FromSystem(2f, 1, 3, 29, 120, 4);
        var metrics = VirtualTexturingMetrics.ToMetrics(reading).ToDictionary(m => m.Id);

        Assert.Multiple(() =>
        {
            Assert.That(metrics[VirtualTexturingMetrics.WorkingSetLodBias].Value, Is.EqualTo(2));
            Assert.That(metrics[VirtualTexturingMetrics.WorkingSetLodBias].UnitType, Is.EqualTo(MetricUnits.Factor));
            Assert.That(metrics[VirtualTexturingMetrics.WorkingSetLodBiasLevels].Value, Is.EqualTo(1).Within(1e-9));
            Assert.That(VirtualTexturingReading.FromSystem(1f, 0, 0, 0, 0, 0).WorkingSetLodBiasLevels, Is.EqualTo(0));
            Assert.That(metrics[VirtualTexturingMetrics.MipBias].Value, Is.EqualTo(1));
            Assert.That(metrics[VirtualTexturingMetrics.AvailableRequests].Value, Is.EqualTo(29));
            Assert.That(metrics[VirtualTexturingMetrics.MaterialsLeftToLoad].Value, Is.EqualTo(120));
        });
    }

    [Test]
    public void A_failed_read_makes_every_value_unavailable_with_the_reason()
    {
        var metrics = VirtualTexturingMetrics.ToMetrics(VirtualTexturingReading.Unavailable("system missing"));
        Assert.That(metrics.All(m => m.Availability == MetricAvailability.Unavailable && m.Reason == "system missing"), Is.True);
    }

    [Test]
    public void Diagnostic_rows_write_the_latest_read_of_the_interval_and_empty_cells_without_one()
    {
        var accumulator = new DiagnosticLogAccumulator(null);
        var header = DiagnosticLogCsv.FormatHeader(null).Split(',');
        var bias = Array.IndexOf(header, "vtWorkingSetLodBias");
        var left = Array.IndexOf(header, "vtMaterialsLeftToLoad");
        Assert.That(bias, Is.GreaterThan(0));

        accumulator.AddVirtualTexturing(VirtualTexturingReading.FromSystem(1.25f, 1, 2, 30, 50, 0));
        accumulator.AddVirtualTexturing(VirtualTexturingReading.FromSystem(1.5f, 1, 2, 30, 40, 0));
        var read = DiagnosticLogCsv.FormatRow(accumulator.Complete(DateTimeOffset.UnixEpoch, 1, default)).Split(',');
        var none = DiagnosticLogCsv.FormatRow(accumulator.Complete(DateTimeOffset.UnixEpoch, 2, default)).Split(',');

        Assert.Multiple(() =>
        {
            Assert.That(read.Length, Is.EqualTo(header.Length));
            Assert.That(read[bias], Is.EqualTo("1.5"));
            Assert.That(read[left], Is.EqualTo("40"));
            Assert.That(none[bias], Is.Empty, "a row without a read does not repeat the previous one");
        });
    }

    [Test]
    public void Snapshot_and_report_carry_the_values_with_their_meaning_and_the_vram_caveat()
    {
        var global = new GlobalMetricsSnapshot(1, 1, 1, new Dictionary<string, RecorderReading>(),
            virtualTexturing: VirtualTexturingReading.FromSystem(1.5f, 1, 2, 30, 0, 0));

        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput { Global = global });
        Assert.That(snapshot.Global.VirtualTexturing.Select(m => m.Id), Is.EqualTo(new[]
        {
            VirtualTexturingMetrics.WorkingSetLodBias, VirtualTexturingMetrics.WorkingSetLodBiasLevels, VirtualTexturingMetrics.MipBias,
            VirtualTexturingMetrics.BusyRequests, VirtualTexturingMetrics.AvailableRequests, VirtualTexturingMetrics.MaterialsLeftToLoad
        }));

        var report = ProfilerReportBuilder.Build(snapshot, metadata: new RuntimeReportMetadata());
        var bias = report.GlobalMetrics.Single(m => m.Name == VirtualTexturingMetrics.WorkingSetLodBias);
        Assert.Multiple(() =>
        {
            Assert.That(bias.Value, Is.EqualTo(1.5));
            Assert.That(bias.Note, Does.Contain("read back from the GPU"));
            Assert.That(report.Warnings, Does.Contain(VirtualTexturingMetrics.Limitation));
            Assert.That(VirtualTexturingMetrics.Limitation, Does.Contain("not VRAM usage"));
        });
    }

    [Test]
    public void Samples_without_a_read_show_no_virtual_texturing_section()
    {
        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput { Global = new GlobalMetricsSnapshot(1, 1, 1, new Dictionary<string, RecorderReading>()) });
        Assert.That(snapshot.Global.VirtualTexturing, Is.Empty);
        var report = ProfilerReportBuilder.Build(snapshot, metadata: new RuntimeReportMetadata());
        Assert.That(report.Warnings, Does.Not.Contain(VirtualTexturingMetrics.Limitation));
    }
}
