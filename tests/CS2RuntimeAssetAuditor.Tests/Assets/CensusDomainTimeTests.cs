using System;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Capabilities;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Core.Scanning;
using CS2RuntimeAssetAuditor.Assets.Export;
using CS2RuntimeAssetAuditor.Export;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    /// <summary>
    /// Object references and network edges are copied in different frames (the edges only after every object is
    /// reduced), so each domain keeps the time it was copied instead of sharing the census start time.
    /// </summary>
    [TestFixture]
    public sealed class CensusDomainTimeTests
    {
        private static readonly DateTimeOffset ObjectsAt = new DateTimeOffset(2026, 10, 11, 10, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset NetworkAt = ObjectsAt.AddSeconds(42);

        private static readonly PrefabRecord Building = Record("Building.FireStation", PrefabTraits.Building);
        private static readonly PrefabRecord Road = Record("Road.Small", PrefabTraits.Network);

        [Test]
        public void Network_edges_carry_the_time_they_were_copied()
        {
            var reducer = new CensusReducer(new[] { Building, Road }, 1, 1, ObjectsAt, new ScanOptions(true, true));
            reducer.AddObject(Building.Key, isSubordinate: false);
            reducer.MarkNetworkCaptured(NetworkAt);
            reducer.AddNetworkEdge(Road.Key);

            var snapshot = reducer.BuildSnapshot();
            Assert.That(snapshot.CapturedAt, Is.EqualTo(ObjectsAt));
            Assert.That(snapshot.NetworkCapturedAt, Is.EqualTo(NetworkAt));
            snapshot.TryGetEntry(Building.Key, out var building);
            snapshot.TryGetEntry(Road.Key, out var road);
            Assert.That(building.Counters.TopLevelObjects.CapturedAt, Is.EqualTo(ObjectsAt));
            Assert.That(building.Counters.LiveObjectReferences.CapturedAt, Is.EqualTo(ObjectsAt));
            Assert.That(road.Counters.NetworkEdges.CapturedAt, Is.EqualTo(NetworkAt));
            Assert.That(road.Counters.NetworkEdges.Value, Is.EqualTo(1));
        }

        [Test]
        public void Without_a_network_copy_there_is_no_network_time()
        {
            var reducer = new CensusReducer(new[] { Building, Road }, 1, 1, ObjectsAt, new ScanOptions(true, false));
            reducer.AddObject(Building.Key, isSubordinate: false);

            var snapshot = reducer.BuildSnapshot();
            Assert.That(snapshot.NetworkCapturedAt, Is.Null);
            snapshot.TryGetEntry(Road.Key, out var road);
            Assert.That(road.Counters.NetworkEdges.Availability, Is.EqualTo(Availability.NotScanned));
        }

        [Test]
        public void Report_exports_both_domain_times_and_each_edge_observation_time()
        {
            var reducer = new CensusReducer(new[] { Building, Road }, 1, 1, ObjectsAt, new ScanOptions(true, true));
            reducer.AddObject(Building.Key, isSubordinate: false);
            reducer.MarkNetworkCaptured(NetworkAt);
            reducer.AddNetworkEdge(Road.Key);
            var census = reducer.BuildSnapshot();
            var capabilities = new CapabilityReport("1.6.2f1", CompatibilityState.Supported, Array.Empty<CapabilityStatus>());

            var report = new AuditReportBuilder(new PrivacySanitizer("Kate", "WORKSTATION"))
                .Build(new[] { Building, Road }, 1, ObjectsAt, census, capabilities, "1.0", NetworkAt.AddSeconds(1));

            Assert.That(report.CensusCapturedAt, Is.EqualTo(ObjectsAt.ToString("O")));
            Assert.That(report.CensusNetworkCapturedAt, Is.EqualTo(NetworkAt.ToString("O")));
            var road = report.Census.Single(entry => entry.PrefabId == Road.Key.PrefabId);
            Assert.That(road.Counters.NetworkEdges.CapturedAt, Is.EqualTo(NetworkAt.ToString("O")));
        }

        private static PrefabRecord Record(string id, PrefabTraits traits) =>
            new PrefabRecord(new PrefabKey(id, "Prefab"), id, traits, new AssetOriginEvidence(isBuiltin: true));
    }
}
