using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Rendering
{
    public sealed class RenderGraphInput
    {
        public RenderGraphInput(PrefabRecord prefab, object runtimePrefab)
        {
            Prefab = prefab ?? throw new ArgumentNullException(nameof(prefab));
            RuntimePrefab = runtimePrefab ?? throw new ArgumentNullException(nameof(runtimePrefab));
        }
        public PrefabRecord Prefab { get; }
        public object RuntimePrefab { get; }
    }

    public sealed class RuntimeRenderAssetBinding
    {
        public RuntimeRenderAssetBinding(RenderAssetKey key, object runtimeAsset)
        {
            if (!key.IsValid) throw new ArgumentException("A stable render-asset key is required.", nameof(key));
            Key = key;
            RuntimeAsset = runtimeAsset ?? throw new ArgumentNullException(nameof(runtimeAsset));
        }
        public RenderAssetKey Key { get; }
        public object RuntimeAsset { get; }
    }

    public sealed class RenderResolution
    {
        public RenderResolution(
            RenderCoverage coverage,
            IEnumerable<RenderAssetRecord>? renderAssets = null,
            IEnumerable<PrefabRenderRelation>? relations = null,
            string? diagnosticCode = null,
            IEnumerable<RuntimeRenderAssetBinding>? runtimeAssets = null)
        {
            if (!Enum.IsDefined(typeof(RenderCoverage), coverage)) throw new ArgumentOutOfRangeException(nameof(coverage));
            Coverage = coverage;
            RenderAssets = Array.AsReadOnly((renderAssets ?? Array.Empty<RenderAssetRecord>()).ToArray());
            Relations = Array.AsReadOnly((relations ?? Array.Empty<PrefabRenderRelation>()).ToArray());
            RuntimeAssets = Array.AsReadOnly((runtimeAssets ?? Array.Empty<RuntimeRenderAssetBinding>()).ToArray());
            DiagnosticCode = diagnosticCode;
        }
        public RenderCoverage Coverage { get; }
        public IReadOnlyList<RenderAssetRecord> RenderAssets { get; }
        public IReadOnlyList<PrefabRenderRelation> Relations { get; }
        public IReadOnlyList<RuntimeRenderAssetBinding> RuntimeAssets { get; }
        public string? DiagnosticCode { get; }
    }

    public interface IRenderAssetResolver
    {
        bool CanResolve(RenderGraphInput input);
        RenderResolution Resolve(RenderGraphInput input);
    }
}
