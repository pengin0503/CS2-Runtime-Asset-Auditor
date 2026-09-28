using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class CaptureSessionDiagnosticsTests
{
    [Test]
    public void Managed_fallback_failure_is_retained_when_full_marker_timing_exists()
    {
        var capture = new CaptureSession("capture", new CaptureTrigger(CaptureTriggerKind.Manual, 1d, null), 8);
        var timing = new SystemTimingSnapshot();
        timing.AddSystem("Marker.System", 1.5d, MetricConfidence.Full);
        capture.SetSystemTiming(timing);

        capture.AddManagedTimingFallbackUnavailableWarning("Harmony instrumentation failed to install.");

        Assert.That(capture.Warnings, Does.Contain(
            "Managed SystemBase timing fallback unavailable: Harmony instrumentation failed to install."));
    }
}
