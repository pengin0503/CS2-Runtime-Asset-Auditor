using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Profiling;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class SafetyStopCooldownTests
{
    private const double CooldownSeconds = 10d;

    [Test]
    public void Overhead_safety_stop_enters_cooldown_instead_of_immediately_retriggering()
    {
        using var controller = CreateController();
        StartAutomaticCapture(controller, at: 0);

        TripOverheadSafetyStop(controller);

        Assert.Multiple(() =>
        {
            Assert.That(controller.CurrentSession, Is.Null);
            Assert.That(controller.State, Is.EqualTo(CaptureState.Cooldown));
            Assert.That(controller.ConsecutiveSafetyStops, Is.EqualTo(1));
            Assert.That(controller.CompletedSessions[^1].Warnings, Has.Some.Contains("Automatic capture is paused for 10 s"));
        });

        // Sustained low efficiency must not start a new automatic capture during the cooldown.
        controller.Observe(3, LowEfficiencySample(3));
        controller.Observe(6, LowEfficiencySample(6));
        Assert.That(controller.CurrentSession, Is.Null);
        Assert.That(controller.State, Is.EqualTo(CaptureState.Cooldown));

        controller.Observe(2 + CooldownSeconds + 0.1, LowEfficiencySample(12.1));
        Assert.That(controller.State, Is.EqualTo(CaptureState.Monitoring));
    }

    [Test]
    public void Repeated_safety_stops_back_off_exponentially_and_a_normal_capture_resets_the_backoff()
    {
        var machine = CreateStateMachine();
        using var controller = CreateController(machine);

        StartAutomaticCapture(controller, at: 0);
        TripOverheadSafetyStop(controller);
        Assert.That(machine.CurrentCooldownSeconds, Is.EqualTo(CooldownSeconds));

        // After the first cooldown, the next automatic capture is stopped again.
        StartAutomaticCapture(controller, at: 20);
        TripOverheadSafetyStop(controller);
        Assert.Multiple(() =>
        {
            Assert.That(controller.ConsecutiveSafetyStops, Is.EqualTo(2));
            Assert.That(machine.CurrentCooldownSeconds, Is.EqualTo(CooldownSeconds * 2));
        });

        // A capture that runs its full course resets the backoff counter.
        controller.RequestManualCapture(50);
        controller.Observe(56, global: null); // deep phase (5 s) ends
        controller.Observe(58, global: null); // post-buffer (1 s) ends and the capture completes
        Assert.That(controller.ConsecutiveSafetyStops, Is.Zero);
    }

    [Test]
    public void Requested_and_session_change_interruptions_return_to_monitoring_without_cooldown()
    {
        using var controller = CreateController();
        controller.RequestManualCapture(0);
        controller.InterruptActiveCapture("session changed", CaptureInterruptionReason.SessionChanged);
        Assert.That(controller.State, Is.EqualTo(CaptureState.Monitoring));

        controller.RequestManualCapture(1);
        controller.InterruptActiveCapture("disabled", CaptureInterruptionReason.MonitoringDisabled);
        Assert.That(controller.State, Is.EqualTo(CaptureState.Monitoring));
    }

    [Test]
    public void Clearing_completed_sessions_forgets_captures_from_an_earlier_city()
    {
        using var controller = CreateController();
        controller.RequestManualCapture(0);
        controller.InterruptActiveCapture("session changed", CaptureInterruptionReason.SessionChanged);
        Assert.That(controller.CompletedSessions, Is.Not.Empty);

        controller.ClearCompletedSessions();

        Assert.That(controller.CompletedSessions, Is.Empty);
    }

    private static void StartAutomaticCapture(DeepCaptureController controller, double at)
    {
        controller.Observe(at, LowEfficiencySample(at));
        controller.Observe(at + 2, LowEfficiencySample(at + 2));
        Assert.That(controller.State, Is.EqualTo(CaptureState.DeepCapture), "automatic capture should start");
    }

    private static void TripOverheadSafetyStop(DeepCaptureController controller)
    {
        // Four load reductions (three breaches each) are allowed before the fifth sustained breach aborts.
        for (var i = 0; i < 15 && controller.CurrentSession != null; i++)
            controller.ReportProfilerOverheadShare(0.50);
        Assert.That(controller.CurrentSession, Is.Null, "the overhead safety limit should stop the capture");
    }

    private static GlobalMetricsSnapshot LowEfficiencySample(double at)
        => new(at, 4d, 1d, new Dictionary<string, RecorderReading>());

    private static DeepCaptureStateMachine CreateStateMachine() => new(
        efficiencyThreshold: 0.8, sustainSeconds: 2, deepSeconds: 5, postSeconds: 1, cooldownSeconds: CooldownSeconds);

    private static DeepCaptureController CreateController(DeepCaptureStateMachine? machine = null)
    {
        var manager = new RecorderManager(new Backend());
        var controller = new DeepCaptureController(manager, machine ?? CreateStateMachine(), maxConcurrent: 8, overheadCeiling: 0.08);
        controller.Initialize();
        return controller;
    }

    private sealed class Backend : IRecorderBackend
    {
        private readonly RecorderDescriptor[] _descriptors = Enumerable.Range(0, 8)
            .Select(index => new RecorderDescriptor($"marker-{index}", "CPU", $"Marker {index}", "TimeNanoseconds", "Int64"))
            .ToArray();

        public IReadOnlyList<RecorderDescriptor> Discover() => _descriptors;
        public IActiveRecorder Start(RecorderDescriptor descriptor, int capacity) => new Recorder(descriptor.Id);
    }

    private sealed class Recorder : IActiveRecorder
    {
        public Recorder(string id) => Id = id;
        public string Id { get; }
        public RecorderReading Read() => new(1d, 1);
        public void Dispose() { }
    }
}
