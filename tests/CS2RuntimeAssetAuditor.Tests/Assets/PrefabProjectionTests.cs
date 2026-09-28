using NUnit.Framework;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.GameIntegration.Prefabs;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    [TestFixture]
    public sealed class PrefabProjectionTests
    {
        [Test]
        public void Prefab_key_uses_only_stable_prefab_identity()
        {
            var first = new PrefabKey("Road.Small", "NetworkPrefab");
            var samePrefabFromAnotherRuntimeWorld = new PrefabKey("Road.Small", "NetworkPrefab");

            Assert.That(first, Is.EqualTo(samePrefabFromAnotherRuntimeWorld));
            Assert.That(first.PrefabId, Is.EqualTo("Road.Small"));
            Assert.That(first.PrefabType, Is.EqualTo("NetworkPrefab"));
        }

        [Test]
        public void Source_evidence_preserves_overlapping_origin_facts()
        {
            var evidence = new AssetOriginEvidence(
                isBuiltin: true,
                isSubscribedMod: true,
                isPackaged: true,
                dlcPrerequisiteIds: new[] { "dlc-a" },
                assetPackMembership: new[] { "regional-pack" },
                assetDatabaseSource: "asset-database",
                paradoxModsPlatformId: "12345");

            Assert.That(evidence.IsBuiltin, Is.True);
            Assert.That(evidence.IsSubscribedMod, Is.True);
            Assert.That(evidence.IsPackaged, Is.True);
            Assert.That(evidence.DlcPrerequisiteIds, Is.EquivalentTo(new[] { "dlc-a" }));
            Assert.That(evidence.AssetPackMembership, Is.EquivalentTo(new[] { "regional-pack" }));
            Assert.That(evidence.AssetDatabaseSource, Is.EqualTo("asset-database"));
            Assert.That(evidence.ParadoxModsPlatformId, Is.EqualTo("12345"));
        }

        [Test]
        public void Prefab_traits_can_represent_multiple_classifications()
        {
            var traits = PrefabTraits.Building | PrefabTraits.ServiceBuilding | PrefabTraits.RenderOnly;

            Assert.That(traits.HasFlag(PrefabTraits.Building), Is.True);
            Assert.That(traits.HasFlag(PrefabTraits.ServiceBuilding), Is.True);
            Assert.That(traits.HasFlag(PrefabTraits.RenderOnly), Is.True);
            Assert.That(traits.HasFlag(PrefabTraits.Vehicle), Is.False);
        }

        [Test]
        public void Prefab_record_keeps_stable_identity_and_source_evidence()
        {
            var evidence = new AssetOriginEvidence(isBuiltin: true);
            var key = new PrefabKey("Building.FireStation", "BuildingPrefab");
            var record = new PrefabRecord(key, "Fire Station", PrefabTraits.Building | PrefabTraits.ServiceBuilding, evidence);

            Assert.That(record.Key, Is.EqualTo(key));
            Assert.That(record.DisplayName, Is.EqualTo("Fire Station"));
            Assert.That(record.Traits.HasFlag(PrefabTraits.Building), Is.True);
            Assert.That(record.OriginEvidence, Is.SameAs(evidence));
        }

        [Test]
        public void Prefab_classifier_keeps_composable_building_prop_tree_vehicle_and_network_traits()
        {
            var traits = PrefabClassifier.Classify(
                isBuilding: true,
                isServiceBuilding: true,
                isProp: true,
                isTree: true,
                isVehicle: true,
                isNetwork: true);

            Assert.That(traits, Is.EqualTo(
                PrefabTraits.Building | PrefabTraits.ServiceBuilding | PrefabTraits.Prop |
                PrefabTraits.Tree | PrefabTraits.Vehicle | PrefabTraits.Network));
        }

        [Test]
        public void Service_building_classification_always_includes_building_trait()
        {
            var traits = PrefabClassifier.Classify(isServiceBuilding: true);

            Assert.That(traits.HasFlag(PrefabTraits.Building), Is.True);
            Assert.That(traits.HasFlag(PrefabTraits.ServiceBuilding), Is.True);
        }

        [Test]
        public void Unread_source_lists_remain_distinct_from_observed_empty_lists()
        {
            var unread = new AssetOriginEvidence();
            var observedEmpty = new AssetOriginEvidence(
                dlcPrerequisiteIds: System.Array.Empty<string>(),
                assetPackMembership: System.Array.Empty<string>());

            Assert.That(unread.DlcPrerequisiteIds, Is.Null);
            Assert.That(unread.AssetPackMembership, Is.Null);
            Assert.That(observedEmpty.DlcPrerequisiteIds, Is.Empty);
            Assert.That(observedEmpty.AssetPackMembership, Is.Empty);
        }

        [Test]
        public void Source_metadata_reader_preserves_overlapping_origin_evidence()
        {
            var evidence = SourceMetadataReader.Read(
                isBuiltin: true,
                isSubscribedMod: true,
                isPackaged: true,
                assetDatabaseSource: "assetdb:prefab-123");

            Assert.That(evidence.IsBuiltin, Is.True);
            Assert.That(evidence.IsSubscribedMod, Is.True);
            Assert.That(evidence.IsPackaged, Is.True);
            Assert.That(evidence.AssetDatabaseSource, Is.EqualTo("assetdb:prefab-123"));
        }
    }
}
