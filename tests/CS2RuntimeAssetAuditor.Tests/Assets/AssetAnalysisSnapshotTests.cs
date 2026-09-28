using System;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Findings;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using CS2RuntimeAssetAuditor.Assets.Core.Scanning;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    [TestFixture]
    public sealed class AssetAnalysisSnapshotTests
    {
        private static readonly DateTimeOffset CapturedAt = new DateTimeOffset(2026, 9, 27, 8, 30, 0, TimeSpan.Zero);

        [Test]
        public void Snapshot_preserves_render_relations_observation_availability_and_findings()
        {
            var prefab = new PrefabKey("Building.House", "Building");
            var render = new RenderAssetKey("Render.House", "Game.Prefabs.RenderPrefab");
            var relation = new PrefabRenderRelation(prefab, render, RenderRelationKind.DirectMesh);
            var finding = new Finding(
                "APA-LOD-001",
                FindingStatus.Notice,
                FindingCategory.Lod,
                "No lower LOD observed",
                "Observation only.",
                new[] { "asset=Building.House" },
                FindingBasis.Observation,
                "1.0");
            var entry = new PrefabAnalysisEntry(
                prefab,
                RenderCoverage.Supported,
                new[] { relation },
                Observation<long>.FromValue(1200, ObservationOrigin.Derived, CapturedAt),
                Observation<double>.Unavailable(Availability.NotScanned, ObservationOrigin.Derived, CapturedAt),
                Observation<long>.FromValue(2, ObservationOrigin.AssetDatabase, CapturedAt),
                Observation<long>.FromValue(3, ObservationOrigin.Derived, CapturedAt),
                Observation<long>.FromValue(1024 * 1024, ObservationOrigin.Estimated, CapturedAt),
                new[] { finding });

            var snapshot = new AssetAnalysisSnapshot(7, 3, 11, CapturedAt, new[] { entry }, Array.Empty<RenderAssetAnalysisRecord>());

            Assert.That(snapshot.TryGetPrefab(prefab, out var resolved), Is.True);
            Assert.That(resolved.RenderCoverage, Is.EqualTo(RenderCoverage.Supported));
            Assert.That(resolved.Relations.Single().RenderAssetKey, Is.EqualTo(render));
            Assert.That(resolved.Lod0Vertices.Value, Is.EqualTo(1200));
            Assert.That(resolved.Lod1RetentionPercent.Availability, Is.EqualTo(Availability.NotScanned));
            Assert.That(resolved.Findings.Single().RuleId, Is.EqualTo("APA-LOD-001"));
            Assert.That(snapshot.Findings.Single().RuleId, Is.EqualTo("APA-LOD-001"));
        }

        [Test]
        public void Published_state_replaces_analysis_only_after_success_for_current_world()
        {
            var state = new PublishedAuditState();
            state.ResetForWorld(4);
            var first = EmptySnapshot(worldGeneration: 4, analysisGeneration: 1);
            var failedReplacement = EmptySnapshot(worldGeneration: 4, analysisGeneration: 2);
            var wrongWorld = EmptySnapshot(worldGeneration: 5, analysisGeneration: 3);

            Assert.That(state.TryPublishAnalysis(first, scanSucceeded: true), Is.True);
            Assert.That(state.TryPublishAnalysis(failedReplacement, scanSucceeded: false), Is.False);
            Assert.That(state.TryPublishAnalysis(wrongWorld, scanSucceeded: true), Is.False);
            Assert.That(state.Analysis, Is.SameAs(first));
        }

        private static AssetAnalysisSnapshot EmptySnapshot(long worldGeneration, long analysisGeneration)
        {
            return new AssetAnalysisSnapshot(
                worldGeneration,
                catalogGeneration: 1,
                analysisGeneration,
                CapturedAt,
                Array.Empty<PrefabAnalysisEntry>(),
                Array.Empty<RenderAssetAnalysisRecord>());
        }
    }
}
