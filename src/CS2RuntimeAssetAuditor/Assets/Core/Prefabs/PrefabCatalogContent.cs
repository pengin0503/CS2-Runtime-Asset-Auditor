using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Assets.Core.Prefabs
{
    // Decides whether a fresh catalog capture describes the same Prefab records as the published catalog.
    // Census and Asset Analysis snapshots are bound to a catalog generation, so the generation must only
    // advance when the catalog content actually changed; otherwise every rescan would orphan valid snapshots.
    public static class PrefabCatalogContent
    {
        public static bool HasSameRecords(IReadOnlyList<PrefabRecord> left, IReadOnlyList<PrefabRecord> right)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (left.Count != right.Count)
                return false;

            var byKey = new Dictionary<PrefabKey, PrefabRecord>(left.Count);
            foreach (var record in left)
                byKey[record.Key] = record;
            if (byKey.Count != left.Count)
                return false;

            foreach (var record in right)
            {
                if (!byKey.TryGetValue(record.Key, out var other) || !SameRecord(record, other))
                    return false;
            }
            return true;
        }

        private static bool SameRecord(PrefabRecord left, PrefabRecord right)
        {
            return StringComparer.Ordinal.Equals(left.DisplayName, right.DisplayName)
                && left.Traits == right.Traits
                && SameEvidence(left.OriginEvidence, right.OriginEvidence);
        }

        private static bool SameEvidence(AssetOriginEvidence left, AssetOriginEvidence right)
        {
            return left.IsBuiltin == right.IsBuiltin
                && left.IsSubscribedMod == right.IsSubscribedMod
                && left.IsPackaged == right.IsPackaged
                && StringComparer.Ordinal.Equals(left.AssetDatabaseSource, right.AssetDatabaseSource)
                && StringComparer.Ordinal.Equals(left.ParadoxModsPlatformId, right.ParadoxModsPlatformId)
                && SameIdentifiers(left.DlcPrerequisiteIds, right.DlcPrerequisiteIds)
                && SameIdentifiers(left.AssetPackMembership, right.AssetPackMembership);
        }

        private static bool SameIdentifiers(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
        {
            if (left == null || right == null)
                return left == null && right == null;
            if (left.Count != right.Count)
                return false;
            for (var index = 0; index < left.Count; index++)
                if (!StringComparer.Ordinal.Equals(left[index], right[index]))
                    return false;
            return true;
        }
    }
}
