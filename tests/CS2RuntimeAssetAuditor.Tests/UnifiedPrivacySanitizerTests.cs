using System;
using CS2RuntimeAssetAuditor.Coordination;
using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.Assets.Export;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class UnifiedPrivacySanitizerTests
{
    [Test]
    public void Redacts_values_without_renaming_schema_fields()
    {
        var json = @"{""runtime"":""runtime"",""nested"":{""assets"":""assets""}}";
        var sanitized = RuntimeAssetAuditReportSerializer.SanitizeJsonStrings(json,
            value => value == "runtime" || value == "assets" ? "[redacted]" : value);
        Assert.That(sanitized, Is.EqualTo(@"{""runtime"":""[redacted]"",""nested"":{""assets"":""[redacted]""}}"));
    }

    [Test]
    public void Sanitizes_runtime_and_asset_strings_through_one_serialization_boundary()
    {
        var runtime = PerformanceReport.CreateForTest();
        runtime.Warnings.Add(@"C:\Users\Alice\Runtime\issue");
        var assets = new AuditReport { GameVersion = "/home/alice/assets/file" };
        var report = RuntimeAssetAuditReportBuilder.Build(runtime, assets,
            DiagnosticSessionContext.Create("game", "build", DateTimeOffset.UtcNow), DateTimeOffset.UtcNow);
        var json = RuntimeAssetAuditReportSerializer.Serialize(report);
        Assert.That(json, Does.Not.Contain("Alice").IgnoreCase);
        Assert.That(json, Does.Not.Contain("/home/alice"));
        Assert.That(json, Does.Contain("redacted"));
    }
}
