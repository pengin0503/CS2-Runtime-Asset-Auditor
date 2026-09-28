using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Rendering
{
    public sealed class RenderGraphSnapshot
    {
        private readonly IReadOnlyDictionary<PrefabKey, RenderCoverage> _coverage;
        private readonly IReadOnlyDictionary<RenderAssetKey, object> _runtimeAssets;

        internal RenderGraphSnapshot(
            IDictionary<PrefabKey, RenderCoverage> coverage,
            IEnumerable<RenderAssetRecord> renderAssets,
            IEnumerable<PrefabRenderRelation> relations,
            IDictionary<RenderAssetKey, object>? runtimeAssets = null)
        {
            _coverage = new ReadOnlyDictionary<PrefabKey, RenderCoverage>(new Dictionary<PrefabKey, RenderCoverage>(coverage));
            _runtimeAssets = new ReadOnlyDictionary<RenderAssetKey, object>(new Dictionary<RenderAssetKey, object>(runtimeAssets ?? new Dictionary<RenderAssetKey, object>()));
            RenderAssets = Array.AsReadOnly(renderAssets.ToArray());
            Relations = Array.AsReadOnly(relations.ToArray());
        }

        public IReadOnlyList<RenderAssetRecord> RenderAssets { get; }
        public IReadOnlyList<PrefabRenderRelation> Relations { get; }
        public int RuntimeAssetCount => _runtimeAssets.Count;
        public bool TryGetCoverage(PrefabKey prefabKey, out RenderCoverage coverage) => _coverage.TryGetValue(prefabKey, out coverage);
        public bool TryGetRuntimeAsset(RenderAssetKey key, out object runtimeAsset) => _runtimeAssets.TryGetValue(key, out runtimeAsset!);
    }

    public sealed class RenderGraphAccumulator
    {
        private readonly IReadOnlyList<IRenderAssetResolver> _resolvers;
        private readonly Dictionary<PrefabKey, RenderCoverage> _coverage = new Dictionary<PrefabKey, RenderCoverage>();
        private readonly Dictionary<RenderAssetKey, RenderAssetRecord> _assets = new Dictionary<RenderAssetKey, RenderAssetRecord>();
        private readonly Dictionary<RenderAssetKey, object> _runtimeAssets = new Dictionary<RenderAssetKey, object>();
        private readonly List<PrefabRenderRelation> _relations = new List<PrefabRenderRelation>();
        private readonly HashSet<string> _relationKeys = new HashSet<string>(StringComparer.Ordinal);

        public RenderGraphAccumulator(IEnumerable<IRenderAssetResolver> resolvers)
        {
            if (resolvers == null) throw new ArgumentNullException(nameof(resolvers));
            _resolvers = Array.AsReadOnly(resolvers.ToArray());
        }

        public int InputCount { get; private set; }

        public void Add(RenderGraphInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            InputCount++;

            IRenderAssetResolver? resolver = null;
            try
            {
                foreach (var candidate in _resolvers)
                {
                    if (!candidate.CanResolve(input))
                        continue;
                    resolver = candidate;
                    break;
                }
            }
            catch
            {
                _coverage[input.Prefab.Key] = RenderCoverage.Failed;
                return;
            }

            if (resolver == null)
            {
                _coverage[input.Prefab.Key] = RenderCoverage.Unknown;
                return;
            }

            RenderResolution resolution;
            try
            {
                resolution = resolver.Resolve(input) ?? throw new InvalidOperationException("A render resolver returned no resolution.");
            }
            catch
            {
                _coverage[input.Prefab.Key] = RenderCoverage.Failed;
                return;
            }

            _coverage[input.Prefab.Key] = resolution.Coverage;
            foreach (var asset in resolution.RenderAssets)
                if (!_assets.ContainsKey(asset.Key)) _assets.Add(asset.Key, asset);
            foreach (var binding in resolution.RuntimeAssets)
                if (!_runtimeAssets.ContainsKey(binding.Key)) _runtimeAssets.Add(binding.Key, binding.RuntimeAsset);

            foreach (var relation in resolution.Relations)
            {
                if (relation.PrefabKey != input.Prefab.Key)
                {
                    _coverage[input.Prefab.Key] = RenderCoverage.Failed;
                    continue;
                }
                var relationKey = relation.PrefabKey + "|" + relation.RenderAssetKey + "|" + relation.RelationKind + "|" + (relation.LodLevel?.ToString() ?? string.Empty);
                if (_relationKeys.Add(relationKey)) _relations.Add(relation);
            }
        }

        public RenderGraphSnapshot Snapshot()
        {
            return new RenderGraphSnapshot(
                _coverage,
                _assets.Values.OrderBy(asset => asset.Key.RenderAssetType, StringComparer.Ordinal).ThenBy(asset => asset.Key.RenderAssetId, StringComparer.Ordinal),
                _relations.OrderBy(relation => relation.PrefabKey.PrefabType, StringComparer.Ordinal)
                    .ThenBy(relation => relation.PrefabKey.PrefabId, StringComparer.Ordinal)
                    .ThenBy(relation => relation.LodLevel ?? -1)
                    .ThenBy(relation => relation.RelationKind)
                    .ThenBy(relation => relation.RenderAssetKey.RenderAssetId, StringComparer.Ordinal),
                _runtimeAssets);
        }
    }

    public sealed class RenderGraphBuilder
    {
        private readonly IReadOnlyList<IRenderAssetResolver> _resolvers;
        public RenderGraphBuilder(IEnumerable<IRenderAssetResolver> resolvers)
        {
            if (resolvers == null) throw new ArgumentNullException(nameof(resolvers));
            _resolvers = Array.AsReadOnly(resolvers.ToArray());
        }

        public RenderGraphSnapshot Build(IEnumerable<RenderGraphInput> inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            var accumulator = new RenderGraphAccumulator(_resolvers);
            foreach (var input in inputs)
            {
                if (input == null) throw new ArgumentException("Render graph inputs cannot contain null entries.", nameof(inputs));
                accumulator.Add(input);
            }
            return accumulator.Snapshot();
        }
    }
}
