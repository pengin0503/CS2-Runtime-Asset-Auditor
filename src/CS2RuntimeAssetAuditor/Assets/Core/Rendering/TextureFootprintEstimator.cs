using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;

namespace CS2RuntimeAssetAuditor.Assets.Core.Rendering
{
    public static class TextureFootprintEstimator
    {
        private readonly struct FormatLayout
        {
            public FormatLayout(int blockWidth, int blockHeight, int bytesPerBlock) { BlockWidth = blockWidth; BlockHeight = blockHeight; BytesPerBlock = bytesPerBlock; }
            public int BlockWidth { get; }
            public int BlockHeight { get; }
            public int BytesPerBlock { get; }
        }

        // Keys are GraphicsFormat member names exactly as TextureAsset.format.ToString() produces them.
        // Values that share one enum constant (for example RGB_DXT1_SRGB and RGBA_DXT1_SRGB) are all listed
        // because Enum.ToString() may return either alias.
        private static readonly Dictionary<string, FormatLayout> Layouts = new Dictionary<string, FormatLayout>(StringComparer.Ordinal)
        {
            ["R8_UNorm"] = new FormatLayout(1, 1, 1),
            ["R8G8_UNorm"] = new FormatLayout(1, 1, 2),
            ["R8G8B8A8_UNorm"] = new FormatLayout(1, 1, 4),
            ["R8G8B8A8_SRGB"] = new FormatLayout(1, 1, 4),
            ["R16G16B16A16_SFloat"] = new FormatLayout(1, 1, 8),
            ["RGB_DXT1_UNorm"] = new FormatLayout(4, 4, 8),
            ["RGB_DXT1_SRGB"] = new FormatLayout(4, 4, 8),
            ["RGBA_DXT1_UNorm"] = new FormatLayout(4, 4, 8),
            ["RGBA_DXT1_SRGB"] = new FormatLayout(4, 4, 8),
            ["RGBA_DXT3_UNorm"] = new FormatLayout(4, 4, 16),
            ["RGBA_DXT3_SRGB"] = new FormatLayout(4, 4, 16),
            ["RGBA_DXT5_UNorm"] = new FormatLayout(4, 4, 16),
            ["RGBA_DXT5_SRGB"] = new FormatLayout(4, 4, 16),
            ["R_BC4_UNorm"] = new FormatLayout(4, 4, 8),
            ["R_BC4_SNorm"] = new FormatLayout(4, 4, 8),
            ["RG_BC5_UNorm"] = new FormatLayout(4, 4, 16),
            ["RG_BC5_SNorm"] = new FormatLayout(4, 4, 16),
            ["RGB_BC6H_UFloat"] = new FormatLayout(4, 4, 16),
            ["RGB_BC6H_SFloat"] = new FormatLayout(4, 4, 16),
            ["RGBA_BC7_UNorm"] = new FormatLayout(4, 4, 16),
            ["RGBA_BC7_SRGB"] = new FormatLayout(4, 4, 16)
        };

        public static IReadOnlyCollection<string> SupportedFormatNames => Layouts.Keys;

        public const string MetricName = "Estimated logical full texture payload";

        // arrayLayers is the slice count of a 2D texture array (1 for a plain 2D texture). Layers are not
        // mip-reduced; each layer carries its own full 2D mip chain.
        public static Observation<long> Estimate(int width, int height, int arrayLayers, int mipsCount, string format, DateTimeOffset capturedAt)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (arrayLayers <= 0) throw new ArgumentOutOfRangeException(nameof(arrayLayers));
            if (mipsCount <= 0) throw new ArgumentOutOfRangeException(nameof(mipsCount));
            if (string.IsNullOrWhiteSpace(format)) throw new ArgumentException("Graphics format is required.", nameof(format));
            if (!Layouts.TryGetValue(format, out var layout))
                return Observation<long>.Unavailable(Availability.Unsupported, ObservationOrigin.Estimated, capturedAt);

            long total = 0;
            var w = width; var h = height;
            for (var mip = 0; mip < mipsCount; mip++)
            {
                var blocksX = (w + layout.BlockWidth - 1) / layout.BlockWidth;
                var blocksY = (h + layout.BlockHeight - 1) / layout.BlockHeight;
                total = checked(total + checked((long)blocksX * blocksY * arrayLayers * layout.BytesPerBlock));
                w = Math.Max(1, w / 2); h = Math.Max(1, h / 2);
            }
            return Observation<long>.FromValue(total, ObservationOrigin.Estimated, capturedAt);
        }
    }
}
