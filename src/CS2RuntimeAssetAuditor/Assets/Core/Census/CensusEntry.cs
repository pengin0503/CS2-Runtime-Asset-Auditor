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
    }
}
