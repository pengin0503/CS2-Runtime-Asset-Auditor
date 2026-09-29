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
        private ProfilerOverheadTracker _overhead;
        private GlobalSnapshotHistory _history;
        private IReadOnlyDictionary<string, string> _recorderUnits = new Dictionary<string, string>();
        private double _nextSampleAt;
        private RuntimeFrameSampleReader _frameReader;
        private RuntimeIntervalAccumulator _frameInterval = new RuntimeIntervalAccumulator();

        public string Name => "Global";
        public GlobalMetricsSnapshot Latest { get; private set; }
        public ProfilerOverheadTracker Overhead => _overhead;
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
            _overhead = new ProfilerOverheadTracker();
            _history = new GlobalSnapshotHistory(HistoryCapacity);
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
                _recorderManager?.DeactivateAll();

            if (!monitoringEnabled)
                return;

            if (transition == MonitoringTransition.Enabled)
            {
                RestoreNormalRecorders();
                // Frames seen before monitoring was turned off must not be folded into the next sample.
                _frameInterval = new RuntimeIntervalAccumulator();
            }

            _frameInterval.AddFrame(_frameReader.Read());

            var now = CurrentTimestampSeconds;
            if (now < _nextSampleAt)
                return;

            _nextSampleAt = now + SamplingPeriodSeconds;
            _overhead.Measure(now, () => Sample(now));
        }

        public void Sample(double timestampSeconds)
        {
            var readings = _recorderManager.SampleActive()
                .Where(pair => pair.Value.Count > 0)
                .ToDictionary(pair => pair.Key, pair => pair.Value, System.StringComparer.Ordinal);

            Latest = new GlobalMetricsSnapshot(
                timestampSeconds,
                _simulationSystem.selectedSpeed,
                _simulationSystem.smoothSpeed,
                readings,
                _recorderUnits,
                _frameInterval.Complete());
            _history?.Add(Latest);
        }

        public IReadOnlyList<GlobalMetricsSnapshot> GetRecentHistory(double windowSeconds = 5d)
        {
            if (_history == null || Latest == null)
                return System.Array.Empty<GlobalMetricsSnapshot>();
            return _history.Recent(Latest.TimestampSeconds, windowSeconds);
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
