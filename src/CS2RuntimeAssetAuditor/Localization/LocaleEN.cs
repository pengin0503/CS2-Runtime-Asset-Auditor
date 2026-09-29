using System.Collections.Generic;
using Colossal;

namespace CS2RuntimeAssetAuditor.Localization
{
    public sealed class LocaleEN : IDictionarySource
    {
        private readonly Setting _setting;

        public LocaleEN(Setting setting)
        {
            _setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { _setting.GetSettingsLocaleID(), "CS2 Runtime Asset Auditor" },
                { _setting.GetOptionTabLocaleID(Setting.MainTab), "General" },
                { _setting.GetOptionGroupLocaleID(Setting.ScanningGroup), "Scanning" },
                { _setting.GetOptionGroupLocaleID(Setting.AnalysisGroup), "Analysis" },
                { _setting.GetOptionGroupLocaleID(Setting.DisplayGroup), "Display" },
                { _setting.GetOptionGroupLocaleID(Setting.AdvancedGroup), "Advanced" },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.CollectSubordinateObjects)), "Collect subordinate objects" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.CollectSubordinateObjects)), "Include supported subordinate objects in the next census. Disabled collection is reported as not scanned, not zero." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.CollectNetworkEdges)), "Collect network edges" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.CollectNetworkEdges)), "Include supported network-edge evidence in the next census." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.FrameBudgetMsOption)), "Managed frame budget (ms)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.FrameBudgetMsOption)), "Maximum managed work budget used by bounded audit slices each frame." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.ProgressUpdateMs)), "Progress update interval (ms)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ProgressUpdateMs)), "Minimum interval between progress-only UI snapshot updates while a scan is active." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.RefreshCatalogAtScanStart)), "Refresh catalog at scan start" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.RefreshCatalogAtScanStart)), "Refresh the prefab catalog before the next Asset Audit starts." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.EnableHeuristicFindings)), "Heuristic findings" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.EnableHeuristicFindings)), "Enable versioned, evidence-based heuristic findings in Asset Audit results." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.EnablePeerOutliers)), "Peer-outlier analysis" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.EnablePeerOutliers)), "Flag Prefabs whose LOD0 vertex count or estimated texture payload is far above comparable Prefabs of the same type (at least 5 peers, above the peer P95 and 2x the median). Applies to the next Asset Audit." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.ComparisonPopulationOption)), "Comparison population" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ComparisonPopulationOption)), "Reference population for peer-outlier analysis, always within the same Prefab type. Applies to the next Asset Audit." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.ShowNoticeFindings)), "Show Notice findings" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ShowNoticeFindings)), "Include informational Notice findings in the Warnings view." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.UiScalePercent)), "Diagnostic panel scale (%)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.UiScalePercent)), "Scale the in-game diagnostic panel from 75% to 150%." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.PageSize)), "Asset page size" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.PageSize)), "Number of assets requested per bounded UI page." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.MetadataCacheLimit)), "Metadata cache limit" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.MetadataCacheLimit)), "Maximum number of surface metadata entries (and the texture assets they reference) kept in memory during an Asset Audit. Lower values use less memory but may read shared surfaces more than once." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.DeepInspectionLimit)), "Deep Inspection limit" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.DeepInspectionLimit)), "Maximum number of render assets that keep Deep Inspection results at the same time; the oldest result is dropped first." },

                { _setting.GetEnumValueLocaleID(Setting.ComparisonPopulationChoice.SameCategory), "Same category" },
                { _setting.GetEnumValueLocaleID(Setting.ComparisonPopulationChoice.BuiltinDlc), "Vanilla / DLC" },
                { _setting.GetEnumValueLocaleID(Setting.ComparisonPopulationChoice.Custom), "Custom assets" },
                { _setting.GetEnumValueLocaleID(Setting.ComparisonPopulationChoice.SameSourcePack), "Same source pack" },
                { _setting.GetOptionGroupLocaleID(Setting.MonitoringGroup), "Monitoring" },
                { _setting.GetOptionGroupLocaleID(Setting.CaptureGroup), "Capture" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.EnableMonitoring)), "Enable monitoring" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.EnableMonitoring)), "Collect lightweight runtime metrics and allow Deep Capture." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.EnableAutomaticCapture)), "Enable automatic capture" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.EnableAutomaticCapture)), "Start Deep Capture after a sustained simulation slowdown." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.UiRefreshMilliseconds)), "UI refresh interval (ms)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.UiRefreshMilliseconds)), "Set the interval between full panel updates." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.EfficiencyThresholdPercent)), "Capture efficiency threshold (%)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.EfficiencyThresholdPercent)), "Trigger automatic capture below this simulation efficiency." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.LowEfficiencySustainSeconds)), "Low efficiency duration (s)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.LowEfficiencySustainSeconds)), "Required slowdown duration before automatic capture." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.PrebufferSeconds)), "Prebuffer duration (s)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.PrebufferSeconds)), "Keep recent monitoring data before Deep Capture." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.DeepCaptureSeconds)), "Deep Capture duration (s)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.DeepCaptureSeconds)), "Record extended profiler markers for this duration." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.PostbufferSeconds)), "Postbuffer duration (s)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.PostbufferSeconds)), "Continue observing metrics after Deep Capture." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.CooldownSeconds)), "Capture cooldown (s)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.CooldownSeconds)), "Delay the next automatic capture." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.SamplingIntervalMilliseconds)), "Sampling interval (ms)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.SamplingIntervalMilliseconds)), "Set the normal monitoring sampling interval." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.MaxConcurrentMarkers)), "Concurrent profiler markers" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.MaxConcurrentMarkers)), "Limit the number of markers enabled together." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.ProfilerOverheadLimitPercent)), "Profiler overhead limit (%)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ProfilerOverheadLimitPercent)), "Reduce sampling work when profiler overhead remains high." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.MaxCompletedCaptures)), "Stored captures" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.MaxCompletedCaptures)), "Maximum number of completed captures retained." }
            };
        }

        public void Unload()
        {
        }
    }
}
