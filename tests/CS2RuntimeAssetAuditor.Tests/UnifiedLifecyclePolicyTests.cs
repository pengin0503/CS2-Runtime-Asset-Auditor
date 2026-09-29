using CS2RuntimeAssetAuditor.Assets.Core.Scanning;
using CS2RuntimeAssetAuditor.Coordination;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class UnifiedLifecyclePolicyTests
{
    [Test]
    public void Single_mod_registers_runtime_advisor_and_asset_systems()
    {
        var root = FindRoot();
        var sources = Directory.GetFiles(Path.Combine(root, "src", "CS2RuntimeAssetAuditor"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText).ToArray();
        var mod = File.ReadAllText(Path.Combine(root, "src", "CS2RuntimeAssetAuditor", "Mod.cs"));
        Assert.That(sources.Count(source => source.Contains(": IMod")), Is.EqualTo(1));
        foreach (var system in new[] { "GlobalMetricsCollector", "DomainMetricsSystem", "CaptureRuntimeSystem", "AdvisorSystem", "AssetAuditSystem", "ProfilerUISystem" })
            Assert.That(mod, Does.Contain("UpdateAt<" + system + ">"), system);
        Assert.That(mod, Does.Contain("UpdateAt<DiagnosticSessionSystem>"));
        var lifecycle = File.ReadAllText(Path.Combine(root, "src", "CS2RuntimeAssetAuditor", "Lifecycle", "DiagnosticSessionSystem.cs"));
        Assert.That(lifecycle, Does.Contain("OnGamePreload"));
        Assert.That(lifecycle, Does.Contain("OnGameLoadingComplete"));
        Assert.That(string.Join("\n", sources), Does.Not.Contain("EnsureDiagnosticSession("),
            "World identity must not define a city session; the game reuses one World across loads.");
    }

    [Test]
    public void Capture_and_asset_scan_use_one_coordinator()
    {
        var root = FindRoot();
        var capture = File.ReadAllText(Path.Combine(root, "src", "CS2RuntimeAssetAuditor", "Profiling", "CaptureRuntimeSystem.cs"));
        var asset = File.ReadAllText(Path.Combine(root, "src", "CS2RuntimeAssetAuditor", "Assets", "GameIntegration", "AssetAuditSystem.cs"));
        Assert.That(capture, Does.Contain("DiagnosticWorkKind.RuntimeDeepCapture"));
        Assert.That(asset, Does.Contain("DiagnosticWorkKind.AssetHeavyScan"));
        Assert.That(asset, Does.Contain("ConsumeAssetInterruptionRequest"));
    }

    [Test]
    public void Runtime_heavy_work_slot_is_held_until_the_capture_completes()
    {
        // Post-buffer samples belong to the capture, so queued asset scans must not start before it completes.
        var root = FindRoot();
        var capture = File.ReadAllText(Path.Combine(root, "src", "CS2RuntimeAssetAuditor", "Profiling", "CaptureRuntimeSystem.cs"));
        var transition = capture.Substring(capture.IndexOf("beforeState == CaptureState.DeepCapture", StringComparison.Ordinal));
        transition = transition.Substring(0, transition.IndexOf('}'));
        Assert.That(transition, Does.Not.Contain("WorkCoordinator.Complete"));
        var completed = capture.Substring(capture.IndexOf("private void HandleCaptureCompleted", StringComparison.Ordinal));
        Assert.That(completed, Does.Contain("WorkCoordinator.Complete(DiagnosticWorkKind.RuntimeDeepCapture)"));
    }

    [Test]
    public void Interrupted_scan_never_becomes_a_publishable_complete_snapshot()
    {
        var scan = ScanSession.Start(ScanKind.Census, 1, DateTimeOffset.UtcNow);
        scan.RequestCancellation();
        scan.MarkInterruptedByRuntimeCapture();
        Assert.That(scan.State, Is.EqualTo(ScanState.InterruptedByRuntimeCapture));
        Assert.That(scan.CanPublish, Is.False);
        Assert.That(scan.DiagnosticCode, Is.EqualTo("InterruptedByRuntimeCapture"));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CS2RuntimeAssetAuditor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
