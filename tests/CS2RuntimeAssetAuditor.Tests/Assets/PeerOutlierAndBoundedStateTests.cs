using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Findings;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    [TestFixture]
    public sealed class PeerOutlierAndBoundedStateTests
    {
        private static readonly DateTimeOffset CapturedAt = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

        [Test]
        public void Vertex_outlier_is_reported_against_the_same_category()
        {
            var (entries, catalog) = Population("Building", new long[] { 1000, 1100, 900, 1050, 950, 1000, 20000 });

            var findings = PeerOutlierEvaluator.Evaluate(entries, catalog, ComparisonPopulation.SameCategory, CapturedAt);

            Assert.That(findings.Keys.Select(key => key.PrefabId).ToArray(), Is.EqualTo(new[] { "Building-6" }));
            var finding = findings.Values.Single().Single();
            Assert.Multiple(() =>
            {
                Assert.That(finding.RuleId, Is.EqualTo(PeerOutlierEvaluator.VertexRuleId));
                Assert.That(finding.Basis, Is.EqualTo(FindingBasis.PeerComparison));
                Assert.That(finding.Evidence, Does.Contain("population=SameCategory"));
                Assert.That(finding.Evidence, Does.Contain("lod0Vertices=20000"));
            });
        }

        [Test]
        public void Populations_below_the_minimum_sample_size_produce_no_findings()
        {
            // Five values leave only four peers for each candidate.
            var (entries, catalog) = Population("Building", new long[] { 1000, 1100, 900, 1050, 20000 });
            Assert.That(PeerOutlierEvaluator.Evaluate(entries, catalog, ComparisonPopulation.SameCategory, CapturedAt), Is.Empty);
        }

        [Test]
        public void Categories_are_never_mixed()
        {
            var (buildings, buildingCatalog) = Population("Building", new long[] { 100, 110, 90, 105, 95 });
            var (trees, treeCatalog) = Population("Tree", new long[] { 20000, 21000, 19000, 20500, 19500 });
            var catalog = buildingCatalog.Concat(treeCatalog).ToDictionary(pair => pair.Key, pair => pair.Value);

            var findings = PeerOutlierEvaluator.Evaluate(buildings.Concat(trees), catalog, ComparisonPopulation.SameCategory, CapturedAt);

            Assert.That(findings, Is.Empty, "large trees are not outliers relative to small buildings");
        }

        [Test]
        public void Builtin_population_compares_custom_assets_against_vanilla_baseline()
        {
            var (vanilla, vanillaCatalog) = Population("Prop", new long[] { 1000, 1000, 1000, 1000, 1000 }, builtin: true);
            var (custom, customCatalog) = Population("Prop", new long[] { 30000, 30000, 30000, 30000, 30000 }, builtin: false, idOffset: 10);
            var catalog = vanillaCatalog.Concat(customCatalog).ToDictionary(pair => pair.Key, pair => pair.Value);
            var entries = vanilla.Concat(custom).ToArray();

            var againstVanilla = PeerOutlierEvaluator.Evaluate(entries, catalog, ComparisonPopulation.BuiltinDlc, CapturedAt);
            var againstCustom = PeerOutlierEvaluator.Evaluate(entries, catalog, ComparisonPopulation.Custom, CapturedAt);

            Assert.Multiple(() =>
            {
                Assert.That(againstVanilla.Count, Is.EqualTo(5), "every custom prop is heavy relative to vanilla props");
                Assert.That(againstCustom, Is.Empty, "custom props are not outliers relative to each other");
            });
        }

        [Test]
        public void Same_source_pack_skips_assets_without_pack_or_source_evidence()
        {
            var (entries, catalog) = Population("Prop", new long[] { 1000, 1000, 1000, 1000, 1000, 50000 }, builtin: false);
            Assert.That(PeerOutlierEvaluator.Evaluate(entries, catalog, ComparisonPopulation.SameSourcePack, CapturedAt), Is.Empty);

            var (packed, packedCatalog) = Population("Prop", new long[] { 1000, 1000, 1000, 1000, 1000, 50000 }, builtin: false, pack: "pack-a");
            Assert.That(PeerOutlierEvaluator.Evaluate(packed, packedCatalog, ComparisonPopulation.SameSourcePack, CapturedAt).Count, Is.EqualTo(1));
        }

        [Test]
        public void Additional_findings_are_merged_into_the_snapshot()
        {
            var (entries, catalog) = Population("Building", new long[] { 1000, 1100, 900, 1050, 950, 1000, 20000 });
            var snapshot = new AssetAnalysisSnapshot(1, 1, 1, CapturedAt, entries, Array.Empty<RenderAssetAnalysisRecord>());

            var enriched = snapshot.WithAdditionalFindings(PeerOutlierEvaluator.Evaluate(entries, catalog, ComparisonPopulation.SameCategory, CapturedAt));

            Assert.That(enriched.Findings.Select(finding => finding.RuleId).ToArray(), Is.EqualTo(new[] { PeerOutlierEvaluator.VertexRuleId }));
            Assert.That(snapshot.Findings, Is.Empty);
        }

        [Test]
        public void Deep_inspection_retention_limit_drops_the_oldest_other_results()
        {
            var keys = Enumerable.Range(0, 3).Select(index => new RenderAssetKey("Render." + index, "RenderPrefab")).ToArray();
            var snapshot = new AssetAnalysisSnapshot(1, 1, 1, CapturedAt, Array.Empty<PrefabAnalysisEntry>(),
                keys.Select(key => new RenderAssetAnalysisRecord(new RenderAssetRecord(key, key.RenderAssetId), null)));

            snapshot = snapshot.WithDeepInspection(keys[0], Inspection(1), 2, retentionLimit: 2);
            snapshot = snapshot.WithDeepInspection(keys[1], Inspection(2), 3, retentionLimit: 2);
            snapshot = snapshot.WithDeepInspection(keys[2], Inspection(3), 4, retentionLimit: 2);

            var inspected = snapshot.RenderAssets.Where(record => record.RenderAsset.DeepInspection != null)
                .Select(record => record.RenderAsset.Key).ToArray();
            Assert.That(inspected, Is.EquivalentTo(new[] { keys[1], keys[2] }));
        }

        [Test]
        public void Lru_cache_evicts_the_least_recently_used_entry()
        {
            var cache = new BoundedLruCache<string, int>(2);
            cache.Add("a", 1);
            cache.Add("b", 2);
            Assert.That(cache.TryGet("a", out _), Is.True);
            cache.Add("c", 3);

            Assert.Multiple(() =>
            {
                Assert.That(cache.Count, Is.EqualTo(2));
                Assert.That(cache.TryGet("b", out _), Is.False, "b was least recently used");
                Assert.That(cache.TryGet("a", out var a) && a == 1, Is.True);
                Assert.That(cache.TryGet("c", out var c) && c == 3, Is.True);
                Assert.That(cache.Evictions, Is.EqualTo(1));
            });
        }

        private static DeepInspectionObservation Inspection(int seconds)
            => DeepInspectionObservation.Available(Array.Empty<MaterialBindingObservation>(), Array.Empty<string>(), CapturedAt.AddSeconds(seconds));

        private static (PrefabAnalysisEntry[] Entries, Dictionary<PrefabKey, PrefabRecord> Catalog) Population(
            string type, long[] vertices, bool? builtin = null, string? pack = null, int idOffset = 0)
        {
            var entries = new List<PrefabAnalysisEntry>();
            var catalog = new Dictionary<PrefabKey, PrefabRecord>();
            for (var index = 0; index < vertices.Length; index++)
            {
                var key = new PrefabKey(type + "-" + (index + idOffset), type);
                entries.Add(new PrefabAnalysisEntry(
                    key,
                    RenderCoverage.Supported,
                    Array.Empty<PrefabRenderRelation>(),
                    Observation<long>.FromValue(vertices[index], ObservationOrigin.Derived, CapturedAt),
                    Observation<double>.Unavailable(Availability.NotApplicable, ObservationOrigin.Derived, CapturedAt),
                    Observation<long>.FromValue(1, ObservationOrigin.Derived, CapturedAt),
                    Observation<long>.FromValue(1, ObservationOrigin.Derived, CapturedAt),
                    Observation<long>.Unavailable(Availability.NotScanned, ObservationOrigin.Derived, CapturedAt)));
                catalog[key] = new PrefabRecord(key, key.PrefabId, PrefabTraits.None,
                    new AssetOriginEvidence(isBuiltin: builtin, assetPackMembership: pack == null ? null : new[] { pack }));
            }
            return (entries.ToArray(), catalog);
        }
    }
}
