using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Core
{
    public sealed class RollingMetricSeries
    {
        private readonly MetricSample[] _buffer;
        private int _nextIndex;
        private int _count;

        public RollingMetricSeries(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _buffer = new MetricSample[capacity];
        }

        public int Capacity => _buffer.Length;
        public int Count => _count;

        public void Add(MetricSample sample)
        {
            _buffer[_nextIndex] = sample;
            _nextIndex = (_nextIndex + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }

        public IReadOnlyList<MetricSample> Snapshot()
        {
            var result = new MetricSample[_count];
            if (_count == 0)
                return result;

            var firstIndex = _count == _buffer.Length ? _nextIndex : 0;
            for (var i = 0; i < _count; i++)
                result[i] = _buffer[(firstIndex + i) % _buffer.Length];

            return result;
        }
    }
}
