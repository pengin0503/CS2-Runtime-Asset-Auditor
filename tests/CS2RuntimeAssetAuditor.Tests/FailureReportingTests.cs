using CS2RuntimeAssetAuditor.Assets.Core.Capabilities;
using CS2RuntimeAssetAuditor.Assets.Core.Scanning;
using CS2RuntimeAssetAuditor.Export;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class FailureReportingTests
{
    [TestCase(ScanStage.ResolvingRenderGraph, CapabilityId.GeometryMetadata)]
    [TestCase(ScanStage.CollectingGeometry, CapabilityId.GeometryMetadata)]
    [TestCase(ScanStage.CollectingSurfaceTexture, CapabilityId.SurfaceMetadata)]
    public void Failed_collection_stage_degrades_the_capability_it_reads(ScanStage stage, CapabilityId expected)
    {
        Assert.That(AuditStageFailure.CapabilityFor(stage), Is.EqualTo(expected));
    }

    [Test]
    public void Findings_evaluation_failure_degrades_no_game_capability()
    {
        Assert.That(AuditStageFailure.CapabilityFor(ScanStage.EvaluatingFindings), Is.Null);
        Assert.That(AuditStageFailure.DetailFor(ScanStage.EvaluatingFindings), Is.EqualTo("asset_findings_stage_failed"));
    }

    [Test]
    public void Export_results_share_one_protocol()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ExportResultFormat.Succeeded("report.json"), Is.EqualTo("ok:report.json"));
            Assert.That(ExportResultFormat.Failed("APA-EXP-001", "disk full"), Is.EqualTo("error:APA-EXP-001: disk full"));
            Assert.That(ExportResultFormat.Failed(null!, "disk full"), Is.EqualTo("error:disk full"));
            Assert.That(ExportResultFormat.Failed("APA-EXP-004", " "), Is.EqualTo("error:APA-EXP-004: unknown error"));
        });
    }

    [Test]
    public void Asset_ui_never_publishes_json_error_payloads_or_swallows_exceptions()
    {
        var root = FindRoot();
        var ui = File.ReadAllText(Path.Combine(root, "src", "CS2RuntimeAssetAuditor", "Assets", "UI", "AssetAuditUISystem.cs"));
        var audit = File.ReadAllText(Path.Combine(root, "src", "CS2RuntimeAssetAuditor", "Assets", "GameIntegration", "AssetAuditSystem.cs"));
        Assert.That(ui, Does.Not.Contain("errorCode"));
        foreach (var source in new[] { ui, audit })
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(source, @"catch\s*\r?\n\s*\{"), Is.False,
                "handled failures must keep the exception so it can be logged");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CS2RuntimeAssetAuditor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
