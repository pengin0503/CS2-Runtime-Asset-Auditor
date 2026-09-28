using System;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    [TestFixture]
    public sealed class DeepInspectionSnapshotTests
    {
        [Test]
        public void Deep_inspection_replaces_only_selected_render_record_and_advances_analysis_generation()
        {
            var capturedAt = new DateTimeOffset(2026, 9, 27, 9, 10, 0, TimeSpan.Zero);
            var selectedKey = new RenderAssetKey("Render.A", "RenderPrefab");
            var otherKey = new RenderAssetKey("Render.B", "RenderPrefab");
            var selected = new RenderAssetAnalysisRecord(new RenderAssetRecord(selectedKey, "A"), null);
            var other = new RenderAssetAnalysisRecord(new RenderAssetRecord(otherKey, "B"), null);
            var snapshot = new AssetAnalysisSnapshot(2, 4, 7, capturedAt, Array.Empty<PrefabAnalysisEntry>(), new[] { selected, other });
            var inspection = DeepInspectionObservation.Available(
                new[] { new MaterialBindingObservation("Material A", "Shader/A", Array.Empty<string>(), 2000, 1, true) },
                new[] { "Surface.A" },
                capturedAt.AddSeconds(1));

            var enriched = snapshot.WithDeepInspection(selectedKey, inspection, analysisGeneration: 8);

            Assert.That(enriched.AnalysisGeneration, Is.EqualTo(8));
            Assert.That(enriched.TryGetRenderAsset(selectedKey, out var selectedAfter), Is.True);
            Assert.That(selectedAfter.RenderAsset.DeepInspection, Is.SameAs(inspection));
            Assert.That(enriched.TryGetRenderAsset(otherKey, out var otherAfter), Is.True);
            Assert.That(otherAfter.RenderAsset.DeepInspection, Is.Null);
            Assert.That(snapshot.TryGetRenderAsset(selectedKey, out var original), Is.True);
            Assert.That(original.RenderAsset.DeepInspection, Is.Null);
        }

        [Test]
        public void Unknown_render_key_is_rejected_without_mutating_snapshot()
        {
            var capturedAt = new DateTimeOffset(2026, 9, 27, 9, 10, 0, TimeSpan.Zero);
            var snapshot = new AssetAnalysisSnapshot(2, 4, 7, capturedAt, Array.Empty<PrefabAnalysisEntry>(), Array.Empty<RenderAssetAnalysisRecord>());
            var inspection = DeepInspectionObservation.Unavailable(Availability.Failed, capturedAt, "APA-DEEP-001");

            Assert.Throws<InvalidOperationException>(() => snapshot.WithDeepInspection(new RenderAssetKey("missing", "RenderPrefab"), inspection, 8));
            Assert.That(snapshot.AnalysisGeneration, Is.EqualTo(7));
        }
    }
}
