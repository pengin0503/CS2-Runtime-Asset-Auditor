using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using CS2RuntimeAssetAuditor.Collectors;
using CS2RuntimeAssetAuditor.Core.DiagnosticLog;
using CS2RuntimeAssetAuditor.Core.Loading;
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
        private static readonly LoadingTraceRecorder _loadingTrace = new LoadingTraceRecorder();
        public static LoadingTraceRecorder LoadingTrace => _loadingTrace;
        public static LoadingMetricsBehaviour LoadingMetrics { get; private set; }
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
            Error(exception, context);
        }

        private static readonly object EventLogLock = new object();
        private static ModEventLogFile? _eventLog;
        private static bool _eventLogUnavailable;

        /// <summary>
        /// Writes a line to the game's mod log and to the mod's own events file
        /// (<c>ModsData/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor-events.log</c>). Logging never throws.
        /// </summary>
        public static void Info(string message)
        {
            try { Log.Info(message); }
            catch { }
            WriteEvent("INFO", message);
        }

        public static void Warn(string message)
        {
            try { Log.Warn(message); }
            catch { }
            WriteEvent("WARN", message);
        }

        public static void Error(System.Exception exception, string message)
        {
            try { Log.Error(exception, message); }
            catch { }
            WriteEvent("ERROR", exception == null ? message : message + " | " + ReportPrivacy.Sanitize(exception.ToString()));
        }

        private static void WriteEvent(string level, string message)
        {
            lock (EventLogLock)
            {
                if (_eventLogUnavailable)
                    return;
                try
                {
                    _eventLog ??= new ModEventLogFile(System.IO.Path.Combine(
                        Colossal.PSI.Environment.EnvPath.kUserDataPath, "ModsData", Id, Id + "-events.log"));
                    _eventLog.Write(System.DateTime.Now, level, message);
                }
                catch (System.Exception ex)
                {
                    // Stop trying for this game run rather than failing on every line; say so once in the game log.
                    _eventLogUnavailable = true;
                    _eventLog?.Dispose();
                    _eventLog = null;
                    try { Log.Error(ex, "The mod's events file could not be written; later lines go only to the game log."); }
                    catch { }
                }
            }
        }

        private static void CloseEventLog()
        {
            lock (EventLogLock)
            {
                _eventLog?.Dispose();
                _eventLog = null;
            }
        }

        public void OnLoad(UpdateSystem updateSystem)
        {
            _loadingTrace.MarkModStarted(System.DateTimeOffset.UtcNow);
            var version = typeof(Mod).Assembly.GetName().Version?.ToString() ?? "unknown";
            Info($"{nameof(OnLoad)} version={version} build={BuildIdentityProvider.Current} {LoggerState}");

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
            LoadingMetrics = LoadingMetricsBehaviour.Install();

            updateSystem.UpdateAt<DiagnosticSessionSystem>(SystemUpdatePhase.MainLoop);
            updateSystem.UpdateAt<AssetAuditSystem>(SystemUpdatePhase.MainLoop);
            updateSystem.UpdateAt<GlobalMetricsCollector>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<DomainMetricsSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<CaptureRuntimeSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<DiagnosticLogSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<AdvisorSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<ProfilerUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<AssetAuditUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<AssetAuditSettingsSyncSystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
            Info($"{nameof(OnDispose)} {LoggerState}");
            if (LoadingMetrics != null)
            {
                LoadingMetrics.enabled = false;
                UnityEngine.Object.Destroy(LoadingMetrics.gameObject);
            }
            LoadingMetrics = null;
            ProfilerReportBuilder.RuntimeMetadataProvider = null;
            ProfilerReportBuilder.CaptureConfigurationProvider = null;
            Settings?.UnregisterInOptionsUI();
            Settings = null;
            _sessions.Close();
            _workCoordinator.Complete(DiagnosticWorkKind.RuntimeDeepCapture);
            _workCoordinator.Complete(DiagnosticWorkKind.AssetHeavyScan);
            CloseEventLog();
        }

        /// <summary>
        /// The game logger's level, written with lifecycle lines: if the game's mod log stops receiving lines again,
        /// the events file shows whether the logger's level changed.
        /// </summary>
        public static string LoggerState
        {
            get
            {
                try { return $"gameLoggerLevel={Log.effectivenessLevel}"; }
                catch { return "gameLoggerLevel=unknown"; }
            }
        }
    }
}
