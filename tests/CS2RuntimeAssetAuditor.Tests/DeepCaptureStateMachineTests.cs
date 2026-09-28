using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class DeepCaptureStateMachineTests
{
    [Test]
    public void Sustained_low_efficiency_enters_deep_capture_after_two_seconds()
    {
        var machine = new DeepCaptureStateMachine(
            efficiencyThreshold: 0.80,
            sustainSeconds: 2,
            deepSeconds: 10,
            postSeconds: 5,
            cooldownSeconds: 30);

        machine.Observe(0, selectedSpeed: 4, actualSpeed: 2.5);
        machine.Observe(1.9, 4, 2.5);
        Assert.That(machine.State, Is.EqualTo(CaptureState.Monitoring));

        machine.Observe(2.0, 4, 2.5);
        Assert.That(machine.State, Is.EqualTo(CaptureState.DeepCapture));
        Assert.That(machine.LastTrigger?.Kind, Is.EqualTo(CaptureTriggerKind.AutomaticLowEfficiency));
    }

    [Test]
    public void Paused_simulation_never_auto_triggers()
    {
        var machine = DeepCaptureStateMachine.CreateDefault();
        machine.Observe(0, 0, 0);
        machine.Observe(10, 0, 0);
        Assert.That(machine.State, Is.EqualTo(CaptureState.Monitoring));
    }

    [Test]
    public void Suppressed_automatic_trigger_does_not_accumulate_loading_time()
    {
        var machine = DeepCaptureStateMachine.CreateDefault();

        machine.Observe(0, 1, 0, automaticTriggerAllowed: false);
        machine.Observe(10, 1, 0, automaticTriggerAllowed: false);
        machine.Observe(10.1, 1, 0, automaticTriggerAllowed: true);
        machine.Observe(12.0, 1, 0, automaticTriggerAllowed: true);

        Assert.That(machine.State, Is.EqualTo(CaptureState.Monitoring));

        machine.Observe(12.1, 1, 0, automaticTriggerAllowed: true);
        Assert.That(machine.State, Is.EqualTo(CaptureState.DeepCapture));
    }

    [Test]
    public void Manual_request_enters_deep_capture_immediately()
    {
        var machine = DeepCaptureStateMachine.CreateDefault();
        machine.RequestManualCapture(5);
        Assert.That(machine.State, Is.EqualTo(CaptureState.DeepCapture));
        Assert.That(machine.LastTrigger?.Kind, Is.EqualTo(CaptureTriggerKind.Manual));
    }

    [Test]
    public void Brief_recovery_resets_auto_trigger_sustain_window()
    {
        var machine = DeepCaptureStateMachine.CreateDefault();
        machine.Observe(0, 4, 2.5);
        machine.Observe(1.5, 4, 4);
        machine.Observe(2, 4, 2.5);
        machine.Observe(3.9, 4, 2.5);
        Assert.That(machine.State, Is.EqualTo(CaptureState.Monitoring));
    }

    [Test]
    public void Capture_progresses_through_postbuffer_cooldown_and_back_to_monitoring()
    {
        var machine = DeepCaptureStateMachine.CreateDefault();
        machine.RequestManualCapture(0);

        machine.Observe(10, 4, 4);
        Assert.That(machine.State, Is.EqualTo(CaptureState.PostBuffer));

        machine.Observe(15, 4, 4);
        Assert.That(machine.State, Is.EqualTo(CaptureState.Cooldown));

        machine.Observe(45, 4, 4);
        Assert.That(machine.State, Is.EqualTo(CaptureState.Monitoring));
    }
}
