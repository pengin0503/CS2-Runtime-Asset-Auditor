using System;
using System.Collections.Generic;

namespace CS2RuntimeProfiler.Core
{
    public sealed class GlobalSnapshotHistory
    {
        private readonly int _capacity;
        private readonly GlobalMetricsSnapshot[] _samples;
        private int _start;
        private int _count;

        public GlobalSnapshotHistory(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _capacity = capacity;
            _samples = new GlobalMetricsSnapshot[capacity];
        }

        public int Count => _count;

        public void Add(GlobalMetricsSnapshot sample)
        {
            if (sample == null)
                return;

            if (_count < _capacity)
            {
                _samples[PhysicalIndex(_count)] = sample;
                _count++;
                return;
            }

            _samples[_start] = sample;
            _start = (_start + 1) % _capacity;
        }

        public IReadOnlyList<GlobalMetricsSnapshot> Snapshot()
        {
            var result = new GlobalMetricsSnapshot[_count];
            for (var i = 0; i < _count; i++)
                result[i] = _samples[PhysicalIndex(i)];

            return result;
        }

        public IReadOnlyList<GlobalMetricsSnapshot> Recent(double nowSeconds, double windowSeconds)
        {
            if (windowSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(windowSeconds));

            var lowerBound = nowSeconds - windowSeconds;
            var matchCount = 0;

            for (var i = 0; i < _count; i++)
            {
                var sample = _samples[PhysicalIndex(i)];
                if (sample.TimestampSeconds >= lowerBound && sample.TimestampSeconds <= nowSeconds)
                    matchCount++;
            }

            var result = new GlobalMetricsSnapshot[matchCount];
            var resultIndex = 0;
            for (var i = 0; i < _count; i++)
            {
                var sample = _samples[PhysicalIndex(i)];
                if (sample.TimestampSeconds < lowerBound || sample.TimestampSeconds > nowSeconds)
                    continue;

                result[resultIndex++] = sample;
            }

            return result;
        }

        private int PhysicalIndex(int logicalIndex)
            => (_start + logicalIndex) % _capacity;
    }
}
