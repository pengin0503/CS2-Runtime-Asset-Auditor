using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CS2RuntimeProfiler.Profiling
{
    public static class HarmonyRuntimeResolver
    {
        private const string HarmonyTypeName = "HarmonyLib.Harmony";
        private const string HarmonyAssemblyName = "0Harmony";
        private const string HarmonyFileName = "0Harmony.dll";

        public static Assembly Resolve(
            IEnumerable<Assembly> loadedAssemblies,
            Func<AssemblyName, Assembly> loader,
            Func<string, Assembly> pathLoader = null,
            string baseDirectory = null)
        {
            try
            {
                var loaded = (loadedAssemblies ?? Array.Empty<Assembly>())
                    .FirstOrDefault(assembly => HasHarmonyType(assembly));
                if (loaded != null)
                    return loaded;

                if (loader != null)
                {
                    try
                    {
                        var byName = loader(new AssemblyName(HarmonyAssemblyName));
                        if (HasHarmonyType(byName))
                            return byName;
                    }
                    catch
                    {
                        // Fall through to the explicit sibling-path load below.
                    }
                }

                if (pathLoader == null || string.IsNullOrWhiteSpace(baseDirectory))
                    return null;

                try
                {
                    var byPath = pathLoader(Path.Combine(baseDirectory, HarmonyFileName));
                    return HasHarmonyType(byPath) ? byPath : null;
                }
                catch
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool HasHarmonyType(Assembly assembly)
        {
            if (assembly == null)
                return false;

            try
            {
                return assembly.GetType(HarmonyTypeName, throwOnError: false) != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
