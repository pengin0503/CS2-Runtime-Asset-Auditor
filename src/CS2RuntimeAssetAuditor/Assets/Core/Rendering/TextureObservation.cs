using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;

namespace CS2RuntimeAssetAuditor.Assets.Core.Rendering
{
    public sealed class TextureObservation
    {
        private TextureObservation(string textureAssetId, Observation<int> width, Observation<int> height, Observation<int> depth, Observation<string> format, Observation<string> dimension, Observation<int> mipsCount, Observation<string> filterMode, Observation<string> wrapMode, Observation<int> anisoLevel, Observation<long> estimatedLogicalPayload)
        {
            if (string.IsNullOrWhiteSpace(textureAssetId)) throw new ArgumentException("Texture asset ID is required.", nameof(textureAssetId));
            TextureAssetId = textureAssetId; Width = width; Height = height; Depth = depth; Format = format; Dimension = dimension; MipsCount = mipsCount; FilterMode = filterMode; WrapMode = wrapMode; AnisoLevel = anisoLevel; EstimatedLogicalPayload = estimatedLogicalPayload;
        }
        public string TextureAssetId { get; }
        public Observation<int> Width { get; }
        public Observation<int> Height { get; }
        public Observation<int> Depth { get; }
        public Observation<string> Format { get; }
        public Observation<string> Dimension { get; }
        public Observation<int> MipsCount { get; }
        public Observation<string> FilterMode { get; }
        public Observation<string> WrapMode { get; }
        public Observation<int> AnisoLevel { get; }
        public Observation<long> EstimatedLogicalPayload { get; }

        public static TextureObservation FromMetadata(string id, int width, int height, int depth, string format, string dimension, int mips, string filter, string wrap, int aniso, Observation<long> estimatedPayload, DateTimeOffset capturedAt)
        {
            if (estimatedPayload == null) throw new ArgumentNullException(nameof(estimatedPayload));
            return new TextureObservation(id,
                Observation<int>.FromValue(width, ObservationOrigin.AssetDatabase, capturedAt), Observation<int>.FromValue(height, ObservationOrigin.AssetDatabase, capturedAt), Observation<int>.FromValue(depth, ObservationOrigin.AssetDatabase, capturedAt),
                Observation<string>.FromValue(format, ObservationOrigin.AssetDatabase, capturedAt), Observation<string>.FromValue(dimension, ObservationOrigin.AssetDatabase, capturedAt), Observation<int>.FromValue(mips, ObservationOrigin.AssetDatabase, capturedAt),
                Observation<string>.FromValue(filter, ObservationOrigin.AssetDatabase, capturedAt), Observation<string>.FromValue(wrap, ObservationOrigin.AssetDatabase, capturedAt), Observation<int>.FromValue(aniso, ObservationOrigin.AssetDatabase, capturedAt), estimatedPayload);
        }

        public static TextureObservation Available(string id, int width, int height, int depth, string format, string dimension, int mips, string filter, string wrap, int aniso, long estimatedPayload, DateTimeOffset capturedAt)
            => FromMetadata(id, width, height, depth, format, dimension, mips, filter, wrap, aniso, Observation<long>.FromValue(estimatedPayload, ObservationOrigin.Estimated, capturedAt), capturedAt);

        // The texture's header has never been read (it was not loaded, for example because it is served through
        // virtual texturing). Its metadata is unknown, which is neither a read failure nor a zero-sized texture.
        public static TextureObservation NotResident(string id, DateTimeOffset capturedAt)
        {
            return new TextureObservation(id,
                Observation<int>.Unavailable(Availability.NotScanned, ObservationOrigin.AssetDatabase, capturedAt),
                Observation<int>.Unavailable(Availability.NotScanned, ObservationOrigin.AssetDatabase, capturedAt),
                Observation<int>.Unavailable(Availability.NotScanned, ObservationOrigin.AssetDatabase, capturedAt),
                Observation<string>.Unavailable(Availability.NotScanned, ObservationOrigin.AssetDatabase, capturedAt),
                Observation<string>.Unavailable(Availability.NotScanned, ObservationOrigin.AssetDatabase, capturedAt),
                Observation<int>.Unavailable(Availability.NotScanned, ObservationOrigin.AssetDatabase, capturedAt),
                Observation<string>.Unavailable(Availability.NotScanned, ObservationOrigin.AssetDatabase, capturedAt),
                Observation<string>.Unavailable(Availability.NotScanned, ObservationOrigin.AssetDatabase, capturedAt),
                Observation<int>.Unavailable(Availability.NotScanned, ObservationOrigin.AssetDatabase, capturedAt),
                Observation<long>.Unavailable(Availability.NotScanned, ObservationOrigin.Estimated, capturedAt));
        }

        public static IReadOnlyList<TextureObservation> Deduplicate(IEnumerable<TextureObservation> observations)
        {
            if (observations == null) throw new ArgumentNullException(nameof(observations));
            return Array.AsReadOnly(observations.GroupBy(o => o.TextureAssetId, StringComparer.Ordinal).Select(g => g.First()).OrderBy(o => o.TextureAssetId, StringComparer.Ordinal).ToArray());
        }
    }
}
