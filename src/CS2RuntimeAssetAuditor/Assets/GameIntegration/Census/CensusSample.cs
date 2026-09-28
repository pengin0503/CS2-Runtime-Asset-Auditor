using System;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Census
{
    public sealed class CensusSample
    {
        private CensusSample(PrefabKey prefabKey, bool isNetworkEdge, bool isSubordinate)
        {
            if (!prefabKey.IsValid)
                throw new ArgumentException("A Census sample needs a stable Prefab key.", nameof(prefabKey));
            PrefabKey = prefabKey;
            IsNetworkEdge = isNetworkEdge;
            IsSubordinate = isSubordinate;
        }

        public PrefabKey PrefabKey { get; }

        public bool IsNetworkEdge { get; }

        public bool IsSubordinate { get; }

        public static CensusSample ForObject(PrefabKey prefabKey, bool isSubordinate)
        {
            return new CensusSample(prefabKey, isNetworkEdge: false, isSubordinate);
        }

        public static CensusSample ForObjectFromMarkers(PrefabKey prefabKey, bool hasOwnerMarker, bool hasControllerMarker)
        {
            return ForObject(prefabKey, hasOwnerMarker || hasControllerMarker);
        }

        public static CensusSample ForNetworkEdge(PrefabKey prefabKey)
        {
            return new CensusSample(prefabKey, isNetworkEdge: true, isSubordinate: false);
        }

        public void AddTo(CensusReducer reducer)
        {
            if (reducer == null)
                throw new ArgumentNullException(nameof(reducer));
            if (IsNetworkEdge)
                reducer.AddNetworkEdge(PrefabKey);
            else
                reducer.AddObject(PrefabKey, IsSubordinate);
        }
    }
}
