using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Advisor.Settings;
using CS2RuntimeAssetAuditor.Core.Advisor;
using Game.Settings;

namespace CS2RuntimeAssetAuditor.Advisor
{
    /// <summary>
    /// The overall graphics quality preset of the Options screen. The game exposes it through
    /// QualitySetting.GetLevel/SetLevel (a manual Options property), not through a public property, so the reflective
    /// catalog cannot discover it. Only presets are offered: a Custom level has no preset that restores it, so it is
    /// never a recommendation source, and a change between presets is fully reversible by applying the old preset.
    /// Applying a preset overwrites every detailed graphics option, so it always requires confirmation.
    /// </summary>
    internal static class GraphicsQualityPresetSetting
    {
        public const string SettingId = "Game.Settings.GraphicsSettings::QualityPreset";
        public const string SemanticTag = "rendering.ordered-quality";

        public static GameSettingDescriptor? Describe()
        {
            var graphics = SharedSettings.instance?.graphics;
            if (graphics == null)
                return null;
            var presets = Presets(graphics);
            if (presets.Count < 2)
                return null;
            return new GameSettingDescriptor
            {
                SettingId = SettingId,
                Category = "graphics",
                DisplayName = "Graphics quality preset",
                ValueKind = SettingValueKind.Enumeration,
                CurrentValue = Name(graphics, graphics.GetLevel()),
                AllowedValues = presets.Select(level => Name(graphics, level)).ToArray(),
                SemanticTags = new[] { SemanticTag },
                IsUserFacing = true,
                IsReadable = true,
                IsWritable = true,
                IsCurrentlyVisible = true,
                IsCurrentlyEnabled = true,
                HasSafeReversibleWritePath = true,
                ApplyBehavior = SettingApplyBehavior.ConfirmationRequired,
                CapabilityState = SettingCapabilityState.Available
            };
        }

        public static AutomaticSettingAdapter? CreateAdapter()
        {
            var descriptor = Describe();
            if (descriptor == null)
                return null;
            return new AutomaticSettingAdapter(descriptor,
                read: () => Graphics(graphics => Name(graphics, graphics.GetLevel())),
                directSetter: value => Graphics(graphics =>
                {
                    graphics.SetLevel(Parse(graphics, value), apply: true);
                    return value;
                }),
                customSetter: null,
                validate: value => Graphics(graphics => Presets(graphics).Any(level => Name(graphics, level) == value)),
                // SetLevel(apply: true) already applies and saves the Options.
                applyAndSave: () => { });
        }

        private static T Graphics<T>(Func<GraphicsSettings, T> action)
        {
            var graphics = SharedSettings.instance?.graphics
                ?? throw new InvalidOperationException("Graphics settings are unavailable.");
            return action(graphics);
        }

        // Presets in ascending quality order (the Level enum order), excluding Custom.
        private static IReadOnlyList<QualitySetting.Level> Presets(GraphicsSettings graphics) =>
            graphics.EnumerateAvailableLevels()
                .Where(level => level != QualitySetting.Level.Custom)
                .Distinct()
                .OrderBy(level => (int)level)
                .ToArray();

        // The Options screen names Level.Disabled "VeryLow" for this setting; use the same names.
        private static string Name(GraphicsSettings graphics, QualitySetting.Level level) => graphics.GetMockName(level);

        private static QualitySetting.Level Parse(GraphicsSettings graphics, string value)
        {
            foreach (var level in Presets(graphics))
                if (Name(graphics, level) == value)
                    return level;
            throw new ArgumentException("Unknown graphics quality preset: " + value, nameof(value));
        }
    }
}
