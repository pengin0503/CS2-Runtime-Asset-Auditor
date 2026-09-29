using CS2RuntimeAssetAuditor.Export;
using NUnit.Framework;
using Sanitizer = CS2RuntimeAssetAuditor.Assets.Export.PrivacySanitizer;

namespace CS2RuntimeAssetAuditor.Tests;

public class PrivacySanitizerTests
{
    [Test]
    public void Sanitizer_removes_windows_user_paths_and_user_name()
    {
        var text = @"Failure at C:\Users\Alice\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsData\Profiler";
        var sanitized = ReportPrivacy.Sanitize(text);

        Assert.That(sanitized, Does.Not.Contain("Alice"));
        Assert.That(sanitized, Does.Not.Contain(@"C:\Users\"));
        Assert.That(sanitized, Does.Contain(Sanitizer.RedactedPath));
    }

    [Test]
    public void Export_model_does_not_include_city_name_by_default()
    {
        var report = PerformanceReport.CreateForTest();
        Assert.That(report.CityName, Is.Null);
    }

    [Test]
    public void Sanitizer_keeps_non_personal_diagnostic_context()
    {
        var text = "Pathfinding queue unavailable: m_PathfindActions missing";
        Assert.That(ReportPrivacy.Sanitize(text), Is.EqualTo(text));
    }

    [TestCase("tom", "Custom", "Custom")]
    [TestCase("tom", "Bottom edge", "Bottom edge")]
    [TestCase("road", "RoadSection", "RoadSection")]
    [TestCase("game", "Game.Simulation.PathfindQueueSystem", "Game.Simulation.PathfindQueueSystem")]
    [TestCase("game", "MyMod.Game.Systems", "MyMod.Game.Systems")]
    [TestCase("tom", "Report by tom.", "Report by [redacted].")]
    [TestCase("tom", "owner=Tom (local)", "owner=[redacted] (local)")]
    [TestCase("Alice", "alice's save", "[redacted]'s save")]
    public void Account_name_is_redacted_only_as_a_whole_word(string userName, string input, string expected)
    {
        var sanitizer = new Sanitizer(userName, "UNUSED-MACHINE");
        Assert.That(sanitizer.SanitizeText(input), Is.EqualTo(expected));
    }

    [Test]
    public void Machine_name_is_redacted_as_a_whole_word()
    {
        var sanitizer = new Sanitizer("unused-user", "DESKTOP-AB12");
        Assert.That(sanitizer.SanitizeText("Host DESKTOP-AB12 failed"), Is.EqualTo("Host [redacted] failed"));
    }

    [Test]
    public void Very_short_account_names_are_not_used_as_redaction_patterns()
    {
        var sanitizer = new Sanitizer("ab", "xy");
        Assert.That(sanitizer.SanitizeText("ab cd xy"), Is.EqualTo("ab cd xy"));
    }

    [Test]
    public void Unix_and_unc_paths_are_redacted_but_relative_text_is_not()
    {
        var sanitizer = new Sanitizer("unused-user", "unused-machine");
        Assert.Multiple(() =>
        {
            Assert.That(sanitizer.SanitizeText("at /home/bob/mods/x.dll"), Is.EqualTo("at [redacted-path]"));
            Assert.That(sanitizer.SanitizeText(@"at \\server\share\file"), Is.EqualTo("at [redacted-path]"));
            Assert.That(sanitizer.SanitizeText("ms/frame and a/b"), Is.EqualTo("ms/frame and a/b"));
        });
    }
}
