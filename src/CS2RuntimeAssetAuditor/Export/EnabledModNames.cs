using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Export
{
    /// <summary>One entry of the game's <c>ModManager</c> (<c>ModManager.ModInfo</c>) as the report reads it.</summary>
    public readonly struct ModLoadInfo
    {
        public ModLoadInfo(string name, bool isLoaded, string state)
        {
            Name = name;
            IsLoaded = isLoaded;
            State = state;
        }

        /// <summary>The code mod's assembly full name (<c>ExecutableAsset.fullName</c>).</summary>
        public string Name { get; }
        public bool IsLoaded { get; }
        public string State { get; }
    }

    /// <summary>
    /// Turns the game's own mod lists into report labels. <c>ModManager.ListModsEnabled()</c> (1.6.2f1) returns the
    /// full assembly name of each loaded code mod followed by the name of each UI module; code mods are reported by
    /// their simple assembly name, as before, and UI modules by their name. Code mods the game did not load are
    /// listed separately with the game's reason.
    /// </summary>
    public static class EnabledModNames
    {
        // States that are not a failed mod: not loaded yet, loaded, unloaded at shutdown, or a library assembly
        // without an IMod (IsNotModWarning).
        private static readonly HashSet<string> NotFailures = new HashSet<string>(StringComparer.Ordinal)
        {
            "Unknown", "Loaded", "Disposed", "IsNotModWarning"
        };

        public static IReadOnlyList<string> Enabled(IEnumerable<string>? listModsEnabled, IEnumerable<ModLoadInfo>? mods)
        {
            var codeModNames = new HashSet<string>(
                (mods ?? Enumerable.Empty<ModLoadInfo>()).Select(mod => mod.Name).Where(name => name != null),
                StringComparer.Ordinal);
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in listModsEnabled ?? Enumerable.Empty<string>())
            {
                if (entry == null) continue;
                var label = codeModNames.Contains(entry) ? SimpleAssemblyName(entry) : entry.Trim();
                if (IsSafeLabel(label)) names.Add(label);
            }
            return names.ToArray();
        }

        public static IReadOnlyList<string> Failed(IEnumerable<ModLoadInfo>? mods)
        {
            var failed = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in mods ?? Enumerable.Empty<ModLoadInfo>())
            {
                if (mod.IsLoaded || mod.State == null || NotFailures.Contains(mod.State)) continue;
                var label = SimpleAssemblyName(mod.Name);
                if (IsSafeLabel(label)) failed.Add(label + " (" + mod.State + ")");
            }
            return failed.ToArray();
        }

        /// <summary>"Foo, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" to "Foo".</summary>
        public static string SimpleAssemblyName(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;
            var comma = fullName!.IndexOf(',');
            return (comma < 0 ? fullName : fullName.Substring(0, comma)).Trim();
        }

        // Labels never carry a path (privacy) and stay short.
        public static bool IsSafeLabel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            var trimmed = value!.Trim();
            return trimmed.Length <= 160
                && trimmed.IndexOf('\\') < 0
                && trimmed.IndexOf('/') < 0
                && trimmed.IndexOf(":\\", StringComparison.Ordinal) < 0;
        }
    }
}
