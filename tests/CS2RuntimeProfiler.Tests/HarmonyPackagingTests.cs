using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class HarmonyPackagingTests
{
    [Test]
    public void Mod_project_packages_and_verifies_private_Harmony_runtime()
    {
        var projectPath = FindProjectFile();
        var document = XDocument.Load(projectPath);
        var properties = document.Descendants()
            .Where(element => element.Name.LocalName == "CopyLocalLockFileAssemblies")
            .Select(element => element.Value.Trim())
            .ToArray();
        var harmonyReference = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "PackageReference"
                && string.Equals((string)element.Attribute("Include"), "Lib.Harmony", StringComparison.Ordinal));
        var verifier = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Target"
                && string.Equals((string)element.Attribute("Name"), "VerifyHarmonyRuntimeWasCopied", StringComparison.Ordinal));
        var verifierError = verifier?.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Error");

        Assert.Multiple(() =>
        {
            Assert.That(properties, Does.Contain("true"), "NuGet runtime assemblies must be copied beside the mod DLL.");
            Assert.That(harmonyReference, Is.Not.Null, "Lib.Harmony must be an explicit private runtime dependency.");
            Assert.That((string)harmonyReference?.Attribute("Version"), Is.EqualTo("2.2.2"));
            Assert.That((string)harmonyReference?.Attribute("PrivateAssets"), Is.EqualTo("all"));
            Assert.That(verifierError, Is.Not.Null, "Build must fail if 0Harmony.dll was not copied.");
            Assert.That((string)verifierError?.Attribute("Condition"), Does.Contain("0Harmony.dll"));
        });
    }

    private static string FindProjectFile()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "CS2RuntimeProfiler", "CS2RuntimeProfiler.csproj");
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate src/CS2RuntimeProfiler/CS2RuntimeProfiler.csproj from the test directory.");
    }
}
