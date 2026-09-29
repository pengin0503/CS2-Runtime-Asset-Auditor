using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Core.Frames;

namespace CS2RuntimeAssetAuditor.Core
{
    public sealed class GlobalMetricsSnapshot
    {
        public GlobalMetricsSnapshot(
            double timestampSeconds,
            double selectedSpeed,
            double actualSpeed,
            IReadOnlyDictionary<string, RecorderReading> recorderReadings,
            IReadOnlyDictionary<string, string> recorderUnits = null,
            RuntimeInterval? frameInterval = null)
        {
            TimestampSeconds = timestampSeconds;
            SelectedSpeed = selectedSpeed;
            ActualSpeed = actualSpeed;
            Efficiency = SimulationEfficiency.Calculate(selectedSpeed, actualSpeed);
            RecorderReadings = recorderReadings ?? new Dictionary<string, RecorderReading>();
            RecorderUnits = recorderUnits ?? new Dictionary<string, string>();
            FrameInterval = frameInterval;
        }

        public double TimestampSeconds { get; }
        public double SelectedSpeed { get; }
        public double ActualSpeed { get; }
        public double Efficiency { get; }
        public IReadOnlyDictionary<string, RecorderReading> RecorderReadings { get; }
        public IReadOnlyDictionary<string, string> RecorderUnits { get; }

        /// <summary>
        /// Frames, frame timings and simulation steps the mod measured itself since the previous sample; null when
        /// the sample was not taken by the per-frame collector.
        /// </summary>
        public RuntimeInterval? FrameInterval { get; }
    }
}
