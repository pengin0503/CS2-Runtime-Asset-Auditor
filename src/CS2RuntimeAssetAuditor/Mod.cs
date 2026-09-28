using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using CS2RuntimeAssetAuditor.Collectors;
using CS2RuntimeAssetAuditor.Advisor;
using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.Localization;
using CS2RuntimeAssetAuditor.Profiling;
using CS2RuntimeAssetAuditor.UI;
using Game;
using Game.Modding;
using Game.SceneFlow;

namespace CS2RuntimeAssetAuditor
{
    public sealed class Mod : IMod
    {
        public const string Id = "CS2RuntimeAssetAuditor";
        public static readonly ILog Log = LogManager.GetLogger($"{nameof(CS2RuntimeAssetAuditor)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        public static Setting Settings { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            var version = typeof(Mod).Assembly.GetName().Version?.ToString() ?? "unknown";
            Log.Info($"{nameof(OnLoad)} version={version} build={BuildIdentityProvider.Current}");

            Settings = new Setting(this);
            ProfilerReportBuilder.RuntimeMetadataProvider = RuntimeReportMetadataProvider.Capture;
            ProfilerReportBuilder.CaptureConfigurationProvider = RuntimeCaptureConfigurationProvider.Capture;

            var localizationManager = GameManager.instance?.localizationManager;
            if (localizationManager != null)
            {
                localizationManager.AddSource("ja-JP", new LocaleJA(Settings));
                // Keep the options readable even when the game falls back to its base locale.
                // This mod intentionally presents its user-facing interface in Japanese.
                localizationManager.AddSource("en-US", new LocaleJA(Settings));
            }

            AssetDatabase.global.LoadSettings(Id, Settings, new Setting(this));
            Settings.RegisterInOptionsUI();

            updateSystem.UpdateAt<GlobalMetricsCollector>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<DomainMetricsSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<CaptureRuntimeSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<AdvisorSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<ProfilerUISystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));
            ProfilerReportBuilder.RuntimeMetadataProvider = null;
            ProfilerReportBuilder.CaptureConfigurationProvider = null;
            Settings?.UnregisterInOptionsUI();
            Settings = null;
        }
    }
}
