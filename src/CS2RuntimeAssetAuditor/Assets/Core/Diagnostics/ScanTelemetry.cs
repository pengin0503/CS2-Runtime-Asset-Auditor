using System;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Assets.Core.Diagnostics
{
    public sealed class ScanTelemetrySnapshot
    {
        public ScanTelemetrySnapshot(
            DateTimeOffset startedAt,
            DateTimeOffset capturedAt,
            long processedItems,
            long sliceCount,
            int sampleCount,
            double elapsedMilliseconds,
            double maxSliceMilliseconds,
            double p95SliceMilliseconds)
        {
            StartedAt = startedAt;
            CapturedAt = capturedAt;
            ProcessedItems = processedItems;
            SliceCount = sliceCount;
            SampleCount = sampleCount;
            ElapsedMilliseconds = elapsedMilliseconds;
            MaxSliceMilliseconds = maxSliceMilliseconds;
            P95SliceMilliseconds = p95SliceMilliseconds;
        }

        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset CapturedAt { get; }
        public long ProcessedItems { get; }
        public long SliceCount { get; }
        public int SampleCount { get; }
        public double ElapsedMilliseconds { get; }
        public double MaxSliceMilliseconds { get; }
        public double P95SliceMilliseconds { get; }
    }

    public sealed class ScanTelemetry
    {
        public const int DefaultSampleCapacity = 256;

        private readonly double[] _sliceMilliseconds;
        private int _sampleCount;
        private int _nextSampleIndex;
        private long _processedItems;
        private long _sliceCount;
        private double _maxSliceMilliseconds;

        public ScanTelemetry(DateTimeOffset startedAt, int sampleCapacity = DefaultSampleCapacity)
        {
            if (sampleCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleCapacity));
            StartedAt = startedAt;
            _sliceMilliseconds = new double[sampleCapacity];
        }

        public DateTimeOffset StartedAt { get; }

        public void RecordManagedSlice(TimeSpan elapsed, long processedItems)
        {
            if (elapsed < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (processedItems < 0)
                throw new ArgumentOutOfRangeException(nameof(processedItems));

            var milliseconds = elapsed.TotalMilliseconds;
            _processedItems = checked(_processedItems + processedItems);
            _sliceCount = checked(_sliceCount + 1);
            if (milliseconds > _maxSliceMilliseconds)
                _maxSliceMilliseconds = milliseconds;

            _sliceMilliseconds[_nextSampleIndex] = milliseconds;
            _nextSampleIndex = (_nextSampleIndex + 1) % _sliceMilliseconds.Length;
            if (_sampleCount < _sliceMilliseconds.Length)
                _sampleCount++;
        }

        public ScanTelemetrySnapshot Snapshot(DateTimeOffset capturedAt)
        {
            var elapsed = capturedAt >= StartedAt ? capturedAt - StartedAt : TimeSpan.Zero;
            var samples = new double[_sampleCount];
            Array.Copy(_sliceMilliseconds, samples, _sampleCount);
            Array.Sort(samples);

            var p95 = 0d;
            if (samples.Length > 0)
            {
                var nearestRank = (int)Math.Ceiling(0.95d * samples.Length);
                p95 = samples[Math.Max(0, nearestRank - 1)];
            }

            return new ScanTelemetrySnapshot(
                StartedAt,
                capturedAt,
                _processedItems,
                _sliceCount,
                _sampleCount,
                elapsed.TotalMilliseconds,
                _maxSliceMilliseconds,
                p95);
        }
    }
}
