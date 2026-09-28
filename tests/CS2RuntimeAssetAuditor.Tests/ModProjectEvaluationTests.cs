using System.Xml.Linq;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class ModProjectEvaluationTests
{
    [Test]
    public void Mod_project_delegates_runtime_paths_to_the_CS2_toolchain()
    {
        var projectPath = FindModProjectPath();
        var projectText = File.ReadAllText(projectPath);

        Assert.Multiple(() =>
        {
            Assert.That(projectText, Does.Contain("<Import Project=\"$(CSII_TOOLPATH)\\Mod.props\" />"));
            Assert.That(projectText, Does.Contain("<Import Project=\"$(CSII_TOOLPATH)\\Mod.targets\" />"));
            Assert.That(projectText, Does.Not.Contain("CSII_MANAGED_PATH"));
            Assert.That(projectText, Does.Not.Contain("<CustomManagedPath"));
            Assert.That(projectText, Does.Not.Contain("<MSCORLIBPath"));
        });
    }

    [Test]
    public void Mod_project_updates_framework_serialization_reference_instead_of_adding_one()
    {
        var document = XDocument.Load(FindModProjectPath());
        var references = document
            .Descendants("Reference")
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                references.Any(reference => (string?)reference.Attribute("Include") == "System.Runtime.Serialization"),
                Is.False,
                "Adding a new System.Runtime.Serialization reference can resolve a second framework profile through the CS2 Managed search path.");
            Assert.That(
                references.Any(reference => (string?)reference.Attribute("Update") == "System.Runtime.Serialization"),
                Is.True,
                "The existing framework reference should only have Private metadata updated.");
        });
    }

    [Test]
    public void Production_sources_avoid_framework_profile_conflicting_Queue_type()
    {
        var sourceRoot = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CS2RuntimeAssetAuditor");
        var productionSource = string.Join(
            Environment.NewLine,
            Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !Path.GetRelativePath(sourceRoot, path)
                    .Split(Path.DirectorySeparatorChar)
                    .Any(segment => segment is "bin" or "obj"))
                .Select(File.ReadAllText));

        Assert.That(
            productionSource,
            Does.Not.Contain("Queue<"),
            "System.Collections.Generic.Queue<T> resolves from both System and mscorlib in the current CS2 build profile. Use an array-backed fixed-capacity ring buffer instead.");
    }

    [Test]
    public void Domain_metrics_uses_the_Game_Tools_Temp_component()
    {
        var sourcePath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CS2RuntimeAssetAuditor",
            "Collectors",
            "DomainMetricsSystem.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.That(
            source,
            Does.Contain("ComponentType.ReadOnly<Game.Tools.Temp>()"),
            "The CS2 Game.dll exposes Temp as Game.Tools.Temp, not Game.Common.Temp.");
    }

    private static string FindModProjectPath()
    {
        return Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CS2RuntimeAssetAuditor",
            "CS2RuntimeAssetAuditor.csproj");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CS2RuntimeAssetAuditor.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test directory.");
    }
}
