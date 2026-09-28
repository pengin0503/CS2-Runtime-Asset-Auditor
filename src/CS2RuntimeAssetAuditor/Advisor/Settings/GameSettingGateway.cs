using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CS2RuntimeAssetAuditor.Core.Advisor;

namespace CS2RuntimeAssetAuditor.Advisor.Settings
{
    public sealed class GameSettingGateway : IGameSettingGateway
    {
        private readonly IReadOnlyDictionary<string, AutomaticSettingAdapter> _adapters;
        private readonly IStandardGameSettingCatalog _catalog;

        public GameSettingGateway() : this(BuildStandardAdapters(), new GameSettingCatalogBuilder()) { }

        public GameSettingGateway(IEnumerable<AutomaticSettingAdapter> adapters)
            : this(adapters, null) { }

        public GameSettingGateway(IEnumerable<AutomaticSettingAdapter> adapters, IStandardGameSettingCatalog catalog)
        {
            _adapters = (adapters ?? Array.Empty<AutomaticSettingAdapter>())
                .Where(adapter => adapter != null)
                .GroupBy(adapter => adapter.Descriptor.SettingId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            _catalog = catalog;
        }

        public IReadOnlyList<GameSettingDescriptor> GetCatalog()
            => _catalog?.GetCatalog().Select(descriptor => _adapters.ContainsKey(descriptor.SettingId)
                ? descriptor : descriptor.AsReadOnlyForAdvisor()).ToArray()
                ?? _adapters.Values.Select(adapter => adapter.Descriptor).ToArray();

        public string Read(string settingId)
        {
            if (settingId == null || !_adapters.TryGetValue(settingId, out var adapter)) return null;
            try { return adapter.Read(); }
            catch (Exception) { return null; }
        }

        public SettingApplyResult Apply(string settingId, string value, bool confirmed = false)
        {
            var result = new SettingApplyResult { Requested = value };
            if (settingId == null || !_adapters.TryGetValue(settingId, out var adapter))
            {
                result.FailureReason = "NoVerifiedStandardOptionsAdapter";
                return result;
            }
            var setting = adapter.Descriptor;
            result.ApplyBehavior = setting.ApplyBehavior;
            try
            {
                result.ObservedBefore = adapter.Read();
                if (!setting.IsUserFacing || !setting.IsReadable || !setting.IsWritable
                    || setting.IsCurrentlyVisible != true || setting.IsCurrentlyEnabled != true
                    || !setting.HasSafeReversibleWritePath)
                {
                    result.FailureReason = "ReadOnlyForAdvisor";
                    return result;
                }
                if (setting.ApplyBehavior == SettingApplyBehavior.ConfirmationRequired && !confirmed)
                {
                    result.FailureReason = "ConfirmationRequired";
                    return result;
                }
                if (!adapter.Validate(value))
                {
                    result.FailureReason = "InvalidRequestedValue";
                    return result;
                }
                adapter.WriteAndSave(value);
                result.ObservedAfter = adapter.Read();
                result.Succeeded = string.Equals(result.ObservedAfter, value, StringComparison.Ordinal);
                if (!result.Succeeded) result.FailureReason = "PostApplyMismatch";
            }
            catch (Exception ex)
            {
                result.FailureReason = "AdapterFailure:" + ex.GetType().Name;
            }
            return result;
        }

        public SettingApplyResult Restore(string settingId, string expectedCurrentValue, string originalValue, bool confirmed = false)
        {
            var current = Read(settingId);
            if (current == null || !string.Equals(current, expectedCurrentValue, StringComparison.Ordinal))
                return new SettingApplyResult { Requested = originalValue, ObservedBefore = current,
                    ObservedAfter = current, FailureReason = "ExternallyModified" };
            return Apply(settingId, originalValue, confirmed);
        }

        private static IEnumerable<AutomaticSettingAdapter> BuildStandardAdapters()
        {
            var catalog = new GameSettingCatalogBuilder().GetCatalog().ToDictionary(d => d.SettingId, StringComparer.Ordinal);
            foreach (var root in GameSettingCatalogBuilder.GetBuiltInRoots())
            foreach (var property in root.Instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var id = root.Instance.GetType().FullName + "::" + property.Name;
                if (!catalog.TryGetValue(id, out var descriptor)) continue;
                var adapter = AutomaticSettingAdapter.FromStandardRoot(root.Instance, property, descriptor);
                if (adapter != null) yield return adapter;
            }
        }
    }
}
