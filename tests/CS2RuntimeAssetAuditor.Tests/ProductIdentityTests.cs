using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class ProductIdentityTests
{
    [Test]
    public void Product_uses_one_unified_mod_identity_assembly_and_export_root()
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "src", "CS2RuntimeAssetAuditor")))
            root = root.Parent;
        Assert.That(root, Is.Not.Null);
        var source = File.ReadAllText(Path.Combine(root!.FullName, "src", "CS2RuntimeAssetAuditor", "Mod.cs"));
        var project = File.ReadAllText(Path.Combine(root.FullName, "src", "CS2RuntimeAssetAuditor", "CS2RuntimeAssetAuditor.csproj"));
        var exporter = File.ReadAllText(Path.Combine(root.FullName, "src", "CS2RuntimeAssetAuditor", "Export", "ReportExporter.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("public const string Id = \"CS2RuntimeAssetAuditor\""));
            Assert.That(project, Does.Contain("<AssemblyName>CS2RuntimeAssetAuditor</AssemblyName>"));
            Assert.That(exporter, Does.Contain("\"ModsData\", Mod.Id"));
            Assert.That(source, Does.Not.Contain("CS2 Runtime Profiler"));
        });
    }
}
