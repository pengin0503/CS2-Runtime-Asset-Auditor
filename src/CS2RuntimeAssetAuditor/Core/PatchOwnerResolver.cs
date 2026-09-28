using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core
{
    /// <summary>
    /// Harmony reports patch owners by Harmony ID (for example "Mods_Yenyang_Anarchy"), while system
    /// ownership is keyed by assembly name (for example "Anarchy"). Resolving each patch to the assembly
    /// that declares the patch method keeps one attribution key per mod.
    /// </summary>
    public static class PatchOwnerResolver
    {
        public static IReadOnlyList<PatchOwnerInfo> Resolve(IEnumerable<(string HarmonyId, string PatchAssemblyName)> patches)
        {
            var result = new List<PatchOwnerInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (harmonyId, patchAssemblyName) in patches ?? Enumerable.Empty<(string, string)>())
            {
                var ownerId = !string.IsNullOrWhiteSpace(patchAssemblyName) ? patchAssemblyName.Trim()
                    : !string.IsNullOrWhiteSpace(harmonyId) ? harmonyId.Trim()
                    : null;
                if (ownerId == null || !seen.Add(ownerId))
                    continue;
                result.Add(new PatchOwnerInfo(ownerId, string.IsNullOrWhiteSpace(harmonyId) ? ownerId : harmonyId.Trim()));
            }

            return result;
        }
    }
}
