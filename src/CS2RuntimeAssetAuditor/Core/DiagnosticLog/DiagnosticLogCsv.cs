using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CS2RuntimeAssetAuditor.Core.Frames;

namespace CS2RuntimeAssetAuditor.Core.DiagnosticLog
{
    /// <summary>
    /// CSV layout of the diagnostic log. Numbers use the invariant culture so a player's locale (for example
    /// a decimal comma) cannot change the file; an absent value is an empty cell, never 0.
    /// </summary>
    public static class DiagnosticLogCsv
    {
        public const string RecorderColumnPrefix = "recorder:";

        public static readonly IReadOnlyList<string> FixedColumns = new[]
        {
            "utcTime",
            "elapsedSeconds",
            "intervalSeconds",
            "frames",
            "frameMsMedian",
            "frameMsP95",
            "frameMsMax",
            "frameTimingSamples",
            "cpuMainMsMedian",
            "cpuMainMsP95",
            "cpuRenderMsMedian",
            "cpuRenderMsP95",
            "gpuMsMedian",
            "gpuMsP95",
            "presentWaitMsMedian",
            "presentWaitMsP95",
            "selectedSpeed",
            "actualSpeedMean",
            "efficiencyMean",
            "pausedFrames",
            "simSteps",
            "simStepsPerSecond",
            "simMaxStepsPerFrame",
            "simFramesAtRenderCap",
            "simFramesWithoutStep",
            "simStepMsMedian",
            "simStepMsMax",
            "performancePreference",
            "pathfindLeadFramesMin",
            "pathfindLowLeadFrames",
            "pathfindPendingRequestsMax",
            "gcCollections",
            "managedHeapMiB",
            "captureState",
            "captureTrigger",
            "captureId",
            "frameRateEfficiencyCeiling",
            "modUpdateMsMedian",
            "modUpdateMsMax",
            "modUpdateSlowestSystem",
            "modUpdateSlowestSystemMs",
            "autoSaveStarts"
        };

        public static readonly IReadOnlyList<string> MarkerInventoryColumns = new[]
        {
            "category",
            "name",
            "unit",
            "dataType",
            "normalMonitoring"
        };

        public static string FormatHeader(IReadOnlyList<DiagnosticRecorderColumn> recorderColumns)
        {
            var fields = new List<string>(FixedColumns);
            if (recorderColumns != null)
            {
                foreach (var column in recorderColumns)
                {
                    var unit = string.IsNullOrEmpty(column.Unit) ? string.Empty : " [" + column.Unit + "]";
                    fields.Add(RecorderColumnPrefix + column.Name + unit);
                }
            }
            return Join(fields);
        }

        public static string FormatRow(DiagnosticLogRow row)
        {
            if (row == null)
                throw new ArgumentNullException(nameof(row));

            var interval = row.Interval ?? new RuntimeInterval();
            var fields = new List<string>(FixedColumns.Count + row.RecorderValues.Count)
            {
                row.UtcTime.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                Number(row.ElapsedSeconds),
                Number(interval.IntervalSeconds),
                Integer(interval.Frames),
                Median(interval.FrameMs),
                P95(interval.FrameMs),
                Max(interval.FrameMs),
                Integer(interval.FrameTimingSamples),
                Median(interval.CpuMainThreadMs),
                P95(interval.CpuMainThreadMs),
                Median(interval.CpuRenderThreadMs),
                P95(interval.CpuRenderThreadMs),
                Median(interval.GpuMs),
                P95(interval.GpuMs),
                Median(interval.PresentWaitMs),
                P95(interval.PresentWaitMs),
                Number(interval.SelectedSpeed),
                Number(interval.ActualSpeedMean),
                Number(interval.EfficiencyMean),
                Integer(interval.PausedFrames),
                Integer(interval.SimulationSteps),
                Number(interval.SimulationStepsPerSecond),
                Integer(interval.SimulationMaxStepsPerFrame),
                Integer(interval.SimulationFramesAtRenderCap),
                Integer(interval.SimulationFramesWithoutStep),
                Median(interval.SimulationStepMs),
                Max(interval.SimulationStepMs),
                interval.PerformancePreference ?? string.Empty,
                interval.PathfindLeadFramesMin.HasValue ? interval.PathfindLeadFramesMin.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                Integer(interval.PathfindLowLeadFrames),
                Integer(interval.PathfindPendingRequestsMax),
                Integer(row.GcCollections),
                Number(row.ManagedHeapMiB),
                row.CaptureState ?? string.Empty,
                row.CaptureTrigger ?? string.Empty,
                row.CaptureId ?? string.Empty,
                Number(interval.FrameRateEfficiencyCeiling),
                Median(row.ModUpdateMs),
                Max(row.ModUpdateMs),
                row.ModUpdateSlowestSystem ?? string.Empty,
                Number(row.ModUpdateSlowestSystemMs),
                Integer(row.AutoSaveStarts)
            };
            foreach (var value in row.RecorderValues)
                fields.Add(Number(value));
            return Join(fields);
        }

        public static string FormatMarkerInventoryHeader() => Join(MarkerInventoryColumns);

        public static string FormatMarkerInventoryRow(RecorderDescriptor descriptor, bool normalMonitoring)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));
            return Join(new[]
            {
                descriptor.Category,
                descriptor.Name,
                descriptor.UnitType,
                descriptor.DataType,
                normalMonitoring ? "true" : "false"
            });
        }

        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
                return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string Join(IEnumerable<string> fields)
        {
            var builder = new StringBuilder();
            var first = true;
            foreach (var field in fields)
            {
                if (!first)
                    builder.Append(',');
                builder.Append(Escape(field));
                first = false;
            }
            return builder.ToString();
        }

        private static string Median(SampleSummary summary) => summary.HasValue ? Number(summary.Median) : string.Empty;
        private static string P95(SampleSummary summary) => summary.HasValue ? Number(summary.P95) : string.Empty;
        private static string Max(SampleSummary summary) => summary.HasValue ? Number(summary.Max) : string.Empty;

        private static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Integer(int? value) => value.HasValue ? Integer(value.Value) : string.Empty;

        private static string Number(double? value)
        {
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
                return string.Empty;
            return value.Value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
