using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Core.Frames
{
    /// <summary>Wall time the mod's own systems spent in one rendered frame.</summary>
    public readonly struct ModFrameCost
    {
        public ModFrameCost(long frameIndex, double totalMs, string? slowestSystem, double slowestSystemMs)
        {
            FrameIndex = frameIndex;
            TotalMs = totalMs;
            SlowestSystem = slowestSystem;
            SlowestSystemMs = slowestSystemMs;
        }

        public long FrameIndex { get; }
        public double TotalMs { get; }
        public string? SlowestSystem { get; }
        public double SlowestSystemMs { get; }
        public bool HasValue => SlowestSystem != null;
    }

    /// <summary>A system update that took long enough to be written to the mod log.</summary>
    public readonly struct SlowModUpdate
    {
        public SlowModUpdate(string system, double milliseconds, long frameIndex, int suppressedSinceLastReport)
        {
            System = system;
            Milliseconds = milliseconds;
            FrameIndex = frameIndex;
            SuppressedSinceLastReport = suppressedSinceLastReport;
        }

        public string System { get; }
        public double Milliseconds { get; }
        public long FrameIndex { get; }

        /// <summary>Slow updates of the same system that were not reported because of the rate limit.</summary>
        public int SuppressedSinceLastReport { get; }
    }

    /// <summary>
    /// Adds up the update time of the mod's systems per rendered frame, so a long frame can be split into the
    /// mod's share and the rest of the game. A frame's total is complete once a system reports for a later
    /// frame, so readers see the previous frame's cost.
    /// </summary>
    public sealed class ModUpdateCostTracker
    {
        public const double DefaultSlowUpdateMilliseconds = 100d;
        public const double DefaultReportIntervalSeconds = 10d;

        private readonly double _slowUpdateMilliseconds;
        private readonly double _reportIntervalSeconds;
        private readonly Dictionary<string, double> _lastReportAt = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _suppressed = new Dictionary<string, int>(StringComparer.Ordinal);

        private long _frameIndex = long.MinValue;
        private double _totalMs;
        private string? _slowestSystem;
        private double _slowestSystemMs;

        public ModUpdateCostTracker(
            double slowUpdateMilliseconds = DefaultSlowUpdateMilliseconds,
            double reportIntervalSeconds = DefaultReportIntervalSeconds)
        {
            if (slowUpdateMilliseconds <= 0d) throw new ArgumentOutOfRangeException(nameof(slowUpdateMilliseconds));
            if (reportIntervalSeconds < 0d) throw new ArgumentOutOfRangeException(nameof(reportIntervalSeconds));
            _slowUpdateMilliseconds = slowUpdateMilliseconds;
            _reportIntervalSeconds = reportIntervalSeconds;
        }

        /// <summary>The last frame whose total is complete; no value until a second frame has started.</summary>
        public ModFrameCost LastCompletedFrame { get; private set; }

        /// <summary>
        /// Adds one system update. Returns a report when the update was slow and the system has not been
        /// reported within the report interval; later slow updates in that interval are only counted.
        /// </summary>
        public SlowModUpdate? Add(long frameIndex, string system, double milliseconds, double nowSeconds)
        {
            if (string.IsNullOrEmpty(system) || double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0d)
                return null;

            if (frameIndex != _frameIndex)
            {
                if (_slowestSystem != null)
                    LastCompletedFrame = new ModFrameCost(_frameIndex, _totalMs, _slowestSystem, _slowestSystemMs);
                _frameIndex = frameIndex;
                _totalMs = 0d;
                _slowestSystem = null;
                _slowestSystemMs = 0d;
            }

            _totalMs += milliseconds;
            if (_slowestSystem == null || milliseconds > _slowestSystemMs)
            {
                _slowestSystem = system;
                _slowestSystemMs = milliseconds;
            }

            if (milliseconds < _slowUpdateMilliseconds)
                return null;
            if (_lastReportAt.TryGetValue(system, out var last) && nowSeconds - last < _reportIntervalSeconds)
            {
                _suppressed[system] = (_suppressed.TryGetValue(system, out var count) ? count : 0) + 1;
                return null;
            }

            _lastReportAt[system] = nowSeconds;
            var suppressed = _suppressed.TryGetValue(system, out var skipped) ? skipped : 0;
            _suppressed[system] = 0;
            return new SlowModUpdate(system, milliseconds, frameIndex, suppressed);
        }
    }
}
