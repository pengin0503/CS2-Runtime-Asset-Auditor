using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.Core.Query
{
    public sealed class AssetPageItem
    {
        public AssetPageItem(
            PrefabRecord asset,
            CensusEntry? censusEntry,
            Observation<long> instances,
            CensusCountKind countKind,
            CensusPresence presence)
        {
            Asset = asset ?? throw new ArgumentNullException(nameof(asset));
            CensusEntry = censusEntry;
            Instances = instances ?? throw new ArgumentNullException(nameof(instances));
            CountKind = countKind;
            Presence = presence;
        }

        public PrefabRecord Asset { get; }

        public CensusEntry? CensusEntry { get; }

        public Observation<long> Instances { get; }

        public CensusCountKind CountKind { get; }

        public CensusPresence Presence { get; }
    }

    public sealed class AssetPage
    {
        public AssetPage(IReadOnlyList<AssetPageItem> items, int totalCount, int offset, int limit)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
            TotalCount = totalCount;
            Offset = offset;
            Limit = limit;
        }

        public IReadOnlyList<AssetPageItem> Items { get; }

        public int TotalCount { get; }

        public int Offset { get; }

        public int Limit { get; }
    }
}
