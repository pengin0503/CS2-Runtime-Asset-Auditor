using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.IO.AssetDatabase;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Rendering
{
    public sealed class SurfaceAssetReader
    {
        private readonly GenerationCache<string, SurfaceObservation> _cache = new GenerationCache<string, SurfaceObservation>();

        public SurfaceObservation Read(SurfaceAsset surface, long analysisGeneration, DateTimeOffset capturedAt)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            var id = StableId(surface);
            return _cache.GetOrAdd(analysisGeneration, id, () => ReadCore(surface, id, capturedAt));
        }

        public void ClearCache() => _cache.Clear();

        private static SurfaceObservation ReadCore(SurfaceAsset surface, string id, DateTimeOffset capturedAt)
        {
            var loadedHere = !surface.isDataLoaded;
            if (loadedHere) surface.LoadProperties(false);
            try
            {
                var textures = surface.textures ?? new Dictionary<string, TextureAsset>();
                return new SurfaceObservation(id,
                    Observation<int>.FromValue(surface.materialTemplateHash, ObservationOrigin.AssetDatabase, capturedAt),
                    Observation<bool>.FromValue(surface.isVTMaterial, ObservationOrigin.AssetDatabase, capturedAt),
                    Observation<bool>.FromValue(surface.isCurrentlyUsingVT, ObservationOrigin.AssetDatabase, capturedAt),
                    surface.floats?.Count ?? 0,
                    surface.ints?.Count ?? 0,
                    surface.vectors?.Count ?? 0,
                    surface.colors?.Count ?? 0,
                    surface.keywords ?? Array.Empty<string>(),
                    textures.Values.Where(texture => texture != null).Select(StableTextureId));
            }
            finally
            {
                if (loadedHere) surface.UnloadProperties(false);
            }
        }

        private static string StableId(SurfaceAsset asset)
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
