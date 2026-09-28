using System;
using CS2RuntimeAssetAuditor.Core;

namespace CS2RuntimeAssetAuditor.Attribution
{
    public static class AssemblyAttributor
    {
        public static SystemSourceKind ClassifyName(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return SystemSourceKind.Unknown;

            if (string.Equals(assemblyName, "Game", StringComparison.OrdinalIgnoreCase))
                return SystemSourceKind.Vanilla;

            if (string.Equals(assemblyName, "CS2RuntimeAssetAuditor", StringComparison.OrdinalIgnoreCase))
                return SystemSourceKind.Profiler;

            if (assemblyName.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase) ||
                assemblyName.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
                assemblyName.StartsWith("Colossal.", StringComparison.OrdinalIgnoreCase) ||
                assemblyName.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
                assemblyName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(assemblyName, "mscorlib", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(assemblyName, "netstandard", StringComparison.OrdinalIgnoreCase))
                return SystemSourceKind.Runtime;

            return SystemSourceKind.Mod;
        }
    }
}
