using System;
using System.Linq;
using System.Reflection;
using CS2RuntimeAssetAuditor.Assets.Core.Findings;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    [TestFixture]
    public sealed class FindingEngineTests
    {
        private static readonly DateTimeOffset CapturedAt = new DateTimeOffset(2026, 9, 27, 7, 5, 0, TimeSpan.Zero);

        [Test]
        public void Lod_retention_math_is_exact_and_deterministic()
        {
            var retention = LodMetrics.RetentionPercent(5000, 4199, CapturedAt);
            var reduction = LodMetrics.ReductionPercent(5000, 4199, CapturedAt);
            Assert.That(retention.Value, Is.EqualTo(83.98d).Within(0.0000001));
            Assert.That(reduction.Value, Is.EqualTo(16.02d).Within(0.0000001));
        }

        [Test]
        public void Missing_lower_lod_is_notice_not_warning()
        {
            var findings = new FindingEngine().EvaluateGeometryLod(new GeometryFindingInput("house", "Building", false, false, null, null), CapturedAt);
            Assert.That(findings.Any(f => f.RuleId == "APA-LOD-001" && f.Status == FindingStatus.Notice), Is.True);
            Assert.That(findings.Any(f => f.Status == FindingStatus.Warning), Is.False);
        }

        [Test]
        public void Weak_lod_reduction_is_heuristic_potential_issue()
        {
            var finding = new FindingEngine().EvaluateGeometryLod(new GeometryFindingInput("house", "Building", true, false, 1000, 920), CapturedAt).Single(f => f.RuleId == "APA-LOD-002");
            Assert.That(finding.Status, Is.EqualTo(FindingStatus.PotentialIssue));
            Assert.That(finding.Basis, Is.EqualTo(FindingBasis.Heuristic));
            Assert.That(finding.RuleVersion, Is.EqualTo(RuleSetInfo.Version));
        }

        [Test]
        public void Broken_required_render_reference_may_be_warning()
        {
            var finding = new FindingEngine().EvaluateGeometryLod(new GeometryFindingInput("broken", "Building", false, true, null, null), CapturedAt).Single(f => f.RuleId == "APA-INT-001");
            Assert.That(finding.Status, Is.EqualTo(FindingStatus.Warning));
            Assert.That(finding.Basis, Is.EqualTo(FindingBasis.Deterministic));
        }

        [Test]
        public void Missing_metric_evidence_does_not_fabricate_heuristic_finding()
        {
            var findings = new FindingEngine().EvaluateGeometryLod(new GeometryFindingInput("unknown", "Building", true, false, null, null), CapturedAt);
            Assert.That(findings.Any(f => f.RuleId == "APA-LOD-002"), Is.False);
        }

        [Test]
        public void High_city_exposure_is_evidence_not_render_cost_proof()
        {
            var finding = new FindingEngine().EvaluateExposure(new ExposureFindingInput("popular-prop", 25000, 24000), CapturedAt).Single();
            Assert.That(finding.Category, Is.EqualTo(FindingCategory.Exposure));
            Assert.That(finding.Status, Is.EqualTo(FindingStatus.Observed));
            Assert.That(finding.Basis, Is.EqualTo(FindingBasis.Observation));
            Assert.That(finding.Explanation, Does.Contain("does not prove").IgnoreCase);
        }

        [Test]
        public void Failed_texture_read_is_item_level_and_does_not_block_other_assets()
        {
            var engine = new FindingEngine();
            var failed = engine.EvaluateTextureRead(new TextureFindingInput("asset-a", "tex-bad", Availability.Failed, "APA-TEX-001"), CapturedAt);
            var healthy = engine.EvaluateTextureRead(new TextureFindingInput("asset-b", "tex-good", Availability.Available, null), CapturedAt);
            Assert.That(failed.Single().Status, Is.EqualTo(FindingStatus.Unknown));
            Assert.That(failed.Single().Evidence, Does.Contain("texture=tex-bad"));
            Assert.That(healthy, Is.Empty);
        }

        [Test]
        public void Finding_engine_produces_no_global_performance_score()
        {
            Assert.That(typeof(FindingEngine).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Any(p => p.Name.Contains("Score", StringComparison.OrdinalIgnoreCase)), Is.False);
        }
    }
}
