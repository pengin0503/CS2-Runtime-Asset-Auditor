using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Core;

namespace CS2RuntimeAssetAuditor.Profiling
{
    public sealed class DeepCaptureController : IDisposable
    {
        private const int ConsecutiveOverheadBreachesBeforeDegrade = 3;
        private const int MaxDegradationActionsBeforeAbort = 4;
        private const int ProfilerMemoryTrendCaptureCount = 4;
        private const double ProfilerMemoryGrowthStepBytes = 128d * 1024d * 1024d;
        private const double ProfilerMemoryHardStopBytes = 512d * 1024d * 1024d;
        private const double ProfilerMemoryTrendWarningBytes = 256d * 1024d * 1024d;
        private const string ProfilerUsedMemoryRecorderId = "Memory\u001fProfiler Used Memory";
        private const double MaxSafetyCooldownSeconds = 600d;

        private readonly RecorderManager _recorders;
        private readonly DeepCaptureStateMachine _stateMachine;
        private readonly List<CaptureSession> _completed = new List<CaptureSession>();
        private readonly HashSet<string> _attemptedMarkerIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _activatedMarkerIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _capturedMarkerIds = new HashSet<string>(StringComparer.Ordinal);
        private double _overheadCeiling;
        private int _configuredMaxConcurrent;
        private int _maxCompletedSessions;
        private int _maxConcurrent;
        private int _currentBatchIndex = -1;
        private int _sampleStride = 1;
        private int _sampleCounter;
        private int _consecutiveOverheadBreaches;
        private int _degradationActions;
        private int _profilerMemoryDegradeLevel;
        private double _batchStartedAt;
        private double _lastObservedSeconds;
        private int _consecutiveSafetyStops;
        private CaptureState _lastState;
        private MarkerBatchPlan _plan;

        public DeepCaptureController(RecorderManager recorders, DeepCaptureStateMachine stateMachine, int maxConcurrent = 150, double overheadCeiling = 0.08, int maxCompletedSessions = 20)
        {
            _recorders = recorders ?? throw new ArgumentNullException(nameof(recorders));
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _configuredMaxConcurrent = Math.Max(1, maxConcurrent);
            _maxConcurrent = _configuredMaxConcurrent;
            _overheadCeiling = Math.Max(0.001, overheadCeiling);
            _maxCompletedSessions = Math.Max(1, maxCompletedSessions);
            _lastState = _stateMachine.State;
        }

        public event Action<CaptureSession> CaptureCompleted;

        public CaptureState State => _stateMachine.State;
        public CaptureSession CurrentSession { get; private set; }
        public IReadOnlyList<CaptureSession> CompletedSessions => _completed;
        public int CurrentBatchSize => _maxConcurrent;
        public int SamplingStride => _sampleStride;
        public int ConsecutiveSafetyStops => _consecutiveSafetyStops;

        public void ClearCompletedSessions() => _completed.Clear();

        public void Initialize()
        {
            _recorders.DiscoverAvailableMarkers();
            _plan = MarkerBatchPlanner.Create(_recorders.Descriptors.Select(d => d.Id), _maxConcurrent);
        }

        public void UpdateConfiguration(int maxConcurrent, double overheadCeiling, int maxCompletedSessions)
        {
            _configuredMaxConcurrent = Math.Max(1, maxConcurrent);
            _overheadCeiling = Math.Max(0.001d, overheadCeiling);
            _maxCompletedSessions = Math.Max(1, maxCompletedSessions);
            while (_completed.Count > _maxCompletedSessions) _completed.RemoveAt(0);
            if (CurrentSession != null) return;
            _maxConcurrent = _configuredMaxConcurrent;
            if (_recorders.Descriptors != null) _plan = MarkerBatchPlanner.Create(_recorders.Descriptors.Select(d => d.Id), _maxConcurrent);
        }

        public void RequestManualCapture(double nowSeconds, IEnumerable<GlobalMetricsSnapshot> prebuffer = null)
        {
            _lastObservedSeconds = nowSeconds;
            var before = _stateMachine.State;
            _stateMachine.RequestManualCapture(nowSeconds);
            if (before != CaptureState.DeepCapture && _stateMachine.State == CaptureState.DeepCapture)
                BeginCapture(nowSeconds, FindLatestSample(prebuffer, nowSeconds), prebuffer);
            _lastState = _stateMachine.State;
        }

        public void Observe(double nowSeconds, GlobalMetricsSnapshot global, IEnumerable<GlobalMetricsSnapshot> prebuffer = null)
        {
            Observe(nowSeconds, global, automaticTriggerAllowed: true, prebuffer);
        }

        public void Observe(
            double nowSeconds,
            GlobalMetricsSnapshot global,
            bool automaticTriggerAllowed,
            IEnumerable<GlobalMetricsSnapshot> prebuffer = null)
        {
            _lastObservedSeconds = nowSeconds;
            var before = _stateMachine.State;
            _stateMachine.Observe(
                nowSeconds,
                global?.SelectedSpeed ?? 0,
                global?.ActualSpeed ?? 0,
                automaticTriggerAllowed);
            var after = _stateMachine.State;

            if (before != CaptureState.DeepCapture && after == CaptureState.DeepCapture)
                BeginCapture(nowSeconds, global, prebuffer);

            if (after == CaptureState.DeepCapture && CurrentSession != null)
            {
                if (global != null)
                {
                    CurrentSession.AddGlobalSample(global);
                    ObserveProfilerMemory(global);
                    if (CurrentSession == null)
                    {
                        _lastState = _stateMachine.State;
                        return;
                    }
                }
                CaptureDeepSample(nowSeconds);
            }

            if (before == CaptureState.DeepCapture && after != CaptureState.DeepCapture) _recorders.DeactivateAll();
            if (after == CaptureState.PostBuffer && CurrentSession != null && global != null) CurrentSession.AddGlobalSample(global);
            if (CurrentSession != null && after != CaptureState.DeepCapture && after != CaptureState.PostBuffer)
            {
                // A capture that ran its full course proves the current conditions are safe to measure again.
                _consecutiveSafetyStops = 0;
                FinalizeCapture();
            }
            _lastState = after;
        }

        public void InterruptActiveCapture(string warning) => InterruptActiveCapture(warning, CaptureInterruptionReason.Requested);

        public void InterruptActiveCapture(string warning, CaptureInterruptionReason reason)
        {
            _recorders.DeactivateAll();
            double? cooldown = null;
            if (reason == CaptureInterruptionReason.SafetyLimit)
            {
                _consecutiveSafetyStops++;
                // Back off exponentially: the conditions that tripped the limit usually persist.
                var multiplier = Math.Pow(2d, Math.Min(10, _consecutiveSafetyStops - 1));
                cooldown = Math.Min(MaxSafetyCooldownSeconds, Math.Max(_stateMachine.CooldownSeconds, 1d) * multiplier);
            }
            else if (reason == CaptureInterruptionReason.SessionChanged)
            {
                _consecutiveSafetyStops = 0;
            }

            if (CurrentSession != null)
            {
                CurrentSession.AddWarning(warning);
                if (cooldown.HasValue)
                    CurrentSession.AddWarning(
                        $"Automatic capture is paused for {cooldown.Value:0} s after safety stop #{_consecutiveSafetyStops}; repeated stops extend the pause.");
                FinalizeCapture();
            }

            if (cooldown.HasValue)
                _stateMachine.EnterCooldown(_lastObservedSeconds, cooldown.Value);
            else
                _stateMachine.ResetToMonitoring();
            ResetMarkerCoverageTracking();
            _currentBatchIndex = -1;
            _sampleCounter = 0;
            _consecutiveOverheadBreaches = 0;
            _degradationActions = 0;
            _profilerMemoryDegradeLevel = 0;
            _lastState = _stateMachine.State;
        }

        public void ReportProfilerOverheadShare(double share)
        {
            if (CurrentSession == null) { _consecutiveOverheadBreaches = 0; return; }
            if (double.IsNaN(share) || share < 0d || share <= _overheadCeiling) { _consecutiveOverheadBreaches = 0; return; }
            _consecutiveOverheadBreaches++;
            if (_consecutiveOverheadBreaches < ConsecutiveOverheadBreachesBeforeDegrade) return;
            _consecutiveOverheadBreaches = 0;

            if (_degradationActions >= MaxDegradationActionsBeforeAbort)
            {
                InterruptActiveCapture(
                    $"Measured capture-controller overhead remained above {_overheadCeiling:P0} after repeated load reductions; the capture was finalized early. "
                    + "This overhead metric does not include managed SystemBase timing instrumentation cost.",
                    CaptureInterruptionReason.SafetyLimit);
                return;
            }

            DegradeCaptureLoad(
                $"Measured capture-controller overhead exceeded {_overheadCeiling:P0} repeatedly; marker batching reduced to {{0}} concurrent recorders. "
                    + "This overhead metric does not include managed SystemBase timing instrumentation cost.",
                "Measured capture-controller overhead remains high; sampling stride increased to {0}. "
                    + "This overhead metric does not include managed SystemBase timing instrumentation cost.");
        }

        public void Dispose() => _recorders.Dispose();

        private void BeginCapture(double nowSeconds, GlobalMetricsSnapshot triggerSample, IEnumerable<GlobalMetricsSnapshot> prebuffer)
        {
            _lastObservedSeconds = nowSeconds;
            string discoveryWarning = null;
            try { _recorders.DiscoverAvailableMarkers(); }
            catch (Exception ex) { discoveryWarning = $"Profiler marker refresh failed at capture start; using the remaining catalog where available: {ex.Message}"; }

            _maxConcurrent = _configuredMaxConcurrent;
            _sampleStride = 1;
            _consecutiveOverheadBreaches = 0;
            _degradationActions = 0;
            _profilerMemoryDegradeLevel = 0;
            _plan = MarkerBatchPlanner.Create(_recorders.Descriptors.Select(d => d.Id), _maxConcurrent);
            CurrentSession = new CaptureSession($"capture-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}", _stateMachine.LastTrigger ?? new CaptureTrigger(CaptureTriggerKind.Manual, nowSeconds, null), 4096);
            CurrentSession.SetTriggerSnapshot(triggerSample);
            if (triggerSample != null) ObserveProfilerMemory(triggerSample);
            if (!string.IsNullOrWhiteSpace(discoveryWarning)) CurrentSession.AddWarning(discoveryWarning);
            if (prebuffer != null) foreach (var sample in prebuffer) CurrentSession.AddGlobalSample(sample);
            ResetMarkerCoverageTracking();
            _currentBatchIndex = -1;
            _sampleCounter = 0;
            ActivateNextBatch(nowSeconds);
        }

        private void ObserveProfilerMemory(GlobalMetricsSnapshot global)
        {
            if (CurrentSession == null || global == null
                || !global.RecorderReadings.TryGetValue(ProfilerUsedMemoryRecorderId, out var reading)
                || reading.Count <= 0
                || double.IsNaN(reading.Value)
                || double.IsInfinity(reading.Value)
                || reading.Value < 0d)
                return;

            CurrentSession.ObserveProfilerMemory(reading.Value);
            var delta = CurrentSession.ProfilerMemoryDeltaBytes;
            if (!delta.HasValue || delta.Value < ProfilerMemoryGrowthStepBytes)
                return;

            var deltaMiB = delta.Value / (1024d * 1024d);
            if (delta.Value >= ProfilerMemoryHardStopBytes)
            {
                InterruptActiveCapture(
                    $"Profiler memory grew by {deltaMiB:0.#} MiB during this capture, exceeding the 512 MiB safety limit; the capture was finalized early.",
                    CaptureInterruptionReason.SafetyLimit);
                return;
            }

            var targetLevel = (int)Math.Floor(delta.Value / ProfilerMemoryGrowthStepBytes);
            while (CurrentSession != null && _profilerMemoryDegradeLevel < targetLevel)
            {
                _profilerMemoryDegradeLevel++;
                DegradeCaptureLoad(
                    $"Profiler memory grew by {deltaMiB:0.#} MiB during this capture; marker batching reduced to {{0}} concurrent recorders.",
                    $"Profiler memory grew by {deltaMiB:0.#} MiB during this capture; sampling stride increased to {{0}}.");
            }
        }

        private void DegradeCaptureLoad(string batchWarningFormat, string strideWarningFormat)
        {
            if (CurrentSession == null) return;
            _degradationActions++;
            if (_maxConcurrent > 1)
            {
                _maxConcurrent = Math.Max(1, _maxConcurrent / 2);
                _plan = MarkerBatchPlanner.Create(_recorders.Descriptors.Select(d => d.Id), _maxConcurrent);
                CurrentSession.AddWarning(string.Format(batchWarningFormat, _maxConcurrent));
            }
            else
            {
                _sampleStride = Math.Min(16, _sampleStride * 2);
                CurrentSession.AddWarning(string.Format(strideWarningFormat, _sampleStride));
            }
        }

        private static GlobalMetricsSnapshot FindLatestSample(IEnumerable<GlobalMetricsSnapshot> samples, double nowSeconds)
        {
            return (samples ?? Array.Empty<GlobalMetricsSnapshot>())
                .Where(sample => sample != null && sample.TimestampSeconds <= nowSeconds)
                .OrderBy(sample => sample.TimestampSeconds)
                .LastOrDefault();
        }

        private void CaptureDeepSample(double nowSeconds)
        {
            if (_plan == null || _plan.Batches.Count == 0 || CurrentSession == null) return;
            if (nowSeconds - _batchStartedAt >= 1d) ActivateNextBatch(nowSeconds);
            _sampleCounter++;
            if (_sampleCounter % _sampleStride != 0) return;
            foreach (var pair in _recorders.SampleActive())
            {
                if (pair.Value.Count <= 0) continue;
                _capturedMarkerIds.Add(pair.Key);
                CurrentSession.AddMarkerSample(pair.Key, new MetricSample(nowSeconds, pair.Value.Value, MetricConfidence.Full, pair.Value.Count));
            }
            UpdateMarkerCoverage();
        }

        private void ActivateNextBatch(double nowSeconds)
        {
            _recorders.DeactivateAll();
            if (_plan == null || _plan.Batches.Count == 0 || CurrentSession == null) return;
            _currentBatchIndex = (_currentBatchIndex + 1) % _plan.Batches.Count;
            foreach (var id in _plan.Batches[_currentBatchIndex])
            {
                _attemptedMarkerIds.Add(id);
                if (_recorders.TryActivate(id, 8, out var error)) { _activatedMarkerIds.Add(id); continue; }
                CurrentSession?.AddWarning($"Recorder activation failed for '{id}': {error}");
            }
            UpdateMarkerCoverage();
            _batchStartedAt = nowSeconds;
        }

        private void UpdateMarkerCoverage() => CurrentSession?.SetMarkerCoverage(_plan?.DiscoveredCount ?? 0, _attemptedMarkerIds.Count, _activatedMarkerIds.Count, _capturedMarkerIds.Count, _plan?.IsBatched ?? false);
        private void ResetMarkerCoverageTracking() { _attemptedMarkerIds.Clear(); _activatedMarkerIds.Clear(); _capturedMarkerIds.Clear(); }

        private void AddProfilerMemoryTrendWarning(CaptureSession completed)
        {
            var measured = _completed
                .Where(capture => capture?.ProfilerMemoryBaselineBytes.HasValue == true)
                .ToArray();
            if (measured.Length < ProfilerMemoryTrendCaptureCount)
                return;

            var recent = measured.Skip(measured.Length - ProfilerMemoryTrendCaptureCount).ToArray();
            var strictlyIncreasing = true;
            for (var index = 1; index < recent.Length; index++)
            {
                if (recent[index].ProfilerMemoryBaselineBytes.Value <= recent[index - 1].ProfilerMemoryBaselineBytes.Value)
                {
                    strictlyIncreasing = false;
                    break;
                }
            }

            if (!strictlyIncreasing)
                return;

            var growthBytes = recent[recent.Length - 1].ProfilerMemoryBaselineBytes.Value
                - recent[0].ProfilerMemoryBaselineBytes.Value;
            if (growthBytes < ProfilerMemoryTrendWarningBytes)
                return;

            completed.AddWarning(
                $"Profiler memory baseline increased across four consecutive captures by {growthBytes / (1024d * 1024d):0.#} MiB; "
                + "this is a retention pressure signal, not proof of a memory leak.");
        }

        private void FinalizeCapture()
        {
            var completed = CurrentSession;
            if (completed == null)
                return;

            UpdateMarkerCoverage();
            _completed.Add(completed);
            while (_completed.Count > _maxCompletedSessions) _completed.RemoveAt(0);
            AddProfilerMemoryTrendWarning(completed);
            CurrentSession = null;
            _consecutiveOverheadBreaches = 0;
            _degradationActions = 0;
            _profilerMemoryDegradeLevel = 0;
            CaptureCompleted?.Invoke(completed);
        }
    }
}
