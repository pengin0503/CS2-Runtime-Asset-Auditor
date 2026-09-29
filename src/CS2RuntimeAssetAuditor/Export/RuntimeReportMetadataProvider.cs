using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Modding;
using Game.SceneFlow;
using UnityEngine;

namespace CS2RuntimeAssetAuditor.Export
{
    internal static class RuntimeReportMetadataProvider
    {
        public static RuntimeReportMetadata Capture()
        {
            ReadMods(out var enabled, out var failed);
            return new RuntimeReportMetadata
            {
                BuildId = BuildIdentityProvider.Current,
                HardwareSummary = BuildHardwareSummary(),
                EnabledMods = enabled,
                ModLoadFailures = failed
            };
        }

        private static string BuildHardwareSummary()
        {
            try
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(SystemInfo.processorType))
                    parts.Add($"CPU={SystemInfo.processorType} ({SystemInfo.processorCount} logical)");
                if (SystemInfo.systemMemorySize > 0)
                    parts.Add($"RAM={SystemInfo.systemMemorySize} MB");
                if (!string.IsNullOrWhiteSpace(SystemInfo.graphicsDeviceName))
                    parts.Add($"GPU={SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB, {SystemInfo.graphicsDeviceType})");
                if (!string.IsNullOrWhiteSpace(SystemInfo.operatingSystem))
                    parts.Add($"OS={SystemInfo.operatingSystem}");
                return string.Join("; ", parts);
            }
            catch
            {
                return null;
            }
        }

        // The game's public ModManager API (1.6.2f1): ListModsEnabled() gives the loaded code mods and the UI
        // modules, and each ModInfo gives a code mod's load state. The loaded-IMod scan below is kept only for a
        // game build where ModManager cannot be read.
        private static void ReadMods(out IReadOnlyList<string> enabled, out IReadOnlyList<string> failed)
        {
            enabled = Array.Empty<string>();
            failed = Array.Empty<string>();
            try
            {
                var modManager = GameManager.instance?.modManager;
                if (modManager != null)
                {
                    var mods = new List<ModLoadInfo>();
                    foreach (var info in modManager)
                    {
                        if (info == null) continue;
                        try { mods.Add(new ModLoadInfo(info.name, info.isLoaded, info.state.ToString())); }
                        catch { /* An entry whose asset cannot be read is left out, not guessed. */ }
                    }
                    enabled = EnabledModNames.Enabled(modManager.ListModsEnabled(), mods);
                    failed = EnabledModNames.Failed(mods);
                    return;
                }
            }
            catch
            {
                // Continue into the loaded-code-mod fallback below.
            }

            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectLoadedCodeMods(names);
            enabled = names.ToArray();
        }

        private static void CollectLoadedCodeMods(ISet<string> names)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly == null || assembly.IsDynamic)
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(type => type != null).ToArray();
                }
                catch
                {
                    continue;
                }

                if (!types.Any(type => type != null
                    && type != typeof(IMod)
                    && !type.IsAbstract
                    && typeof(IMod).IsAssignableFrom(type)))
                {
                    continue;
                }

                var assemblyName = assembly.GetName().Name;
                if (EnabledModNames.IsSafeLabel(assemblyName))
                    names.Add(assemblyName);
            }
        }
    }
}
