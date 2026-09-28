using System;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;

namespace CS2RuntimeAssetAuditor.Assets.Core.Census
{
    public sealed class CensusCounters
    {
        public CensusCounters(
            Observation<long> topLevelObjects,
            Observation<long> subordinateObjects,
            Observation<long> liveObjectReferences,
            Observation<long> networkEdges)
        {
            TopLevelObjects = topLevelObjects ?? throw new ArgumentNullException(nameof(topLevelObjects));
            SubordinateObjects = subordinateObjects ?? throw new ArgumentNullException(nameof(subordinateObjects));
            LiveObjectReferences = liveObjectReferences ?? throw new ArgumentNullException(nameof(liveObjectReferences));
            NetworkEdges = networkEdges ?? throw new ArgumentNullException(nameof(networkEdges));

            if (TopLevelObjects.HasValue && SubordinateObjects.HasValue && LiveObjectReferences.HasValue
                && checked(TopLevelObjects.Value + SubordinateObjects.Value) != LiveObjectReferences.Value)
                throw new ArgumentException("Live object references must equal top-level plus subordinate objects.");
        }

        public Observation<long> TopLevelObjects { get; }

        public Observation<long> SubordinateObjects { get; }

        public Observation<long> LiveObjectReferences { get; }

        public Observation<long> NetworkEdges { get; }
    }
}
