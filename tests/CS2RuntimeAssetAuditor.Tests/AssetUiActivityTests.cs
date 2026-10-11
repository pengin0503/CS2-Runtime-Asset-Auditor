using System;
using CS2RuntimeAssetAuditor.Core.DiagnosticLog;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

// Issue #49: the diagnostic log counts Asset panel publishes and live telemetry snapshots per row, with the running
// scan, so their rate can be measured while scanning and while idle.
public class AssetUiActivityTests
{
    [Test]
    public void Counts_are_taken_once_per_row_and_written_with_the_running_scan()
    {
        AssetUiActivity.Take();
        AssetUiActivity.RecordPublish();
        AssetUiActivity.RecordPublish();
        AssetUiActivity.RecordTelemetrySnapshot();
        var counts = AssetUiActivity.Take();
        Assert.That(counts.Publishes, Is.EqualTo(2));
        Assert.That(counts.TelemetrySnapshots, Is.EqualTo(1));
        Assert.That(AssetUiActivity.Take().Publishes, Is.EqualTo(0), "taking clears the counts");

        var header = DiagnosticLogCsv.FormatHeader(null).Split(',');
        var accumulator = new DiagnosticLogAccumulator(null);
        var scanning = DiagnosticLogCsv.FormatRow(accumulator.Complete(DateTimeOffset.UnixEpoch, 1,
            new DiagnosticIntervalContext(null, null, null, null, null, assetScan: "AssetAudit:CollectingGeometry", assetUi: counts))).Split(',');
        var uncounted = DiagnosticLogCsv.FormatRow(accumulator.Complete(DateTimeOffset.UnixEpoch, 2, default)).Split(',');

        Assert.Multiple(() =>
        {
            Assert.That(scanning.Length, Is.EqualTo(header.Length));
            Assert.That(scanning[Array.IndexOf(header, "assetScan")], Is.EqualTo("AssetAudit:CollectingGeometry"));
            Assert.That(scanning[Array.IndexOf(header, "assetUiPublishes")], Is.EqualTo("2"));
            Assert.That(scanning[Array.IndexOf(header, "assetTelemetrySnapshots")], Is.EqualTo("1"));
            Assert.That(uncounted[Array.IndexOf(header, "assetUiPublishes")], Is.Empty);
            Assert.That(uncounted[Array.IndexOf(header, "assetScan")], Is.Empty);
        });
    }
}
