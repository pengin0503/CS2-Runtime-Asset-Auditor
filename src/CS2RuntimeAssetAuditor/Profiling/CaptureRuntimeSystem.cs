using System;
using System.Collections.Generic;
using System.Reflection;
using CS2RuntimeAssetAuditor.Collectors;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Export;
using Game;

namespace CS2RuntimeAssetAuditor.Profiling
{
    /// <summary>
    /// Drives DeepCaptureController only when a new global sample is available.
    /// Deep Capture owns a dedicated recorder manager so normal-monitoring recorders remain continuous.
    /// </summary>
    public partial class CaptureRuntimeSystem : GameSystemBase
    {
        private const double DefaultPrebufferSeconds = 5d;
        private readonly MonitoringLifecycleGate _monitoringGate = new MonitoringLifecycleGate(initiallyEnabled: true);
        private readonly Dictionary<CaptureSession, SystemTimingSnapshot> _managedTimingByCapture =
            new Dictionary<CaptureSession, SystemTimingSnapshot>();
        private GlobalMetricsCollector _global;
        private DomainMetricsSystem _domains;
        private RecorderManager _deepRecorders;
        private DeepCaptureStateMachine _stateMachine;
        private DeepCaptureController _controller;
        private ProfilerOverheadTracker _overhead;
        private ManagedSystemTimingCaptureLifecycle _managedTimingLifecycle;
        private SystemCatalogCache _systemCatalog;
        private string _managedInstrumentationUnavailableReason;
        private double _lastObservedTimestamp = double.NegativeInfinity;
        private double _lastOverheadShare;

        private IReadOnlyList<SystemDescriptor> Systems => _systemCatalog?.Snapshot ?? Array.Empty<SystemDescriptor>();

        public CaptureState State => _controller?.State ?? CaptureState.Monitoring;
        public CaptureSession CurrentSession => _controller?.CurrentSession;
        public IReadOnlyList<CaptureSession> CompletedSessions => _controller?.CompletedSessions ?? Array.Empty<CaptureSession>();
        public double LastOverheadShare => _lastOverheadShare;
        public int CurrentBatchSize => _controller?.CurrentBatchSize ?? 0;
        public int SamplingStride => _controller?.SamplingStride ?? 1;
        public int DiscoveredMarkerCount => _deepRecorders?.Descriptors?.Count ?? 0;

        protected override void OnCreate()
        {
            base.OnCreate();
            _global = World.GetOrCreateSystemManaged<GlobalMetricsCollector>();
            _domains = World.GetOrCreateSystemManaged<DomainMetricsSystem>();
            _deepRecorders = new RecorderManager(new UnityRecorderBackend());
            _overhead = new ProfilerOverheadTracker();
            _stateMachine = DeepCaptureStateMachine.CreateDefault();
            _controller = new DeepCaptureController(
                _deepRecorders,
                _stateMachine,
                maxConcurrent: 150,
                overheadCeiling: 0.08);
            _controller.Initialize();
            ApplyRuntimeSettings();

            _systemCatalog = new SystemCatalogCache(() => new ProfilerCatalog(world: World).Discover());
            if (!_systemCatalog.TryRefresh(out var catalogError))
            {
                Mod.Log.Info(
                    "Initial system catalog discovery failed; per-system timing will use the last known good catalog: "
                    + (catalogError ?? "unknown reason"));
            }

            _managedTimingLifecycle = new ManagedSystemTimingCaptureLifecycle(
                new ManagedSystemTimingInstrumentationAdapter(),
                new ManagedSystemTimingBridgeAdapter());

            _controller.CaptureCompleted += HandleCaptureCompleted;
        }

        protected override void OnUpdate()
        {
            ApplyRuntimeSettings();

            var monitoringEnabled = Mod.Settings == null || Mod.Settings.EnableMonitoring;
            var transition = _monitoringGate.Observe(monitoringEnabled);

            if (transition == MonitoringTransition.Disabled)
            {
                FinishManagedTimingForCapture(_controller?.CurrentSession);
                _controller?.InterruptActiveCapture(
                    "Monitoring was disabled; the active capture was finalized early and recorder activity was stopped.");
                _managedTimingLifecycle?.Abort();
            }

            if (!monitoringEnabled)
                return;

            var latest = _global?.Latest;
            if (latest == null || latest.TimestampSeconds <= _lastObservedTimestamp)
                return;

            var prebuffer = _global.GetRecentHistory(GetPrebufferSeconds());
            var captureConfiguration = RuntimeCaptureConfigurationProvider.Capture();
            _overhead.Measure(latest.TimestampSeconds, () =>
            {
                var beforeSession = _controller.CurrentSession;
                var beforeState = _controller.State;
                _controller.Observe(
                    latest.TimestampSeconds,
                    latest,
                    RuntimeGameStateProbe.IsAutomaticCaptureAllowed(_global?.SimulationRuntimeSystem),
                    prebuffer);
                var afterSession = _controller.CurrentSession;
                var afterState = _controller.State;

                if (beforeSession == null && afterSession != null && afterState == CaptureState.DeepCapture)
                {
                    RefreshSystemCatalogForCapture(afterSession);
                    StartManagedTimingForCapture(afterSession);
                }

                if (afterSession != null
                    && beforeState == CaptureState.DeepCapture
                    && afterState != CaptureState.DeepCapture)
                {
                    FinishManagedTimingForCapture(afterSession);
                }

                _controller.CurrentSession?.SetConfiguration(captureConfiguration);
                _controller.CurrentSession?.SetRuntimeSnapshots(
                    _domains?.Pathfinding?.Latest,
                    _domains?.Entities?.Latest);
            });

            var samplingPeriod = Math.Max(0.001d, _global?.SamplingPeriodSeconds ?? GlobalMetricsCollector.DefaultSamplingPeriodSeconds);
            _lastOverheadShare = Math.Max(
                0d,
                _overhead.LastMilliseconds / (samplingPeriod * 1000d));
            _controller.CurrentSession?.ObserveProfilerOverheadShare(_lastOverheadShare);
            _controller.ReportProfilerOverheadShare(_lastOverheadShare);

            _lastObservedTimestamp = latest.TimestampSeconds;
        }

        public void RequestManualCapture()
        {
            if (Mod.Settings != null && !Mod.Settings.EnableMonitoring)
                return;

            ApplyRuntimeSettings();
            var captureConfiguration = RuntimeCaptureConfigurationProvider.Capture();
            var now = _global?.CurrentTimestampSeconds ?? Math.Max(0d, _lastObservedTimestamp);
            var before = _controller?.CurrentSession;
            _controller?.RequestManualCapture(now, _global?.GetRecentHistory(GetPrebufferSeconds()));
            if (before == null && _controller?.CurrentSession != null && _controller.State == CaptureState.DeepCapture)
            {
                RefreshSystemCatalogForCapture(_controller.CurrentSession);
                StartManagedTimingForCapture(_controller.CurrentSession);
            }
            _controller?.CurrentSession?.SetConfiguration(captureConfiguration);
            _controller?.CurrentSession?.SetRuntimeSnapshots(
                _domains?.Pathfinding?.Latest,
                _domains?.Entities?.Latest);
        }

        protected override void OnDestroy()
        {
            if (_controller != null)
                _controller.CaptureCompleted -= HandleCaptureCompleted;
            _managedTimingLifecycle?.Abort();
            _managedTimingLifecycle = null;
            _managedTimingByCapture.Clear();
            _controller?.Dispose();
            _controller = null;
            _deepRecorders = null;
            _systemCatalog = null;
            base.OnDestroy();
        }

        private double GetPrebufferSeconds()
        {
            return Mod.Settings?.ResolvedPrebufferSeconds ?? DefaultPrebufferSeconds;
        }

        private void ApplyRuntimeSettings()
        {
            var settings = Mod.Settings;
            if (settings == null || _stateMachine == null || _controller == null)
                return;

            _stateMachine.Configure(
                settings.ResolvedEfficiencyThreshold,
                settings.ResolvedLowEfficiencySustainSeconds,
                settings.ResolvedDeepCaptureSeconds,
                settings.ResolvedPostbufferSeconds,
                settings.ResolvedCooldownSeconds,
                settings.EnableAutomaticCapture);

            _controller.UpdateConfiguration(
                settings.ResolvedMaxConcurrentMarkers,
                settings.ResolvedProfilerOverheadLimit,
                settings.ResolvedMaxCompletedCaptures);
        }

        private void RefreshSystemCatalogForCapture(CaptureSession capture)
        {
            if (_systemCatalog == null || _systemCatalog.TryRefresh(out var error))
                return;

            var warning =
                "System catalog refresh failed at capture start; using the last known good catalog: "
                + (error ?? "unknown reason");
            capture?.AddWarning(warning);
            Mod.Log.Info(warning);
        }

        private void StartManagedTimingForCapture(CaptureSession capture)
        {
            if (_managedTimingLifecycle == null)
                return;

            if (_managedTimingLifecycle.TryStart(out var reason))
            {
                _managedInstrumentationUnavailableReason = null;
                return;
            }

            _managedInstrumentationUnavailableReason = reason ?? "unknown reason";
            Mod.Log.Info(
                "Managed SystemBase timing fallback unavailable for this capture: "
                + _managedInstrumentationUnavailableReason);
        }

        private void FinishManagedTimingForCapture(CaptureSession capture)
        {
            if (capture == null || _managedTimingLifecycle?.IsActive != true)
                return;

            try
            {
                _managedTimingByCapture[capture] = _managedTimingLifecycle.Finish(Systems);
            }
            catch (Exception ex)
            {
                capture.AddWarning(
                    "Managed SystemBase timing finalization failed; marker timing remains available.");
                Mod.Log.Error(ex, "Managed SystemBase timing finalization failed");
            }
        }

        private void HandleCaptureCompleted(CaptureSession capture)
        {
            if (capture == null)
                return;

            try
            {
                if (!_managedTimingByCapture.TryGetValue(capture, out var managedTiming))
                {
                    FinishManagedTimingForCapture(capture);
                    if (!_managedTimingByCapture.TryGetValue(capture, out managedTiming))
                        managedTiming = new SystemTimingSnapshot();
                }
                _managedTimingByCapture.Remove(capture);

                CaptureSystemTimingFinalizer.Apply(
                    capture,
                    Systems,
                    _deepRecorders?.Descriptors ?? Array.Empty<RecorderDescriptor>(),
                    managedTiming);

                capture.AddManagedTimingFallbackUnavailableWarning(_managedInstrumentationUnavailableReason);
            }
            catch (Exception ex)
            {
                capture.AddWarning(
                    "System timing projection failed for this capture; per-system timing is unavailable.");
                Mod.Log.Error(ex, "System timing projection failed for a completed capture");
            }

            CaptureCompletionDiagnosticsDispatcher.Dispatch(
                capture,
                message => Mod.Log.Info(message),
                TryFlushModLog);
        }

        private static void TryFlushModLog()
        {
            var logger = Mod.Log;
            if (logger == null)
                return;

            var flush = logger.GetType().GetMethod(
                "Flush",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            flush?.Invoke(logger, null);
        }
    }
}
