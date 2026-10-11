using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.Frames;
using CS2RuntimeAssetAuditor.Lifecycle;
using CS2RuntimeAssetAuditor.Profiling;
using Game;
using Game.Pathfind;
using Game.Simulation;

namespace CS2RuntimeAssetAuditor.Collectors
{
    public partial class GlobalMetricsCollector : GameSystemBase, IMetricCollector
    {
        public const double DefaultSamplingPeriodSeconds = 0.5;
        private const int HistoryCapacity = 60;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly MonitoringLifecycleGate _monitoringGate = new MonitoringLifecycleGate(initiallyEnabled: true);
        private SimulationSystem _simulationSystem;
        private RecorderManager _recorderManager;
        private SessionScopedSnapshots _samples;
        private long _sessionGeneration = long.MinValue;
        private IReadOnlyDictionary<string, string> _recorderUnits = new Dictionary<string, string>();
        private double _nextSampleAt;
        private RuntimeFrameSampleReader _frameReader;
        private VirtualTexturingReader _virtualTexturing;
        private RuntimeIntervalAccumulator _frameInterval = new RuntimeIntervalAccumulator();

        public string Name => "Global";
        /// <summary>The latest sample of the loaded city; null in menus, while loading and before the city's first sample.</summary>
        public GlobalMetricsSnapshot Latest => _samples?.Latest(Mod.Sessions.Generation);
        public RecorderManager Recorders => _recorderManager;
        public double SamplingPeriodSeconds => Mod.Settings?.ResolvedSamplingPeriodSeconds ?? DefaultSamplingPeriodSeconds;
        public double CurrentTimestampSeconds => _clock.Elapsed.TotalSeconds;
        internal SimulationSystem SimulationRuntimeSystem => _simulationSystem;
        internal RuntimeFrameSampleReader FrameSamples => _frameReader;

        protected override void OnCreate()
        {
            base.OnCreate();
            _simulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            _frameReader = new RuntimeFrameSampleReader(_simulationSystem, World.GetOrCreateSystemManaged<PathfindResultSystem>());
            _recorderManager = new RecorderManager(new UnityRecorderBackend());
            _virtualTexturing = new VirtualTexturingReader(World);
            _samples = new SessionScopedSnapshots(HistoryCapacity);
            _recorderManager.DiscoverAvailableMarkers();
            _recorderUnits = _recorderManager.Descriptors.ToDictionary(descriptor => descriptor.Id, descriptor => descriptor.UnitType);
            RestoreNormalRecorders();
        }

        protected override void OnUpdate()
        {
            var start = ModUpdateCost.Start();
            try { RunUpdate(); }
            finally { ModUpdateCost.Stop(nameof(GlobalMetricsCollector), start); }
        }

        private void RunUpdate()
        {
            var monitoringEnabled = Mod.Settings == null || Mod.Settings.EnableMonitoring;
            var transition = _monitoringGate.Observe(monitoringEnabled);

            if (transition == MonitoringTransition.Disabled)
            {
                _recorderManager?.DeactivateAll();
                // Samples from before monitoring was turned off are not shown as current or used as a prebuffer.
                _samples?.Clear();
            }

            if (!monitoringEnabled)
                return;

            var now = CurrentTimestampSeconds;
            if (transition == MonitoringTransition.Enabled)
            {
                RestoreNormalRecorders();
                RestartInterval(now);
            }

            // Samples belong to one loaded city. Menus, the editor and loading screens are not sampled, and a new
            // city starts from an empty interval so frames from the previous city or the loading screen are not
            // folded into its first sample.
            var generation = Mod.Sessions.Generation;
            if (generation != _sessionGeneration)
            {
                _sessionGeneration = generation;
                RestartInterval(now);
            }
            if (!Mod.Sessions.IsActive)
                return;

            _frameInterval.AddFrame(_frameReader.Read());

            if (now < _nextSampleAt)
                return;

            _nextSampleAt = now + SamplingPeriodSeconds;
            Sample(now);
        }

        public void Sample(double timestampSeconds)
        {
            var readings = _recorderManager.SampleActive()
                .Where(pair => pair.Value.Count > 0)
                .ToDictionary(pair => pair.Key, pair => pair.Value, System.StringComparer.Ordinal);

            var interval = _frameInterval.Complete();
            // A frame-rate cap makes the main thread wait for present just like a GPU-bound frame does.
            interval.VSyncCount = UnityEngine.QualitySettings.vSyncCount;
            interval.TargetFrameRate = UnityEngine.Application.targetFrameRate;
            _samples?.Add(_sessionGeneration, new GlobalMetricsSnapshot(
                timestampSeconds,
                _simulationSystem.selectedSpeed,
                _simulationSystem.smoothSpeed,
                readings,
                _recorderUnits,
                interval,
                _virtualTexturing?.Read()));
        }

        /// <summary>Samples of the loaded city only; empty when the city has none yet.</summary>
        public IReadOnlyList<GlobalMetricsSnapshot> GetRecentHistory(double windowSeconds = 5d)
        {
            if (_samples == null)
                return System.Array.Empty<GlobalMetricsSnapshot>();
            return _samples.Recent(Mod.Sessions.Generation, windowSeconds);
        }

        // The first sample of a new interval comes one full period later, so it summarizes that period's frames.
        private void RestartInterval(double now)
        {
            _frameInterval = new RuntimeIntervalAccumulator();
            _nextSampleAt = now + SamplingPeriodSeconds;
        }

        public void RestoreNormalRecorders()
        {
            if (_recorderManager == null)
                return;

            _recorderManager.DeactivateAll();
            foreach (var names in NormalMonitoringRecorderPolicy.GetPreferredRecorderNameGroups())
                ActivatePreferred(8, names);
        }

        protected override void OnDestroy()
        {
            _recorderManager?.Dispose();
            base.OnDestroy();
        }

        private void ActivatePreferred(int capacity, params string[] names)
        {
            var descriptor = _recorderManager.FindFirstByName(names);
            if (descriptor != null)
                _recorderManager.TryActivate(descriptor.Id, capacity, out _);
        }
    }
}
