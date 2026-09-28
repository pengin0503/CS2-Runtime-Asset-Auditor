using CS2RuntimeProfiler.Export;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class PrivacySanitizerTests
{
    [Test]
    public void Sanitizer_removes_windows_user_paths_and_user_name()
    {
        var text = @"Failure at C:\Users\Alice\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsData\Profiler";
        var sanitized = PrivacySanitizer.Sanitize(text);

        Assert.That(sanitized, Does.Not.Contain("Alice"));
        Assert.That(sanitized, Does.Not.Contain(@"C:\Users\"));
        Assert.That(sanitized, Does.Contain("<user-path>"));
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
        Assert.That(PrivacySanitizer.Sanitize(text), Is.EqualTo(text));
    }
}
