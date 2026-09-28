using System;
using CS2RuntimeAssetAuditor.Coordination;
using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.Assets.Export;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class UnifiedExportTests
{
    [Test]
    public void Envelope_has_separate_optional_sections_and_no_invented_measurements()
    {
        var session = DiagnosticSessionContext.Create("game", "build", DateTimeOffset.UtcNow);
        var report = RuntimeAssetAuditReportBuilder.Build(null, new AuditReport(), session, DateTimeOffset.UtcNow);
        var json = RuntimeAssetAuditReportSerializer.Serialize(report);
        Assert.That(json, Does.Contain("\"schemaVersion\":1"));
        foreach (var field in new[] { "generatedAtUtc", "product", "session", "runtime", "advisor", "assets", "evidenceLinks", "capabilities", "diagnostics", "privacy" })
            Assert.That(json, Does.Contain("\"" + field + "\""));
        Assert.That(json, Does.Contain("\"runtime\":null"));
        Assert.That(json, Does.Contain("\"assets\":"));
        Assert.That(json, Does.Not.Contain("perAssetGpuMs"));
    }
}
