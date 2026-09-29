using System;

namespace CS2RuntimeAssetAuditor.Core.Frames
{
    /// <summary>
    /// Folds per-frame game reads into one <see cref="RuntimeInterval"/>. The values are the ones needed to tell
    /// apart why the simulation ran slower than selected (see <c>SimulationSystem.OnUpdate</c> in 1.6.2f1): the
    /// per-rendered-frame step cap, the PerformancePreference budget, the pathfinding lead, or slow steps.
    /// </summary>
    public sealed class RuntimeIntervalAccumulator
    {
        public const int DefaultWindowCapacity = 2048;

        private readonly SampleWindow _frameMs;
        private readonly SampleWindow _cpuMainMs;
        private readonly SampleWindow _cpuRenderMs;
        private readonly SampleWindow _gpuMs;
        private readonly SampleWindow _presentWaitMs;
        private readonly SampleWindow _stepMs;

        private uint? _previousFrameIndex;

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
        private int _stepCountedRunningFrames;
        private int _maxStepsPerFrame;
        private int _framesAtRenderCap;
        private int _framesWithoutStep;
        private string? _performancePreference;
        private long? _pathfindLeadMin;
        private int _pathfindLowLeadFrames;
        private int? _pathfindPendingMax;

        public RuntimeIntervalAccumulator(int windowCapacity = DefaultWindowCapacity)
        {
            _frameMs = new SampleWindow(windowCapacity);
            _cpuMainMs = new SampleWindow(windowCapacity);
            _cpuRenderMs = new SampleWindow(windowCapacity);
            _gpuMs = new SampleWindow(windowCapacity);
            _presentWaitMs = new SampleWindow(windowCapacity, includeZero: true);
            _stepMs = new SampleWindow(windowCapacity);
        }

        public int FrameCount => _frames;

        public void AddFrame(in RuntimeFrameSample frame)
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

        /// <summary>Returns the interval so far and starts a new one; the step baseline carries over.</summary>
        public RuntimeInterval Complete()
        {
            var interval = new RuntimeInterval
            {
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
                RunningFrames = _runningFrames,
                PausedFrames = _pausedFrames,
                SimulationSteps = _stepCountedFrames > 0 ? _steps : (int?)null,
                SimulationStepsPerSecond = _stepCountedFrames > 0 && _wallSeconds > 0d ? _steps / _wallSeconds : (double?)null,
                SimulationMaxStepsPerFrame = _stepCountedFrames > 0 ? _maxStepsPerFrame : (int?)null,
                SimulationStepCountedFrames = _stepCountedRunningFrames,
                SimulationFramesAtRenderCap = _framesAtRenderCap,
                SimulationFramesWithoutStep = _framesWithoutStep,
                SimulationStepMs = _stepMs.Summarize(),
                PerformancePreference = _performancePreference,
                PathfindLeadFramesMin = _pathfindLeadMin,
                PathfindLowLeadFrames = _pathfindLowLeadFrames,
                PathfindPendingRequestsMax = _pathfindPendingMax
            };
            if (_selectedSpeed.HasValue && interval.FramesPerSecond.HasValue)
                interval.FrameRateEfficiencyCeiling = SimulationStepLimits.FrameRateEfficiencyCeiling(_selectedSpeed.Value, interval.FramesPerSecond.Value);
            ResetInterval();
            return interval;
        }

        private void ObserveSteps(in RuntimeFrameSample frame, bool running)
        {
            var index = frame.SimulationFrameIndex;
            var previous = _previousFrameIndex;
            _previousFrameIndex = index;
            if (!previous.HasValue || index < previous.Value)
                return;

            var delta = index - previous.Value;
            // More than the per-frame cap means frameIndex was replaced (a load), not stepped.
            if (delta > SimulationStepLimits.MaxStepsPerRenderedFrame)
                return;

            var steps = (int)delta;
            _stepCountedFrames++;
            _steps += steps;
            if (steps > _maxStepsPerFrame)
                _maxStepsPerFrame = steps;
            if (!running)
                return;
            _stepCountedRunningFrames++;
            if (steps == 0)
                _framesWithoutStep++;
            else if (steps >= SimulationStepLimits.RenderFrameStepCap(frame.SelectedSpeed))
                _framesAtRenderCap++;
        }

        private void ObservePathfinding(in RuntimeFrameSample frame)
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
            if (lead >= 0 && lead < SimulationStepLimits.PathfindThrottleLeadFrames)
                _pathfindLowLeadFrames++;
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
            _stepCountedRunningFrames = 0;
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
        }
    }
}
