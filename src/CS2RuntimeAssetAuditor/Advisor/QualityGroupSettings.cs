using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Advisor.Settings;
using CS2RuntimeAssetAuditor.Core.Advisor;
using Game.Settings;

namespace CS2RuntimeAssetAuditor.Advisor
{
    /// <summary>
    /// The level dropdown of a detailed graphics-quality group (Options > Graphics, advanced). Like the overall preset
    /// the game exposes it through QualitySetting.GetLevel/SetLevel, not a property (1.6.2f1 QualitySetting&lt;T&gt;
    /// .AddToPageData). GetLevel names a level only when every value of the group equals that level's defaults, so
    /// applying the previous level restores the group exactly; a Custom group has no level that restores it and is
    /// never offered. Level.Disabled (the feature switched off, for example no shadows) is not offered either.
    /// </summary>
    internal static class QualityGroupSettings
    {
        private static readonly IReadOnlyDictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            [QualityGroupRules.Shadows.SettingTypeName] = typeof(ShadowsQualitySettings),
            [QualityGroupRules.LevelOfDetail.SettingTypeName] = typeof(LevelOfDetailQualitySettings),
            [QualityGroupRules.DynamicResolution.SettingTypeName] = typeof(DynamicResolutionScaleSettings),
            [QualityGroupRules.Texture.SettingTypeName] = typeof(TextureQualitySettings)
        };

        public static IEnumerable<GameSettingDescriptor> Describe()
        {
            foreach (var rule in QualityGroupRules.All)
            {
                var descriptor = Describe(rule);
                if (descriptor != null)
                    yield return descriptor;
            }
        }

        public static IEnumerable<AutomaticSettingAdapter> CreateAdapters()
        {
            foreach (var rule in QualityGroupRules.All)
            {
                // The gateway keeps this descriptor for the session, so the lock is checked live in validate instead.
                var descriptor = Describe(rule);
                if (descriptor == null)
                    continue;
                descriptor.IsCurrentlyEnabled = true;
                yield return new AutomaticSettingAdapter(descriptor,
                    read: () => With(rule, group => Name(group, group.GetLevel())),
                    directSetter: value => With(rule, group =>
                    {
                        group.SetLevel(Parse(group, value), apply: true);
                        return value;
                    }),
                    customSetter: null,
                    // Checked again at apply time: DLSS or FSR can lock dynamic resolution after the diagnosis.
                    validate: value => With(rule, group => !group.IsOptionFullyDisabled() && Levels(group).Any(level => Name(group, level) == value)),
                    // SetLevel(apply: true) already applies and saves the Options.
                    applyAndSave: () => { });
            }
        }

        private static GameSettingDescriptor? Describe(QualityGroupRule rule)
        {
            var group = Find(rule);
            if (group == null)
                return null;
            var levels = Levels(group);
            if (levels.Count < 2)
                return null;
            return new GameSettingDescriptor
            {
                SettingId = rule.SettingId,
                Category = "graphics",
                DisplayName = rule.DisplayName,
                ValueKind = SettingValueKind.Enumeration,
                CurrentValue = Name(group, group.GetLevel()),
                AllowedValues = levels.Select(level => Name(group, level)).ToArray(),
                SemanticTags = new[] { rule.SemanticTag },
                IsUserFacing = true,
                IsReadable = true,
                IsWritable = true,
                IsCurrentlyVisible = true,
                // The Options screen disables the dropdown in this case (for example dynamic resolution under DLSS).
                IsCurrentlyEnabled = !group.IsOptionFullyDisabled(),
                HasSafeReversibleWritePath = true,
                ApplyBehavior = SettingApplyBehavior.ConfirmationRequired,
                CapabilityState = SettingCapabilityState.Available
            };
        }

        private static QualitySetting? Find(QualityGroupRule rule)
            => Types.TryGetValue(rule.SettingTypeName, out var type) ? SharedSettings.instance?.graphics?.GetQualitySetting(type) : null;

        private static T With<T>(QualityGroupRule rule, Func<QualitySetting, T> action)
            => action(Find(rule) ?? throw new InvalidOperationException(rule.SettingTypeName + " is unavailable."));

        // Levels in ascending quality order (the Level enum order), without Custom and without Disabled.
        private static IReadOnlyList<QualitySetting.Level> Levels(QualitySetting group) =>
            group.EnumerateAvailableLevels()
                .Where(level => level != QualitySetting.Level.Custom && level != QualitySetting.Level.Disabled)
                .Distinct()
                .OrderBy(level => (int)level)
                .ToArray();

        // The Options screen's names: dynamic resolution calls its levels Constant, Automatic and Disabled.
        private static string Name(QualitySetting group, QualitySetting.Level level) => group.GetMockName(level);

        private static QualitySetting.Level Parse(QualitySetting group, string value)
        {
            foreach (var level in Levels(group))
                if (Name(group, level) == value)
                    return level;
            throw new ArgumentException("Unknown quality level: " + value, nameof(value));
        }
    }
}
