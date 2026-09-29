using System;
using Game;
using CS2RuntimeAssetAuditor.Lifecycle;

namespace CS2RuntimeAssetAuditor.Assets.UI
{
    // Keeps the live React panel in sync when values are changed through the game's Options menu.
    // The fast path is allocation-free and only performs primitive comparisons once per UI update.
    public sealed partial class AssetAuditSettingsSyncSystem : GameSystemBase
    {
        protected override void OnUpdate()
        {
            var start = ModUpdateCost.Start();
            try { RunUpdate(); }
            finally { ModUpdateCost.Stop(nameof(AssetAuditSettingsSyncSystem), start); }
        }

        private void RunUpdate()
        {
            if (!World.IsCreated)
                return;

            World.GetExistingSystemManaged<AssetAuditUISystem>()?.SynchronizeStoredSettings();
        }
    }

    public sealed partial class AssetAuditUISystem
    {
        internal void SynchronizeStoredSettings()
        {
            var stored = Mod.Settings;
            if (stored == null || StoredSettingsMatch(stored))
                return;

            var next = NormalizeSettings(FromStoredSettings(stored));
            if (SettingsMatch(_uiSettings, next))
                return;

            _uiSettings = next;
            PublishSnapshot();
        }

        private bool StoredSettingsMatch(global::CS2RuntimeAssetAuditor.Setting stored)
        {
            return stored.CollectSubordinateObjects == _uiSettings.CollectSubordinateObjects
                && stored.CollectNetworkEdges == _uiSettings.CollectNetworkEdges
                && NearlyEqual(stored.FrameBudgetMs, _uiSettings.FrameBudgetMs)
                && stored.ProgressUpdateMs == _uiSettings.ProgressUpdateMs
                && stored.RefreshCatalogAtScanStart == _uiSettings.RefreshCatalogAtScanStart
                && stored.EnableHeuristicFindings == _uiSettings.EnableHeuristicFindings
                && stored.EnablePeerOutliers == _uiSettings.EnablePeerOutliers
                && StringComparer.Ordinal.Equals(stored.ComparisonPopulation, _uiSettings.ComparisonPopulation)
                && stored.ShowNoticeFindings == _uiSettings.ShowNoticeFindings
                && stored.PageSize == _uiSettings.PageSize
                && stored.MetadataCacheLimit == _uiSettings.MetadataCacheLimit
                && stored.DeepInspectionLimit == _uiSettings.DeepInspectionLimit
                && NearlyEqual(stored.UiScale, _uiSettings.UiScale);
        }

        private static bool SettingsMatch(UiScanOptions left, UiScanOptions right)
        {
            return left.CollectSubordinateObjects == right.CollectSubordinateObjects
                && left.CollectNetworkEdges == right.CollectNetworkEdges
                && NearlyEqual(left.FrameBudgetMs, right.FrameBudgetMs)
                && left.ProgressUpdateMs == right.ProgressUpdateMs
                && left.RefreshCatalogAtScanStart == right.RefreshCatalogAtScanStart
                && left.EnableHeuristicFindings == right.EnableHeuristicFindings
                && left.EnablePeerOutliers == right.EnablePeerOutliers
                && StringComparer.Ordinal.Equals(left.ComparisonPopulation, right.ComparisonPopulation)
                && left.ShowNoticeFindings == right.ShowNoticeFindings
                && left.PageSize == right.PageSize
                && left.MetadataCacheLimit == right.MetadataCacheLimit
                && left.DeepInspectionLimit == right.DeepInspectionLimit
                && NearlyEqual(left.UiScale, right.UiScale);
        }

        private static bool NearlyEqual(double left, double right) => Math.Abs(left - right) < 0.000001;
    }
}
