using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Core
{
    public sealed class RenderCounterDefinition
    {
        public RenderCounterDefinition(string id, string markerName, string unitType, string meaning)
        {
            Id = id;
            MarkerName = markerName;
            UnitType = unitType;
            Meaning = meaning;
        }

        /// <summary>Stable id used in the UI, the report and the advisor evidence.</summary>
        public string Id { get; }

        /// <summary>The Unity "Render" profiler counter (verified in the 1.6.2f1 retail build's marker inventory).</summary>
        public string MarkerName { get; }

        public string UnitType { get; }
        public string Meaning { get; }
    }

    /// <summary>
    /// Unity's rendering statistics counters, read by normal monitoring once per sample (the value of the last
    /// rendered frame). They count work the CPU submitted for drawing; they are not GPU time and not the cost of an
    /// individual asset. Draw calls and batches are kept apart: Unity defines them differently even where the values
    /// are equal. No counter here measures VRAM usage.
    /// </summary>
    public static class RenderCounters
    {
        public const string UnavailableReason = "The game did not report this rendering counter in this sample.";

        public const string Limitation =
            "Rendering counters count what the CPU submitted for the last frame. They are not GPU time, not VRAM usage and not the cost of an individual asset; compare them across the same city, camera and settings.";

        public static readonly IReadOnlyList<RenderCounterDefinition> All = new[]
        {
            new RenderCounterDefinition("render.drawCalls", "Draw Calls Count", "Count",
                "Draw calls submitted to the graphics API in the last frame."),
            new RenderCounterDefinition("render.batches", "Batches Count", "Count",
                "Batches Unity submitted in the last frame; defined separately from draw calls."),
            new RenderCounterDefinition("render.setPassCalls", "SetPass Calls Count", "Count",
                "Shader pass (material state) changes in the last frame."),
            new RenderCounterDefinition("render.triangles", "Triangles Count", "Count",
                "Triangles submitted in the last frame, across all cameras and passes, including shadows."),
            new RenderCounterDefinition("render.vertices", "Vertices Count", "Count",
                "Vertices submitted in the last frame, across all cameras and passes, including shadows."),
            new RenderCounterDefinition("render.shadowCasters", "Shadow Casters Count", "Count",
                "Objects drawn into shadow maps in the last frame."),
            new RenderCounterDefinition("render.renderTexturesBytes", "Render Textures Bytes", "Bytes",
                "Memory of render textures (render targets); part of GPU memory, not total VRAM usage."),
            new RenderCounterDefinition("render.usedBuffersBytes", "Used Buffers Bytes", "Bytes",
                "Memory of graphics buffers in use; part of GPU memory, not total VRAM usage.")
        };

        /// <summary>The marker names normal monitoring activates, one recorder each.</summary>
        public static IEnumerable<string> MarkerNames()
        {
            foreach (var counter in All)
                yield return counter.MarkerName;
        }

        /// <summary>
        /// The counters of one sample. A counter without a reading is unavailable, never 0. Readings are keyed by
        /// recorder id ("Category\u001fName").
        /// </summary>
        public static IReadOnlyList<NamedMetricValue> Read(IReadOnlyDictionary<string, RecorderReading>? readings)
        {
            var byName = new Dictionary<string, RecorderReading>(StringComparer.Ordinal);
            if (readings != null)
                foreach (var pair in readings)
                    if (pair.Value.Count > 0)
                        byName[MarkerNameOf(pair.Key)] = pair.Value;

            var result = new List<NamedMetricValue>(All.Count);
            foreach (var counter in All)
            {
                result.Add(byName.TryGetValue(counter.MarkerName, out var reading)
                    ? NamedMetricValue.Available(counter.Id, reading.Value, MetricConfidence.Full, counter.UnitType)
                    : NamedMetricValue.Unavailable(counter.Id, UnavailableReason));
            }
            return result;
        }

        public static bool IsRenderCounter(string recorderId)
        {
            var name = MarkerNameOf(recorderId);
            foreach (var counter in All)
                if (string.Equals(counter.MarkerName, name, StringComparison.Ordinal))
                    return true;
            return false;
        }

        public static RenderCounterDefinition? Find(string id)
        {
            foreach (var counter in All)
                if (string.Equals(counter.Id, id, StringComparison.Ordinal))
                    return counter;
            return null;
        }

        private static string MarkerNameOf(string recorderId)
        {
            if (string.IsNullOrEmpty(recorderId))
                return string.Empty;
            var separator = recorderId.LastIndexOf('\u001f');
            return separator < 0 ? recorderId : recorderId.Substring(separator + 1);
        }
    }

    public enum VideoMemoryCounterMeaning { Unavailable, ReportedCapacity, Unclassified }

    /// <summary>
    /// Unity's "Render/Video Memory Bytes" counter. On the tested retail system (1.6.2f1, RX 7800 XT) it stayed at
    /// 16,962,813,952 B, exactly the 16,177 MB SystemInfo.graphicsMemorySize reports, so it is the GPU memory capacity
    /// rather than usage. Where it does not match the reported capacity its meaning is unverified, and it is never
    /// shown or compared as VRAM usage.
    /// </summary>
    public static class VideoMemoryCounter
    {
        public const string MarkerName = "Video Memory Bytes";
        private const double MiB = 1024d * 1024d;

        public static VideoMemoryCounterMeaning Classify(double? counterBytes, int? reportedCapacityMegabytes)
        {
            if (!counterBytes.HasValue || counterBytes.Value <= 0)
                return VideoMemoryCounterMeaning.Unavailable;
            if (!reportedCapacityMegabytes.HasValue || reportedCapacityMegabytes.Value <= 0)
                return VideoMemoryCounterMeaning.Unclassified;
            // graphicsMemorySize is whole megabytes (MiB), so allow one unit of rounding.
            return Math.Abs(counterBytes.Value / MiB - reportedCapacityMegabytes.Value) <= 1d
                ? VideoMemoryCounterMeaning.ReportedCapacity
                : VideoMemoryCounterMeaning.Unclassified;
        }

        public static string Describe(VideoMemoryCounterMeaning meaning)
        {
            switch (meaning)
            {
                case VideoMemoryCounterMeaning.ReportedCapacity:
                    return "Equals the GPU memory capacity the driver reports (SystemInfo.graphicsMemorySize), so it is capacity, not VRAM usage.";
                case VideoMemoryCounterMeaning.Unclassified:
                    return "Does not match the reported GPU memory capacity; its meaning is not verified on this system, so it is not used as VRAM usage.";
                default:
                    return "Not recorded.";
            }
        }
    }
}
