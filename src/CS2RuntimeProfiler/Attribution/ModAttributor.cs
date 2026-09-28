using System;
using System.Collections.Generic;

namespace CS2RuntimeProfiler.Attribution
{
    public sealed class ModAttributor
    {
        private readonly IReadOnlyDictionary<string, string> _assemblyToMod;

        public ModAttributor(IReadOnlyDictionary<string, string> assemblyToMod)
        {
            _assemblyToMod = assemblyToMod ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string Resolve(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return null;

            return _assemblyToMod.TryGetValue(assemblyName, out var modName) ? modName : null;
        }
    }
}
