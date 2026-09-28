using System.Collections.Generic;

namespace CS2RuntimeProfiler.Core
{
    public sealed class GlobalMetricsSnapshot
    {
        public GlobalMetricsSnapshot(
            double timestampSeconds,
            double selectedSpeed,
            double actualSpeed,
            IReadOnlyDictionary<string, RecorderReading> recorderReadings,
            IReadOnlyDictionary<string, string> recorderUnits = null)
        {
            TimestampSeconds = timestampSeconds;
            SelectedSpeed = selectedSpeed;
            ActualSpeed = actualSpeed;
            Efficiency = SimulationEfficiency.Calculate(selectedSpeed, actualSpeed);
            RecorderReadings = recorderReadings ?? new Dictionary<string, RecorderReading>();
            RecorderUnits = recorderUnits ?? new Dictionary<string, string>();
        }

        public double TimestampSeconds { get; }
        public double SelectedSpeed { get; }
        public double ActualSpeed { get; }
        public double Efficiency { get; }
        public IReadOnlyDictionary<string, RecorderReading> RecorderReadings { get; }
        public IReadOnlyDictionary<string, string> RecorderUnits { get; }
    }
}
