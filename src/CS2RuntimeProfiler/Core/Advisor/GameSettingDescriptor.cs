using System.Collections.Generic;

namespace CS2RuntimeProfiler.Core.Advisor
{
    public enum SettingValueKind { Boolean, Integer, Float, String, Enumeration, Keybinding, Unsupported }
    public enum SettingApplyBehavior { Immediate, ApplyRequired, ConfirmationRequired, RestartRequired, ReadOnlyForAdvisor }
    public enum SettingCapabilityState { Available, ReadOnlyForAdvisor }

    // A snapshot of a standard Options entry. No game objects or reflection handles cross this boundary.
    public sealed class GameSettingDescriptor
    {
        public string SettingId { get; set; } = "";
        public string Category { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public SettingValueKind ValueKind { get; set; }
        public string CurrentValue { get; set; } = "";
        public IReadOnlyList<string> AllowedValues { get; set; } = new string[0];
        // Only tags backed by a verified standard Options control may drive recommendations.
        public IReadOnlyList<string> SemanticTags { get; set; } = new string[0];
        public double? Minimum { get; set; }
        public double? Maximum { get; set; }
        public bool IsUserFacing { get; set; }
        public bool IsReadable { get; set; }
        public bool IsWritable { get; set; }
        public bool? IsCurrentlyVisible { get; set; }
        public bool? IsCurrentlyEnabled { get; set; }
        public bool HasSafeReversibleWritePath { get; set; }
        public bool RequiresRestart { get; set; }
        public SettingApplyBehavior ApplyBehavior { get; set; }
        public SettingCapabilityState CapabilityState { get; set; }

        public GameSettingDescriptor AsReadOnlyForAdvisor()
        {
            var copy = (GameSettingDescriptor)MemberwiseClone();
            copy.IsWritable = false;
            copy.HasSafeReversibleWritePath = false;
            copy.CapabilityState = SettingCapabilityState.ReadOnlyForAdvisor;
            copy.ApplyBehavior = SettingApplyBehavior.ReadOnlyForAdvisor;
            return copy;
        }
    }
}
