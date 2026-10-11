using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class PanelVisibilityTests
{
    [Test]
    public void A_repeated_close_keeps_the_panel_closed_and_updates_nothing()
    {
        var panel = new PanelVisibility();
        panel.Request(true);

        Assert.Multiple(() =>
        {
            Assert.That(panel.Request(false), Is.EqualTo(PanelBindingUpdate.Visibility | PanelBindingUpdate.Hud));
            Assert.That(panel.Request(false), Is.EqualTo(PanelBindingUpdate.None));
            Assert.That(panel.IsVisible, Is.False);
        });
    }

    [Test]
    public void Opening_the_panel_rebuilds_the_full_snapshot_at_once()
    {
        var panel = new PanelVisibility();

        Assert.Multiple(() =>
        {
            Assert.That(panel.Request(true), Is.EqualTo(PanelBindingUpdate.Visibility | PanelBindingUpdate.Hud | PanelBindingUpdate.Snapshot));
            Assert.That(panel.IsVisible, Is.True);
            Assert.That(panel.Request(true), Is.EqualTo(PanelBindingUpdate.None));
        });
    }

    [Test]
    public void A_closed_panel_refreshes_only_the_hud()
    {
        var panel = new PanelVisibility();
        Assert.That(panel.RefreshTick(), Is.EqualTo(PanelBindingUpdate.Hud));

        panel.Request(true);
        Assert.That(panel.RefreshTick(), Is.EqualTo(PanelBindingUpdate.Hud | PanelBindingUpdate.Snapshot));
    }
}
