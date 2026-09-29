using System.Xml.Linq;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

// A mod folder without CS2RuntimeAssetAuditor.mjs loads the DLL but shows no launcher icon. The UI build used to be
// skipped silently when the project was built on its own, because $(SolutionDir) is "*Undefined*" there.
public class UiPackagingTests
{
    [Test]
    public void Mod_build_always_builds_the_UI_into_the_output_the_toolchain_deploys()
    {
        var buildUi = FindTarget("BuildUI");
        var projectText = File.ReadAllText(FindModProjectPath());

        Assert.Multiple(() =>
        {
            Assert.That(projectText, Does.Not.Contain("$(SolutionDir)"),
                "The UI project must be located relative to the mod project, not the solution.");
            Assert.That((string?)buildUi.Attribute("BeforeTargets"), Is.EqualTo("DeployWIP"),
                "The bundle must be in $(OutDir) before Mod.targets copies it to the Mods folder (DeployWIP removes that folder first).");
            Assert.That((string?)buildUi.Attribute("Condition") ?? string.Empty, Does.Not.Contain("Exists("),
                "A missing UI project must fail the build instead of silently skipping the bundle.");

            var exec = buildUi.Elements("Exec").Single();
            Assert.That((string?)exec.Attribute("Command"), Is.EqualTo("npm run build"));
            Assert.That((string?)exec.Attribute("EnvironmentVariables"), Is.EqualTo("CS2_MOD_UI_OUTPUT_DIR=$(TargetDir)"));
        });
    }

    [Test]
    public void Mod_build_fails_when_a_shipped_UI_file_is_missing()
    {
        var document = XDocument.Load(FindModProjectPath());
        var required = document.Descendants("ModUiRequiredFile")
            .Select(item => (string?)item.Attribute("Include"))
            .ToArray();
        var errors = FindTarget("BuildUI").Elements("Error")
            .Select(error => (string?)error.Attribute("Condition") ?? string.Empty)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(required, Is.EquivalentTo(new[]
            {
                "$(AssemblyName).mjs",
                "$(AssemblyName).css",
                "cs2-runtime-asset-auditor-images\\profiler-icon.svg"
            }));
            Assert.That(errors, Has.Some.EqualTo("!Exists('$(TargetDir)%(ModUiRequiredFile.Identity)')"));
            Assert.That(errors, Has.Some.Contains("node_modules"), "Missing UI dependencies must be reported with the fix.");
        });
    }

    [Test]
    public void Webpack_writes_to_the_mod_output_when_the_mod_build_passes_one()
    {
        var config = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "UI", "webpack.config.js"));

        Assert.Multiple(() =>
        {
            Assert.That(config, Does.Contain("process.env.CS2_MOD_UI_OUTPUT_DIR"));
            Assert.That(config, Does.Contain("generator: { filename: \"cs2-runtime-asset-auditor-images/[name][ext]\" }"),
                "The icon path must match the file the mod build verifies.");
        });
    }

    private static XElement FindTarget(string name)
    {
        var target = XDocument.Load(FindModProjectPath())
            .Descendants("Target")
            .SingleOrDefault(element => (string?)element.Attribute("Name") == name);
        Assert.That(target, Is.Not.Null, $"Target {name} must exist.");
        return target!;
    }

    private static string FindModProjectPath() =>
        Path.Combine(FindRepositoryRoot(), "src", "CS2RuntimeAssetAuditor", "CS2RuntimeAssetAuditor.csproj");

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
