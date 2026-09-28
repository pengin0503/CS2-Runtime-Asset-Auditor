using CS2RuntimeAssetAuditor.Coordination;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class DiagnosticWorkCoordinatorTests
{
    [Test]
    public void Idle_work_starts_and_identical_requests_are_idempotent()
    {
        var coordinator = new DiagnosticWorkCoordinator();
        Assert.That(coordinator.Request(DiagnosticWorkKind.AssetHeavyScan), Is.EqualTo(DiagnosticWorkDecision.Started));
        Assert.That(coordinator.Request(DiagnosticWorkKind.AssetHeavyScan), Is.EqualTo(DiagnosticWorkDecision.AlreadyActive));
        coordinator.Complete(DiagnosticWorkKind.AssetHeavyScan);
        Assert.That(coordinator.Request(DiagnosticWorkKind.RuntimeDeepCapture), Is.EqualTo(DiagnosticWorkDecision.Started));
        Assert.That(coordinator.Request(DiagnosticWorkKind.RuntimeDeepCapture), Is.EqualTo(DiagnosticWorkDecision.AlreadyActive));
    }

    [Test]
    public void Asset_work_queues_during_capture_then_can_start_when_capture_ends()
    {
        var coordinator = new DiagnosticWorkCoordinator();
        coordinator.Request(DiagnosticWorkKind.RuntimeDeepCapture);
        Assert.That(coordinator.Request(DiagnosticWorkKind.AssetHeavyScan), Is.EqualTo(DiagnosticWorkDecision.Queued));
        Assert.That(coordinator.HasQueuedAssetWork, Is.True);
        Assert.That(coordinator.IsActive(DiagnosticWorkKind.AssetHeavyScan), Is.False);
        coordinator.Complete(DiagnosticWorkKind.RuntimeDeepCapture);
        Assert.That(coordinator.Request(DiagnosticWorkKind.AssetHeavyScan), Is.EqualTo(DiagnosticWorkDecision.Started));
        Assert.That(coordinator.HasQueuedAssetWork, Is.False);
    }

    [Test]
    public void Runtime_capture_requests_a_single_asset_interruption()
    {
        var coordinator = new DiagnosticWorkCoordinator();
        coordinator.Request(DiagnosticWorkKind.AssetHeavyScan);
        Assert.That(coordinator.Request(DiagnosticWorkKind.RuntimeDeepCapture), Is.EqualTo(DiagnosticWorkDecision.Started));
        Assert.That(coordinator.ConsumeAssetInterruptionRequest(), Is.True);
        Assert.That(coordinator.ConsumeAssetInterruptionRequest(), Is.False);
        coordinator.Complete(DiagnosticWorkKind.AssetHeavyScan);
        Assert.That(coordinator.IsActive(DiagnosticWorkKind.AssetHeavyScan), Is.False);
    }
}
