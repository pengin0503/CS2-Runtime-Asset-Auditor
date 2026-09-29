using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Core.Loading
{
    // The recorder has no game dependencies. Callers supply cheap observations at a fixed cadence.
    public sealed class LoadingTraceRecorder
    {
        public const double SamplePeriodSeconds = 2d;
        public const int MaximumRetainedSamples = 512;
        private readonly List<LoadingMemorySample> _samples = new List<LoadingMemorySample>();
        private readonly List<LoadingMilestone> _milestones = new List<LoadingMilestone>();
        private DateTimeOffset _modStartedAtUtc;
        private DateTimeOffset? _startedAtUtc;
        private DateTimeOffset? _completedAtUtc;
        private double _nextSampleAt;
        private long _sampleNumber;
        private long _retentionStride = 1;
        private long? _peakUnityBytes;
        private long? _peakRamBytes;
        private long? _peakGraphicsDriverBytes;
        private LoadingMemorySample _lastSample;
        private bool? _lastCacheState;
        private bool _observedUncachedToCachedTransition;
        private bool _assetDatabaseObserved;
        private string _purpose;

        public bool IsLoading => _startedAtUtc.HasValue && !_completedAtUtc.HasValue;

        public void MarkModStarted(DateTimeOffset atUtc)
        {
            if (_modStartedAtUtc != default(DateTimeOffset)) return;
            _modStartedAtUtc = atUtc.ToUniversalTime();
        }

        public void Begin(DateTimeOffset atUtc, string purpose)
        {
            _startedAtUtc = atUtc.ToUniversalTime();
            _completedAtUtc = null;
            _purpose = purpose;
            _samples.Clear();
            _milestones.Clear();
            _nextSampleAt = 0;
            _sampleNumber = 0;
            _retentionStride = 1;
            _peakUnityBytes = _peakRamBytes = _peakGraphicsDriverBytes = null;
            _lastSample = null;
            _lastCacheState = null;
            _observedUncachedToCachedTransition = false;
            _assetDatabaseObserved = false;
            Mark("loadStarted", atUtc);
        }

        public void Clear()
        {
            _startedAtUtc = null;
            _completedAtUtc = null;
            _samples.Clear();
            _milestones.Clear();
            _lastSample = null;
        }

        public void Mark(string name, DateTimeOffset atUtc)
        {
            if (!IsLoading || string.IsNullOrEmpty(name)) return;
            _milestones.Add(new LoadingMilestone(name, atUtc.ToUniversalTime()));
        }

        public bool ShouldSample(double elapsedSeconds)
        {
            if (!IsLoading || elapsedSeconds < _nextSampleAt) return false;
            _nextSampleAt = elapsedSeconds + SamplePeriodSeconds;
            return true;
        }

        public void Observe(DateTimeOffset atUtc, long? unityBytes, long? ramBytes,
            long? graphicsDriverBytes, int? registeredAssetCount, bool? anyDatabaseCached)
        {
            if (!IsLoading) return;
            if (!_assetDatabaseObserved && registeredAssetCount.HasValue)
            {
                Mark("assetDatabaseObserved", atUtc);
                _assetDatabaseObserved = true;
            }
            if (_lastCacheState == false && anyDatabaseCached == true)
                _observedUncachedToCachedTransition = true;
            if (anyDatabaseCached.HasValue) _lastCacheState = anyDatabaseCached;

            var sample = new LoadingMemorySample(atUtc.ToUniversalTime(), Positive(unityBytes),
                Positive(ramBytes), Positive(graphicsDriverBytes), registeredAssetCount, anyDatabaseCached);
            _lastSample = sample;
            _peakUnityBytes = Max(_peakUnityBytes, sample.UnityAllocatedBytes);
            _peakRamBytes = Max(_peakRamBytes, sample.ProcessWorkingSetBytes);
            _peakGraphicsDriverBytes = Max(_peakGraphicsDriverBytes, sample.GraphicsDriverAllocatedBytes);
            var sampleNumber = _sampleNumber++;
            if (sampleNumber % _retentionStride != 0) return;
            if (_samples.Count == MaximumRetainedSamples)
            {
                for (var i = 0; i < MaximumRetainedSamples / 2; i++)
                    _samples[i] = _samples[i * 2];
                _samples.RemoveRange(MaximumRetainedSamples / 2, MaximumRetainedSamples / 2);
                _retentionStride *= 2;
                if (sampleNumber % _retentionStride != 0) return;
            }
            _samples.Add(sample);
        }

        public void Complete(DateTimeOffset atUtc, bool cityOperable)
        {
            if (!IsLoading) return;
            if (cityOperable) Mark("gameLoadingComplete", atUtc);
            _completedAtUtc = atUtc.ToUniversalTime();
        }

        public LoadingTraceSnapshot Snapshot()
        {
            if (!_startedAtUtc.HasValue) return null;
            return new LoadingTraceSnapshot(_modStartedAtUtc, _startedAtUtc.Value, _completedAtUtc,
                _purpose, _milestones.ToArray(), _samples.ToArray(), _lastSample,
                _peakUnityBytes, _peakRamBytes, _peakGraphicsDriverBytes, _observedUncachedToCachedTransition);
        }

        private static long? Positive(long? value) => value >= 0 ? value : null;
        private static long? Max(long? a, long? b) => !a.HasValue ? b : !b.HasValue ? a : Math.Max(a.Value, b.Value);
    }

    public sealed class LoadingMilestone
    {
        public LoadingMilestone(string name, DateTimeOffset atUtc) { Name = name; AtUtc = atUtc; }
        public string Name { get; }
        public DateTimeOffset AtUtc { get; }
    }

    public sealed class LoadingMemorySample
    {
        public LoadingMemorySample(DateTimeOffset atUtc, long? unityBytes, long? ramBytes,
            long? graphicsDriverBytes, int? registeredAssetCount, bool? anyDatabaseCached)
        {
            AtUtc = atUtc;
            UnityAllocatedBytes = unityBytes;
            ProcessWorkingSetBytes = ramBytes;
            GraphicsDriverAllocatedBytes = graphicsDriverBytes;
            RegisteredAssetCount = registeredAssetCount;
            AnyDatabaseCached = anyDatabaseCached;
        }
        public DateTimeOffset AtUtc { get; }
        public long? UnityAllocatedBytes { get; }
        public long? ProcessWorkingSetBytes { get; }
        public long? GraphicsDriverAllocatedBytes { get; }
        public int? RegisteredAssetCount { get; }
        public bool? AnyDatabaseCached { get; }
    }

    public sealed class LoadingTraceSnapshot
    {
        public LoadingTraceSnapshot(DateTimeOffset modStartedAtUtc, DateTimeOffset startedAtUtc,
            DateTimeOffset? completedAtUtc, string purpose, LoadingMilestone[] milestones,
            LoadingMemorySample[] samples, LoadingMemorySample end, long? peakUnityBytes,
            long? peakRamBytes, long? peakGraphicsDriverBytes, bool observedUncachedToCachedTransition)
        {
            ModStartedAtUtc = modStartedAtUtc;
            StartedAtUtc = startedAtUtc;
            CompletedAtUtc = completedAtUtc;
            Purpose = purpose;
            Milestones = milestones;
            Samples = samples;
            End = end;
            PeakUnityAllocatedBytes = peakUnityBytes;
            PeakProcessWorkingSetBytes = peakRamBytes;
            PeakGraphicsDriverAllocatedBytes = peakGraphicsDriverBytes;
            ObservedUncachedToCachedTransition = observedUncachedToCachedTransition;
        }
        public DateTimeOffset ModStartedAtUtc { get; }
        public DateTimeOffset StartedAtUtc { get; }
        public DateTimeOffset? CompletedAtUtc { get; }
        public string Purpose { get; }
        public LoadingMilestone[] Milestones { get; }
        public LoadingMemorySample[] Samples { get; }
        public LoadingMemorySample End { get; }
        public long? PeakUnityAllocatedBytes { get; }
        public long? PeakProcessWorkingSetBytes { get; }
        public long? PeakGraphicsDriverAllocatedBytes { get; }
        public bool ObservedUncachedToCachedTransition { get; }
    }
}
