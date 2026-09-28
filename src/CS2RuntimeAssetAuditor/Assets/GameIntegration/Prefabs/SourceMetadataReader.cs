using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Prefabs
{
    public static class SourceMetadataReader
    {
        public static AssetOriginEvidence Read(
            bool? isBuiltin,
            bool? isSubscribedMod,
            bool? isPackaged,
            IEnumerable<string>? dlcPrerequisiteIds = null,
            IEnumerable<string>? assetPackMembership = null,
            string? assetDatabaseSource = null,
            string? paradoxModsPlatformId = null)
        {
            return new AssetOriginEvidence(
                isBuiltin,
                isSubscribedMod,
                isPackaged,
                dlcPrerequisiteIds,
                assetPackMembership,
                assetDatabaseSource,
                paradoxModsPlatformId);
        }
    }
}
