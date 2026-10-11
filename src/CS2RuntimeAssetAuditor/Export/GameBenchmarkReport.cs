using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace CS2RuntimeAssetAuditor.Export
{
    /// <summary>
    /// Min / average / p95 / max of one frame-time series of the game's benchmark, in milliseconds. The game keeps only
    /// positive finite samples; with none left it stores zeros, which this type reports as absent.
    /// </summary>
    [DataContract]
    public sealed class ReportBenchmarkStats
    {
        [DataMember(Name = "min", Order = 1)] public double Min { get; set; }
        [DataMember(Name = "average", Order = 2)] public double Average { get; set; }
        [DataMember(Name = "p95", Order = 3)] public double P95 { get; set; }
        [DataMember(Name = "max", Order = 4)] public double Max { get; set; }

        public static ReportBenchmarkStats? From(double min, double average, double p95, double max)
            => average > 0 && IsFinite(min) && IsFinite(average) && IsFinite(p95) && IsFinite(max)
                ? new ReportBenchmarkStats { Min = min, Average = average, P95 = p95, Max = max }
                : null;

        internal ReportBenchmarkStats Copy() => new ReportBenchmarkStats { Min = Min, Average = Average, P95 = P95, Max = Max };

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// The latest result of the game's own benchmark (Options > Benchmark, Game.Settings.BenchmarkSettings.latestResult in
    /// 1.6.2f1): a bundled save played along a fixed camera path, so it is a baseline of this PC and these settings, not
    /// of the player's city. It is reported as found; the mod does not run the benchmark.
    /// </summary>
    [DataContract]
    public sealed class ReportGameBenchmark
    {
        public const string Meaning =
            "Result of the game's own benchmark (a bundled save on a fixed camera path): a baseline of this PC and these settings, not of the city in this report. "
            + "cpuGameMs is the game's main-thread time minus its render-thread time, and gpuBoundPercent counts frames whose GPU time exceeded the main-thread time; "
            + "neither is the same measure as this mod's frame-time classification. The mod keeps monitoring while the benchmark runs, so its own cost is included.";

        public const string DifferentGameVersionWarning =
            "The game's benchmark result was recorded on a different game version than this report; compare it with care.";

        [DataMember(Name = "recordedAtUtc", Order = 1, EmitDefaultValue = false)] public string? RecordedAtUtc { get; set; }
        [DataMember(Name = "gameVersion", Order = 2, EmitDefaultValue = false)] public string? GameVersion { get; set; }
        [DataMember(Name = "graphicsQuality", Order = 3, EmitDefaultValue = false)] public string? GraphicsQuality { get; set; }
        [DataMember(Name = "screenResolution", Order = 4, EmitDefaultValue = false)] public string? ScreenResolution { get; set; }
        [DataMember(Name = "cpuModel", Order = 5, EmitDefaultValue = false)] public string? CpuModel { get; set; }
        [DataMember(Name = "gpuModel", Order = 6, EmitDefaultValue = false)] public string? GpuModel { get; set; }
        /// <summary>The game stores SystemInfo.graphicsDeviceVersion here (the graphics API string), not a driver version.</summary>
        [DataMember(Name = "graphicsDeviceVersion", Order = 7, EmitDefaultValue = false)] public string? GraphicsDeviceVersion { get; set; }
        [DataMember(Name = "systemMemoryMb", Order = 8, EmitDefaultValue = false)] public int? SystemMemoryMb { get; set; }
        [DataMember(Name = "videoMemoryMb", Order = 9, EmitDefaultValue = false)] public int? VideoMemoryMb { get; set; }
        [DataMember(Name = "framesRendered", Order = 10)] public int FramesRendered { get; set; }
        [DataMember(Name = "simulationTicks", Order = 11)] public int SimulationTicks { get; set; }
        [DataMember(Name = "averageFps", Order = 12)] public double AverageFps { get; set; }
        /// <summary>Null when the benchmark had no GPU timings (every GPU sample was 0, so no frame could count as GPU bound).</summary>
        [DataMember(Name = "gpuBoundPercent", Order = 13, EmitDefaultValue = false)] public double? GpuBoundPercent { get; set; }
        [DataMember(Name = "mapLoadingSeconds", Order = 14, EmitDefaultValue = false)] public double? MapLoadingSeconds { get; set; }
        [DataMember(Name = "cpuFrameMs", Order = 15, EmitDefaultValue = false)] public ReportBenchmarkStats? CpuFrameMs { get; set; }
        [DataMember(Name = "cpuGameMs", Order = 16, EmitDefaultValue = false)] public ReportBenchmarkStats? CpuGameMs { get; set; }
        [DataMember(Name = "cpuRenderMs", Order = 17, EmitDefaultValue = false)] public ReportBenchmarkStats? CpuRenderMs { get; set; }
        [DataMember(Name = "gpuMs", Order = 18, EmitDefaultValue = false)] public ReportBenchmarkStats? GpuMs { get; set; }
        [DataMember(Name = "note", Order = 19, EmitDefaultValue = false)] public string? Note { get; set; }

        /// <summary>
        /// Builds the report entry from the stored values; null when the game holds no completed run (hasData false or no
        /// frame rendered, as in the defaults written before the first run). lastUpdatedTicks is DateTime.UtcNow.Ticks.
        /// </summary>
        public static ReportGameBenchmark? Create(bool hasData, int framesRendered, long lastUpdatedTicks)
        {
            if (!hasData || framesRendered <= 0)
                return null;
            return new ReportGameBenchmark
            {
                FramesRendered = framesRendered,
                RecordedAtUtc = lastUpdatedTicks > 0 && lastUpdatedTicks <= DateTime.MaxValue.Ticks
                    ? new DateTime(lastUpdatedTicks, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture)
                    : null,
                Note = Meaning
            };
        }

        public static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

        public static int? Positive(int value) => value > 0 ? value : (int?)null;

        internal ReportGameBenchmark SanitizedCopy() => new ReportGameBenchmark
        {
            RecordedAtUtc = RecordedAtUtc,
            GameVersion = ReportPrivacy.Sanitize(GameVersion),
            GraphicsQuality = ReportPrivacy.Sanitize(GraphicsQuality),
            ScreenResolution = ReportPrivacy.Sanitize(ScreenResolution),
            CpuModel = ReportPrivacy.Sanitize(CpuModel),
            GpuModel = ReportPrivacy.Sanitize(GpuModel),
            GraphicsDeviceVersion = ReportPrivacy.Sanitize(GraphicsDeviceVersion),
            SystemMemoryMb = SystemMemoryMb,
            VideoMemoryMb = VideoMemoryMb,
            FramesRendered = FramesRendered,
            SimulationTicks = SimulationTicks,
            AverageFps = AverageFps,
            GpuBoundPercent = GpuBoundPercent,
            MapLoadingSeconds = MapLoadingSeconds,
            CpuFrameMs = CpuFrameMs?.Copy(),
            CpuGameMs = CpuGameMs?.Copy(),
            CpuRenderMs = CpuRenderMs?.Copy(),
            GpuMs = GpuMs?.Copy(),
            Note = ReportPrivacy.Sanitize(Note)
        };
    }
}
