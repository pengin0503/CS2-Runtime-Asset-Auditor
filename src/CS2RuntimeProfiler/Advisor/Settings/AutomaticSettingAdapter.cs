using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CS2RuntimeProfiler.Core.Advisor;

namespace CS2RuntimeProfiler.Advisor.Settings
{
    public sealed class AutomaticSettingAdapter
    {
        private readonly Func<string> _read;
        private readonly Action<string> _directSetter;
        private readonly Action<string> _customSetter;
        private readonly Func<string, bool> _validate;
        private readonly Action _applyAndSave;

        public AutomaticSettingAdapter(GameSettingDescriptor descriptor, Func<string> read,
            Action<string> directSetter, Action<string> customSetter, Func<string, bool> validate, Action applyAndSave)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            _read = read ?? throw new ArgumentNullException(nameof(read));
            _directSetter = directSetter ?? throw new ArgumentNullException(nameof(directSetter));
            _customSetter = customSetter;
            _validate = validate ?? throw new ArgumentNullException(nameof(validate));
            _applyAndSave = applyAndSave ?? throw new ArgumentNullException(nameof(applyAndSave));
        }

        public GameSettingDescriptor Descriptor { get; }
        public string Read() => _read();
        public bool Validate(string value) => value != null && _validate(value);
        public void WriteAndSave(string value)
        {
            if (_customSetter != null) _customSetter(value);
            else _directSetter(value);
            _applyAndSave();
        }

        // Only standard built-in roots and public, unconditioned Options controls reach this factory.
        internal static AutomaticSettingAdapter FromStandardRoot(object root, PropertyInfo property,
            GameSettingDescriptor descriptor)
        {
            if (root?.GetType().FullName?.StartsWith("Game.Settings.", StringComparison.Ordinal) != true
                || property?.GetMethod?.IsPublic != true || property.SetMethod?.IsPublic != true
                || descriptor?.IsUserFacing != true || descriptor.IsWritable != true
                || descriptor.IsCurrentlyVisible != true || descriptor.IsCurrentlyEnabled != true
                || property.GetCustomAttributesData().Any(a => a.AttributeType.Name == "SettingsUISetterAttribute"))
                return null;
            var apply = root.GetType().GetMethod("ApplyAndSave", BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (apply == null) return null;
            var type = property.PropertyType;
            var permitted = type == typeof(bool) || type.IsEnum
                || ((type == typeof(int) || type == typeof(float) || type == typeof(double))
                    && descriptor.Minimum.HasValue && descriptor.Maximum.HasValue);
            if (!permitted) return null;
            return new AutomaticSettingAdapter(descriptor,
                () => Convert.ToString(property.GetValue(root), CultureInfo.InvariantCulture) ?? "",
                value => property.SetValue(root, Parse(type, value)), null,
                value => IsValid(type, value, descriptor), () => apply.Invoke(root, null));
        }

        private static object Parse(Type type, string value)
        {
            if (type == typeof(bool)) return bool.Parse(value);
            if (type.IsEnum) return Enum.Parse(type, value, false);
            if (type == typeof(int)) return int.Parse(value, CultureInfo.InvariantCulture);
            if (type == typeof(float)) return float.Parse(value, CultureInfo.InvariantCulture);
            return double.Parse(value, CultureInfo.InvariantCulture);
        }

        private static bool IsValid(Type type, string value, GameSettingDescriptor descriptor)
        {
            if (type == typeof(bool)) return bool.TryParse(value, out _);
            if (type.IsEnum) return descriptor.AllowedValues.Contains(value, StringComparer.Ordinal);
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return false;
            return !double.IsInfinity(number) && !double.IsNaN(number)
                && number >= descriptor.Minimum && number <= descriptor.Maximum
                && (type != typeof(int) || int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
        }
    }
}
