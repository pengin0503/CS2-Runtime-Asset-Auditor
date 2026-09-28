using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core
{
    public sealed class ManagedSystemTimingAccumulator
    {
        private sealed class SystemState
        {
            private readonly double[] _distributionSamples;
            private int _distributionStart;
            private int _distributionCount;

            public SystemState(int capacity)
            {
                _distributionSamples = new double[capacity];
            }

            public long CallCount { get; private set; }
            public double TotalMilliseconds { get; private set; }
            public double CurrentMilliseconds { get; private set; }
            public double MaxMilliseconds { get; private set; }

            public void Record(double milliseconds)
            {
                CallCount++;
                TotalMilliseconds += milliseconds;
                CurrentMilliseconds = milliseconds;
                if (CallCount == 1 || milliseconds > MaxMilliseconds)
                    MaxMilliseconds = milliseconds;

                if (_distributionCount < _distributionSamples.Length)
                {
                    var index = (_distributionStart + _distributionCount) % _distributionSamples.Length;
                    _distributionSamples[index] = milliseconds;
                    _distributionCount++;
                    return;
                }

                _distributionSamples[_distributionStart] = milliseconds;
                _distributionStart = (_distributionStart + 1) % _distributionSamples.Length;
            }

            public double[] GetDistributionSamples()
            {
                var samples = new double[_distributionCount];
                for (var i = 0; i < _distributionCount; i++)
                {
                    var index = (_distributionStart + i) % _distributionSamples.Length;
                    samples[i] = _distributionSamples[index];
                }

                return samples;
            }
        }

        private readonly int _maxSamplesPerSystem;
        private readonly Dictionary<string, SystemState> _systems =
            new Dictionary<string, SystemState>(StringComparer.Ordinal);

        public ManagedSystemTimingAccumulator(int maxSamplesPerSystem = 512)
        {
            if (maxSamplesPerSystem < 1)
                throw new ArgumentOutOfRangeException(nameof(maxSamplesPerSystem));
            _maxSamplesPerSystem = maxSamplesPerSystem;
        }

        public int SystemCount => _systems.Count;

        public void Record(string systemId, double milliseconds)
        {
            if (string.IsNullOrWhiteSpace(systemId)
                || milliseconds < 0d
                || double.IsNaN(milliseconds)
                || double.IsInfinity(milliseconds))
            {
                return;
            }

            if (!_systems.TryGetValue(systemId, out var state))
            {
                state = new SystemState(_maxSamplesPerSystem);
                _systems[systemId] = state;
            }

            state.Record(milliseconds);
        }

        public void Clear() => _systems.Clear();

        public SystemTimingSnapshot BuildSnapshot(IEnumerable<SystemDescriptor> systems) => BuildSnapshot(systems, windowFrames: null);

        /// <param name="windowFrames">Frames rendered while this accumulator was active. When positive,
        /// each entry also reports total time per frame, which stays comparable between systems that
        /// update every frame and systems that update rarely (for example serialization on autosave).</param>
        public SystemTimingSnapshot BuildSnapshot(IEnumerable<SystemDescriptor> systems, int? windowFrames)
        {
            var descriptors = (systems ?? Array.Empty<SystemDescriptor>())
                .Where(system => system != null && !string.IsNullOrWhiteSpace(system.FullTypeName))
                .GroupBy(system => system.FullTypeName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var snapshot = new SystemTimingSnapshot();

            foreach (var pair in _systems.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var state = pair.Value;
                var values = state.GetDistributionSamples();
                if (state.CallCount <= 0 || values.Length == 0)
                    continue;

                var calls = state.CallCount >= int.MaxValue ? int.MaxValue : (int)state.CallCount;
                var mean = state.TotalMilliseconds / state.CallCount;
                descriptors.TryGetValue(pair.Key, out var descriptor);
                var aggregate = SystemMetricAggregate.FromStreamingSummary(
                    pair.Key,
                    values,
                    state.CurrentMilliseconds,
                    mean,
                    state.MaxMilliseconds,
                    state.TotalMilliseconds,
                    calls,
                    MetricConfidence.Managed);
                snapshot.AddSystemAggregate(
                    aggregate,
                    descriptor?.AssemblyName ?? string.Empty,
                    descriptor?.PatchOwners?.Select(owner => owner.OwnerId) ?? Array.Empty<string>(),
                    descriptor?.SourceKind ?? SystemSourceKind.Unknown,
                    descriptor?.IsAggregateContainer ?? false,
                    windowFrames > 0 ? state.TotalMilliseconds / windowFrames.Value : (double?)null);
            }

            return snapshot;
        }
    }
}
