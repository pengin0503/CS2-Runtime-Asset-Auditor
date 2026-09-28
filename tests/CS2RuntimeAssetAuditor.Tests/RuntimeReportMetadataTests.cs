using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class RuntimeReportMetadataTests
{
    [TearDown]
    public void TearDown()
    {
        ProfilerReportBuilder.RuntimeMetadataProvider = null;
    }

    [Test]
    public void Builder_includes_runtime_hardware_enabled_mods_and_build_id()
    {
        var metadata = new RuntimeReportMetadata
        {
            BuildId = "abcdef123456",
            HardwareSummary = "CPU=Example CPU; RAM=32768 MB; GPU=Example GPU",
            EnabledMods = new[] { "Traffic.Mod", "Asset.Mod", "Traffic.Mod", "" }
        };

        var report = ProfilerReportBuilder.Build(
            new UiSnapshot(),
            gameVersion: "1.6.2f1",
            profilerVersion: "1.0.0",
            metadata: metadata);

        Assert.Multiple(() =>
        {
            Assert.That(report.BuildId, Is.EqualTo("abcdef123456"));
            Assert.That(report.HardwareSummary, Is.EqualTo(metadata.HardwareSummary));
            Assert.That(report.EnabledMods, Is.EqualTo(new[] { "Asset.Mod", "Traffic.Mod" }));
        });
    }

    [Test]
    public void Builder_uses_registered_runtime_metadata_provider_when_metadata_is_not_explicit()
    {
        ProfilerReportBuilder.RuntimeMetadataProvider = () => new RuntimeReportMetadata
        {
            BuildId = "runtime-build",
            HardwareSummary = "CPU=Runtime CPU",
            EnabledMods = new[] { "Runtime.Mod" }
        };

        var report = ProfilerReportBuilder.Build(new UiSnapshot());

        Assert.Multiple(() =>
        {
            Assert.That(report.BuildId, Is.EqualTo("runtime-build"));
            Assert.That(report.HardwareSummary, Is.EqualTo("CPU=Runtime CPU"));
            Assert.That(report.EnabledMods, Is.EqualTo(new[] { "Runtime.Mod" }));
        });
    }
}
