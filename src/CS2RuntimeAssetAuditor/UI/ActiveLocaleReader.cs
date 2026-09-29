using System;
using System.Reflection;
using Game.SceneFlow;

namespace CS2RuntimeAssetAuditor.UI
{
    /// <summary>
    /// Reads the game's active interface locale (for example "ja-JP" or "en-US"). Read through reflection so a
    /// renamed member in a future game build degrades to English instead of breaking the mod.
    /// </summary>
    internal static class ActiveLocaleReader
    {
        private const string Fallback = "en-US";
        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static string Read()
        {
            try
            {
                var manager = GameManager.instance?.localizationManager;
                if (manager == null)
                    return Fallback;
                var type = ((object)manager).GetType();
                var value = type.GetProperty("activeLocaleId", InstanceFlags)?.GetValue(manager, null) as string
                    ?? type.GetField("m_ActiveLocaleId", InstanceFlags)?.GetValue(manager) as string;
                return string.IsNullOrWhiteSpace(value) ? Fallback : value;
            }
            catch (Exception)
            {
                return Fallback;
            }
        }
    }
}
