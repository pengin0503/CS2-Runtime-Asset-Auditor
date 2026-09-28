using System;
using System.Linq;
using NUnit.Framework;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Core.Scanning;
using CS2RuntimeAssetAuditor.Assets.GameIntegration.Census;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    [TestFixture]
    public sealed class CensusReducerTests
    {
        private static readonly DateTimeOffset CapturedAt = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);

        [Test]
        public void Profile_v1_reduces_top_level_subordinate_live_and_network_counts()
        {
            var building = MakeRecord("Building.FireStation", "BuildingPrefab", PrefabTraits.Building | PrefabTraits.ServiceBuilding);
            var prop = MakeRecord("Prop.Bench", "PropPrefab", PrefabTraits.Prop);
            var network = MakeRecord("Road.Small", "NetworkPrefab", PrefabTraits.Network);
            var reducer = new CensusReducer(new[] { building, prop, network }, 7, 3, CapturedAt, new ScanOptions(true, true));

            reducer.AddObject(building.Key, isSubordinate: false);
            reducer.AddObject(building.Key, isSubordinate: false);
            reducer.AddObject(prop.Key, isSubordinate: true);
            reducer.AddNetworkEdge(network.Key);
            reducer.AddNetworkEdge(network.Key);

            var snapshot = reducer.BuildSnapshot();
            Assert.That(CensusQueryProfile.V1, Is.EqualTo("1"));
            Assert.That(snapshot.QueryProfileVersion, Is.EqualTo("1"));
            Assert.That(snapshot.WorldGeneration, Is.EqualTo(7));
            Assert.That(snapshot.CatalogGeneration, Is.EqualTo(3));
            Assert.That(snapshot.CapturedAt, Is.EqualTo(CapturedAt));
            Assert.That(snapshot.ScanOptions.CollectSubordinateObjects, Is.True);
            Assert.That(snapshot.ScanOptions.CollectNetworkEdges, Is.True);

            Assert.That(snapshot.TryGetEntry(building.Key, out var buildingEntry), Is.True);
            Assert.That(buildingEntry.Counters.TopLevelObjects.Value, Is.EqualTo(2));
            Assert.That(buildingEntry.Counters.SubordinateObjects.Value, Is.EqualTo(0));
            Assert.That(buildingEntry.Counters.LiveObjectReferences.Value, Is.EqualTo(2));
            Assert.That(snapshot.TryGetEntry(prop.Key, out var propEntry), Is.True);
            Assert.That(propEntry.Counters.SubordinateObjects.Value, Is.EqualTo(1));
            Assert.That(propEntry.Counters.LiveObjectReferences.Value, Is.EqualTo(1));
            Assert.That(snapshot.TryGetEntry(network.Key, out var networkEntry), Is.True);
            Assert.That(networkEntry.Counters.NetworkEdges.Value, Is.EqualTo(2));
        }

        [Test]
        public void Omitted_subordinate_and_network_metrics_remain_not_scanned()
        {
            var prop = MakeRecord("Prop.Bench", "PropPrefab", PrefabTraits.Prop);
            var network = MakeRecord("Road.Small", "NetworkPrefab", PrefabTraits.Network);
            var reducer = new CensusReducer(new[] { prop, network }, 1, 4, CapturedAt, new ScanOptions(false, false));
            reducer.AddObject(prop.Key, isSubordinate: true);
            reducer.AddNetworkEdge(network.Key);

            var snapshot = reducer.BuildSnapshot();
            Assert.That(snapshot.TryGetEntry(prop.Key, out var propEntry), Is.True);
            Assert.That(propEntry.Counters.TopLevelObjects.Availability, Is.EqualTo(Availability.Available));
            Assert.That(propEntry.Counters.SubordinateObjects.Availability, Is.EqualTo(Availability.NotScanned));
            Assert.That(propEntry.Counters.LiveObjectReferences.Availability, Is.EqualTo(Availability.NotScanned));
            Assert.That(propEntry.Presence, Is.EqualTo(CensusPresence.Unknown));
            Assert.That(snapshot.TryGetEntry(network.Key, out var networkEntry), Is.True);
            Assert.That(networkEntry.Counters.NetworkEdges.Availability, Is.EqualTo(Availability.NotScanned));
            Assert.That(snapshot.ScanOptions.CollectSubordinateObjects, Is.False);
        }

        [Test]
        public void Zero_is_not_present_and_render_only_assets_are_not_applicable()
        {
            var building = MakeRecord("Building.FireStation", "BuildingPrefab", PrefabTraits.Building);
            var renderOnly = MakeRecord("Render.TreeShadow", "RenderPrefab", PrefabTraits.RenderOnly);
            var reducer = new CensusReducer(new[] { building, renderOnly }, 1, 1, CapturedAt, new ScanOptions(true, true));

            var snapshot = reducer.BuildSnapshot();
            Assert.That(snapshot.TryGetEntry(building.Key, out var buildingEntry), Is.True);
            Assert.That(buildingEntry.Counters.TopLevelObjects.Value, Is.EqualTo(0));
            Assert.That(buildingEntry.Presence, Is.EqualTo(CensusPresence.NotPresentAtSnapshot));
            Assert.That(snapshot.TryGetEntry(renderOnly.Key, out var renderEntry), Is.True);
            Assert.That(renderEntry.Counters.TopLevelObjects.Availability, Is.EqualTo(Availability.NotApplicable));
            Assert.That(renderEntry.Presence, Is.EqualTo(CensusPresence.NotApplicable));
        }

        [Test]
        public void Failed_or_cancelled_publication_keeps_last_success_and_world_reset_invalidates_it()
        {
            var building = MakeRecord("Building.FireStation", "BuildingPrefab", PrefabTraits.Building);
            var first = new CensusReducer(new[] { building }, 1, 1, CapturedAt, new ScanOptions(true, true)).BuildSnapshot();
            var second = new CensusReducer(new[] { building }, 1, 2, CapturedAt.AddMinutes(1), new ScanOptions(true, true)).BuildSnapshot();
            var published = new PublishedAuditState();
            published.ResetForWorld(1);

            Assert.That(published.TryPublishCensus(first, scanSucceeded: true), Is.True);
            Assert.That(published.TryPublishCensus(second, scanSucceeded: false), Is.False);
            Assert.That(published.Census, Is.SameAs(first));
            Assert.That(published.TryPublishCensus(second, scanSucceeded: true), Is.True);
            Assert.That(published.Census, Is.SameAs(second));

            published.ResetForWorld(2);
            Assert.That(published.Census, Is.Null);
            Assert.That(published.TryPublishCensus(first, scanSucceeded: true), Is.False);
        }

        [Test]
        public void Owner_and_controller_markers_classify_one_subordinate_sample()
        {
            var prop = MakeRecord("Prop.Bench", "PropPrefab", PrefabTraits.Prop);
            var reducer = new CensusReducer(new[] { prop }, 1, 1, CapturedAt, new ScanOptions(true, true));
            var sample = CensusSample.ForObjectFromMarkers(prop.Key, hasOwnerMarker: true, hasControllerMarker: true);

            sample.AddTo(reducer);

            var snapshot = reducer.BuildSnapshot();
            Assert.That(snapshot.TryGetEntry(prop.Key, out var entry), Is.True);
            Assert.That(entry.Counters.TopLevelObjects.Value, Is.EqualTo(0));
            Assert.That(entry.Counters.SubordinateObjects.Value, Is.EqualTo(1));
            Assert.That(entry.Counters.LiveObjectReferences.Value, Is.EqualTo(1));
        }

        [Test]
        public void Foreign_world_snapshot_cannot_replace_the_current_world_snapshot()
        {
            var building = MakeRecord("Building.FireStation", "BuildingPrefab", PrefabTraits.Building);
            var current = new CensusReducer(new[] { building }, 8, 1, CapturedAt, new ScanOptions(true, true)).BuildSnapshot();
            var foreign = new CensusReducer(new[] { building }, 9, 2, CapturedAt.AddMinutes(1), new ScanOptions(true, true)).BuildSnapshot();
            var published = new PublishedAuditState();
            published.ResetForWorld(8);

            Assert.That(published.TryPublishCensus(current, scanSucceeded: true), Is.True);
            Assert.That(published.TryPublishCensus(foreign, scanSucceeded: true), Is.False);
            Assert.That(published.Census, Is.SameAs(current));
        }

        private static PrefabRecord MakeRecord(string id, string type, PrefabTraits traits)
        {
            return new PrefabRecord(new PrefabKey(id, type), id, traits, new AssetOriginEvidence());
        }
    }
}
