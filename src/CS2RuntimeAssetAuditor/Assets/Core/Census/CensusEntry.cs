using System;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.Core.Census
{
    public sealed class CensusEntry
    {
        public CensusEntry(PrefabKey key, PrefabTraits traits, CensusCounters counters, CensusPresence presence)
        {
            if (!key.IsValid)
                throw new ArgumentException("A Census entry needs a valid Prefab key.", nameof(key));
            Key = key;
            Traits = traits;
            Counters = counters ?? throw new ArgumentNullException(nameof(counters));
            Presence = presence;
        }

        public PrefabKey Key { get; }

        public PrefabTraits Traits { get; }

        public CensusCounters Counters { get; }

        public CensusPresence Presence { get; }

        public CensusCountKind PrimaryCountKind
        {
            get
            {
                if (Traits.HasFlag(PrefabTraits.Network))
                    return CensusCountKind.NetworkEdges;
                if (Traits.HasFlag(PrefabTraits.Prop) || Traits.HasFlag(PrefabTraits.Vehicle))
                    return CensusCountKind.LiveObjectReferences;
                if (Traits.HasFlag(PrefabTraits.Building)
                    || Traits.HasFlag(PrefabTraits.ServiceBuilding)
                    || Traits.HasFlag(PrefabTraits.Tree))
                    return CensusCountKind.TopLevelObjects;
                return CensusCountKind.None;
            }
        }
    }
}
