using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Core.Loading
{
    public enum LoadingTraceOutcome
    {
        /// <summary>The load has started and neither completed nor been interrupted yet.</summary>
        InProgress,
        /// <summary>The game reported that the gameplay load completed.</summary>
        Completed,
        /// <summary>The load ended without the game reporting its completion: another load started, or loading
        /// finished outside gameplay. The trace ends at that point.</summary>
        Interrupted
    }

    // The recorder has no game dependencies. Callers supply cheap observations at a fixed cadence.
    public sealed class LoadingTraceRecorder
    {
        public const double SamplePeriodSeconds = 2d;
        public const int MaximumRetainedSamples = 512;
        public const string LoadStartedMilestone = "loadStarted";
        public const string AssetRegistrationChangedMilestone = "assetRegistrationChanged";
        public const string AssetCacheStateChangedMilestone = "assetCacheStateChanged";
        public const string GameLoadingCompleteMilestone = "gameLoadingComplete";
        public const string LoadInterruptedMilestone = "loadInterrupted";

        private readonly List<LoadingMemorySample> _samples = new List<LoadingMemorySample>();
        private readonly List<LoadingMilestone> _milestones = new List<LoadingMilestone>();
        private DateTimeOffset _modStartedAtUtc;
        private DateTimeOffset? _startedAtUtc;
        private DateTimeOffset? _endedAtUtc;
        private LoadingTraceOutcome _outcome;
        private string _interruptionReason;
        private double _nextSampleAt;
        private long _sampleNumber;
        private long _retentionStride = 1;
        private LoadingPeak _peakUnity;
        private LoadingPeak _peakRam;
        private LoadingPeak _peakGraphicsDriver;
        private LoadingMemorySample _lastSample;
        private bool? _lastCacheState;
        private bool _observedUncachedToCachedTransition;
        private int? _initialAssetCount;
        private bool? _initialCacheState;
        private bool _assetRegistrationChanged;
        private bool _assetCacheStateChanged;
        private string _purpose;
        private LoadingTraceSnapshot _previousInterrupted;

        public bool IsLoading => _startedAtUtc.HasValue && !_endedAtUtc.HasValue;

        public void MarkModStarted(DateTimeOffset atUtc)
        {
            if (_modStartedAtUtc != default(DateTimeOffset)) return;
            _modStartedAtUtc = atUtc.ToUniversalTime();
        }

        /// <summary>
        /// Starts the trace of a new gameplay load. If the previous load was interrupted, its trace is kept alongside
        /// the new one: reports can only be exported during gameplay, so the next load is the first chance to export it.
        /// </summary>
        public void Begin(DateTimeOffset atUtc, string purpose)
        {
            _previousInterrupted = _outcome == LoadingTraceOutcome.Interrupted && _startedAtUtc.HasValue ? CurrentSnapshot(null) : null;
            _startedAtUtc = atUtc.ToUniversalTime();
            _endedAtUtc = null;
            _outcome = LoadingTraceOutcome.InProgress;
            _interruptionReason = null;
            _purpose = purpose;
            _samples.Clear();
            _milestones.Clear();
            _nextSampleAt = 0;
            _sampleNumber = 0;
            _retentionStride = 1;
            _peakUnity = _peakRam = _peakGraphicsDriver = default(LoadingPeak);
            _lastSample = null;
            _lastCacheState = null;
            _observedUncachedToCachedTransition = false;
            _initialAssetCount = null;
            _initialCacheState = null;
            _assetRegistrationChanged = false;
            _assetCacheStateChanged = false;
            Mark(LoadStartedMilestone, atUtc);
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

        /// <summary>
        /// Records one observation. Negative values are unavailable. A graphics driver allocation of zero is also
        /// unavailable: Unity reports it only in development players and the editor and returns 0 elsewhere.
        /// </summary>
        public void Observe(DateTimeOffset atUtc, long? unityBytes, long? ramBytes,
            long? graphicsDriverBytes, int? registeredAssetCount, bool? anyDatabaseCached)
        {
            if (!IsLoading) return;
            var at = atUtc.ToUniversalTime();
            ObserveAssetDatabaseChanges(at, registeredAssetCount, anyDatabaseCached);
            if (_lastCacheState == false && anyDatabaseCached == true)
                _observedUncachedToCachedTransition = true;
            if (anyDatabaseCached.HasValue) _lastCacheState = anyDatabaseCached;

            var sample = new LoadingMemorySample(at, NonNegative(unityBytes),
                NonNegative(ramBytes), Positive(graphicsDriverBytes), registeredAssetCount, anyDatabaseCached);
            _lastSample = sample;
            _peakUnity = _peakUnity.Observe(sample.UnityAllocatedBytes, at);
            _peakRam = _peakRam.Observe(sample.ProcessWorkingSetBytes, at);
            _peakGraphicsDriver = _peakGraphicsDriver.Observe(sample.GraphicsDriverAllocatedBytes, at);
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

        /// <summary>The game reported that the gameplay load completed.</summary>
        public void Complete(DateTimeOffset atUtc)
        {
            if (!IsLoading) return;
            Mark(GameLoadingCompleteMilestone, atUtc);
            _outcome = LoadingTraceOutcome.Completed;
            _endedAtUtc = atUtc.ToUniversalTime();
        }

        /// <summary>
        /// Ends a load that did not complete, keeping everything recorded so far. The reason is a short
        /// machine-readable description of what ended the load, such as the mode of the next load.
        /// </summary>
        public void Interrupt(DateTimeOffset atUtc, string reason)
        {
            if (!IsLoading) return;
            Mark(LoadInterruptedMilestone, atUtc);
            _outcome = LoadingTraceOutcome.Interrupted;
            _interruptionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            _endedAtUtc = atUtc.ToUniversalTime();
        }

        public LoadingTraceSnapshot Snapshot() => _startedAtUtc.HasValue ? CurrentSnapshot(_previousInterrupted) : null;

        private LoadingTraceSnapshot CurrentSnapshot(LoadingTraceSnapshot previousInterrupted) =>
            new LoadingTraceSnapshot(_modStartedAtUtc, _startedAtUtc.Value, _endedAtUtc, _outcome,
                _interruptionReason, _purpose, _milestones.ToArray(), _samples.ToArray(), _lastSample, _sampleNumber,
                _peakUnity, _peakRam, _peakGraphicsDriver, _observedUncachedToCachedTransition, previousInterrupted);

        // The first successful observation is the baseline; it is taken as the load starts, so observing the
        // database is not itself a loading event. A later sampled difference is.
        private void ObserveAssetDatabaseChanges(DateTimeOffset atUtc, int? registeredAssetCount, bool? anyDatabaseCached)
        {
            if (registeredAssetCount.HasValue)
            {
                if (!_initialAssetCount.HasValue)
                    _initialAssetCount = registeredAssetCount;
                else if (!_assetRegistrationChanged && registeredAssetCount != _initialAssetCount)
                {
                    _assetRegistrationChanged = true;
                    Mark(AssetRegistrationChangedMilestone, atUtc);
                }
            }
            if (anyDatabaseCached.HasValue)
            {
                if (!_initialCacheState.HasValue)
                    _initialCacheState = anyDatabaseCached;
                else if (!_assetCacheStateChanged && anyDatabaseCached != _initialCacheState)
                {
                    _assetCacheStateChanged = true;
                    Mark(AssetCacheStateChangedMilestone, atUtc);
                }
            }
        }

        private static long? NonNegative(long? value) => value >= 0 ? value : null;
        private static long? Positive(long? value) => value > 0 ? value : null;
    }

    /// <summary>The largest observed value of one metric and when it was first observed.</summary>
    public readonly struct LoadingPeak
    {
        public LoadingPeak(long? bytes, DateTimeOffset? atUtc) { Bytes = bytes; AtUtc = atUtc; }
        public long? Bytes { get; }
        public DateTimeOffset? AtUtc { get; }

        internal LoadingPeak Observe(long? bytes, DateTimeOffset atUtc) =>
            bytes.HasValue && (!Bytes.HasValue || bytes.Value > Bytes.Value) ? new LoadingPeak(bytes, atUtc) : this;
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
            DateTimeOffset? endedAtUtc, LoadingTraceOutcome outcome, string interruptionReason, string purpose,
            LoadingMilestone[] milestones, LoadingMemorySample[] samples, LoadingMemorySample end, long observationCount,
            LoadingPeak peakUnity, LoadingPeak peakRam, LoadingPeak peakGraphicsDriver,
            bool observedUncachedToCachedTransition, LoadingTraceSnapshot previousInterrupted = null)
        {
            PreviousInterrupted = previousInterrupted;
            ModStartedAtUtc = modStartedAtUtc;
            StartedAtUtc = startedAtUtc;
            EndedAtUtc = endedAtUtc;
            Outcome = outcome;
            InterruptionReason = interruptionReason;
            Purpose = purpose;
            Milestones = milestones;
            Samples = samples;
            End = end;
            ObservationCount = observationCount;
            PeakUnityAllocated = peakUnity;
            PeakProcessWorkingSet = peakRam;
            PeakGraphicsDriverAllocated = peakGraphicsDriver;
            ObservedUncachedToCachedTransition = observedUncachedToCachedTransition;
        }
        public DateTimeOffset ModStartedAtUtc { get; }
        public DateTimeOffset StartedAtUtc { get; }
        /// <summary>When the load completed or was interrupted; null while it is in progress.</summary>
        public DateTimeOffset? EndedAtUtc { get; }
        public DateTimeOffset? CompletedAtUtc => Outcome == LoadingTraceOutcome.Completed ? EndedAtUtc : null;
        public DateTimeOffset? InterruptedAtUtc => Outcome == LoadingTraceOutcome.Interrupted ? EndedAtUtc : null;
        public LoadingTraceOutcome Outcome { get; }
        public string InterruptionReason { get; }
        public string Purpose { get; }
        public LoadingMilestone[] Milestones { get; }
        public LoadingMemorySample[] Samples { get; }
        public LoadingMemorySample End { get; }
        /// <summary>Observations taken, including those the bounded history dropped.</summary>
        public long ObservationCount { get; }
        public LoadingPeak PeakUnityAllocated { get; }
        public LoadingPeak PeakProcessWorkingSet { get; }
        public LoadingPeak PeakGraphicsDriverAllocated { get; }
        public long? PeakUnityAllocatedBytes => PeakUnityAllocated.Bytes;
        public long? PeakProcessWorkingSetBytes => PeakProcessWorkingSet.Bytes;
        public long? PeakGraphicsDriverAllocatedBytes => PeakGraphicsDriverAllocated.Bytes;
        public bool ObservedUncachedToCachedTransition { get; }
        /// <summary>The interrupted load that directly preceded this one, if any. It never has its own predecessor.</summary>
        public LoadingTraceSnapshot PreviousInterrupted { get; }
    }
}
