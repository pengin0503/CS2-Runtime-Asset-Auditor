using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using CS2RuntimeAssetAuditor.Assets.GameIntegration.Rendering;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    [TestFixture]
    public sealed class RenderGraphTests
    {
        [Test]
        public void Unsupported_family_is_unknown_without_synthetic_zero_geometry_resource()
        {
            var network = Record("Road.Small", PrefabTraits.Network);
            var graph = new RenderGraphBuilder(Array.Empty<IRenderAssetResolver>())
                .Build(new[] { new RenderGraphInput(network, new object()) });

            Assert.That(graph.TryGetCoverage(network.Key, out var coverage), Is.True);
            Assert.That(coverage, Is.EqualTo(RenderCoverage.Unknown));
            Assert.That(graph.RenderAssets, Is.Empty);
            Assert.That(graph.Relations, Is.Empty);
        }

        [Test]
        public void Shared_render_asset_is_deduplicated_across_prefabs()
        {
            var houseA = Record("House.A", PrefabTraits.Building);
            var houseB = Record("House.B", PrefabTraits.Building);
            var sharedKey = new RenderAssetKey("Render.SharedHouse", "RenderPrefab");
            var resolver = new FakeResolver(input => Supported(
                new RenderAssetRecord(sharedKey, "Shared House Mesh"),
                new PrefabRenderRelation(input.Prefab.Key, sharedKey, RenderRelationKind.DirectMesh)));

            var graph = new RenderGraphBuilder(new[] { resolver }).Build(new[]
            {
                new RenderGraphInput(houseA, new object()),
                new RenderGraphInput(houseB, new object())
            });

            Assert.That(graph.RenderAssets.Select(asset => asset.Key), Is.EqualTo(new[] { sharedKey }));
            Assert.That(graph.Relations.Count, Is.EqualTo(2));
            Assert.That(graph.Relations.Select(relation => relation.PrefabKey), Is.EquivalentTo(new[] { houseA.Key, houseB.Key }));
        }

        [Test]
        public void Runtime_render_asset_binding_is_deduplicated_by_stable_render_key()
        {
            var houseA = Record("House.A", PrefabTraits.Building);
            var houseB = Record("House.B", PrefabTraits.Building);
            var sharedKey = new RenderAssetKey("Render.SharedHouse", "RenderPrefab");
            var runtimeRender = new object();
            var resolver = new FakeResolver(input => new RenderResolution(
                RenderCoverage.Supported,
                new[] { new RenderAssetRecord(sharedKey, "Shared House Mesh") },
                new[] { new PrefabRenderRelation(input.Prefab.Key, sharedKey, RenderRelationKind.DirectMesh) },
                runtimeAssets: new[] { new RuntimeRenderAssetBinding(sharedKey, runtimeRender) }));

            var graph = new RenderGraphBuilder(new[] { resolver }).Build(new[]
            {
                new RenderGraphInput(houseA, new object()),
                new RenderGraphInput(houseB, new object())
            });

            Assert.That(graph.RuntimeAssetCount, Is.EqualTo(1));
            Assert.That(graph.TryGetRuntimeAsset(sharedKey, out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(runtimeRender));
        }

        [Test]
        public void Incremental_accumulator_matches_batch_deduplication()
        {
            var houseA = Record("House.A", PrefabTraits.Building);
            var houseB = Record("House.B", PrefabTraits.Building);
            var sharedKey = new RenderAssetKey("Render.SharedHouse", "RenderPrefab");
            var runtimeRender = new object();
            var resolver = new FakeResolver(input => new RenderResolution(
                RenderCoverage.Supported,
                new[] { new RenderAssetRecord(sharedKey, "Shared House Mesh") },
                new[] { new PrefabRenderRelation(input.Prefab.Key, sharedKey, RenderRelationKind.DirectMesh) },
                runtimeAssets: new[] { new RuntimeRenderAssetBinding(sharedKey, runtimeRender) }));
            var accumulator = new RenderGraphAccumulator(new[] { resolver });

            accumulator.Add(new RenderGraphInput(houseA, new object()));
            accumulator.Add(new RenderGraphInput(houseB, new object()));
            var graph = accumulator.Snapshot();

            Assert.That(graph.RenderAssets, Has.Count.EqualTo(1));
            Assert.That(graph.Relations, Has.Count.EqualTo(2));
            Assert.That(graph.RuntimeAssetCount, Is.EqualTo(1));
        }

        [Test]
        public void Lod_relation_kind_remains_attributable()
        {
            var tree = Record("Tree.Oak", PrefabTraits.Tree);
            var lodKey = new RenderAssetKey("Render.Oak.LOD1", "RenderPrefab");
            var resolver = new FakeResolver(input => Supported(
                new RenderAssetRecord(lodKey, "Oak LOD1"),
                new PrefabRenderRelation(input.Prefab.Key, lodKey, RenderRelationKind.Lod, lodLevel: 1)));

            var graph = new RenderGraphBuilder(new[] { resolver })
                .Build(new[] { new RenderGraphInput(tree, new object()) });

            var relation = graph.Relations.Single();
            Assert.That(relation.RelationKind, Is.EqualTo(RenderRelationKind.Lod));
            Assert.That(relation.LodLevel, Is.EqualTo(1));
        }

        [Test]
        public void Resolver_probe_failure_is_item_scoped_and_does_not_abort_other_prefabs()
        {
            var broken = Record("House.Broken", PrefabTraits.Building);
            var healthy = Record("House.Healthy", PrefabTraits.Building);
            var healthyKey = new RenderAssetKey("Render.Healthy", "RenderPrefab");
            var resolver = new ProbeResolver(
                canResolve: input => input.Prefab.Key == broken.Key
                    ? throw new InvalidOperationException("Malformed Prefab probe")
                    : true,
                resolve: input => Supported(
                    new RenderAssetRecord(healthyKey, "Healthy Mesh"),
                    new PrefabRenderRelation(input.Prefab.Key, healthyKey, RenderRelationKind.DirectMesh)));

            var graph = new RenderGraphBuilder(new[] { resolver }).Build(new[]
            {
                new RenderGraphInput(broken, new object()),
                new RenderGraphInput(healthy, new object())
            });

            Assert.That(graph.TryGetCoverage(broken.Key, out var brokenCoverage), Is.True);
            Assert.That(brokenCoverage, Is.EqualTo(RenderCoverage.Failed));
            Assert.That(graph.TryGetCoverage(healthy.Key, out var healthyCoverage), Is.True);
            Assert.That(healthyCoverage, Is.EqualTo(RenderCoverage.Supported));
            Assert.That(graph.RenderAssets.Select(asset => asset.Key), Is.EqualTo(new[] { healthyKey }));
            Assert.That(graph.Relations.Select(relation => relation.PrefabKey), Is.EqualTo(new[] { healthy.Key }));
        }

        private static RenderResolution Supported(RenderAssetRecord asset, PrefabRenderRelation relation)
        {
            return new RenderResolution(RenderCoverage.Supported, new[] { asset }, new[] { relation });
        }

        private static PrefabRecord Record(string id, PrefabTraits traits)
        {
            return new PrefabRecord(new PrefabKey(id, traits.ToString()), id, traits, new AssetOriginEvidence());
        }

        private sealed class FakeResolver : IRenderAssetResolver
        {
            private readonly Func<RenderGraphInput, RenderResolution> _resolve;

            public FakeResolver(Func<RenderGraphInput, RenderResolution> resolve)
            {
                _resolve = resolve;
            }

            public bool CanResolve(RenderGraphInput input) => true;
            public RenderResolution Resolve(RenderGraphInput input) => _resolve(input);
        }

        private sealed class ProbeResolver : IRenderAssetResolver
        {
            private readonly Func<RenderGraphInput, bool> _canResolve;
            private readonly Func<RenderGraphInput, RenderResolution> _resolve;

            public ProbeResolver(Func<RenderGraphInput, bool> canResolve, Func<RenderGraphInput, RenderResolution> resolve)
            {
                _canResolve = canResolve;
                _resolve = resolve;
            }

            public bool CanResolve(RenderGraphInput input) => _canResolve(input);
            public RenderResolution Resolve(RenderGraphInput input) => _resolve(input);
        }
    }
}
