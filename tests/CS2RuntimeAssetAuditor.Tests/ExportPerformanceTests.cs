using System;
using System.Diagnostics;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Export;
using CS2RuntimeAssetAuditor.Assets.UI;
using CS2RuntimeAssetAuditor.Export;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class ExportPerformanceTests
{
    [Test]
    public void Csv_counts_owned_and_evidence_attributed_findings_per_row()
    {
        var report = new AuditReport
        {
            Catalog = new[]
            {
                new ReportPrefab { PrefabId = "A", PrefabType = "Building", DisplayName = "A" },
                new ReportPrefab { PrefabId = "B", PrefabType = "Building", DisplayName = "B" }
            }
        };
        report.Analysis.Findings = new[]
        {
            new ReportFinding { RuleId = "r1", PrefabId = "A", PrefabType = "Building" },
            new ReportFinding { RuleId = "r2", PrefabId = "A", PrefabType = "Building" },
            // Unowned finding attributed once by evidence, even when the evidence repeats.
            new ReportFinding { RuleId = "r3", Evidence = new[] { "asset=B", "asset=B" } }
        };

        var lines = new CsvSummaryExporter().Export(report).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines[1], Does.StartWith("A,Building,A,").And.EndWith(",2"));
        Assert.That(lines[2], Does.StartWith("B,Building,B,").And.EndWith(",1"));
    }

    [Test]
    public void Csv_export_of_a_large_playset_stays_linear()
    {
        const int prefabs = 20000;
        var report = new AuditReport
        {
            Catalog = Enumerable.Range(0, prefabs)
                .Select(index => new ReportPrefab { PrefabId = "P" + index, PrefabType = "Building", DisplayName = "P" + index })
                .ToArray()
        };
        report.Analysis.Findings = Enumerable.Range(0, 5000)
            .Select(index => new ReportFinding { RuleId = "r", PrefabId = "P" + index, PrefabType = "Building", Evidence = new[] { "asset=P" + index } })
            .ToArray();

        var stopwatch = Stopwatch.StartNew();
        var csv = new CsvSummaryExporter().Export(report);
        stopwatch.Stop();

        Assert.That(csv.Split('\n').Length, Is.GreaterThan(prefabs));
        // The former per-row scan performed ~10^8 comparisons here; the linear pass finishes far below this bound.
        Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void Hidden_panel_never_publishes_and_deferred_changes_publish_once_shown()
    {
        var interval = TimeSpan.FromMilliseconds(200);
        Assert.Multiple(() =>
        {
            Assert.That(UiPublishPolicy.Decide(false, true, true, true, true, TimeSpan.FromMinutes(1), interval), Is.EqualTo(UiPublishDecision.Skip));
            Assert.That(UiPublishPolicy.Decide(true, true, false, false, true, TimeSpan.Zero, interval), Is.EqualTo(UiPublishDecision.Publish));
            Assert.That(UiPublishPolicy.Decide(true, false, false, false, false, TimeSpan.FromMinutes(1), interval), Is.EqualTo(UiPublishDecision.Skip));
        });
    }
}
