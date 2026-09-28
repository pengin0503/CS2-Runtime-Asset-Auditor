using CS2RuntimeAssetAuditor.Export;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class PerformanceReportSerializerTests
{
    [Test]
    public void Default_report_has_required_schema_sections_but_omits_city_name()
    {
        var json = PerformanceReportSerializer.Serialize(PerformanceReport.CreateForTest());

        Assert.That(json, Does.Contain("\"schemaVersion\":2"));
        Assert.That(json, Does.Contain("\"globalMetrics\""));
        Assert.That(json, Does.Contain("\"systems\""));
        Assert.That(json, Does.Contain("\"modAttribution\""));
        Assert.That(json, Does.Contain("\"pathfinding\""));
        Assert.That(json, Does.Contain("\"domainMetrics\""));
        Assert.That(json, Does.Contain("\"timeline\""));
        Assert.That(json, Does.Contain("\"profilerOverhead\""));
        Assert.That(json, Does.Contain("\"captures\""));
        Assert.That(json, Does.Not.Contain("\"cityName\""));
    }

    [Test]
    public void Warning_paths_are_sanitized_before_serialization()
    {
        var report = PerformanceReport.CreateForTest();
        report.Warnings.Add(@"Read failed at C:\Users\Alice\AppData\LocalLow\Example");

        var json = PerformanceReportSerializer.Serialize(report);

        Assert.That(json, Does.Not.Contain("Alice"));
        Assert.That(json, Does.Not.Contain(@"C:\\Users\\"));
        Assert.That(json, Does.Contain("<user-path>"));
    }
}
