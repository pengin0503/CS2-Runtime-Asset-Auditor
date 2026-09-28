using CS2RuntimeProfiler.Export;
using CS2RuntimeProfiler.UI;
using NUnit.Framework;
using System.Text.Json;

namespace CS2RuntimeProfiler.Tests;

public class ProfilerReportBuilderTests
{
    [Test]
    public void Builder_maps_visible_snapshot_sections_without_city_name()
    {
        var snapshot = new UiSnapshot
        {
            Global = new GlobalUiMetrics
            {
                Available = true,
                TimestampSeconds = 12d,
                SelectedSpeed = 4,
                ActualSpeed = 2.8,
                Efficiency = 0.7,
                RecorderMetrics = new[]
                {
                    new UiMetricRow
                    {
                        Id = "main",
                        Value = 9.2,
                        UnitType = "TimeNanoseconds",
                        Confidence = "Full",
                        Availability = "Available"
                    }
                }
            },
            Capture = new CaptureUiState
            {
                State = "Monitoring",
                DetailCaptureId = "capture-1",
                DetailScope = "historical-capture"
            },
            Captures = new[]
            {
                new CaptureSummaryUi
                {
                    Id = "capture-1",
                    TriggerKind = "Manual",
                    TriggeredAtSeconds = 10d,
                    DurationSeconds = 12d,
                    DiscoveredMarkers = 592,
                    CapturedMarkers = 34,
                    CoverageRatio = 34d / 592d,
                    Batched = true,
                    ProfilerOverheadShare = 0.025d,
                    Warnings = new[] { "diagnostic warning" }
                }
            },
            Systems = new[]
            {
                new SystemUiRow
                {
                    Id = "Example.System",
                    OwnerAssembly = "Example.Mod",
                    CurrentMilliseconds = 6.0,
                    MeanMilliseconds = 4.0,
                    MedianMilliseconds = 4.0,
                    P95Milliseconds = 6.0,
                    P99Milliseconds = 6.0,
                    MaxMilliseconds = 6.0,
                    TotalMilliseconds = 12.0,
                    Calls = 9,
                    Confidence = "Full"
                }
            },
            Mods = new[]
            {
                new ModUiRow { AssemblyName = "Example.Mod", DirectSystemMilliseconds = 6.0, DirectSystemCount = 1 }
            },
            Pathfinding = new PathfindingUiMetrics
            {
                Metrics = new[] { new UiMetricRow { Id = "pendingPathfindActions", Value = 42, Confidence = "Indirect", Availability = "Available" } }
            },
            DomainMetrics = new[]
            {
                new UiMetricRow { Id = "citizens", Value = 40000, Confidence = "Indirect", Availability = "Available" }
            },
            Diagnostics = new DiagnosticsUi { ProfilerOverheadShare = 0.025, UnattributedJobsMilliseconds = 3.1 }
        };

        var report = ProfilerReportBuilder.Build(snapshot, gameVersion: "1.6.0f1", profilerVersion: "0.1.0");

        Assert.That(report.SchemaVersion, Is.EqualTo(2));
        Assert.That(report.GameVersion, Is.EqualTo("1.6.0f1"));
        Assert.That(report.ProfilerVersion, Is.EqualTo("0.1.0"));
        Assert.That(report.CityName, Is.Null);
        Assert.That(report.GlobalMetrics.Any(x => x.Name == "efficiency" && x.Value == 0.7), Is.True);
        Assert.That(report.GlobalMetrics.Single(x => x.Name == "main").Unit, Is.EqualTo("TimeNanoseconds"));
        Assert.That(report.CaptureConfig.Any(x => x.Name == "detailCaptureId" && x.Value == "capture-1"), Is.True);
        Assert.That(report.CaptureConfig.Any(x => x.Name == "detailScope" && x.Value == "historical-capture"), Is.True);
        Assert.That(report.CaptureConfig.Any(x => x.Name == "globalTimestampSeconds" && x.Value == "12"), Is.True);

        var exportedCapture = report.Captures.Single();
        Assert.Multiple(() =>
        {
            Assert.That(exportedCapture.Id, Is.EqualTo("capture-1"));
            Assert.That(exportedCapture.DiscoveredMarkers, Is.EqualTo(592));
            Assert.That(exportedCapture.CapturedMarkers, Is.EqualTo(34));
            Assert.That(exportedCapture.Batched, Is.True);
            Assert.That(exportedCapture.Warnings.Single(), Is.EqualTo("diagnostic warning"));
        });

        var system = report.Systems.Single();
        Assert.Multiple(() =>
        {
            Assert.That(system.OwnerAssembly, Is.EqualTo("Example.Mod"));
            Assert.That(system.CurrentMilliseconds, Is.EqualTo(6.0));
            Assert.That(system.MeanMilliseconds, Is.EqualTo(4.0));
            Assert.That(system.MedianMilliseconds, Is.EqualTo(4.0));
            Assert.That(system.P95Milliseconds, Is.EqualTo(6.0));
            Assert.That(system.P99Milliseconds, Is.EqualTo(6.0));
            Assert.That(system.MaxMilliseconds, Is.EqualTo(6.0));
            Assert.That(system.TotalMilliseconds, Is.EqualTo(12.0));
            Assert.That(system.Calls, Is.EqualTo(9));
        });

        Assert.That(report.ModAttribution.Any(x => x.Name == "Example.Mod"), Is.True);
        Assert.That(report.Pathfinding.Single().Name, Is.EqualTo("pendingPathfindActions"));
        Assert.That(report.DomainMetrics.Single().Name, Is.EqualTo("citizens"));
        var captureOverhead = report.ProfilerOverhead.Single(x => x.Name == "captureOverheadShare");
        Assert.That(captureOverhead.Value, Is.EqualTo(0.025));
        Assert.That(captureOverhead.Note, Does.Contain("managed SystemBase timing instrumentation"));
        Assert.That(captureOverhead.Note, Does.Contain("not total profiler overhead"));
    }

    [Test]
    public void Unavailable_only_metric_groups_are_not_reported_as_available()
    {
        var snapshot = new UiSnapshot
        {
            Pathfinding = new PathfindingUiMetrics
            {
                Metrics = new[]
                {
                    new UiMetricRow
                    {
                        Id = "requestsPerSecond",
                        Value = null,
                        Confidence = "Unavailable",
                        Availability = "Unavailable",
                        Reason = "No verified counter."
                    }
                }
            },
            DomainMetrics = new[]
            {
                new UiMetricRow
                {
                    Id = "serviceVehicles",
                    Value = null,
                    Confidence = "Unavailable",
                    Availability = "Unavailable",
                    Reason = "No verified source."
                }
            }
        };

        var report = ProfilerReportBuilder.Build(snapshot);

        Assert.That(report.Capabilities.Single(x => x.Name == "pathfinding").Value, Is.EqualTo("unavailable"));
        Assert.That(report.Capabilities.Single(x => x.Name == "domainMetrics").Value, Is.EqualTo("unavailable"));

        snapshot.DomainMetrics = new[]
        {
            new UiMetricRow
            {
                Id = "citizens",
                Value = 100,
                Confidence = "Indirect",
                Availability = "Available"
            }
        };
        snapshot.Pathfinding = new PathfindingUiMetrics
        {
            Metrics = new[]
            {
                new UiMetricRow
                {
                    Id = "pendingPathfindActions",
                    Value = 4,
                    Confidence = "Indirect",
                    Availability = "Available"
                }
            }
        };

        report = ProfilerReportBuilder.Build(snapshot);
        Assert.That(report.Capabilities.Single(x => x.Name == "domainMetrics").Value, Is.EqualTo("available"));
        Assert.That(report.Capabilities.Single(x => x.Name == "pathfinding").Value, Is.EqualTo("available"));
    }

    [Test]
    public void Unmeasured_unattributed_job_time_is_exported_as_unavailable_not_zero()
    {
        var unavailable = ProfilerReportBuilder.Build(new UiSnapshot());
        var unavailableMetric = unavailable.ProfilerOverhead.Single(x => x.Name == "unattributedJobsMilliseconds");

        Assert.Multiple(() =>
        {
            Assert.That(unavailableMetric.Value, Is.Null);
            Assert.That(unavailableMetric.Confidence, Is.EqualTo("Unavailable"));
            Assert.That(unavailableMetric.Availability, Is.EqualTo("Unavailable"));
        });

        using (var document = JsonDocument.Parse(PerformanceReportSerializer.Serialize(unavailable)))
        {
            var exportedMetric = document.RootElement.GetProperty("profilerOverhead")
                .EnumerateArray()
                .Single(item => item.GetProperty("name").GetString() == "unattributedJobsMilliseconds");
            Assert.That(exportedMetric.GetProperty("availability").GetString(), Is.EqualTo("Unavailable"));
            Assert.That(exportedMetric.TryGetProperty("value", out _), Is.False);
        }

        var measuredZero = ProfilerReportBuilder.Build(new UiSnapshot
        {
            Diagnostics = new DiagnosticsUi { UnattributedJobsMilliseconds = 0d }
        });
        var measuredMetric = measuredZero.ProfilerOverhead.Single(x => x.Name == "unattributedJobsMilliseconds");
        Assert.That(measuredMetric.Value, Is.EqualTo(0d));
        Assert.That(measuredMetric.Availability, Is.EqualTo("Available"));
    }
}
