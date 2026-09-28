using System;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;

namespace CS2RuntimeAssetAuditor.Assets.Core.Scanning
{
    public sealed class PublishedAuditState
    {
        public long? WorldGeneration { get; private set; }
        public CensusSnapshot? Census { get; private set; }
        public AssetAnalysisSnapshot? Analysis { get; private set; }

        public void ResetForWorld(long worldGeneration)
        {
            WorldGeneration = worldGeneration;
            Census = null;
            Analysis = null;
        }

        public bool TryPublishCensus(CensusSnapshot snapshot, bool scanSucceeded)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (!scanSucceeded || !WorldGeneration.HasValue || snapshot.WorldGeneration != WorldGeneration.Value)
                return false;

            Census = snapshot;
            return true;
        }

        public bool TryPublishAnalysis(AssetAnalysisSnapshot snapshot, bool scanSucceeded)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (!scanSucceeded || !WorldGeneration.HasValue || snapshot.WorldGeneration != WorldGeneration.Value)
                return false;

            Analysis = snapshot;
            return true;
        }
    }
}
