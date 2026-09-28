using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using CS2RuntimeAssetAuditor.Assets.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets.UI
{
    [TestFixture]
    public sealed class UiBindingContractTests
    {
        [Test]
        public void Analysis_and_deep_inspection_triggers_have_stable_contract_names()
        {
            Assert.That(UiBindingContract.RequestAssetAudit, Is.EqualTo("requestAssetAudit"));
            Assert.That(UiBindingContract.RequestDeepInspection, Is.EqualTo("requestDeepInspection"));
        }

        [Test]
        public void Render_asset_key_round_trips_ui_binding_text()
        {
            var key = new RenderAssetKey("Render:House.A", "Game.Prefabs.RenderPrefab");

            Assert.That(RenderAssetKey.TryParse(key.ToString(), out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(key));
            Assert.That(RenderAssetKey.TryParse("invalid", out _), Is.False);
        }

        [Test]
        public void Export_request_defaults_to_full_json_and_carries_stable_selected_keys()
        {
            var request = new UiExportRequest();
            Assert.That(request.Format, Is.EqualTo("Json"));
            Assert.That(request.Scope, Is.EqualTo("Full"));
            Assert.That(request.SelectedKeys, Is.Empty);

            request.SelectedKeys = new[] { new UiPrefabKey { PrefabId = "House:A", PrefabType = "Building" } };
            Assert.That(request.SelectedKeys[0].PrefabId, Is.EqualTo("House:A"));
            Assert.That(request.SelectedKeys[0].PrefabType, Is.EqualTo("Building"));
        }
    }
}
