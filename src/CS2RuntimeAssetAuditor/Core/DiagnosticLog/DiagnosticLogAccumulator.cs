using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Core.Frames;

namespace CS2RuntimeAssetAuditor.Core.DiagnosticLog
{
    /// <summary>
    /// Builds diagnostic-log rows: the mod's own frame, simulation and pathfinding interval plus the latest
    /// reading of each normal-monitoring recorder and the managed-memory change.
    /// </summary>
    public sealed class DiagnosticLogAccumulator
    {
        private readonly RuntimeIntervalAccumulator _interval;
        private readonly DiagnosticRecorderColumn[] _recorderColumns;
        private readonly Dictionary<string, int> _recorderColumnIndex;
        private readonly double?[] _recorderValues;
        private readonly SampleWindow _modUpdateMs;
        private long _lastModFrame = long.MinValue;
        private double _modUpdateMaxMs = -1d;
        private string? _modUpdateMaxSystem;
        private double _modUpdateMaxSystemMs;
        private int? _previousGcCount;

        public DiagnosticLogAccumulator(IReadOnlyList<DiagnosticRecorderColumn>? recorderColumns, int windowCapacity = RuntimeIntervalAccumulator.DefaultWindowCapacity)
        {
            _interval = new RuntimeIntervalAccumulator(windowCapacity);
            _recorderColumns = recorderColumns == null ? Array.Empty<DiagnosticRecorderColumn>() : new List<DiagnosticRecorderColumn>(recorderColumns).ToArray();
            _recorderColumnIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < _recorderColumns.Length; i++)
                _recorderColumnIndex[_recorderColumns[i].Id] = i;
            _recorderValues = new double?[_recorderColumns.Length];
            _modUpdateMs = new SampleWindow(windowCapacity, includeZero: true);
        }

        public IReadOnlyList<DiagnosticRecorderColumn> RecorderColumns => _recorderColumns;
        public int FrameCount => _interval.FrameCount;

        public void AddFrame(in RuntimeFrameSample frame) => _interval.AddFrame(frame);

        /// <summary>
        /// Adds the mod's own update time for one completed frame. The same frame passed again is ignored, so a
        /// caller can pass the latest completed frame every frame.
        /// </summary>
        public void AddModFrameCost(in ModFrameCost cost)
        {
            if (!cost.HasValue || cost.FrameIndex == _lastModFrame)
                return;
            _lastModFrame = cost.FrameIndex;
            _modUpdateMs.Add(cost.TotalMs);
            if (cost.TotalMs > _modUpdateMaxMs)
            {
                _modUpdateMaxMs = cost.TotalMs;
                _modUpdateMaxSystem = cost.SlowestSystem;
                _modUpdateMaxSystemMs = cost.SlowestSystemMs;
            }
        }

        /// <summary>Keeps the latest reading of each recorder column; readings for other recorders are ignored.</summary>
        public void AddRecorderReadings(IReadOnlyDictionary<string, RecorderReading>? readings)
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
                Interval = _interval.Complete(),
                ManagedHeapMiB = context.ManagedHeapBytes.HasValue ? context.ManagedHeapBytes.Value / (1024d * 1024d) : (double?)null,
                GcCollections = GcDelta(context.GcCollectionCount),
                AutoSaveStarts = context.AutoSaveStarts,
                CaptureState = context.CaptureState,
                CaptureTrigger = context.CaptureTrigger,
                CaptureId = context.CaptureId,
                RecorderValues = (double?[])_recorderValues.Clone(),
                ModUpdateMs = _modUpdateMs.Summarize(),
                ModUpdateSlowestSystem = _modUpdateMaxSystem,
                ModUpdateSlowestSystemMs = _modUpdateMaxSystem == null ? (double?)null : _modUpdateMaxSystemMs
            };
            _modUpdateMs.Reset();
            _modUpdateMaxMs = -1d;
            _modUpdateMaxSystem = null;
            _modUpdateMaxSystemMs = 0d;
            for (var i = 0; i < _recorderValues.Length; i++)
                _recorderValues[i] = null;
            return row;
        }

        private int? GcDelta(int? count)
        {
            var previous = _previousGcCount;
            _previousGcCount = count;
            if (!count.HasValue || !previous.HasValue || count.Value < previous.Value)
                return null;
            return count.Value - previous.Value;
        }
    }
}
