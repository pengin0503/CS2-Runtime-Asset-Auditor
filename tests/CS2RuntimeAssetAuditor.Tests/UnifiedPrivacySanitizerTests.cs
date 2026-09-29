using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
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
        var output = new MemoryStream();
        using (var json = JsonReaderWriterFactory.CreateJsonWriter(output, Encoding.UTF8, ownsStream: false))
        {
            var writer = new SanitizingJsonWriter(json, value => value == "runtime" || value == "assets" ? "[redacted]" : value);
            writer.WriteStartElement("root");
            writer.WriteAttributeString("type", "object");
            writer.WriteStartElement("runtime");
            writer.WriteValue("runtime");
            writer.WriteEndElement();
            writer.WriteStartElement("nested");
            writer.WriteAttributeString("type", "object");
            writer.WriteStartElement("assets");
            writer.WriteValue("assets");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.Flush();
        }
        Assert.That(Encoding.UTF8.GetString(output.ToArray()), Is.EqualTo(@"{""runtime"":""[redacted]"",""nested"":{""assets"":""[redacted]""}}"));
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
