using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class UnifiedSettingsTests
{
    [Test]
    public void Persistent_settings_contain_runtime_and_asset_options_without_a_second_mod_setting()
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "CS2RuntimeAssetAuditor.sln"))) root = root.Parent;
        Assert.That(root, Is.Not.Null);
        var src = Path.Combine(root!.FullName, "src", "CS2RuntimeAssetAuditor");
        var setting = File.ReadAllText(Path.Combine(src, "Setting.cs"));
        var mod = File.ReadAllText(Path.Combine(src, "Mod.cs"));
        foreach (var name in new[] { "EnableMonitoring", "DeepCaptureSeconds", "UiScalePercent", "CollectSubordinateObjects", "CollectNetworkEdges", "FrameBudgetMs", "PageSize", "EnableHeuristicFindings", "ShowNoticeFindings" })
            Assert.That(setting, Does.Contain(name), name);
        Assert.That(Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories)
            .Count(path => File.ReadAllText(path).Contains(": ModSetting")), Is.EqualTo(1));
        Assert.That(mod, Does.Contain("public const string Id = \"CS2RuntimeAssetAuditor\""));
    }
}
