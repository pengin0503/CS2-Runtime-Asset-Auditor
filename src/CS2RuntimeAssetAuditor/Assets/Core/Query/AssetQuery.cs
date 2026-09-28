using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.Core.Query
{
    public enum AssetSourceFilter
    {
        Any,
        Builtin,
        SubscribedMod,
        Packaged,
        UserProvided,
        Unknown
    }

    public sealed class AssetQuery
    {
        public const int DefaultPageSize = 100;

        public AssetQuery(
            string? searchText = null,
            PrefabTraits? traitFilter = null,
            AssetSourceFilter sourceFilter = AssetSourceFilter.Any,
            CensusPresence? presenceFilter = null,
            AssetSort sort = AssetSort.DisplayNameAscending,
            int offset = 0,
            int limit = DefaultPageSize)
        {
            if (offset < 0)
                throw new System.ArgumentOutOfRangeException(nameof(offset));
            if (limit <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(limit));

            SearchText = searchText?.Trim();
            TraitFilter = traitFilter;
            SourceFilter = sourceFilter;
            PresenceFilter = presenceFilter;
            Sort = sort;
            Offset = offset;
            Limit = limit;
        }

        public string? SearchText { get; }

        public PrefabTraits? TraitFilter { get; }

        public AssetSourceFilter SourceFilter { get; }

        public CensusPresence? PresenceFilter { get; }

        public AssetSort Sort { get; }

        public int Offset { get; }

        public int Limit { get; }
    }
}
