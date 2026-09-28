using System.Linq;
using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class CaptureSystemTimingFinalizerTests
{
    [Test]
    public void Apply_projects_captured_ecs_marker_samples_into_capture_system_timing()
    {
        var capture = new CaptureSession(
            "capture-test",
            new CaptureTrigger(CaptureTriggerKind.Manual, 0d, null),
            maxSamplesPerSeries: 16);
        capture.AddMarkerSample("ecs-system", new MetricSample(1d, 2_000_000d, MetricConfidence.Full));
        capture.AddMarkerSample("ecs-system", new MetricSample(2d, 4_000_000d, MetricConfidence.Full));

        var systems = new[]
        {
            new SystemDescriptor(
                "Game.Pathfind.ExampleSystem",
                "Game",
                SystemSourceKind.Vanilla,
                null,
                MetricConfidence.Unavailable)
        };
        var recorders = new[]
        {
            new RecorderDescriptor(
                "ecs-system",
                "Scripts",
                "Main World Game.Pathfind.ExampleSystem",
                "TimeNanoseconds",
                "Int64")
        };

        var timing = CaptureSystemTimingFinalizer.Apply(capture, systems, recorders);

        Assert.That(capture.SystemTiming, Is.SameAs(timing));
        var row = timing.Systems.Single();
        Assert.That(row.SystemId, Is.EqualTo("Game.Pathfind.ExampleSystem"));
        Assert.That(row.Milliseconds, Is.EqualTo(4d).Within(0.0001d));
        Assert.That(row.MeanMilliseconds, Is.EqualTo(3d).Within(0.0001d));
        Assert.That(row.P95Milliseconds, Is.EqualTo(4d).Within(0.0001d));
        Assert.That(row.P99Milliseconds, Is.EqualTo(4d).Within(0.0001d));
        Assert.That(row.MaxMilliseconds, Is.EqualTo(4d).Within(0.0001d));
        Assert.That(row.TotalMilliseconds, Is.EqualTo(6d).Within(0.0001d));
        Assert.That(row.Calls, Is.Null, "call count is unavailable when capture samples do not carry recorder counts");
        Assert.That(row.Confidence, Is.EqualTo(MetricConfidence.Full));
        Assert.That(capture.Warnings, Is.Empty);
    }

    [Test]
    public void Apply_uses_managed_fallback_when_profiler_markers_have_no_system_samples()
    {
        var capture = new CaptureSession(
            "managed-fallback",
            new CaptureTrigger(CaptureTriggerKind.Manual, 0d, null),
            maxSamplesPerSeries: 16);
        capture.SetMarkerCoverage(617, 617, 617, 38, true);

        var systems = new[]
        {
            new SystemDescriptor("Game.Simulation.ManagedSystem", "Game", SystemSourceKind.Vanilla, null, MetricConfidence.Unavailable)
        };
        var managed = new SystemTimingSnapshot();
        managed.AddSystem("Game.Simulation.ManagedSystem", 1.25, MetricConfidence.Managed, "Game", sourceKind: SystemSourceKind.Vanilla);

        var timing = CaptureSystemTimingFinalizer.Apply(capture, systems, System.Array.Empty<RecorderDescriptor>(), managed);

        Assert.Multiple(() =>
        {
            Assert.That(timing.Systems.Single().Confidence, Is.EqualTo(MetricConfidence.Managed));
            Assert.That(capture.Warnings, Has.Some.Contains("managed synchronous"));
            Assert.That(capture.Warnings, Has.None.Contains("System timing unavailable"));
        });
    }

    [Test]
    public void Apply_warns_when_native_marker_rows_and_managed_fallback_rows_are_mixed()
    {
        var capture = new CaptureSession(
            "mixed-timing",
            new CaptureTrigger(CaptureTriggerKind.Manual, 0d, null),
            maxSamplesPerSeries: 16);
        capture.SetMarkerCoverage(100, 100, 100, 1, true);
        capture.AddMarkerSample("native-system", new MetricSample(1d, 2_000_000d, MetricConfidence.Full, 1));

        var systems = new[]
        {
            new SystemDescriptor("Game.NativeSystem", "Game", SystemSourceKind.Vanilla, null,
                MetricConfidence.Unavailable, profilerMarkerName: "Native System Marker", allowLegacyProfilerMarkerMatching: false),
            new SystemDescriptor("Game.ManagedOnlySystem", "Game", SystemSourceKind.Vanilla, null,
                MetricConfidence.Unavailable, allowLegacyProfilerMarkerMatching: false)
        };
        var recorders = new[]
        {
            new RecorderDescriptor("native-system", "Scripts", "Native System Marker", "TimeNanoseconds", "Int64")
        };
        var managed = new SystemTimingSnapshot();
        managed.AddSystem("Game.NativeSystem", 9d, MetricConfidence.Managed, "Game", sourceKind: SystemSourceKind.Vanilla);
        managed.AddSystem("Game.ManagedOnlySystem", 3d, MetricConfidence.Managed, "Game", sourceKind: SystemSourceKind.Vanilla);

        var timing = CaptureSystemTimingFinalizer.Apply(capture, systems, recorders, managed);

        Assert.Multiple(() =>
        {
            Assert.That(timing.Systems.Count, Is.EqualTo(2));
            Assert.That(timing.Systems.Single(x => x.SystemId == "Game.NativeSystem").Confidence, Is.EqualTo(MetricConfidence.Full));
            Assert.That(timing.Systems.Single(x => x.SystemId == "Game.ManagedOnlySystem").Confidence, Is.EqualTo(MetricConfidence.Managed));
            Assert.That(capture.Warnings, Has.Some.Contains("Job/Burst"));
            Assert.That(capture.Warnings, Has.Some.Contains("total CPU"));
        });
    }

    [Test]
    public void Apply_explains_projection_stages_when_no_ecs_system_marker_can_be_projected()
    {
        var capture = new CaptureSession(
            "no-system-match",
            new CaptureTrigger(CaptureTriggerKind.Manual, 0d, null),
            maxSamplesPerSeries: 16);
        capture.SetMarkerCoverage(592, 34, true);
        capture.AddMarkerSample("unrelated-sample", new MetricSample(1d, 2_000_000d, MetricConfidence.Full));

        var systems = new[]
        {
            new SystemDescriptor("Game.Pathfind.UniqueSystem", "Game", SystemSourceKind.Vanilla, null, MetricConfidence.Unavailable),
            new SystemDescriptor("Game.Pathfind.AmbiguousSystem", "Game", SystemSourceKind.Vanilla, null, MetricConfidence.Unavailable),
            new SystemDescriptor("Game.Pathfind.NonTimeSystem", "Game", SystemSourceKind.Vanilla, null, MetricConfidence.Unavailable)
        };
        var recorders = new[]
        {
            new RecorderDescriptor("unique-no-sample", "Scripts", "Main World Game.Pathfind.UniqueSystem", "TimeNanoseconds", "Int64"),
            new RecorderDescriptor("ambiguous-a", "Scripts", "Main World Game.Pathfind.AmbiguousSystem", "TimeNanoseconds", "Int64"),
            new RecorderDescriptor("ambiguous-b", "Scripts", "Other World Game.Pathfind.AmbiguousSystem", "TimeNanoseconds", "Int64"),
            new RecorderDescriptor("non-time", "Scripts", "Main World Game.Pathfind.NonTimeSystem", "Count", "Int64")
        };

        var timing = CaptureSystemTimingFinalizer.Apply(capture, systems, recorders);

        var warning = capture.Warnings.Single();
        Assert.Multiple(() =>
        {
            Assert.That(timing.Systems, Is.Empty);
            Assert.That(warning, Does.Contain("catalogSystems=3"));
            Assert.That(warning, Does.Contain("profilerMarkers=4"));
            Assert.That(warning, Does.Contain("timeMarkers=3"));
            Assert.That(warning, Does.Contain("uniqueMatches=1"));
            Assert.That(warning, Does.Contain("ambiguousMatches=1"));
            Assert.That(warning, Does.Contain("uniqueMatchesWithoutSamples=1"));
            Assert.That(warning, Does.Contain("sampledMarkers=1"));
            Assert.That(warning, Does.Contain("capturedMarkers=34/592"));
        });
    }
}
