using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using Game.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Rendering
{
    public sealed class ObjectGeometryResolver : IRenderAssetResolver
    {
        public bool CanResolve(RenderGraphInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return input.RuntimePrefab is ObjectGeometryPrefab;
        }

        public RenderResolution Resolve(RenderGraphInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!(input.RuntimePrefab is ObjectGeometryPrefab objectGeometry))
                return new RenderResolution(RenderCoverage.Unknown, diagnosticCode: "render_family_not_object_geometry");

            var assets = new Dictionary<RenderAssetKey, RenderAssetRecord>();
            var runtimeAssets = new Dictionary<RenderAssetKey, RuntimeRenderAssetBinding>();
            var relations = new List<PrefabRenderRelation>();
            var meshes = objectGeometry.m_Meshes;
            if (meshes == null || meshes.Length == 0)
                return new RenderResolution(RenderCoverage.Unknown, diagnosticCode: MeshReferenceCoverage.NoMeshRelations);

            // Every reference is counted: a Prefab with one resolved and one broken mesh is not complete.
            var resolved = 0;
            var missing = 0;
            var unsupported = 0;
            foreach (var meshInfo in meshes)
            {
                var mesh = meshInfo?.m_Mesh;
                if (mesh == null)
                {
                    missing++;
                    continue;
                }
                if (!(mesh is RenderPrefab renderPrefab))
                {
                    unsupported++;
                    continue;
                }
                resolved++;
                AddRenderAsset(input, renderPrefab, RenderRelationKind.DirectMesh, null, assets, runtimeAssets, relations);
                if (renderPrefab.components == null)
                    continue;
                foreach (var component in renderPrefab.components)
                {
                    if (!(component is LodProperties lod) || lod.m_LodMeshes == null)
                        continue;
                    for (var index = 0; index < lod.m_LodMeshes.Length; index++)
                    {
                        var lodRenderPrefab = lod.m_LodMeshes[index];
                        if (lodRenderPrefab == null)
                        {
                            missing++;
                            continue;
                        }
                        AddRenderAsset(input, lodRenderPrefab, RenderRelationKind.Lod, index + 1, assets, runtimeAssets, relations);
                    }
                }
            }

            // The resolved part is kept for inspection; the coverage tells analysis not to total it as the whole Prefab.
            var coverage = MeshReferenceCoverage.Classify(resolved, missing, unsupported);
            return new RenderResolution(coverage.Coverage, assets.Values, relations, coverage.DiagnosticCode, runtimeAssets.Values);
        }

        private static void AddRenderAsset(
            RenderGraphInput input,
            RenderPrefab renderPrefab,
            RenderRelationKind relationKind,
            int? lodLevel,
            IDictionary<RenderAssetKey, RenderAssetRecord> assets,
            IDictionary<RenderAssetKey, RuntimeRenderAssetBinding> runtimeAssets,
            ICollection<PrefabRenderRelation> relations)
        {
            var id = StableRenderId(renderPrefab);
            var type = renderPrefab.GetType().FullName ?? "Game.Prefabs.RenderPrefab";
            var key = new RenderAssetKey(id, type);
            if (!assets.ContainsKey(key)) assets.Add(key, new RenderAssetRecord(key, DisplayName(renderPrefab, id)));
            if (!runtimeAssets.ContainsKey(key)) runtimeAssets.Add(key, new RuntimeRenderAssetBinding(key, renderPrefab));
            relations.Add(new PrefabRenderRelation(input.Prefab.Key, key, relationKind, lodLevel));
        }

        private static string StableRenderId(RenderPrefab renderPrefab)
        {
            var asset = renderPrefab.asset;
            if (!string.IsNullOrWhiteSpace(asset?.identifier)) return asset!.identifier;
            if (!string.IsNullOrWhiteSpace(asset?.uniqueName)) return asset!.uniqueName;
            if (!string.IsNullOrWhiteSpace(renderPrefab.name)) return renderPrefab.name;
            throw new InvalidOperationException("A stable RenderPrefab identifier is unavailable.");
        }

        private static string DisplayName(RenderPrefab renderPrefab, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(renderPrefab.name)) return renderPrefab.name;
            var assetName = renderPrefab.asset?.name;
            if (assetName != null && !string.IsNullOrWhiteSpace(assetName)) return assetName;
            return fallback;
        }
    }
}
