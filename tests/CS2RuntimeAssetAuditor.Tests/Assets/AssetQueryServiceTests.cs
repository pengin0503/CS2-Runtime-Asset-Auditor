using System;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Core.Query;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    public sealed class AssetQueryServiceTests
    {
        private static readonly DateTimeOffset CapturedAt = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

        [Test]
        public void SearchMatchesDisplayNamePrefabIdAndSourcePackIdentifiers()
        {
            var records = CreateCatalog();
            var service = new AssetQueryService(records, BuildSnapshot(records));

            Assert.That(service.Query(new AssetQuery(searchText: "detached")).Items.Select(item => item.Asset.Key.PrefabId),
                Is.EqualTo(new[] { "asset-house-01" }));
            Assert.That(service.Query(new AssetQuery(searchText: "asset-car-02")).Items.Select(item => item.Asset.Key.PrefabId),
                Is.EqualTo(new[] { "asset-car-02" }));
            Assert.That(service.Query(new AssetQuery(searchText: "road-pack")).Items.Select(item => item.Asset.Key.PrefabId),
                Is.EqualTo(new[] { "asset-car-02" }));
        }

        [Test]
        public void TypeSourceAndPresenceFiltersCanBeAppliedTogether()
        {
            var records = CreateCatalog();
            var service = new AssetQueryService(records, BuildSnapshot(records));
            var page = service.Query(new AssetQuery(
                traitFilter: PrefabTraits.Vehicle,
                sourceFilter: AssetSourceFilter.SubscribedMod,
                presenceFilter: CensusPresence.Present));

            Assert.That(page.TotalCount, Is.EqualTo(1));
            Assert.That(page.Items.Single().Asset.Key.PrefabId, Is.EqualTo("asset-car-02"));
        }

        [Test]
        public void QueryPageSizeIsBoundedAndReportsTotalMatches()
        {
            var records = Enumerable.Range(0, 750)
                .Select(index => Record("bulk-" + index.ToString("D4"), "Bulk " + index, PrefabTraits.Building))
                .ToArray();
            var page = new AssetQueryService(records, census: null).Query(new AssetQuery(limit: 10000));

            Assert.That(page.Items.Count, Is.LessThanOrEqualTo(AssetQueryService.MaximumPageSize));
            Assert.That(page.TotalCount, Is.EqualTo(750));
        }

        [Test]
        public void InstanceMetricUsesTypeSpecificCountKindAndPreservesZeroAndNotScanned()
        {
            var house = Record("house", "House", PrefabTraits.Building);
            var prop = Record("prop", "Prop", PrefabTraits.Prop);
            var road = Record("road", "Road", PrefabTraits.Network);
            var records = new[] { house, prop, road };
            var reducer = new CensusReducer(records, 3, 8, CapturedAt, new ScanOptions(false, true));
            reducer.AddObject(house.Key, isSubordinate: false);
            reducer.AddNetworkEdge(road.Key);
            var service = new AssetQueryService(records, reducer.BuildSnapshot());

            var houseRow = service.Query(new AssetQuery(searchText: "house")).Items.Single();
            var propRow = service.Query(new AssetQuery(searchText: "prop")).Items.Single();
            var roadRow = service.Query(new AssetQuery(searchText: "road")).Items.Single();

            Assert.That(houseRow.CountKind, Is.EqualTo(CensusCountKind.TopLevelObjects));
            Assert.That(houseRow.Instances.Value, Is.EqualTo(1));
            Assert.That(propRow.CountKind, Is.EqualTo(CensusCountKind.LiveObjectReferences));
            Assert.That(propRow.Instances.Availability, Is.EqualTo(Availability.NotScanned));
            Assert.That(roadRow.CountKind, Is.EqualTo(CensusCountKind.NetworkEdges));
            Assert.That(roadRow.Instances.Value, Is.EqualTo(1));
        }

        [Test]
        public void MissingCensusIsNotScannedRatherThanAZeroCount()
        {
            var house = Record("house", "House", PrefabTraits.Building);
            var row = new AssetQueryService(new[] { house }, census: null)
                .Query(new AssetQuery()).Items.Single();

            Assert.That(row.CountKind, Is.EqualTo(CensusCountKind.TopLevelObjects));
            Assert.That(row.Instances.Availability, Is.EqualTo(Availability.NotScanned));
            Assert.That(row.Instances.HasValue, Is.False);
            Assert.That(row.Presence, Is.EqualTo(CensusPresence.Unknown));
        }

        [Test]
        public void CensusFromAnOlderCatalogGenerationDoesNotJoinCurrentAssetRows()
        {
            var house = Record("house", "House", PrefabTraits.Building);
            var reducer = new CensusReducer(new[] { house }, 3, 8, CapturedAt, ScanOptions.Default);
            reducer.AddObject(house.Key, isSubordinate: false);
            var row = new AssetQueryService(new[] { house }, reducer.BuildSnapshot(), catalogGeneration: 9)
                .Query(new AssetQuery()).Items.Single();

            Assert.That(row.Instances.Availability, Is.EqualTo(Availability.NotScanned));
            Assert.That(row.Presence, Is.EqualTo(CensusPresence.Unknown));
        }

        private static PrefabRecord[] CreateCatalog()
        {
            return new[]
            {
                new PrefabRecord(new PrefabKey("asset-house-01", "Building"), "Detached House", PrefabTraits.Building,
                    new AssetOriginEvidence(isBuiltin: true, assetDatabaseSource: "vanilla")),
                new PrefabRecord(new PrefabKey("asset-car-02", "Vehicle"), "City Car", PrefabTraits.Vehicle,
                    new AssetOriginEvidence(isSubscribedMod: true, assetPackMembership: new[] { "road-pack" })),
                Record("asset-zero-03", "Empty Lot", PrefabTraits.Building)
            };
        }

        private static CensusSnapshot BuildSnapshot(PrefabRecord[] records)
        {
            var reducer = new CensusReducer(records, 3, 8, CapturedAt, ScanOptions.Default);
            reducer.AddObject(records[0].Key, isSubordinate: false);
            reducer.AddObject(records[1].Key, isSubordinate: false);
            return reducer.BuildSnapshot();
        }

        private static PrefabRecord Record(string id, string name, PrefabTraits traits, AssetOriginEvidence? origin = null)
        {
            return new PrefabRecord(new PrefabKey(id, traits.ToString()), name, traits, origin ?? new AssetOriginEvidence());
        }
    }
}
