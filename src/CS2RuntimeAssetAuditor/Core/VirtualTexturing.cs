using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Core
{
    /// <summary>
    /// One read of the game's virtual-texturing streaming state (Colossal.IO.AssetDatabase.VirtualTexturing
    /// .TextureStreamingSystem, 1.6.2f1). The mod only reads properties the system already maintains; it issues no
    /// GPU readback and does not touch the VT cache. Values the system has not produced yet are null, never 0.
    /// </summary>
    public sealed class VirtualTexturingReading
    {
        private VirtualTexturingReading(
            double? workingSetLodBias, int? mipBias, int? busyRequests, int? availableRequests,
            int? materialsLeftToLoad, int? duplicatesLeftToProcess, string? unavailableReason)
        {
            WorkingSetLodBias = workingSetLodBias;
            MipBias = mipBias;
            BusyRequests = busyRequests;
            AvailableRequests = availableRequests;
            MaterialsLeftToLoad = materialsLeftToLoad;
            DuplicatesLeftToProcess = duplicatesLeftToProcess;
            UnavailableReason = unavailableReason;
        }

        /// <summary>
        /// workingSetLodBias as the game reads it back from the GPU: a factor the game turns into a shader LOD bias of
        /// log2(factor), so 1 adds no bias and larger values make VT sampling use coarser mips. Null until the first
        /// readback completed (the property starts at 0). The readback is asynchronous, so the value is a few frames old.
        /// </summary>
        public double? WorkingSetLodBias { get; }

        /// <summary>log2 of <see cref="WorkingSetLodBias"/>: the bias in mip levels the game applies.</summary>
        public double? WorkingSetLodBiasLevels => WorkingSetLodBias.HasValue ? Math.Log(WorkingSetLodBias.Value, 2d) : (double?)null;

        /// <summary>The mip bias VT was initialized with (from the texture-quality setting); null before VT is initialized.</summary>
        public int? MipBias { get; }

        /// <summary>Tile-streaming read requests in flight / pooled and idle.</summary>
        public int? BusyRequests { get; }
        public int? AvailableRequests { get; }

        /// <summary>VT materials still queued for their initial load / duplicate materials still queued for initialization.</summary>
        public int? MaterialsLeftToLoad { get; }
        public int? DuplicatesLeftToProcess { get; }

        /// <summary>Why nothing could be read (system missing or the read failed); null when the read succeeded.</summary>
        public string? UnavailableReason { get; }

        public static VirtualTexturingReading FromSystem(
            float workingSetLodBias, int mipBias, int busyRequests, int availableRequests,
            int materialsLeftToLoad, int duplicatesLeftToProcess)
        {
            return new VirtualTexturingReading(
                workingSetLodBias > 0f && !float.IsNaN(workingSetLodBias) && !float.IsInfinity(workingSetLodBias) ? workingSetLodBias : (double?)null,
                mipBias >= 0 ? mipBias : (int?)null,
                busyRequests >= 0 ? busyRequests : (int?)null,
                availableRequests >= 0 ? availableRequests : (int?)null,
                materialsLeftToLoad >= 0 ? materialsLeftToLoad : (int?)null,
                duplicatesLeftToProcess >= 0 ? duplicatesLeftToProcess : (int?)null,
                null);
        }

        public static VirtualTexturingReading Unavailable(string reason)
            => new VirtualTexturingReading(null, null, null, null, null, null, string.IsNullOrWhiteSpace(reason) ? "Not read." : reason);
    }

    public static class VirtualTexturingMetrics
    {
        public const string WorkingSetLodBias = "vt.workingSetLodBias";
        public const string WorkingSetLodBiasLevels = "vt.workingSetLodBiasLevels";
        public const string MipBias = "vt.mipBias";
        public const string BusyRequests = "vt.busyRequests";
        public const string AvailableRequests = "vt.availableRequests";
        public const string MaterialsLeftToLoad = "vt.materialsLeftToLoad";

        public const string Limitation =
            "Virtual-texturing values describe how the game's texture streaming is coping with the textures in view, read from the game's own state. A working-set LOD bias above 1 means the game is sampling coarser texture mips to fit the VT working set; it is not VRAM usage and on its own does not show a VRAM shortage. Compare it over time with camera moves, draw distance and the texture-quality setting.";

        private const string NoReadback = "The game has not completed a working-set readback yet (VT starting up or no frame rendered in this city).";
        private const string NotInitialized = "Virtual texturing is not initialized yet.";

        private static readonly IReadOnlyDictionary<string, string> Meanings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkingSetLodBias] = "Working-set LOD bias factor read back from the GPU (1 = no extra bias). A few frames old.",
            [WorkingSetLodBiasLevels] = "The same bias in mip levels (log2 of the factor); above 0 the game samples coarser VT mips.",
            [MipBias] = "Mip bias VT was initialized with; follows the texture-quality setting.",
            [BusyRequests] = "Tile-streaming read requests in flight.",
            [AvailableRequests] = "Idle tile-streaming request slots in the pool.",
            [MaterialsLeftToLoad] = "VT materials still waiting for their initial load (non-zero while textures are still streaming in after a load)."
        };

        public static string? Meaning(string id) => Meanings.TryGetValue(id, out var meaning) ? meaning : null;

        /// <summary>The metrics of one reading in a fixed order; a value the game has not produced is unavailable with the reason.</summary>
        public static IReadOnlyList<NamedMetricValue> ToMetrics(VirtualTexturingReading? reading)
        {
            var failure = reading == null ? "Not sampled." : reading.UnavailableReason;
            return new[]
            {
                Value(WorkingSetLodBias, reading?.WorkingSetLodBias, failure ?? NoReadback, MetricUnits.Factor),
                Value(WorkingSetLodBiasLevels, reading?.WorkingSetLodBiasLevels, failure ?? NoReadback, MetricUnits.MipLevels),
                Value(MipBias, reading?.MipBias, failure ?? NotInitialized, MetricUnits.Count),
                Value(BusyRequests, reading?.BusyRequests, failure ?? NotInitialized, MetricUnits.Count),
                Value(AvailableRequests, reading?.AvailableRequests, failure ?? NotInitialized, MetricUnits.Count),
                Value(MaterialsLeftToLoad, reading?.MaterialsLeftToLoad, failure ?? NotInitialized, MetricUnits.Count)
            };
        }

        private static NamedMetricValue Value(string id, double? value, string reason, string unit)
            => value.HasValue
                ? NamedMetricValue.Available(id, value.Value, MetricConfidence.Full, unit)
                : NamedMetricValue.Unavailable(id, reason);
    }
}
