using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Advisor.Settings;
using CS2RuntimeAssetAuditor.Core.Advisor;

namespace CS2RuntimeAssetAuditor.Advisor
{
    /// <summary>
    /// The settings the Advisor reads and changes: the reflective standard Options catalog plus the graphics quality
    /// preset, which the game exposes through methods rather than a property.
    /// </summary>
    internal sealed class AdvisorSettingCatalog : IStandardGameSettingCatalog
    {
        private readonly GameSettingCatalogBuilder _standard = new GameSettingCatalogBuilder();

        public IReadOnlyList<GameSettingDescriptor> GetCatalog()
        {
            var entries = _standard.GetCatalog().ToList();
            try
            {
                var preset = GraphicsQualityPresetSetting.Describe();
                if (preset != null)
                    entries.Add(preset);
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Reading the graphics quality preset failed", ex);
            }
            return entries;
        }

        public static IGameSettingGateway CreateGateway()
        {
            var adapters = GameSettingGateway.BuildStandardAdapters().ToList();
            try
            {
                var preset = GraphicsQualityPresetSetting.CreateAdapter();
                if (preset != null)
                    adapters.Add(preset);
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("The graphics quality preset cannot be changed by the Advisor", ex);
            }
            return new GameSettingGateway(adapters, new AdvisorSettingCatalog());
        }
    }
}
