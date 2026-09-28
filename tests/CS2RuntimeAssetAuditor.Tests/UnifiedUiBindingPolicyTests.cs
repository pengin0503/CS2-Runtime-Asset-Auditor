using CS2RuntimeAssetAuditor.Assets.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class UnifiedUiBindingPolicyTests
{
    [Test]
    public void Asset_bindings_use_a_distinct_group_and_explicit_availability()
    {
        Assert.That(UiBindingContract.Group, Is.EqualTo("CS2RuntimeAssetAuditor.assets"));
        Assert.That(new UiObservation().Availability, Is.EqualTo("NotScanned"));
        Assert.That(new UiObservation().Value, Is.Null);
        Assert.That(new UiScanStatus().QueuedBecauseRuntimeCapture, Is.False);
    }

    [Test]
    public void Asset_backend_delegates_requests_without_owning_a_second_panel()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CS2RuntimeAssetAuditor.sln"))) directory = directory.Parent;
        Assert.That(directory, Is.Not.Null);
        var src = File.ReadAllText(Path.Combine(directory!.FullName, "src", "CS2RuntimeAssetAuditor", "Assets", "UI", "AssetAuditUISystem.cs"));
        Assert.That(src, Does.Not.Contain("GameTopLeft"));
        Assert.That(src, Does.Not.Contain("panelVisible"));
        Assert.That(src, Does.Contain("RequestCensusScan("));
        Assert.That(src, Does.Contain("RequestAssetAudit("));
        Assert.That(src, Does.Not.Contain("EntityManager.CreateEntity"));
    }
}
