using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.IO.AssetDatabase;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using Game.Prefabs;
using UnityEngine;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Rendering
{
    public sealed class DeepInspectionReader
    {
        public DeepInspectionObservation Read(RenderAssetKey selectedKey, RenderPrefab renderPrefab, DateTimeOffset capturedAt)
        {
            if (!selectedKey.IsValid) throw new ArgumentException("A selected render-asset key is required.", nameof(selectedKey));
            if (renderPrefab == null) throw new ArgumentNullException(nameof(renderPrefab));

            if (!Matches(selectedKey, renderPrefab))
                return DeepInspectionObservation.Unavailable(Availability.Failed, capturedAt, "APA-DEEP-001");

            Material[]? acquiredMaterials = null;
            var acquired = false;
            try
            {
                var surfaceIds = (renderPrefab.surfaceAssets ?? Enumerable.Empty<SurfaceAsset>())
                    .Where(surface => surface != null)
                    .Select(StableSurfaceId)
                    .ToArray();

                acquiredMaterials = renderPrefab.ObtainMaterials(false);
                acquired = true;
                var copied = new List<MaterialBindingObservation>();
                if (acquiredMaterials != null)
                {
                    foreach (var material in acquiredMaterials)
                    {
                        if (material == null) continue;
                        var shader = material.shader;
                        copied.Add(new MaterialBindingObservation(
                            material.name ?? string.Empty,
                            shader == null ? string.Empty : shader.name ?? string.Empty,
                            material.shaderKeywords ?? Array.Empty<string>(),
                            material.renderQueue,
                            material.passCount,
                            material.enableInstancing));
                    }
                }

                return DeepInspectionObservation.Available(copied, surfaceIds, capturedAt);
            }
            catch
            {
                return DeepInspectionObservation.Unavailable(Availability.Failed, capturedAt, "APA-DEEP-002");
            }
            finally
            {
                acquiredMaterials = null;
                if (acquired)
                    renderPrefab.ReleaseMaterials();
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
