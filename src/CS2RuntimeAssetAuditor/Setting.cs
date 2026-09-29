using System;
using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;

namespace CS2RuntimeAssetAuditor
{
    [FileLocation(Mod.Id)]
    [SettingsUITabOrder(MainTab)]
    [SettingsUIGroupOrder(MonitoringGroup, CaptureGroup, ScanningGroup, AnalysisGroup, DisplayGroup, KeyBindingGroup, AdvancedGroup)]
    [SettingsUIKeyboardAction(TogglePanelActionName)]
    public sealed class Setting : ModSetting
    {
        internal const string MainTab = "Main";
        internal const string MonitoringGroup = "Monitoring";
        internal const string DisplayGroup = "Display";
        internal const string CaptureGroup = "Capture";
        internal const string AdvancedGroup = "Advanced";
        internal const string ScanningGroup = "Scanning";
        internal const string AnalysisGroup = "Analysis";
        internal const string KeyBindingGroup = "KeyBinding";

        // Opens or closes the diagnostic panel. It has no default key so the mod never takes a key another
        // mod or the game already uses; the player assigns one in the options.
        internal const string TogglePanelActionName = "TogglePanel";

        public enum ComparisonPopulationChoice { SameCategory, BuiltinDlc, Custom, SameSourcePack }

        public Setting(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        [SettingsUISection(MainTab, MonitoringGroup)]
        public bool EnableMonitoring { get; set; }

        [SettingsUISection(MainTab, MonitoringGroup)]
        public bool EnableAutomaticCapture { get; set; }

        [SettingsUISection(MainTab, DisplayGroup)]
        [SettingsUISlider(min = 75, max = 150, step = 5, scalarMultiplier = 1)]
        public int UiScalePercent { get; set; }

        [SettingsUISection(MainTab, DisplayGroup)]
        [SettingsUISlider(min = 250, max = 2000, step = 250, scalarMultiplier = 1)]
        public int UiRefreshMilliseconds { get; set; }

        [SettingsUISection(MainTab, KeyBindingGroup)]
        [SettingsUIKeyboardBinding(TogglePanelActionName)]
        public ProxyBinding TogglePanelBinding { get; set; }

        [SettingsUISection(MainTab, CaptureGroup)]
        [SettingsUISlider(min = 50, max = 100, step = 5, scalarMultiplier = 1)]
        public int EfficiencyThresholdPercent { get; set; }

        [SettingsUISection(MainTab, CaptureGroup)]
        [SettingsUISlider(min = 1, max = 10, step = 1, scalarMultiplier = 1)]
        public int LowEfficiencySustainSeconds { get; set; }

        [SettingsUISection(MainTab, CaptureGroup)]
        [SettingsUISlider(min = 0, max = 15, step = 1, scalarMultiplier = 1)]
        public int PrebufferSeconds { get; set; }

        [SettingsUISection(MainTab, CaptureGroup)]
        [SettingsUISlider(min = 3, max = 30, step = 1, scalarMultiplier = 1)]
        public int DeepCaptureSeconds { get; set; }

        [SettingsUISection(MainTab, CaptureGroup)]
        [SettingsUISlider(min = 0, max = 15, step = 1, scalarMultiplier = 1)]
        public int PostbufferSeconds { get; set; }

        [SettingsUISection(MainTab, CaptureGroup)]
        [SettingsUISlider(min = 5, max = 120, step = 5, scalarMultiplier = 1)]
        public int CooldownSeconds { get; set; }

        [SettingsUISection(MainTab, AdvancedGroup)]
        [SettingsUIAdvanced]
        [SettingsUISlider(min = 250, max = 2000, step = 250, scalarMultiplier = 1)]
        public int SamplingIntervalMilliseconds { get; set; }

        [SettingsUISection(MainTab, AdvancedGroup)]
        [SettingsUIAdvanced]
        [SettingsUISlider(min = 25, max = 300, step = 25, scalarMultiplier = 1)]
        public int MaxConcurrentMarkers { get; set; }

        [SettingsUISection(MainTab, AdvancedGroup)]
        [SettingsUIAdvanced]
        [SettingsUISlider(min = 1, max = 20, step = 1, scalarMultiplier = 1)]
        public int ProfilerOverheadLimitPercent { get; set; }

        [SettingsUISection(MainTab, AdvancedGroup)]
        [SettingsUIAdvanced]
        [SettingsUISlider(min = 5, max = 50, step = 5, scalarMultiplier = 1)]
        public int MaxCompletedCaptures { get; set; }

        [SettingsUISection(MainTab, ScanningGroup)]
        public bool CollectSubordinateObjects { get; set; }

        [SettingsUISection(MainTab, ScanningGroup)]
        public bool CollectNetworkEdges { get; set; }

        [SettingsUIHidden]
        public double FrameBudgetMs { get; set; }

        [SettingsUISection(MainTab, ScanningGroup)]
        [SettingsUISlider(min = 0.25f, max = 8f, step = 0.25f, scalarMultiplier = 1)]
        public float FrameBudgetMsOption
        {
            get => (float)FrameBudgetMs;
            set => FrameBudgetMs = value;
        }

        [SettingsUISection(MainTab, ScanningGroup)]
        [SettingsUISlider(min = 50, max = 2000, step = 50, scalarMultiplier = 1)]
        public int ProgressUpdateMs { get; set; }

        [SettingsUISection(MainTab, ScanningGroup)]
        public bool RefreshCatalogAtScanStart { get; set; }

        [SettingsUISection(MainTab, AnalysisGroup)]
        public bool EnableHeuristicFindings { get; set; }

        [SettingsUISection(MainTab, AnalysisGroup)]
        public bool EnablePeerOutliers { get; set; }

        [SettingsUIHidden]
        public string ComparisonPopulation { get; set; } = "SameCategory";

        [SettingsUISection(MainTab, AnalysisGroup)]
        public ComparisonPopulationChoice ComparisonPopulationOption
        {
            get => Enum.TryParse(ComparisonPopulation, out ComparisonPopulationChoice choice) ? choice : ComparisonPopulationChoice.SameCategory;
            set => ComparisonPopulation = value.ToString();
        }

        [SettingsUISection(MainTab, AnalysisGroup)]
        public bool ShowNoticeFindings { get; set; }

        [SettingsUISection(MainTab, AdvancedGroup)]
        [SettingsUIAdvanced]
        [SettingsUISlider(min = 25, max = 200, step = 25, scalarMultiplier = 1)]
        public int PageSize { get; set; }

        [SettingsUISection(MainTab, AdvancedGroup)]
        [SettingsUIAdvanced]
        [SettingsUISlider(min = 64, max = 4096, step = 64, scalarMultiplier = 1)]
        public int MetadataCacheLimit { get; set; }

        [SettingsUISection(MainTab, AdvancedGroup)]
        [SettingsUIAdvanced]
        [SettingsUISlider(min = 1, max = 16, step = 1, scalarMultiplier = 1)]
        public int DeepInspectionLimit { get; set; }

        // Both subsystems share the existing panel scale rather than persisting two values.
        [SettingsUIHidden]
        public double UiScale
        {
            get => UiScalePercent / 100.0;
            set => UiScalePercent = Clamp((int)Math.Round(value * 100.0), 75, 150);
        }

        // Panel geometry in screen pixels, written when the user moves or resizes the profiler panel.
        // A non-positive width marks "use the default layout". Hidden from the options UI.
        [SettingsUIHidden]
        public int PanelLeft { get; set; }

        [SettingsUIHidden]
        public int PanelTop { get; set; }

        [SettingsUIHidden]
        public int PanelWidth { get; set; }

        [SettingsUIHidden]
        public int PanelHeight { get; set; }

        internal int ResolvedUiScalePercent => Clamp(UiScalePercent, 75, 150);
        internal double ResolvedUiRefreshPeriodSeconds => Clamp(UiRefreshMilliseconds, 250, 2000) / 1000d;
        internal double ResolvedSamplingPeriodSeconds => Clamp(SamplingIntervalMilliseconds, 250, 2000) / 1000d;
        internal double ResolvedEfficiencyThreshold => Clamp(EfficiencyThresholdPercent, 50, 100) / 100d;
        internal double ResolvedLowEfficiencySustainSeconds => Clamp(LowEfficiencySustainSeconds, 1, 10);
        internal double ResolvedPrebufferSeconds => Clamp(PrebufferSeconds, 0, 15);
        internal double ResolvedDeepCaptureSeconds => Clamp(DeepCaptureSeconds, 3, 30);
        internal double ResolvedPostbufferSeconds => Clamp(PostbufferSeconds, 0, 15);
        internal double ResolvedCooldownSeconds => Clamp(CooldownSeconds, 5, 120);
        internal int ResolvedMaxConcurrentMarkers => Clamp(MaxConcurrentMarkers, 25, 300);
        internal double ResolvedProfilerOverheadLimit => Clamp(ProfilerOverheadLimitPercent, 1, 20) / 100d;
        internal int ResolvedMaxCompletedCaptures => Clamp(MaxCompletedCaptures, 5, 50);

        public override void SetDefaults()
        {
            EnableMonitoring = true;
            EnableAutomaticCapture = true;
            UiScalePercent = 100;
            UiRefreshMilliseconds = 500;
            SamplingIntervalMilliseconds = 500;
            EfficiencyThresholdPercent = 80;
            LowEfficiencySustainSeconds = 2;
            PrebufferSeconds = 5;
            DeepCaptureSeconds = 10;
            PostbufferSeconds = 5;
            CooldownSeconds = 30;
            MaxConcurrentMarkers = 150;
            ProfilerOverheadLimitPercent = 8;
            MaxCompletedCaptures = 20;
            CollectSubordinateObjects = true;
            CollectNetworkEdges = true;
            FrameBudgetMs = 1.0;
            ProgressUpdateMs = 200;
            RefreshCatalogAtScanStart = true;
            EnableHeuristicFindings = true;
            EnablePeerOutliers = true;
            ComparisonPopulation = "SameCategory";
            ShowNoticeFindings = true;
            PageSize = 100;
            MetadataCacheLimit = 512;
            DeepInspectionLimit = 1;
            PanelLeft = 0;
            PanelTop = 0;
            PanelWidth = 0;
            PanelHeight = 0;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
