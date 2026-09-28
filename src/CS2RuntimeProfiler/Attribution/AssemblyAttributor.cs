using System;
using CS2RuntimeProfiler.Core;

namespace CS2RuntimeProfiler.Attribution
{
    public static class AssemblyAttributor
    {
        public static SystemSourceKind ClassifyName(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return SystemSourceKind.Unknown;

            if (string.Equals(assemblyName, "Game", StringComparison.OrdinalIgnoreCase))
                return SystemSourceKind.Vanilla;

            if (string.Equals(assemblyName, "CS2RuntimeProfiler", StringComparison.OrdinalIgnoreCase))
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
