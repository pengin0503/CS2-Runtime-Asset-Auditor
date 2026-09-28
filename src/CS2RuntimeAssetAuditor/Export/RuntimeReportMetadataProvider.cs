using System;
using System.Collections;
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
        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static RuntimeReportMetadata Capture()
        {
            return new RuntimeReportMetadata
            {
                BuildId = BuildIdentityProvider.Current,
                HardwareSummary = BuildHardwareSummary(),
                EnabledMods = GetEnabledMods()
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

        private static IReadOnlyList<string> GetEnabledMods()
        {
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var gameManager = GameManager.instance;
                if (gameManager != null)
                {
                    var modManager = ReadMember(gameManager, "modManager") ?? ReadMember(gameManager, "m_ModManager");
                    if (modManager != null)
                    {
                        CollectFromActiveModApi(modManager, names);
                        if (names.Count == 0)
                            CollectFromKnownActiveCollections(modManager, names);
                    }
                }
            }
            catch
            {
                // Continue into the loaded-code-mod fallback below.
            }

            // Some current game builds expose a ModManager but not a parameterless GetActiveMods API
            // compatible with older builds. Loaded IMod implementations are a conservative fallback:
            // they identify code mods that actually reached the runtime without guessing asset-only mods.
            if (names.Count == 0)
                CollectLoadedCodeMods(names);

            return names.ToArray();
        }

        private static void CollectFromActiveModApi(object modManager, ISet<string> names)
        {
            if (modManager == null)
                return;

            try
            {
                var methods = modManager.GetType()
                    .GetMethods(InstanceFlags)
                    .Where(method => string.Equals(method.Name, "GetActiveMods", StringComparison.Ordinal))
                    .OrderBy(method => method.GetParameters().Length)
                    .ToArray();

                foreach (var method in methods)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length != 0)
                        continue;

                    object activeMods;
                    try
                    {
                        activeMods = method.Invoke(modManager, null);
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var item in Enumerate(activeMods))
                        CollectModNames(item, names, depth: 0);

                    if (names.Count > 0)
                        return;
                }
            }
            catch
            {
            }
        }

        private static void CollectFromKnownActiveCollections(object modManager, ISet<string> names)
        {
            foreach (var memberName in new[]
            {
                "activeMods", "ActiveMods", "m_ActiveMods",
                "enabledMods", "EnabledMods", "m_EnabledMods",
                "mods", "Mods", "m_Mods"
            })
            {
                var value = ReadMember(modManager, memberName);
                foreach (var item in Enumerate(value))
                    CollectModNames(item, names, depth: 0);

                if (names.Count > 0)
                    return;
            }
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
                if (IsSafeLabel(assemblyName))
                    names.Add(assemblyName);
            }
        }

        private static IEnumerable<object> Enumerate(object value)
        {
            if (value == null || value is string)
                yield break;

            if (value is IEnumerable sequence)
            {
                foreach (var item in sequence)
                {
                    if (item != null)
                        yield return item;
                }
                yield break;
            }

            yield return value;
        }

        private static void CollectModNames(object value, ISet<string> names, int depth)
        {
            if (value == null || depth > 3)
                return;

            if (value is IMod mod)
            {
                var assemblyName = mod.GetType().Assembly.GetName().Name;
                if (IsSafeLabel(assemblyName))
                    names.Add(assemblyName);
                return;
            }

            if (value is string text)
            {
                if (IsSafeLabel(text))
                    names.Add(text.Trim());
                return;
            }

            foreach (var memberName in new[] { "displayName", "DisplayName", "modName", "ModName", "name", "Name", "modId", "ModId" })
            {
                var memberValue = ReadMember(value, memberName) as string;
                if (IsSafeLabel(memberValue))
                {
                    names.Add(memberValue.Trim());
                    return;
                }
            }

            foreach (var nestedName in new[] { "mod", "Mod", "instance", "Instance", "value", "Value", "key", "Key", "entry", "Entry" })
            {
                var nested = ReadMember(value, nestedName);
                if (nested != null && !ReferenceEquals(nested, value))
                    CollectModNames(nested, names, depth + 1);
            }
        }

        private static object ReadMember(object target, string name)
        {
            if (target == null || string.IsNullOrWhiteSpace(name))
                return null;

            var type = target.GetType();
            try
            {
                var property = type.GetProperty(name, InstanceFlags);
                if (property != null && property.GetIndexParameters().Length == 0)
                    return property.GetValue(target, null);
            }
            catch
            {
            }

            try
            {
                var field = type.GetField(name, InstanceFlags);
                return field?.GetValue(target);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsSafeLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            if (trimmed.Length > 160)
                return false;

            return trimmed.IndexOf('\\') < 0
                && trimmed.IndexOf('/') < 0
                && trimmed.IndexOf(":\\", StringComparison.Ordinal) < 0;
        }
    }
}
