using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace CS2RuntimeProfiler
{
    [FileLocation(Mod.Id)]
    public sealed class Setting : ModSetting
    {
        internal const string MainTab = "Main";
        internal const string MonitoringGroup = "Monitoring";
        internal const string DisplayGroup = "Display";
        internal const string CaptureGroup = "Capture";
        internal const string AdvancedGroup = "Advanced";

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
