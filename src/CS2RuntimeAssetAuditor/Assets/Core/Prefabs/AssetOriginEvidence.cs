using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Assets.Core.Prefabs
{
    public sealed class AssetOriginEvidence
    {
        public AssetOriginEvidence(
            bool? isBuiltin = null,
            bool? isSubscribedMod = null,
            bool? isPackaged = null,
            IEnumerable<string>? dlcPrerequisiteIds = null,
            IEnumerable<string>? assetPackMembership = null,
            string? assetDatabaseSource = null,
            string? paradoxModsPlatformId = null)
        {
            IsBuiltin = isBuiltin;
            IsSubscribedMod = isSubscribedMod;
            IsPackaged = isPackaged;
            DlcPrerequisiteIds = CopyOptionalIdentifiers(dlcPrerequisiteIds, nameof(dlcPrerequisiteIds));
            AssetPackMembership = CopyOptionalIdentifiers(assetPackMembership, nameof(assetPackMembership));
            AssetDatabaseSource = assetDatabaseSource;
            ParadoxModsPlatformId = paradoxModsPlatformId;
        }

        public bool? IsBuiltin { get; }

        public bool? IsSubscribedMod { get; }

        public bool? IsPackaged { get; }

        // Null means the evidence has not been read; an empty list means it was read and empty.
        public IReadOnlyList<string>? DlcPrerequisiteIds { get; }

        // Null means the evidence has not been read; an empty list means it was read and empty.
        public IReadOnlyList<string>? AssetPackMembership { get; }

        public string? AssetDatabaseSource { get; }

        public string? ParadoxModsPlatformId { get; }

        private static IReadOnlyList<string>? CopyOptionalIdentifiers(IEnumerable<string>? identifiers, string parameterName)
        {
            if (identifiers == null)
                return null;

            var copy = new List<string>();
            foreach (var identifier in identifiers)
            {
                if (string.IsNullOrWhiteSpace(identifier))
                    throw new ArgumentException("Source identifiers must be non-empty.", parameterName);
                copy.Add(identifier);
            }

            return copy.AsReadOnly();
        }
    }
}
