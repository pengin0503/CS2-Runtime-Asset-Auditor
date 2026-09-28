using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Core.Advisor;

namespace CS2RuntimeAssetAuditor.Advisor.Settings
{
    public interface IGameSettingGateway
    {
        IReadOnlyList<GameSettingDescriptor> GetCatalog();
        string Read(string settingId);
        SettingApplyResult Apply(string settingId, string value, bool confirmed = false);
        SettingApplyResult Restore(string settingId, string expectedCurrentValue, string originalValue, bool confirmed = false);
    }

    public sealed class SettingApplyResult
    {
        public bool Succeeded { get; set; }
        public string ObservedBefore { get; set; }
        public string Requested { get; set; }
        public string ObservedAfter { get; set; }
        public SettingApplyBehavior ApplyBehavior { get; set; }
        public string FailureReason { get; set; }
    }
}
