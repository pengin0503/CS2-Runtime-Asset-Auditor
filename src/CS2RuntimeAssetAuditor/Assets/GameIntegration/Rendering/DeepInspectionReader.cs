using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.IO.AssetDatabase;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using Game.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Rendering
{
    // Reads each material slot from the SurfaceAsset's serialized properties and the shared template material the
    // game's MaterialLibrary resolves for it. No Material is instantiated and no texture is loaded:
    // RenderPrefab.ObtainMaterials() would call SurfaceAsset.Load (a new Material plus every non-VT texture on the
    // GPU) and RenderPrefab.ReleaseMaterials() releases nothing in the game, so that path leaked VRAM per inspection.
    // The game renders through ManagedBatchSystem materials built from the same template and properties; the values
    // here describe the asset's template and keywords, not that runtime material instance.
    public sealed class DeepInspectionReader
    {
        public DeepInspectionObservation Read(RenderAssetKey selectedKey, RenderPrefab renderPrefab, DateTimeOffset capturedAt)
        {
            if (!selectedKey.IsValid) throw new ArgumentException("A selected render-asset key is required.", nameof(selectedKey));
            if (renderPrefab == null) throw new ArgumentNullException(nameof(renderPrefab));

            if (!Matches(selectedKey, renderPrefab))
                return DeepInspectionObservation.Unavailable(Availability.Failed, capturedAt, "APA-DEEP-001");

            try
            {
                var surfaces = (renderPrefab.surfaceAssets ?? Enumerable.Empty<SurfaceAsset>()).ToArray();
                var materials = new List<MaterialBindingObservation>(surfaces.Length);
                var surfaceIds = new List<string>(surfaces.Length);
                foreach (var surface in surfaces)
                {
                    if (surface == null)
                        continue;
                    var surfaceId = StableSurfaceId(surface);
                    surfaceIds.Add(surfaceId);
                    materials.Add(ReadSlot(surface, surfaceId));
                }

                return DeepInspectionObservation.Available(materials, surfaceIds, capturedAt);
            }
            catch
            {
                return DeepInspectionObservation.Unavailable(Availability.Failed, capturedAt, "APA-DEEP-002");
            }
        }

        private static MaterialBindingObservation ReadSlot(SurfaceAsset surface, string surfaceId)
        {
            // Balance our own reference: properties the game already holds stay loaded, ones we load are released.
            var loadedHere = !surface.isDataLoaded;
            if (loadedHere)
                surface.LoadProperties(false);
            try
            {
                var template = surface.GetTemplateMaterial();
                var shader = template == null ? null : template.shader;
                return new MaterialBindingObservation(
                    string.IsNullOrWhiteSpace(surface.name) ? surfaceId : surface.name,
                    shader == null ? string.Empty : shader.name ?? string.Empty,
                    surface.keywords ?? (IEnumerable<string>)Array.Empty<string>(),
                    template == null ? 0 : template.renderQueue,
                    template == null ? 0 : template.passCount,
                    template != null && template.enableInstancing);
            }
            finally
            {
                if (loadedHere)
                    surface.UnloadProperties(false);
            }
        }

        private static bool Matches(RenderAssetKey key, RenderPrefab renderPrefab)
        {
            try
            {
                var type = renderPrefab.GetType().FullName ?? "Game.Prefabs.RenderPrefab";
                return StringComparer.Ordinal.Equals(key.RenderAssetType, type)
                    && StringComparer.Ordinal.Equals(key.RenderAssetId, StableRenderId(renderPrefab));
            }
            catch
            {
                return false;
            }
        }

        private static string StableRenderId(RenderPrefab renderPrefab)
        {
            if (!string.IsNullOrWhiteSpace(renderPrefab.asset?.identifier)) return renderPrefab.asset!.identifier;
            if (!string.IsNullOrWhiteSpace(renderPrefab.asset?.uniqueName)) return renderPrefab.asset!.uniqueName;
            if (!string.IsNullOrWhiteSpace(renderPrefab.name)) return renderPrefab.name;
            throw new InvalidOperationException("A stable RenderPrefab identifier is unavailable.");
        }

        private static string StableSurfaceId(SurfaceAsset surface)
        {
            if (!string.IsNullOrWhiteSpace(surface.identifier)) return surface.identifier;
            if (!string.IsNullOrWhiteSpace(surface.uniqueName)) return surface.uniqueName;
            if (!string.IsNullOrWhiteSpace(surface.name)) return surface.name;
            throw new InvalidOperationException("A stable SurfaceAsset identifier is unavailable.");
        }
    }
}
