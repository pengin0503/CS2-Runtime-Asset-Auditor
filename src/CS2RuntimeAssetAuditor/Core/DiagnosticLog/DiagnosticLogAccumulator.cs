using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Core.DiagnosticLog
{
    /// <summary>
    /// Folds per-frame game reads into one diagnostic-log row per interval. The values are the ones needed to
    /// tell apart why the simulation ran slower than selected (see <c>SimulationSystem.OnUpdate</c> in 1.6.2f1):
    /// the per-rendered-frame step cap, the PerformancePreference budget, the pathfinding lead, or slow steps.
    /// </summary>
    public sealed class DiagnosticLogAccumulator
    {
        /// <summary>Simulation steps per second at 1x speed.</summary>
        public const double StepsPerSecondAtNormalSpeed = 60d;

        /// <summary>The game slows the simulation when fewer pathfinding lead frames than this remain.</summary>
        public const int PathfindThrottleLeadFrames = 48;

        /// <summary>The game never runs more simulation steps than this in one rendered frame.</summary>
        public const int MaxStepsPerRenderedFrame = 8;

        public const int DefaultWindowCapacity = 2048;

        private readonly DiagnosticRecorderColumn[] _recorderColumns;
        private readonly Dictionary<string, int> _recorderColumnIndex;
        private readonly double?[] _recorderValues;
        private readonly DiagnosticSampleWindow _frameMs;
        private readonly DiagnosticSampleWindow _cpuMainMs;
        private readonly DiagnosticSampleWindow _cpuRenderMs;
        private readonly DiagnosticSampleWindow _gpuMs;
        private readonly DiagnosticSampleWindow _presentWaitMs;
        private readonly DiagnosticSampleWindow _stepMs;

        private uint? _previousFrameIndex;
        private int? _previousGcCount;

        private int _frames;
        private double _wallSeconds;
        private int _frameTimingSamples;
        private double? _selectedSpeed;
        private double _actualSpeedSum;
        private double _efficiencySum;
        private int _runningFrames;
        private int _pausedFrames;
        private int _steps;
        private int _stepCountedFrames;
        private int _maxStepsPerFrame;
        private int _framesAtRenderCap;
        private int _framesWithoutStep;
        private string? _performancePreference;
        private long? _pathfindLeadMin;
        private int _pathfindLowLeadFrames;
        private int? _pathfindPendingMax;

        public DiagnosticLogAccumulator(IReadOnlyList<DiagnosticRecorderColumn> recorderColumns, int windowCapacity = DefaultWindowCapacity)
        {
            _recorderColumns = recorderColumns == null ? Array.Empty<DiagnosticRecorderColumn>() : new List<DiagnosticRecorderColumn>(recorderColumns).ToArray();
            _recorderColumnIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < _recorderColumns.Length; i++)
                _recorderColumnIndex[_recorderColumns[i].Id] = i;
            _recorderValues = new double?[_recorderColumns.Length];
            _frameMs = new DiagnosticSampleWindow(windowCapacity);
            _cpuMainMs = new DiagnosticSampleWindow(windowCapacity);
            _cpuRenderMs = new DiagnosticSampleWindow(windowCapacity);
            _gpuMs = new DiagnosticSampleWindow(windowCapacity);
            _presentWaitMs = new DiagnosticSampleWindow(windowCapacity, includeZero: true);
            _stepMs = new DiagnosticSampleWindow(windowCapacity);
        }

        public IReadOnlyList<DiagnosticRecorderColumn> RecorderColumns => _recorderColumns;
        public int FrameCount => _frames;

        /// <summary>
        /// The render-frame step cap from <c>SimulationSystem.OnUpdate</c>, ignoring the pathfinding factor:
        /// <c>max(1, min(8, round(selectedSpeed * 2)))</c>. Unity's <c>Mathf.RoundToInt</c> rounds halves to even.
        /// </summary>
        public static int RenderFrameStepCap(double selectedSpeed)
        {
            var rounded = (int)Math.Round(selectedSpeed * 2d, MidpointRounding.ToEven);
            return Math.Max(1, Math.Min(MaxStepsPerRenderedFrame, rounded));
        }

        public void AddFrame(in DiagnosticFrameInput frame)
        {
            _frames++;
            if (frame.UnscaledDeltaSeconds > 0d && !double.IsInfinity(frame.UnscaledDeltaSeconds))
                _wallSeconds += frame.UnscaledDeltaSeconds;
            _frameMs.Add(frame.UnscaledDeltaSeconds * 1000d);

            if (frame.HasFrameTiming)
            {
                _frameTimingSamples++;
                _cpuMainMs.Add(frame.CpuMainThreadMs);
                _cpuRenderMs.Add(frame.CpuRenderThreadMs);
                _gpuMs.Add(frame.GpuMs);
                _presentWaitMs.Add(frame.PresentWaitMs);
            }

            _selectedSpeed = frame.SelectedSpeed;
            _performancePreference = frame.PerformancePreference;
            var running = frame.SelectedSpeed > 0d;
            if (running)
            {
                _runningFrames++;
                _actualSpeedSum += frame.ActualSpeed;
                _efficiencySum += SimulationEfficiency.Calculate(frame.SelectedSpeed, frame.ActualSpeed);
            }
            else
            {
                _pausedFrames++;
            }

            ObserveSteps(frame, running);
            _stepMs.Add(frame.SimulationStepSeconds * 1000d);
            ObservePathfinding(frame);
        }

        /// <summary>Keeps the latest reading of each recorder column; readings for other recorders are ignored.</summary>
        public void AddRecorderReadings(IReadOnlyDictionary<string, RecorderReading> readings)
        {
            if (readings == null)
                return;
            foreach (var pair in readings)
            {
                if (pair.Value.Count > 0 && _recorderColumnIndex.TryGetValue(pair.Key, out var index))
                    _recorderValues[index] = pair.Value.Value;
            }
        }

        public DiagnosticLogRow Complete(DateTimeOffset utcTime, double elapsedSeconds, DiagnosticIntervalContext context)
        {
            var row = new DiagnosticLogRow
            {
                UtcTime = utcTime.ToUniversalTime(),
                ElapsedSeconds = elapsedSeconds,
                IntervalSeconds = _wallSeconds,
                Frames = _frames,
                FrameMs = _frameMs.Summarize(),
                FrameTimingSamples = _frameTimingSamples,
                CpuMainThreadMs = _cpuMainMs.Summarize(),
                CpuRenderThreadMs = _cpuRenderMs.Summarize(),
                GpuMs = _gpuMs.Summarize(),
                PresentWaitMs = _presentWaitMs.Summarize(),
                SelectedSpeed = _selectedSpeed,
                ActualSpeedMean = _runningFrames > 0 ? _actualSpeedSum / _runningFrames : (double?)null,
                EfficiencyMean = _runningFrames > 0 ? _efficiencySum / _runningFrames : (double?)null,
                PausedFrames = _pausedFrames,
                SimulationSteps = _stepCountedFrames > 0 ? _steps : (int?)null,
                SimulationStepsPerSecond = _stepCountedFrames > 0 && _wallSeconds > 0d ? _steps / _wallSeconds : (double?)null,
                SimulationMaxStepsPerFrame = _stepCountedFrames > 0 ? _maxStepsPerFrame : (int?)null,
                SimulationFramesAtRenderCap = _framesAtRenderCap,
                SimulationFramesWithoutStep = _framesWithoutStep,
                SimulationStepMs = _stepMs.Summarize(),
                PerformancePreference = _performancePreference,
                PathfindLeadFramesMin = _pathfindLeadMin,
                PathfindLowLeadFrames = _pathfindLowLeadFrames,
                PathfindPendingRequestsMax = _pathfindPendingMax,
                ManagedHeapMiB = context.ManagedHeapBytes.HasValue ? context.ManagedHeapBytes.Value / (1024d * 1024d) : (double?)null,
                GcCollections = GcDelta(context.GcCollectionCount),
                CaptureState = context.CaptureState,
                CaptureTrigger = context.CaptureTrigger,
                CaptureId = context.CaptureId,
                RecorderValues = (double?[])_recorderValues.Clone()
            };
            ResetInterval();
            return row;
        }

        private void ObserveSteps(in DiagnosticFrameInput frame, bool running)
        {
            var index = frame.SimulationFrameIndex;
            var previous = _previousFrameIndex;
            _previousFrameIndex = index;
            if (!previous.HasValue || index < previous.Value)
                return;

            var delta = index - previous.Value;
            // More than the per-frame cap means frameIndex was replaced (a load), not stepped.
            if (delta > MaxStepsPerRenderedFrame)
                return;

            var steps = (int)delta;
            _stepCountedFrames++;
            _steps += steps;
            if (steps > _maxStepsPerFrame)
                _maxStepsPerFrame = steps;
            if (!running)
                return;
            if (steps == 0)
                _framesWithoutStep++;
            else if (steps >= RenderFrameStepCap(frame.SelectedSpeed))
                _framesAtRenderCap++;
        }

        private void ObservePathfinding(in DiagnosticFrameInput frame)
        {
            if (frame.PathfindPendingRequests >= 0)
                _pathfindPendingMax = _pathfindPendingMax.HasValue
                    ? Math.Max(_pathfindPendingMax.Value, frame.PathfindPendingRequests)
                    : frame.PathfindPendingRequests;

            if (frame.PathfindPendingSimulationFrame == uint.MaxValue)
                return;

            // Signed lead after this frame's steps. The game computes it with unsigned arithmetic before
            // stepping, so a negative value here does not throttle the simulation there.
            var lead = (long)frame.PathfindPendingSimulationFrame - frame.SimulationFrameIndex - 1L;
            _pathfindLeadMin = _pathfindLeadMin.HasValue ? Math.Min(_pathfindLeadMin.Value, lead) : lead;
            if (lead >= 0 && lead < PathfindThrottleLeadFrames)
                _pathfindLowLeadFrames++;
        }

        private int? GcDelta(int? count)
        {
            var previous = _previousGcCount;
            _previousGcCount = count;
            if (!count.HasValue || !previous.HasValue || count.Value < previous.Value)
                return null;
            return count.Value - previous.Value;
        }

        private void ResetInterval()
        {
            _frames = 0;
            _wallSeconds = 0d;
            _frameTimingSamples = 0;
            _selectedSpeed = null;
            _actualSpeedSum = 0d;
            _efficiencySum = 0d;
            _runningFrames = 0;
            _pausedFrames = 0;
            _steps = 0;
            _stepCountedFrames = 0;
            _maxStepsPerFrame = 0;
            _framesAtRenderCap = 0;
            _framesWithoutStep = 0;
            _performancePreference = null;
            _pathfindLeadMin = null;
            _pathfindLowLeadFrames = 0;
            _pathfindPendingMax = null;
            _frameMs.Reset();
            _cpuMainMs.Reset();
            _cpuRenderMs.Reset();
            _gpuMs.Reset();
            _presentWaitMs.Reset();
            _stepMs.Reset();
            for (var i = 0; i < _recorderValues.Length; i++)
                _recorderValues[i] = null;
        }
    }
}
