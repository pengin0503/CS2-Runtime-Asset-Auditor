using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class CaptureConfigurationAndLoggingTests
{
    private const double MiB = 1024d * 1024d;

    [Test]
    public void Report_exports_effective_capture_configuration_and_profiler_memory_evidence()
    {
        var capture = new CaptureSession("capture-report", new CaptureTrigger(CaptureTriggerKind.Manual, 10d, null), 16);
        capture.ObserveProfilerMemory(200 * MiB);
        capture.ObserveProfilerMemory(350 * MiB);
        var snapshot = UiSnapshotBuilder.Build(new UiSnapshotInput { Captures = new[] { capture } });
        var config = Configuration(samplingPeriodSeconds: 0.5, efficiencyThreshold: 0.8);

        var report = ProfilerReportBuilder.Build(snapshot, captureConfiguration: config);
        var values = report.CaptureConfig.ToDictionary(item => item.Name, item => item.Value);
        var exportedCapture = report.Captures.Single();

        Assert.Multiple(() =>
        {
            Assert.That(values["samplingPeriodSeconds"], Is.EqualTo("0.5"));
            Assert.That(values["automaticCaptureEnabled"], Is.EqualTo("true"));
            Assert.That(values["efficiencyThreshold"], Is.EqualTo("0.8"));
            Assert.That(values["lowEfficiencySustainSeconds"], Is.EqualTo("2"));
            Assert.That(values["prebufferSeconds"], Is.EqualTo("5"));
            Assert.That(values["deepCaptureSeconds"], Is.EqualTo("10"));
            Assert.That(values["postbufferSeconds"], Is.EqualTo("5"));
            Assert.That(values["cooldownSeconds"], Is.EqualTo("30"));
            Assert.That(values["maxConcurrentMarkers"], Is.EqualTo("150"));
            Assert.That(values["profilerOverheadLimit"], Is.EqualTo("0.08"));
            Assert.That(values["maxCompletedCaptures"], Is.EqualTo("20"));
            Assert.That(exportedCapture.ProfilerMemoryBaselineBytes, Is.EqualTo(200 * MiB));
            Assert.That(exportedCapture.ProfilerMemoryPeakBytes, Is.EqualTo(350 * MiB));
            Assert.That(exportedCapture.ProfilerMemoryDeltaBytes, Is.EqualTo(150 * MiB));
        });
    }

    [Test]
    public void Historical_export_uses_capture_start_configuration_instead_of_current_runtime_configuration()
    {
        var capture = new CaptureSession("historical-config", new CaptureTrigger(CaptureTriggerKind.Manual, 10d, null), 16);
        capture.SetConfiguration(Configuration(samplingPeriodSeconds: 0.5, efficiencyThreshold: 0.8));

        var snapshot = UiSnapshotBuilder.BuildForExport(new UiSnapshotInput
        {
            Captures = new[] { capture },
            SelectedCaptureId = capture.Id
        });
        var currentRuntimeConfig = Configuration(samplingPeriodSeconds: 2.0, efficiencyThreshold: 0.6);

        var report = ProfilerReportBuilder.Build(snapshot, captureConfiguration: currentRuntimeConfig);
        var values = report.CaptureConfig.ToDictionary(item => item.Name, item => item.Value);

        Assert.Multiple(() =>
        {
            Assert.That(values["samplingPeriodSeconds"], Is.EqualTo("0.5"));
            Assert.That(values["efficiencyThreshold"], Is.EqualTo("0.8"));
        });
    }

    [Test]
    public void Completion_log_formatter_summarizes_capture_once_ready_for_runtime_logging()
    {
        var capture = new CaptureSession("capture-log", new CaptureTrigger(CaptureTriggerKind.AutomaticLowEfficiency, 12d, 0.5d), 16);
        capture.SetMarkerCoverage(100, 80, 70, 25, true);
        capture.ObserveProfilerOverheadShare(0.0123);
        capture.ObserveProfilerMemory(200 * MiB);
        capture.ObserveProfilerMemory(350 * MiB);
        capture.AddWarning("example warning");
        var timing = new SystemTimingSnapshot();
        timing.AddSystem("Game.ExampleSystem", 3d, MetricConfidence.Full, "Game");
        capture.SetSystemTiming(timing);

        var message = CaptureCompletionLogFormatter.Format(capture);

        Assert.That(message, Is.EqualTo(
            "capture=capture-log trigger=AutomaticLowEfficiency markers(sampled/activated/attempted/discovered)=25/70/80/100 systems=1 overhead=1.23% profilerMemoryDeltaMiB=150 warnings=1"));
    }

    private static CaptureConfigurationSnapshot Configuration(double samplingPeriodSeconds, double efficiencyThreshold)
        => new()
        {
            SamplingPeriodSeconds = samplingPeriodSeconds,
            AutomaticCaptureEnabled = true,
            EfficiencyThreshold = efficiencyThreshold,
            LowEfficiencySustainSeconds = 2,
            PrebufferSeconds = 5,
            DeepCaptureSeconds = 10,
            PostbufferSeconds = 5,
            CooldownSeconds = 30,
            MaxConcurrentMarkers = 150,
            ProfilerOverheadLimit = 0.08,
            MaxCompletedCaptures = 20
        };
}
