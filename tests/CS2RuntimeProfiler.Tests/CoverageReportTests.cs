using CS2RuntimeProfiler.Export;
using CS2RuntimeProfiler.UI;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class CoverageReportTests
{
    [Test]
    public void Report_exports_explicit_attempted_activated_and_sampled_marker_coverage()
    {
        var snapshot = new UiSnapshot
        {
            Captures = new[]
            {
                new CaptureSummaryUi
                {
                    Id = "capture-coverage",
                    TriggerKind = "Manual",
                    DiscoveredMarkers = 100,
                    AttemptedMarkers = 80,
                    ActivatedMarkers = 70,
                    SampledMarkers = 25,
                    CapturedMarkers = 25,
                    AttemptedRatio = 0.8,
                    ActivatedRatio = 0.7,
                    SampledRatio = 0.25,
                    CoverageRatio = 0.25
                }
            }
        };

        var capture = ProfilerReportBuilder.Build(snapshot).Captures.Single();

        Assert.Multiple(() =>
        {
            Assert.That(capture.DiscoveredMarkers, Is.EqualTo(100));
            Assert.That(capture.AttemptedMarkers, Is.EqualTo(80));
            Assert.That(capture.ActivatedMarkers, Is.EqualTo(70));
            Assert.That(capture.SampledMarkers, Is.EqualTo(25));
            Assert.That(capture.CapturedMarkers, Is.EqualTo(25));
            Assert.That(capture.AttemptedRatio, Is.EqualTo(0.8));
            Assert.That(capture.ActivatedRatio, Is.EqualTo(0.7));
            Assert.That(capture.SampledRatio, Is.EqualTo(0.25));
            Assert.That(capture.CoverageRatio, Is.EqualTo(0.25));
        });
    }
}
