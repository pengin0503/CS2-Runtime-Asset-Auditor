using System.Linq;
using CS2RuntimeAssetAuditor.Export;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class EnabledModNamesTests
{
    private const string TrafficFullName = "Traffic, Version=1.2.0.0, Culture=neutral, PublicKeyToken=null";
    private const string BrokenFullName = "Broken, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null";

    private static readonly ModLoadInfo[] Mods =
    {
        new ModLoadInfo(TrafficFullName, true, "Loaded"),
        new ModLoadInfo(BrokenFullName, false, "LoadAssemblyReferenceError"),
        new ModLoadInfo("SharedLibrary, Version=1.0.0.0", false, "IsNotModWarning"),
        new ModLoadInfo("Duplicate, Version=1.0.0.0", false, "IsNotUniqueWarning")
    };

    [Test]
    public void Enabled_mods_are_the_games_list_with_code_mods_by_assembly_name_and_ui_modules_by_name()
    {
        // ListModsEnabled(): loaded code mods' full assembly names, then UI module names.
        var enabled = EnabledModNames.Enabled(new[] { TrafficFullName, "RoadBuilder UI", "C:\\Users\\kate\\ui" }, Mods);

        Assert.That(enabled, Is.EqualTo(new[] { "RoadBuilder UI", "Traffic" }));
    }

    [Test]
    public void Code_mods_the_game_did_not_load_are_listed_with_the_games_reason()
    {
        Assert.That(EnabledModNames.Failed(Mods), Is.EqualTo(new[]
        {
            "Broken (LoadAssemblyReferenceError)",
            "Duplicate (IsNotUniqueWarning)"
        }));
    }

    [Test]
    public void A_failed_mod_is_not_reported_as_enabled()
    {
        var enabled = EnabledModNames.Enabled(new[] { TrafficFullName }, Mods);

        Assert.That(enabled.Any(name => name.StartsWith("Broken")), Is.False);
    }

    [Test]
    public void Report_lists_load_failures_as_warnings()
    {
        var report = ProfilerReportBuilder.Build(new CS2RuntimeAssetAuditor.UI.UiSnapshot(),
            metadata: new RuntimeReportMetadata
            {
                EnabledMods = new[] { "Traffic" },
                ModLoadFailures = new[] { "Broken (LoadAssemblyReferenceError)" }
            });

        Assert.That(report.EnabledMods, Is.EqualTo(new[] { "Traffic" }));
        Assert.That(report.Warnings, Does.Contain("Mod did not load: Broken (LoadAssemblyReferenceError)"));
    }
}
