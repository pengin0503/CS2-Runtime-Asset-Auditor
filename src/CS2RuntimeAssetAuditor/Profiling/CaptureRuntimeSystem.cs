using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using CS2RuntimeAssetAuditor.Collectors;
using CS2RuntimeAssetAuditor.Coordination;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.Lifecycle;
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
        private long _observedSessionGeneration = -1;

        private IReadOnlyList<SystemDescriptor> Systems => _systemCatalog?.Snapshot ?? Array.Empty<SystemDescriptor>();

        public CaptureState State => _controller?.State ?? CaptureState.Monitoring;
        public CaptureSession CurrentSession => _controller?.CurrentSession;
        public IReadOnlyList<CaptureSession> CompletedSessions => _controller?.CompletedSessions ?? Array.Empty<CaptureSession>();
        public double LastOverheadShare => _lastOverheadShare;
        public int CurrentBatchSize => _controller?.CurrentBatchSize ?? 0;
        public int SamplingStride => _controller?.SamplingStride ?? 1;
        public int DiscoveredMarkerCount => _deepRecorders?.DescriptorCount ?? 0;

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
            var markerDiscoveryStart = Stopwatch.GetTimestamp();
            _controller.Initialize();
            Mod.Info(string.Format(
                CultureInfo.InvariantCulture,
                "Profiler marker discovery timing: markers={0} ms={1:0.0}",
                DiscoveredMarkerCount,
                Milliseconds(markerDiscoveryStart, Stopwatch.GetTimestamp())));
            ApplyRuntimeSettings();

            // The system catalog is read only by a capture, and every capture refreshes it when it starts, so it is
            // not discovered here: that enumerated every type of every loaded assembly during game startup (before
            // most systems exist, so its marker names were mostly unresolved) and was replaced before any use.
            _systemCatalog = new SystemCatalogCache(() => new ProfilerCatalog(world: World).Discover());

            _managedTimingLifecycle = new ManagedSystemTimingCaptureLifecycle(
                new ManagedSystemTimingInstrumentationAdapter(),
                new ManagedSystemTimingBridgeAdapter());

            _controller.CaptureCompleted += HandleCaptureCompleted;
        }
        protected override void OnUpdate()
        {
            var start = ModUpdateCost.Start();
            try { RunUpdate(); }
            finally { ModUpdateCost.Stop(nameof(CaptureRuntimeSystem), start); }
        }

        private void RunUpdate()
        {
            ApplyRuntimeSettings();
            ObserveSessionChange();

            var monitoringEnabled = Mod.Settings == null || Mod.Settings.EnableMonitoring;
            var transition = _monitoringGate.Observe(monitoringEnabled);

            if (transition == MonitoringTransition.Disabled)
            {
                FinishManagedTimingForCapture(_controller?.CurrentSession);
                _controller?.InterruptActiveCapture(
                    "Monitoring was disabled; the active capture was finalized early and recorder activity was stopped.",
                    CaptureInterruptionReason.MonitoringDisabled);
                _managedTimingLifecycle?.Abort();
            }

            // Captures belong to a loaded gameplay city; menus, the editor and loading screens are not measured.
            if (!monitoringEnabled || !Mod.Sessions.IsActive)
                return;

            var latest = _global?.Latest;
            if (latest == null || latest.TimestampSeconds <= _lastObservedTimestamp)
                return;

            var prebuffer = _global.GetRecentHistory(GetPrebufferSeconds());
            var captureConfiguration = RuntimeCaptureConfigurationProvider.Capture();
            _overhead.Measure(() =>
            {
                var beforeSession = _controller.CurrentSession;
                var beforeState = _controller.State;
                var beforeFrameRateSkips = _controller.FrameRateSkips;
                _controller.Observe(
                    latest.TimestampSeconds,
                    latest,
                    RuntimeGameStateProbe.IsAutomaticCaptureAllowed(_global?.SimulationRuntimeSystem),
                    prebuffer);
                var afterSession = _controller.CurrentSession;
                var afterState = _controller.State;
                if (_controller.FrameRateSkips != beforeFrameRateSkips)
                    LogFrameRateSkip(latest);

                if (beforeSession == null && afterSession != null && afterState == CaptureState.DeepCapture)
                    BeginCaptureWork(afterSession);

                // Managed timing covers only the deep phase. The heavy-work slot stays held through the
                // post-buffer: its samples belong to this capture, so queued asset scans wait until it completes.
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

        // One line per run of skipped samples, so the log shows why a slowdown did not start a capture.
        private static void LogFrameRateSkip(GlobalMetricsSnapshot sample)
        {
            var interval = sample.FrameInterval;
            Mod.Info(string.Format(
                CultureInfo.InvariantCulture,
                "Automatic capture skipped: the frame rate explains the slowdown. selectedSpeed={0} efficiency={1:0.###} fps={2} frameRateCeiling={3} renderCapShare={4}",
                sample.SelectedSpeed,
                sample.Efficiency,
                Format(interval?.FramesPerSecond),
                Format(interval?.FrameRateEfficiencyCeiling),
                Format(Core.Frames.FrameRateLimit.RenderCapShare(interval))));
        }

        private static string Format(double? value)
            => value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "unavailable";

        public CaptureSession RequestManualCapture()
        {
            if (Mod.Settings != null && !Mod.Settings.EnableMonitoring)
                return null;
            ObserveSessionChange();
            if (!Mod.Sessions.IsActive)
                return null;

            ApplyRuntimeSettings();
            var captureConfiguration = RuntimeCaptureConfigurationProvider.Capture();
            var now = _global?.CurrentTimestampSeconds ?? Math.Max(0d, _lastObservedTimestamp);
            var before = _controller?.CurrentSession;
            _controller?.RequestManualCapture(now, _global?.GetRecentHistory(GetPrebufferSeconds()));
            var created = _controller?.CurrentSession;
            var started = before == null && created != null &&
                !ReferenceEquals(before, created) && _controller.State == CaptureState.DeepCapture;
            if (started)
                BeginCaptureWork(created);
            _controller?.CurrentSession?.SetConfiguration(captureConfiguration);
            _controller?.CurrentSession?.SetRuntimeSnapshots(
                _domains?.Pathfinding?.Latest,
                _domains?.Entities?.Latest);
            return started ? created : null;
        }

        protected override void OnDestroy()
        {
            Mod.WorkCoordinator.Complete(DiagnosticWorkKind.RuntimeDeepCapture);
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

        private void BeginCaptureWork(CaptureSession capture)
        {
            capture.MarkStarted(Mod.SessionContext?.SessionId, DateTimeOffset.UtcNow);
            Mod.Info(CaptureCompletionLogFormatter.FormatStarted(capture));
            Mod.WorkCoordinator.Request(DiagnosticWorkKind.RuntimeDeepCapture);
            RefreshSystemCatalogForCapture(capture);
            StartManagedTimingForCapture();
        }

        // A capture from an earlier city must not be listed, diagnosed or linked as evidence for the city that
        // is loaded now, so a session change finalizes the active capture and forgets the completed ones.
        private void ObserveSessionChange()
        {
            var generation = Mod.Sessions.Generation;
            if (generation == _observedSessionGeneration)
                return;
            var firstObservation = _observedSessionGeneration < 0;
            _observedSessionGeneration = generation;
            if (firstObservation || _controller == null)
                return;

            FinishManagedTimingForCapture(_controller.CurrentSession);
            _controller.InterruptActiveCapture(
                "The loaded city changed; the active capture was finalized early.",
                CaptureInterruptionReason.SessionChanged);
            _managedTimingLifecycle?.Abort();
            _managedTimingByCapture.Clear();
            _controller.ClearCompletedSessions();
            Mod.WorkCoordinator.Complete(DiagnosticWorkKind.RuntimeDeepCapture);
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
            if (_systemCatalog == null)
                return;

            var start = Stopwatch.GetTimestamp();
            var refreshed = _systemCatalog.TryRefresh(out var error);
            Mod.Info(string.Format(
                CultureInfo.InvariantCulture,
                "System catalog refresh timing: capture={0} systems={1} complete={2} ms={3:0.0}",
                capture?.Id ?? "none",
                _systemCatalog.Snapshot.Count,
                refreshed ? "true" : "false",
                Milliseconds(start, Stopwatch.GetTimestamp())));
            if (refreshed)
                return;

            var warning =
                "System catalog refresh failed at capture start; using the last known good catalog: "
                + (error ?? "unknown reason");
            capture?.AddWarning(warning);
            Mod.Info(warning);
        }

        private void StartManagedTimingForCapture()
        {
            if (_managedTimingLifecycle == null)
                return;

            if (_managedTimingLifecycle.TryStart(out var reason))
            {
                _managedInstrumentationUnavailableReason = null;
                return;
            }

            _managedInstrumentationUnavailableReason = reason ?? "unknown reason";
            Mod.Info(
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
                Mod.Error(ex, "Managed SystemBase timing finalization failed");
            }
        }

        private void HandleCaptureCompleted(CaptureSession capture)
        {
            if (capture == null)
                return;

            var start = Stopwatch.GetTimestamp();
            capture.MarkCompleted(DateTimeOffset.UtcNow);

            Mod.WorkCoordinator.Complete(DiagnosticWorkKind.RuntimeDeepCapture);
            var coordinatorDone = Stopwatch.GetTimestamp();
            var managedDone = coordinatorDone;

            try
            {
                if (!_managedTimingByCapture.TryGetValue(capture, out var managedTiming))
                {
                    FinishManagedTimingForCapture(capture);
                    if (!_managedTimingByCapture.TryGetValue(capture, out managedTiming))
                        managedTiming = new SystemTimingSnapshot();
                }
                _managedTimingByCapture.Remove(capture);
                managedDone = Stopwatch.GetTimestamp();

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
                Mod.Error(ex, "System timing projection failed for a completed capture");
            }
            var timingDone = Stopwatch.GetTimestamp();

            CaptureCompletionDiagnosticsDispatcher.Dispatch(
                capture,
                message => Mod.Info(message),
                TryFlushModLog);
            var logDone = Stopwatch.GetTimestamp();

            // Where the completion frame's time went, so a hitch at capture completion can be traced to a step.
            Mod.Info(string.Format(
                CultureInfo.InvariantCulture,
                "Capture completion timing: id={0} workCoordinatorMs={1:0.0} managedTimingMs={2:0.0} systemTimingMs={3:0.0} completionLogMs={4:0.0} totalMs={5:0.0}",
                capture.Id,
                Milliseconds(start, coordinatorDone),
                Milliseconds(coordinatorDone, managedDone),
                Milliseconds(managedDone, timingDone),
                Milliseconds(timingDone, logDone),
                Milliseconds(start, logDone)));
        }

        private static double Milliseconds(long from, long to) => (to - from) * 1000d / Stopwatch.Frequency;

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
