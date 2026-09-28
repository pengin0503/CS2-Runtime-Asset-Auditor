using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core
{
    /// <summary>
    /// Finalizes per-system timing for a completed capture. Verified profiler-marker timing remains
    /// authoritative; managed synchronous timing may fill systems that have no usable marker sample.
    /// Job/Burst worker time is never inferred from the managed fallback.
    /// </summary>
    public static class CaptureSystemTimingFinalizer
    {
        public static SystemTimingSnapshot Apply(
            CaptureSession capture,
            IEnumerable<SystemDescriptor> systems,
            IEnumerable<RecorderDescriptor> recorders)
        {
            return Apply(capture, systems, recorders, managedFallback: null);
        }

        public static SystemTimingSnapshot Apply(
            CaptureSession capture,
            IEnumerable<SystemDescriptor> systems,
            IEnumerable<RecorderDescriptor> recorders,
            SystemTimingSnapshot managedFallback)
        {
            if (capture == null)
                throw new ArgumentNullException(nameof(capture));

            var systemArray = (systems ?? Array.Empty<SystemDescriptor>()).Where(x => x != null).ToArray();
            var recorderArray = (recorders ?? Array.Empty<RecorderDescriptor>()).Where(x => x != null).ToArray();
            var markerTiming = SystemMarkerTimingProjector.Project(systemArray, recorderArray, capture);
            var timing = SystemTimingSnapshotMerger.Merge(markerTiming, managedFallback);
            capture.SetSystemTiming(timing);

            if (markerTiming.Systems.Count == 0)
            {
                var diagnostics = SystemMarkerTimingProjector.Diagnose(systemArray, recorderArray, capture);
                var sampledMarkers = capture.MarkerSamples.Count;
                var detail = $"catalogSystems={diagnostics.CatalogSystemCount}, profilerMarkers={diagnostics.ProfilerMarkerCount}, timeMarkers={diagnostics.TimeMarkerCount}, uniqueMatches={diagnostics.UniqueMatchCount}, ambiguousMatches={diagnostics.AmbiguousMatchCount}, uniqueMatchesWithoutSamples={diagnostics.UniqueMatchesWithoutSamples}, sampledMarkers={sampledMarkers}, capturedMarkers={capture.MarkerCoverage.Captured}/{capture.MarkerCoverage.Discovered}.";

                if (managedFallback?.Systems?.Count > 0)
                {
                    capture.AddWarning(
                        "ECS profiler markers produced no usable per-system samples; using managed synchronous SystemBase timing fallback. "
                        + "Job/Burst worker time remains unattributed. " + detail);
                }
                else
                {
                    capture.AddWarning(
                        "System timing unavailable: " + detail
                        + " No uniquely matching TimeNanoseconds ECS system marker produced a usable sample, and no managed synchronous fallback sample was available.");
                }
            }
            else
            {
                var managedRows = timing.Systems.Count(x => x.Confidence == MetricConfidence.Managed);
                if (managedRows > 0)
                {
                    var nativeRows = timing.Systems.Count(x => x.Confidence == MetricConfidence.Full);
                    capture.AddWarning(
                        $"System timing mixes native ECS marker timing ({nativeRows} systems) with managed synchronous SystemBase fallback ({managedRows} systems). "
                        + "Managed rows exclude Job/Burst worker time, so Systems/Mods totals do not represent total CPU cost.");
                }
            }

            return timing;
        }
    }
}
