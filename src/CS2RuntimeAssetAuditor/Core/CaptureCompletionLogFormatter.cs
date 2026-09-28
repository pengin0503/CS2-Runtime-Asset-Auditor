using System;
using System.Globalization;

namespace CS2RuntimeAssetAuditor.Core
{
    public static class CaptureCompletionLogFormatter
    {
        private const double BytesPerMiB = 1024d * 1024d;

        public static string Format(CaptureSession capture)
        {
            if (capture == null)
                return "capture=unavailable";

            var coverage = capture.MarkerCoverage;
            var systems = capture.SystemTiming?.Systems?.Count ?? 0;
            var memoryDelta = capture.ProfilerMemoryDeltaBytes.HasValue
                ? (capture.ProfilerMemoryDeltaBytes.Value / BytesPerMiB).ToString("0.#", CultureInfo.InvariantCulture)
                : "unavailable";
            var overhead = (capture.MaxProfilerOverheadShare * 100d).ToString("0.00", CultureInfo.InvariantCulture) + "%";

            return string.Format(
                CultureInfo.InvariantCulture,
                "capture={0} trigger={1} markers(sampled/activated/attempted/discovered)={2}/{3}/{4}/{5} systems={6} overhead={7} profilerMemoryDeltaMiB={8} warnings={9}",
                capture.Id,
                capture.Trigger.Kind,
                coverage.Sampled,
                coverage.Activated,
                coverage.Attempted,
                coverage.Discovered,
                systems,
                overhead,
                memoryDelta,
                capture.Warnings.Count);
        }
    }
}
