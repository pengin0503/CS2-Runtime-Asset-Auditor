using System;
using System.Globalization;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core.Loading
{
    /// <summary>
    /// One-line log entries for the loading trace. The trace itself lives only in memory until a report is
    /// exported, so these lines are what remains in the mod log when the game crashes or quits during a load:
    /// the last progress line shows the stage reached and the peaks sampled up to that point.
    /// </summary>
    public static class LoadingTraceLogFormatter
    {
        private const double BytesPerMiB = 1024d * 1024d;

        public static string FormatProgress(LoadingTraceSnapshot trace, string milestone)
        {
            if (trace == null) return "Loading trace: unavailable";
            var at = trace.Milestones.LastOrDefault(item => item.Name == milestone)?.AtUtc ?? trace.StartedAtUtc;
            return string.Format(CultureInfo.InvariantCulture,
                "Loading trace: milestone={0} purpose={1} elapsed={2} {3}",
                milestone, trace.Purpose ?? "unknown", Seconds(at - trace.StartedAtUtc), Peaks(trace));
        }

        public static string FormatSummary(LoadingTraceSnapshot trace)
        {
            if (trace == null) return "Loading trace: unavailable";
            var ended = trace.EndedAtUtc;
            var summary = string.Format(CultureInfo.InvariantCulture,
                "Loading trace finished: outcome={0} purpose={1} duration={2} observations={3} {4} registeredAssets={5} cacheTransitionObserved={6}",
                trace.Outcome, trace.Purpose ?? "unknown",
                ended.HasValue ? Seconds(ended.Value - trace.StartedAtUtc) : "unavailable",
                trace.ObservationCount, Peaks(trace),
                trace.End?.RegisteredAssetCount?.ToString(CultureInfo.InvariantCulture) ?? "unavailable",
                trace.ObservedUncachedToCachedTransition ? "true" : "false");
            return trace.InterruptionReason == null ? summary : summary + " reason=" + trace.InterruptionReason;
        }

        private static string Peaks(LoadingTraceSnapshot trace) => string.Join(" ",
            Peak("peakUnityMiB", trace.PeakUnityAllocated, trace.StartedAtUtc),
            Peak("peakWorkingSetMiB", trace.PeakProcessWorkingSet, trace.StartedAtUtc),
            Peak("peakGraphicsDriverMiB", trace.PeakGraphicsDriverAllocated, trace.StartedAtUtc));

        private static string Peak(string name, LoadingPeak peak, DateTimeOffset startedAtUtc)
        {
            if (!peak.Bytes.HasValue) return name + "=unavailable";
            var value = (peak.Bytes.Value / BytesPerMiB).ToString("0.#", CultureInfo.InvariantCulture);
            return peak.AtUtc.HasValue ? $"{name}={value}@{Seconds(peak.AtUtc.Value - startedAtUtc)}" : $"{name}={value}";
        }

        private static string Seconds(TimeSpan elapsed) =>
            Math.Max(0d, elapsed.TotalSeconds).ToString("0.0", CultureInfo.InvariantCulture) + "s";
    }
}
