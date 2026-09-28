using System;
using System.Collections.Generic;
using CS2RuntimeProfiler.Core.Advisor;

namespace CS2RuntimeProfiler.Advisor.Settings
{
    // Intermediate evidence gathered from the public Options surface; unknown visibility stays unknown.
    public sealed class SettingUiMemberMetadata
    {
        public string SettingsTypeFullName { get; set; } = "";
        public string MemberName { get; set; } = "";
        public string Category { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public Type ValueType { get; set; } = typeof(object);
        public string CurrentValue { get; set; } = "";
        public IReadOnlyList<string> AllowedValues { get; set; } = new string[0];
        public double? Minimum { get; set; }
        public double? Maximum { get; set; }
        public bool IsStandardSettingsRoot { get; set; }
        public bool IsPublicMember { get; set; }
        public bool IsPublicGetter { get; set; }
        public bool IsPublicSetter { get; set; }
        public bool HasVerifiedApplyOwner { get; set; } = true;
        public bool IsReadable { get; set; }
        public bool? IsCurrentlyVisible { get; set; }
        public bool? IsCurrentlyEnabled { get; set; }
        public bool IsPlatformSupported { get; set; }
        public bool HasStandardValueControl { get; set; }
        public bool IsKeybinding { get; set; }
        public bool IsActionButton { get; set; }
        public bool IsHidden { get; set; }
        public bool IsDeveloperOnly { get; set; }
        public bool HasHideByCondition { get; set; }
        public bool HasDisableByCondition { get; set; }
        public bool RequiresConfirmation { get; set; }
        public bool RequiresRestart { get; set; }
        public bool RequiresCustomSetter { get; set; }
        public bool HasUnverifiedValueScale { get; set; }
    }

    public sealed class SettingUiMetadataReader
    {
        public GameSettingDescriptor Read(SettingUiMemberMetadata metadata)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            if (metadata.IsActionButton || !metadata.IsStandardSettingsRoot || !metadata.IsPublicMember)
                return null;

            var kind = Classify(metadata.ValueType, metadata.IsKeybinding);
            var facing = !metadata.IsHidden && !metadata.IsDeveloperOnly && metadata.IsPlatformSupported
                && metadata.HasStandardValueControl;
            var readable = metadata.IsReadable && metadata.IsPublicGetter;
            var safePath = readable && metadata.IsPublicSetter && metadata.HasVerifiedApplyOwner && !metadata.RequiresCustomSetter
                && !metadata.HasUnverifiedValueScale
                && kind != SettingValueKind.Unsupported;
            var visible = !metadata.HasHideByCondition || metadata.IsCurrentlyVisible == true;
            var enabled = !metadata.HasDisableByCondition || metadata.IsCurrentlyEnabled == true;
            var writable = facing && safePath && visible && enabled;
            return new GameSettingDescriptor
            {
                SettingId = metadata.SettingsTypeFullName + "::" + metadata.MemberName,
                Category = metadata.Category,
                DisplayName = metadata.DisplayName,
                ValueKind = kind,
                CurrentValue = readable ? metadata.CurrentValue : "",
                AllowedValues = metadata.AllowedValues,
                Minimum = metadata.Minimum,
                Maximum = metadata.Maximum,
                IsUserFacing = facing,
                IsReadable = readable,
                IsWritable = writable,
                HasSafeReversibleWritePath = safePath,
                IsCurrentlyVisible = metadata.HasHideByCondition ? metadata.IsCurrentlyVisible : true,
                IsCurrentlyEnabled = metadata.HasDisableByCondition ? metadata.IsCurrentlyEnabled : true,
                RequiresRestart = metadata.RequiresRestart,
                ApplyBehavior = !writable ? SettingApplyBehavior.ReadOnlyForAdvisor
                    : metadata.RequiresConfirmation ? SettingApplyBehavior.ConfirmationRequired
                    : metadata.RequiresRestart ? SettingApplyBehavior.RestartRequired
                    : SettingApplyBehavior.ApplyRequired,
                CapabilityState = writable ? SettingCapabilityState.Available : SettingCapabilityState.ReadOnlyForAdvisor
            };
        }

        private static SettingValueKind Classify(Type type, bool keybinding)
        {
            if (keybinding) return SettingValueKind.Keybinding;
            if (type == typeof(bool)) return SettingValueKind.Boolean;
            if (type.IsEnum) return SettingValueKind.Enumeration;
            if (type == typeof(string)) return SettingValueKind.String;
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return SettingValueKind.Float;
            if (type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(short)) return SettingValueKind.Integer;
            return SettingValueKind.Unsupported;
        }
    }
}
