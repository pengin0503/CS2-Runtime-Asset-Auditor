using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.IO.AssetDatabase;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Rendering
{
    public sealed class SurfaceRead
    {
        public SurfaceRead(SurfaceObservation observation, TextureAsset[] textures)
        {
            Observation = observation ?? throw new ArgumentNullException(nameof(observation));
            Textures = textures ?? throw new ArgumentNullException(nameof(textures));
        }

        public SurfaceObservation Observation { get; }
        public TextureAsset[] Textures { get; }
    }

    public sealed class SurfaceAssetReader
    {
        // Reads the SurfaceAsset's serialized properties. When the game has not loaded them, they are loaded and
        // released here so our reference count stays balanced. LoadProperties(useVT) decides isCurrentlyUsingVT, so the
        // value is only an observation of the game's state when the game itself holds the properties.
        public SurfaceRead Read(SurfaceAsset surface, DateTimeOffset capturedAt)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            var id = StableId(surface);
            var loadedHere = !surface.isDataLoaded;
            if (loadedHere) surface.LoadProperties(false);
            try
            {
                var textures = surface.textures == null
                    ? Array.Empty<TextureAsset>()
                    : surface.textures.Values.Where(texture => texture != null).ToArray();
                var usingVt = loadedHere
                    ? Observation<bool>.Unavailable(Availability.NotApplicable, ObservationOrigin.AssetDatabase, capturedAt)
                    : Observation<bool>.FromValue(surface.isCurrentlyUsingVT, ObservationOrigin.AssetDatabase, capturedAt);
                var observation = new SurfaceObservation(id,
                    Observation<int>.FromValue(surface.materialTemplateHash, ObservationOrigin.AssetDatabase, capturedAt),
                    Observation<bool>.FromValue(surface.isVTMaterial, ObservationOrigin.AssetDatabase, capturedAt),
                    usingVt,
                    surface.floats?.Count ?? 0,
                    surface.ints?.Count ?? 0,
                    surface.vectors?.Count ?? 0,
                    surface.colors?.Count ?? 0,
                    surface.keywords ?? (IEnumerable<string>)Array.Empty<string>(),
                    textures.Select(StableTextureId));
                return new SurfaceRead(observation, textures);
            }
            finally
            {
                if (loadedHere) surface.UnloadProperties(false);
            }
        }

        internal static string StableId(SurfaceAsset asset)
        {
            if (!string.IsNullOrWhiteSpace(asset.identifier)) return asset.identifier;
            if (!string.IsNullOrWhiteSpace(asset.uniqueName)) return asset.uniqueName;
            if (!string.IsNullOrWhiteSpace(asset.name)) return asset.name;
            throw new InvalidOperationException("A stable SurfaceAsset identifier is unavailable.");
        }

        private static string StableTextureId(TextureAsset asset)
        {
            if (!string.IsNullOrWhiteSpace(asset.identifier)) return asset.identifier;
            if (!string.IsNullOrWhiteSpace(asset.uniqueName)) return asset.uniqueName;
            if (!string.IsNullOrWhiteSpace(asset.name)) return asset.name;
            throw new InvalidOperationException("A stable TextureAsset identifier is unavailable.");
        }
    }
}
