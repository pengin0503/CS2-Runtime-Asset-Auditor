using System.Text.RegularExpressions;
using CS2RuntimeAssetAuditor.Export;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class ReportFileNamesTests
{
    private static readonly DateTime LocalTime = new(2026, 10, 11, 9, 30, 5, 123);

    [Test]
    public void Stems_use_the_local_time_with_milliseconds()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReportFileNames.ReportStem(LocalTime), Is.EqualTo("CS2RuntimeAssetAuditor-report-2026-10-11_093005_123"));
            Assert.That(ReportFileNames.AssetCsvStem(LocalTime), Is.EqualTo("CS2RuntimeAssetAuditor-assets-2026-10-11_093005_123"));
        });
    }

    [Test]
    public void Collisions_get_the_numeric_suffix_the_readme_documents()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var stem = ReportFileNames.ReportStem(LocalTime);
            var first = ReportFileWriter.WriteUnique(directory, stem, _ => { });
            var second = ReportFileWriter.WriteUnique(directory, stem, _ => { });
            var third = ReportFileWriter.WriteUnique(directory, stem, _ => { });

            Assert.That(new[] { first, second, third }.Select(Path.GetFileName),
                Is.EqualTo(new[] { stem + ".json", stem + "-1.json", stem + "-2.json" }));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Readme_names_match_the_names_the_exporter_writes()
    {
        var readme = File.ReadAllText(Path.Combine(FindRoot(), "README.md"));
        // The README writes the timestamp as YYYY-MM-DD_HHmmss_fff; map it back to the .NET format the code uses.
        var documented = Regex.Matches(readme, @"CS2RuntimeAssetAuditor-(report|assets)-([A-Za-z_\-]+)\.(json|csv)")
            .Select(match => (prefix: "CS2RuntimeAssetAuditor-" + match.Groups[1].Value + "-", format: match.Groups[2].Value, extension: match.Groups[3].Value))
            .Distinct()
            .ToArray();

        Assert.That(documented, Is.EquivalentTo(new[]
        {
            (ReportFileNames.ReportPrefix, "YYYY-MM-DD_HHmmss_fff", "json"),
            (ReportFileNames.AssetCsvPrefix, "YYYY-MM-DD_HHmmss_fff", "csv")
        }));
        Assert.That("YYYY-MM-DD_HHmmss_fff".Replace("YYYY", "yyyy").Replace("DD", "dd"), Is.EqualTo(ReportFileNames.TimestampFormat));
        Assert.That(readme, Does.Contain("`-1`、`-2`"));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CS2RuntimeAssetAuditor.sln")))
            directory = directory.Parent;
        Assert.That(directory, Is.Not.Null);
        return directory!.FullName;
    }
}
