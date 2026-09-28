using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CS2RuntimeAssetAuditor.Core;

namespace CS2RuntimeAssetAuditor.Attribution
{
    public sealed class HarmonyPatchInspector
    {
        private readonly MethodInfo _getPatchInfo;

        private HarmonyPatchInspector(MethodInfo getPatchInfo)
        {
            _getPatchInfo = getPatchInfo;
        }

        public static HarmonyPatchInspector TryCreate()
        {
            try
            {
                var harmonyAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, "0Harmony", StringComparison.OrdinalIgnoreCase));
                var harmonyType = harmonyAssembly?.GetType("HarmonyLib.Harmony", throwOnError: false);
                var getPatchInfo = harmonyType?.GetMethod(
                    "GetPatchInfo",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(MethodBase) },
                    modifiers: null);
                return new HarmonyPatchInspector(getPatchInfo);
            }
            catch
            {
                return new HarmonyPatchInspector(null);
            }
        }

        public IReadOnlyList<PatchOwnerInfo> GetPatchOwners(MethodBase method)
        {
            if (_getPatchInfo == null || method == null)
                return Array.Empty<PatchOwnerInfo>();

            try
            {
                var patchInfo = _getPatchInfo.Invoke(null, new object[] { method });
                if (patchInfo == null)
                    return Array.Empty<PatchOwnerInfo>();

                var patches = new List<(string HarmonyId, string PatchAssemblyName)>();
                foreach (var listName in new[] { "Prefixes", "Postfixes", "Transpilers", "Finalizers" })
                {
                    // Harmony 2.x exposes these as public readonly fields; accept properties for other layouts.
                    if (!(ReadMember(patchInfo, listName) is IEnumerable list))
                        continue;
                    foreach (var patch in list)
                    {
                        if (patch == null)
                            continue;
                        var owner = ReadMember(patch, "owner") as string;
                        var patchMethod = ReadMember(patch, "PatchMethod") as MethodInfo;
                        patches.Add((owner, patchMethod?.DeclaringType?.Assembly.GetName().Name));
                    }
                }

                if (patches.Count == 0)
                {
                    // Older Harmony layouts: fall back to the owner IDs only.
                    var ownersProperty = patchInfo.GetType().GetProperty("Owners", BindingFlags.Public | BindingFlags.Instance);
                    if (ownersProperty?.GetValue(patchInfo) is IEnumerable<string> owners)
                        patches.AddRange(owners.Select(owner => (owner, (string)null)));
                }

                return PatchOwnerResolver.Resolve(patches);
            }
            catch
            {
                return Array.Empty<PatchOwnerInfo>();
            }
        }

        private static object ReadMember(object instance, string name)
        {
            var type = instance.GetType();
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
                return field.GetValue(instance);
            return type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);
        }
    }
}
