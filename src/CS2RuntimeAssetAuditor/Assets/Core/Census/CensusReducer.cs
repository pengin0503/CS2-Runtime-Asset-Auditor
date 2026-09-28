using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.Core.Census
{
    public sealed class CensusReducer
    {
        private sealed class MutableEntry
        {
            public MutableEntry(PrefabRecord record)
            {
                Record = record;
            }

            public PrefabRecord Record { get; }

            public long TopLevelObjects { get; set; }

            public long SubordinateObjects { get; set; }

            public long NetworkEdges { get; set; }

            public bool ObjectExposureObserved { get; set; }

            public bool NetworkExposureObserved { get; set; }
        }

        private const PrefabTraits ObjectTraits = PrefabTraits.Building
            | PrefabTraits.ServiceBuilding
            | PrefabTraits.Prop
            | PrefabTraits.Tree
            | PrefabTraits.Vehicle;

        private readonly Dictionary<PrefabKey, MutableEntry> _entries;
        private readonly long _worldGeneration;
        private readonly long _catalogGeneration;
        private readonly DateTimeOffset _capturedAt;

        public CensusReducer(
            IEnumerable<PrefabRecord> catalog,
            long worldGeneration,
            long catalogGeneration,
            DateTimeOffset capturedAt,
            ScanOptions scanOptions)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            _worldGeneration = worldGeneration;
            _catalogGeneration = catalogGeneration;
            _capturedAt = capturedAt;
            ScanOptions = scanOptions ?? throw new ArgumentNullException(nameof(scanOptions));
            _entries = new Dictionary<PrefabKey, MutableEntry>();
            foreach (var record in catalog)
            {
                if (record == null)
                    throw new ArgumentException("Catalog entries cannot be null.", nameof(catalog));
                if (_entries.ContainsKey(record.Key))
                    throw new ArgumentException("Catalog Prefab keys must be unique.", nameof(catalog));
                _entries.Add(record.Key, new MutableEntry(record));
            }
        }

        public ScanOptions ScanOptions { get; }

        public void AddObject(PrefabKey key, bool isSubordinate)
        {
            var entry = GetEntry(key);
            entry.ObjectExposureObserved = true;
            if (isSubordinate)
            {
                if (ScanOptions.CollectSubordinateObjects)
                    entry.SubordinateObjects = checked(entry.SubordinateObjects + 1);
                return;
            }

            entry.TopLevelObjects = checked(entry.TopLevelObjects + 1);
        }

        public void AddNetworkEdge(PrefabKey key)
        {
            if (!ScanOptions.CollectNetworkEdges)
                return;
            var entry = GetEntry(key);
            entry.NetworkExposureObserved = true;
            entry.NetworkEdges = checked(entry.NetworkEdges + 1);
        }

        public CensusSnapshot BuildSnapshot()
        {
            var entries = _entries.Values.Select(CreateEntry).ToArray();
            return new CensusSnapshot(
                _worldGeneration,
                CensusQueryProfile.V1,
                ScanOptions,
                _capturedAt,
                _catalogGeneration,
                entries);
        }

        private MutableEntry GetEntry(PrefabKey key)
        {
            if (_entries.TryGetValue(key, out var entry))
                return entry;
            throw new InvalidOperationException("A Census sample referenced a Prefab outside the captured catalog.");
        }

        private CensusEntry CreateEntry(MutableEntry entry)
        {
            var traits = entry.Record.Traits;
            var hasObjectTraits = (traits & ObjectTraits) != PrefabTraits.None;
            var objectUnavailable = !hasObjectTraits && !entry.ObjectExposureObserved
                ? traits.HasFlag(PrefabTraits.Network) || traits.HasFlag(PrefabTraits.RenderOnly)
                    ? Availability.NotApplicable
                    : Availability.NotScanned
                : Availability.Available;
            var subordinateUnavailable = !ScanOptions.CollectSubordinateObjects && objectUnavailable == Availability.Available
                ? Availability.NotScanned
                : objectUnavailable;
            var liveUnavailable = subordinateUnavailable == Availability.NotScanned
                ? Availability.NotScanned
                : objectUnavailable;

            var top = MakeCount(objectUnavailable, entry.TopLevelObjects);
            var subordinate = MakeCount(subordinateUnavailable, entry.SubordinateObjects);
            var live = objectUnavailable == Availability.Available && ScanOptions.CollectSubordinateObjects
                ? Observation<long>.FromValue(checked(entry.TopLevelObjects + entry.SubordinateObjects), ObservationOrigin.Ecs, _capturedAt)
                : MakeCount(liveUnavailable, 0);

            var networkUnavailable = !traits.HasFlag(PrefabTraits.Network) && !entry.NetworkExposureObserved
                ? hasObjectTraits || traits.HasFlag(PrefabTraits.RenderOnly)
                    ? Availability.NotApplicable
                    : Availability.NotScanned
                : ScanOptions.CollectNetworkEdges ? Availability.Available : Availability.NotScanned;
            var network = MakeCount(networkUnavailable, entry.NetworkEdges);
            var counters = new CensusCounters(top, subordinate, live, network);
            return new CensusEntry(entry.Record.Key, traits, counters, GetPresence(counters));
        }

        private Observation<long> MakeCount(Availability availability, long value)
        {
            return availability == Availability.Available
                ? Observation<long>.FromValue(value, ObservationOrigin.Ecs, _capturedAt)
                : Observation<long>.Unavailable(availability, ObservationOrigin.Ecs, _capturedAt);
        }

        private static CensusPresence GetPresence(CensusCounters counters)
        {
            var observations = new[]
            {
                counters.TopLevelObjects,
                counters.SubordinateObjects,
                counters.LiveObjectReferences,
                counters.NetworkEdges
            };
            if (observations.Any(observation => observation.HasValue && observation.Value > 0))
                return CensusPresence.Present;

            var applicable = observations.Where(observation => observation.Availability != Availability.NotApplicable).ToArray();
            if (applicable.Length == 0)
                return CensusPresence.NotApplicable;
            if (applicable.Any(observation => !observation.HasValue))
                return CensusPresence.Unknown;
            return CensusPresence.NotPresentAtSnapshot;
        }
    }
}
