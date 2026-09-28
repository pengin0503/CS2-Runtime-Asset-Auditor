using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CS2RuntimeProfiler.Core.Advisor;

namespace CS2RuntimeProfiler.Advisor.Settings
{
    // Discovery is deliberately read-only. A public setter is evidence of capability, not permission to invoke it.
    public sealed class GameSettingCatalogBuilder : IStandardGameSettingCatalog
    {
        private static readonly string[] RootNames =
        {
            "general", "audio", "gameplay", "radio", "graphics", "editor", "userInterface",
            "input", "userState", "keybinding", "benchmark", "modding"
        };

        private readonly Func<IEnumerable<SettingCategoryRoot>> _roots;
        private readonly SettingUiMetadataReader _reader;

        public GameSettingCatalogBuilder()
            : this(GetBuiltInRoots, new SettingUiMetadataReader()) { }

        public GameSettingCatalogBuilder(Func<IEnumerable<SettingCategoryRoot>> roots, SettingUiMetadataReader reader)
        {
            _roots = roots ?? throw new ArgumentNullException(nameof(roots));
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public IReadOnlyList<GameSettingDescriptor> GetCatalog()
        {
            var entries = new Dictionary<string, GameSettingDescriptor>(StringComparer.Ordinal);
            foreach (var root in _roots())
            {
                if (root == null || root.Instance == null) continue;
                var type = root.Instance.GetType();
                var builtIn = type.FullName?.StartsWith("Game.Settings.", StringComparison.Ordinal) == true;
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    AddValue(root, root.Instance, property, builtIn, false, entries);
                    // Official Options sections can live on the element type of a public collection.
                    // A collection itself is not a reversible value control.
                    if (!builtIn || property.GetMethod?.IsPublic != true ||
                        property.PropertyType == typeof(string) ||
                        !typeof(IEnumerable).IsAssignableFrom(property.PropertyType)) continue;
                    IEnumerable? children;
                    try { children = property.GetValue(root.Instance) as IEnumerable; }
                    catch (Exception) { continue; }
                    if (children == null) continue;
                    try
                    {
                        foreach (var child in children)
                        {
                            if (child == null) continue;
                            var childType = child.GetType();
                            if (childType.FullName?.StartsWith("Game.Settings.", StringComparison.Ordinal) != true ||
                                !childType.GetCustomAttributesData().Any(a => a.AttributeType.Name == "SettingsUISectionAttribute"))
                                continue;
                            foreach (var member in childType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                                if (member.DeclaringType == childType)
                                    AddValue(root, child, member, true, true, entries);
                        }
                    }
                    catch (Exception) { /* A broken category cannot hide other standard Options roots. */ }
                }
            }
            return entries.Values.OrderBy(e => e.Category, StringComparer.Ordinal)
                .ThenBy(e => e.SettingId, StringComparer.Ordinal).ToArray();
        }

        private void AddValue(SettingCategoryRoot root, object owner, PropertyInfo property,
            bool builtIn, bool sectionOnOwner, IDictionary<string, GameSettingDescriptor> entries)
        {
            try
            {
                if (property.GetIndexParameters().Length != 0 || property.GetMethod?.IsPublic != true) return;
                var attributes = property.GetCustomAttributesData();
                var attrs = attributes.Select(a => a.AttributeType.Name).ToArray();
                if (attrs.Contains("SettingsUIButtonAttribute") || attrs.Contains("SettingsUIHiddenAttribute") ||
                    attrs.Contains("SettingsUIDeveloperAttribute")) return;
                // A class-level section proves value controls on that class, but not inherited settings internals.
                var control = attrs.Any(a => a.StartsWith("SettingsUI", StringComparison.Ordinal)
                    && (a.Contains("Section") || a.Contains("Slider") || a.Contains("Dropdown") || a.Contains("Toggle")
                        || a.Contains("Setter") || a.Contains("Text") || a.Contains("Binding")));
                if (builtIn && !control && !sectionOnOwner) return;
                var valueType = property.PropertyType;
                if (valueType != typeof(bool) && valueType != typeof(string) && !valueType.IsEnum
                    && valueType != typeof(int) && valueType != typeof(uint) && valueType != typeof(long)
                    && valueType != typeof(short) && valueType != typeof(float) && valueType != typeof(double)
                    && valueType != typeof(decimal)) return;
                var ownerAttrs = sectionOnOwner
                    ? owner.GetType().GetCustomAttributesData().Select(a => a.AttributeType.Name).ToArray()
                    : Array.Empty<string>();
                var value = property.GetValue(owner);
                var hasHide = attrs.Contains("SettingsUIHideByConditionAttribute") || ownerAttrs.Contains("SettingsUIHideByConditionAttribute");
                var hasDisable = attrs.Contains("SettingsUIDisableByConditionAttribute") || ownerAttrs.Contains("SettingsUIDisableByConditionAttribute");
                var slider = attributes.FirstOrDefault(a => a.AttributeType.Name == "SettingsUISliderAttribute");
                var metadata = new SettingUiMemberMetadata
                {
                    SettingsTypeFullName = owner.GetType().FullName ?? owner.GetType().Name,
                    MemberName = property.Name,
                    Category = root.Category,
                    DisplayName = property.Name,
                    ValueType = valueType,
                    CurrentValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
                    AllowedValues = valueType.IsEnum ? Enum.GetNames(valueType) : Array.Empty<string>(),
                    Minimum = SliderLimit(slider, "min"),
                    Maximum = SliderLimit(slider, "max"),
                    IsStandardSettingsRoot = true,
                    IsPublicMember = true,
                    IsPublicGetter = true,
                    IsPublicSetter = property.SetMethod?.IsPublic == true,
                    HasVerifiedApplyOwner = !sectionOnOwner,
                    IsReadable = true,
                    IsCurrentlyVisible = hasHide ? (bool?)null : true,
                    IsCurrentlyEnabled = hasDisable ? (bool?)null : true,
                    IsPlatformSupported = !attrs.Contains("SettingsUIPlatformAttribute") && !ownerAttrs.Contains("SettingsUIPlatformAttribute"),
                    HasStandardValueControl = control || sectionOnOwner || !builtIn,
                    IsKeybinding = root.Category == "keybinding",
                    HasHideByCondition = hasHide,
                    HasDisableByCondition = hasDisable,
                    RequiresConfirmation = attrs.Contains("SettingsUIConfirmationAttribute") || ownerAttrs.Contains("SettingsUIConfirmationAttribute"),
                    RequiresCustomSetter = attrs.Contains("SettingsUISetterAttribute") || ownerAttrs.Contains("SettingsUISetterAttribute"),
                    HasUnverifiedValueScale = SliderLimit(slider, "scalarMultiplier") is double scale && scale != 1d,
                    RequiresRestart = attrs.Any(a => a.Contains("Restart")) || ownerAttrs.Any(a => a.Contains("Restart"))
                };
                var descriptor = _reader.Read(metadata);
                if (descriptor != null && descriptor.IsUserFacing && builtIn &&
                    descriptor.SettingId == "Game.Settings.GraphicsSettings::depthOfFieldMode" &&
                    valueType.FullName == "Game.Settings.GraphicsSettings+DepthOfFieldMode" &&
                    descriptor.AllowedValues.SequenceEqual(new[] { "Disabled", "Physical", "TiltShift" }, StringComparer.Ordinal))
                    descriptor.SemanticTags = new[] { "rendering.depth-of-field-mode" };
                if (descriptor != null && !entries.ContainsKey(descriptor.SettingId))
                    entries.Add(descriptor.SettingId, descriptor);
            }
            catch (Exception) { /* Fail closed per value control; other Options controls remain readable. */ }
        }

        private static double? SliderLimit(CustomAttributeData? slider, string name)
        {
            var argument = slider?.NamedArguments.FirstOrDefault(a => a.MemberName == name);
            return argument?.TypedValue.Value == null ? (double?)null
                : Convert.ToDouble(argument.Value.TypedValue.Value, CultureInfo.InvariantCulture);
        }

        internal static IEnumerable<SettingCategoryRoot> GetBuiltInRoots()
        {
            var sharedType = Type.GetType("Game.Settings.SharedSettings, Game", false);
            var instance = sharedType?.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (instance == null) yield break;
            foreach (var name in RootNames)
            {
                var property = sharedType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (property?.GetMethod?.IsPublic != true ||
                    property.PropertyType.FullName?.StartsWith("Game.Settings.", StringComparison.Ordinal) != true) continue;
                object value;
                try { value = property.GetValue(instance); }
                catch (Exception) { continue; }
                if (value != null) yield return new SettingCategoryRoot(name, value);
            }
        }
    }
}
