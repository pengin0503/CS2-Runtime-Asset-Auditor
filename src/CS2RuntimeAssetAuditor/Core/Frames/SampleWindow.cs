using System;

namespace CS2RuntimeAssetAuditor.Core.Frames
{
    public readonly struct SampleSummary
    {
        public SampleSummary(int count, double median, double p95, double max)
        {
            Count = count;
            Median = median;
            P95 = p95;
            Max = max;
        }

        public int Count { get; }
        public double Median { get; }
        public double P95 { get; }
        public double Max { get; }
        public bool HasValue => Count > 0;
    }

    /// <summary>
    /// Collects one interval of samples (frame times, step times) without allocating
    /// per frame. Percentiles use the same definitions as <see cref="MetricStatistics"/>: the median averages
    /// the two middle values and P95 is the nearest rank.
    /// </summary>
    public sealed class SampleWindow
    {
        private readonly double[] _values;
        private readonly double[] _sorted;
        private readonly bool _includeZero;
        private int _stored;
        private int _count;
        private double _max;

        /// <param name="includeZero">
        /// Keep zero samples, for values where zero is a real measurement (a frame that did not wait for present)
        /// rather than "not measured".
        /// </param>
        public SampleWindow(int capacity, bool includeZero = false)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _includeZero = includeZero;
            _values = new double[capacity];
            _sorted = new double[capacity];
        }

        /// <summary>Accepted samples in this interval, including any beyond the stored capacity.</summary>
        public int Count => _count;

        /// <summary>
        /// True when more samples arrived than the window stores. The median and P95 then describe only the
        /// first stored samples; the count and the maximum stay exact.
        /// </summary>
        public bool IsSaturated => _count > _stored;

        /// <summary>
        /// Adds a sample. Negative and non-finite values are ignored, and so is zero unless the window was
        /// created with <c>includeZero</c>: in most game APIs this window reads, zero means "not measured"
        /// (for example a simulation step time of 0 when no step ran).
        /// </summary>
        public void Add(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d || (value == 0d && !_includeZero))
                return;
            _count++;
            if (value > _max)
                _max = value;
            if (_stored < _values.Length)
                _values[_stored++] = value;
        }

        public SampleSummary Summarize()
        {
            if (_stored == 0)
                return default;

            Array.Copy(_values, _sorted, _stored);
            Array.Sort(_sorted, 0, _stored);
            var middle = _stored / 2;
            var median = _stored % 2 == 0
                ? (_sorted[middle - 1] + _sorted[middle]) / 2d
                : _sorted[middle];
            var rank = (int)Math.Ceiling(0.95d * _stored);
            rank = Math.Max(1, Math.Min(rank, _stored));
            return new SampleSummary(_count, median, _sorted[rank - 1], _max);
        }

        public void Reset()
        {
            _stored = 0;
            _count = 0;
            _max = 0d;
        }
    }
}
