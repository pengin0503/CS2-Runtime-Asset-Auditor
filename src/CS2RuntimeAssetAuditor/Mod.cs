using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using CS2RuntimeAssetAuditor.Collectors;
using CS2RuntimeAssetAuditor.Assets.GameIntegration;
using CS2RuntimeAssetAuditor.Assets.UI;
using CS2RuntimeAssetAuditor.Advisor;
using CS2RuntimeAssetAuditor.Coordination;
using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.Lifecycle;
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
        private static readonly DiagnosticSessionRegistry _sessions = new DiagnosticSessionRegistry();
        private static readonly DiagnosticWorkCoordinator _workCoordinator = new DiagnosticWorkCoordinator();

        // A city session exists only between a gameplay load completing and the next load starting.
        public static DiagnosticSessionRegistry Sessions => _sessions;
        public static DiagnosticSessionContext SessionContext => _sessions.Current;
        public static DiagnosticWorkCoordinator WorkCoordinator => _workCoordinator;

        /// <summary>
        /// Records a handled failure with its exception so diagnostic codes shown in the UI can be traced in the
        /// log. Logging must never turn a handled failure into an unhandled one.
        /// </summary>
        public static void ReportFailure(string context, System.Exception exception)
        {
            try { Log.Error(exception, context); }
            catch { }
        }

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
                localizationManager.AddSource("en-US", new LocaleEN(Settings));
            }

            // Key bindings must be registered before the settings load so the saved binding is applied to the action.
            try { Settings.RegisterKeyBindings(); }
            catch (System.Exception ex) { ReportFailure("Registering the panel key binding failed; the launcher still opens the panel.", ex); }

            AssetDatabase.global.LoadSettings(Id, Settings, new Setting(this));
            Settings.RegisterInOptionsUI();

            updateSystem.UpdateAt<DiagnosticSessionSystem>(SystemUpdatePhase.MainLoop);
            updateSystem.UpdateAt<AssetAuditSystem>(SystemUpdatePhase.MainLoop);
            updateSystem.UpdateAt<GlobalMetricsCollector>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<DomainMetricsSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<CaptureRuntimeSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<AdvisorSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<ProfilerUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<AssetAuditUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<AssetAuditSettingsSyncSystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));
            ProfilerReportBuilder.RuntimeMetadataProvider = null;
            ProfilerReportBuilder.CaptureConfigurationProvider = null;
            Settings?.UnregisterInOptionsUI();
            Settings = null;
            _sessions.Close();
            _workCoordinator.Complete(DiagnosticWorkKind.RuntimeDeepCapture);
            _workCoordinator.Complete(DiagnosticWorkKind.AssetHeavyScan);
        }
    }
}
