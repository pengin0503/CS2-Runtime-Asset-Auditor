using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.Advisor;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

// Issue #57: the detailed quality groups (shadows, level of detail, dynamic resolution, texture) are trial candidates
// for the bottleneck they can relieve, one step down, with at most medium confidence.
public class QualityGroupRecommendationTests
{
    private static readonly string[] Levels = { "Low", "Medium", "High" };

    [Test]
    public void Gpu_bound_frame_proposes_every_gpu_group_one_step_down_with_at_most_medium_confidence()
    {
        var evidence = Evidence(Metric("render.shadowCasters", 1200), Metric("render.triangles", 4.6e7),
            Metric(VirtualTexturingMetrics.WorkingSetLodBias, 1.5));
        var results = Recommend(Observe(BottleneckCategory.RenderingGpu, AdvisorConfidence.High), evidence,
            Group(QualityGroupRules.Shadows), Group(QualityGroupRules.LevelOfDetail),
            Group(QualityGroupRules.DynamicResolution), Group(QualityGroupRules.Texture));

        foreach (var result in results)
        {
            Assert.That(result.Direction, Is.EqualTo(RecommendationDirection.LowerRecommended), result.SettingId);
            Assert.That(result.RecommendedValue, Is.EqualTo("Medium"), result.SettingId);
        }
        var shadows = results[0];
        Assert.That(shadows.SettingId, Is.EqualTo("Game.Settings.ShadowsQualitySettings::QualityLevel"));
        Assert.That(shadows.DisplayName, Is.EqualTo("Shadow quality"));
        Assert.That(shadows.Confidence, Is.EqualTo(AdvisorConfidence.Medium), "a trial candidate is never high confidence");
        Assert.That(shadows.Priority, Is.EqualTo(RecommendationPriority.Medium));
        Assert.That(shadows.EvidenceIds, Is.EqualTo(new[] { "gpu.frame.ms", "render.shadowCasters" }));
        Assert.That(shadows.Rationale, Is.EqualTo(QualityGroupRules.Shadows.Rationale));
        // Draw calls were not measured, so only the measured triangle count supports the LOD proposal.
        Assert.That(results[1].EvidenceIds, Is.EqualTo(new[] { "gpu.frame.ms", "render.triangles" }));
        Assert.That(results[2].EvidenceIds, Is.EqualTo(new[] { "gpu.frame.ms" }));

        var texture = results[3];
        Assert.That(texture.Confidence, Is.EqualTo(AdvisorConfidence.Low), "texture's effect is unmeasured");
        Assert.That(texture.Priority, Is.EqualTo(RecommendationPriority.Low));
        Assert.That(texture.EvidenceIds, Is.EqualTo(new[] { "gpu.frame.ms", VirtualTexturingMetrics.WorkingSetLodBias }));
    }

    [Test]
    public void Render_thread_bound_frame_proposes_only_the_groups_that_reduce_draw_work()
    {
        var results = Recommend(Observe(BottleneckCategory.RenderThreadCpu, AdvisorConfidence.High),
            Evidence(Metric(VirtualTexturingMetrics.WorkingSetLodBias, 2)), AllGroups());
        Assert.That(results.Select(r => r.Direction), Is.EqualTo(new[]
        {
            RecommendationDirection.LowerRecommended, RecommendationDirection.LowerRecommended,
            RecommendationDirection.NoRecommendation, RecommendationDirection.NoRecommendation
        }));
    }

    [TestCase(BottleneckCategory.MainThreadCpu)]
    [TestCase(BottleneckCategory.SimulationCpu)]
    [TestCase(BottleneckCategory.Unknown)]
    [TestCase(BottleneckCategory.PresentLimited)]
    [TestCase(BottleneckCategory.FrameRateLimit)]
    public void Frames_not_limited_by_rendering_get_no_group_proposal(BottleneckCategory category)
    {
        var results = Recommend(Observe(category, AdvisorConfidence.High),
            Evidence(Metric(VirtualTexturingMetrics.WorkingSetLodBias, 2)), AllGroups());
        Assert.That(results.Select(r => r.Direction), Is.All.EqualTo(RecommendationDirection.NoRecommendation));
        Assert.That(results.Select(r => r.Confidence), Is.All.EqualTo(AdvisorConfidence.InsufficientEvidence));
    }

    [Test]
    public void Low_confidence_or_low_severity_rendering_load_is_not_enough()
    {
        var weak = Recommend(Observe(BottleneckCategory.RenderingGpu, AdvisorConfidence.Low), Evidence(), AllGroups());
        var light = Recommend(new BottleneckObservation(BottleneckCategory.RenderingGpu, BottleneckSeverity.Low,
            AdvisorConfidence.High, new[] { "gpu.frame.ms" }, "measured"), Evidence(), AllGroups());
        Assert.That(weak.Concat(light).Select(r => r.Direction), Is.All.EqualTo(RecommendationDirection.NoRecommendation));
        Assert.That(light.Select(r => r.Direction), Has.None.EqualTo(RecommendationDirection.HeadroomAvailable),
            "raising a group is never proposed");
    }

    [Test]
    public void Medium_confidence_load_keeps_medium_confidence()
    {
        var result = Recommend(Observe(BottleneckCategory.RenderingGpu, AdvisorConfidence.Medium), Evidence(),
            Group(QualityGroupRules.DynamicResolution)).Single();
        Assert.That(result.Confidence, Is.EqualTo(AdvisorConfidence.Medium));
    }

    [TestCase("Low", null, null, TestName = "Lowest level")]
    [TestCase("Custom", null, null, TestName = "Custom level")]
    [TestCase("High", false, null, TestName = "Dropdown locked")]
    [TestCase("High", null, false, TestName = "Dropdown hidden")]
    public void Groups_without_a_restorable_lower_level_get_no_proposal(string current, bool? enabled, bool? visible)
    {
        var setting = Group(QualityGroupRules.Shadows, current);
        setting.IsCurrentlyEnabled = enabled;
        setting.IsCurrentlyVisible = visible;
        var result = Recommend(Observe(BottleneckCategory.RenderingGpu, AdvisorConfidence.High), Evidence(), setting).Single();
        Assert.That(result.Direction, Is.EqualTo(RecommendationDirection.NoRecommendation));
        Assert.That(result.RecommendedValue, Is.EqualTo(current));
    }

    [TestCase(null, RecommendationDirection.NoRecommendation, TestName = "VT bias not read")]
    [TestCase(1d, RecommendationDirection.NoRecommendation, TestName = "VT bias neutral")]
    [TestCase(1.25d, RecommendationDirection.LowerRecommended, TestName = "VT bias above neutral")]
    public void Texture_needs_the_game_to_already_bias_virtual_textures(double? bias, RecommendationDirection expected)
    {
        var evidence = bias.HasValue
            ? Evidence(Metric(VirtualTexturingMetrics.WorkingSetLodBias, bias.Value))
            : Evidence(NamedMetricValue.Unavailable(VirtualTexturingMetrics.WorkingSetLodBias, "not read"));
        var result = Recommend(Observe(BottleneckCategory.RenderingGpu, AdvisorConfidence.High), evidence,
            Group(QualityGroupRules.Texture)).Single();
        Assert.That(result.Direction, Is.EqualTo(expected));
    }

    [Test]
    public void Texture_without_any_evidence_snapshot_gets_no_proposal()
    {
        var result = new RecommendationEngine().Build(new[] { Observe(BottleneckCategory.RenderingGpu, AdvisorConfidence.High) },
            new[] { Group(QualityGroupRules.Texture) }).Single();
        Assert.That(result.Direction, Is.EqualTo(RecommendationDirection.NoRecommendation));
    }

    [Test]
    public void Group_setting_ids_and_tags_are_distinct_from_the_overall_preset()
    {
        Assert.That(QualityGroupRules.All.Select(r => r.SemanticTag), Is.Unique);
        Assert.That(QualityGroupRules.All.Select(r => r.SettingId), Is.Unique);
        Assert.That(QualityGroupRules.All.Select(r => r.SemanticTag), Has.None.EqualTo("rendering.ordered-quality"));
        Assert.That(QualityGroupRules.All.Select(r => r.MaximumConfidence), Has.None.EqualTo(AdvisorConfidence.High));
        Assert.That(QualityGroupRules.ForTags(new[] { "rendering.ordered-quality" }), Is.Null);
        Assert.That(QualityGroupRules.ForTags(null), Is.Null);
    }

    [Test]
    public void Projector_reports_the_median_rendering_counters_and_vt_bias_of_the_capture()
    {
        var capture = new CaptureSession("capture", new CaptureTrigger(CaptureTriggerKind.Manual, 1, null), 8);
        capture.AddGlobalSample(Sample(1, 1000, VirtualTexturingReading.FromSystem(1.5f, 0, 1, 2, 0, 0)));
        capture.AddGlobalSample(Sample(2, 3000, VirtualTexturingReading.FromSystem(0f, 0, 1, 2, 0, 0)));
        capture.AddGlobalSample(Sample(3, 2000, VirtualTexturingReading.FromSystem(2.5f, 0, 1, 2, 0, 0)));

        var evidence = new CaptureAdvisorEvidenceProjector().Project(capture);
        Assert.That(evidence.Find("render.shadowCasters").Value, Is.EqualTo(2000));
        // The sample before the first readback (bias 0 → null) is left out instead of counting as 0.
        Assert.That(evidence.Find(VirtualTexturingMetrics.WorkingSetLodBias).Value, Is.EqualTo(2));
        Assert.That(evidence.Find("render.triangles").Availability, Is.EqualTo(MetricAvailability.Unavailable));

        var empty = new CaptureSession("empty", new CaptureTrigger(CaptureTriggerKind.Manual, 1, null), 8);
        empty.AddGlobalSample(new GlobalMetricsSnapshot(1, 1, 1, null));
        var missing = new CaptureAdvisorEvidenceProjector().Project(empty);
        Assert.That(missing.Find(VirtualTexturingMetrics.WorkingSetLodBias).Availability, Is.EqualTo(MetricAvailability.Unavailable));
        Assert.That(missing.Find("render.shadowCasters").Availability, Is.EqualTo(MetricAvailability.Unavailable));
    }

    private static GlobalMetricsSnapshot Sample(double seconds, double shadowCasters, VirtualTexturingReading vt)
        => new(seconds, 1, 1,
            new Dictionary<string, RecorderReading> { ["Render\u001fShadow Casters Count"] = new(shadowCasters, 1) },
            new Dictionary<string, string> { ["Render\u001fShadow Casters Count"] = "Count" },
            virtualTexturing: vt);

    private static GameSettingDescriptor Group(QualityGroupRule rule, string current = "High") => new()
    {
        SettingId = rule.SettingId,
        Category = "graphics",
        DisplayName = rule.DisplayName,
        ValueKind = SettingValueKind.Enumeration,
        CurrentValue = current,
        AllowedValues = Levels,
        SemanticTags = new[] { rule.SemanticTag },
        IsReadable = true,
        IsUserFacing = true,
        IsWritable = true,
        CapabilityState = SettingCapabilityState.Available,
        ApplyBehavior = SettingApplyBehavior.ConfirmationRequired
    };

    private static GameSettingDescriptor[] AllGroups() => QualityGroupRules.All.Select(rule => Group(rule)).ToArray();

    private static BottleneckObservation Observe(BottleneckCategory category, AdvisorConfidence confidence)
        => new(category, BottleneckSeverity.High, confidence, new[] { "gpu.frame.ms" }, "measured");

    private static NamedMetricValue Metric(string id, double value)
        => NamedMetricValue.Available(id, value, MetricConfidence.Full, "Count");

    private static AdvisorEvidenceSnapshot Evidence(params NamedMetricValue[] metrics) => new(DateTime.UtcNow, metrics);

    private static IReadOnlyList<SettingRecommendation> Recommend(BottleneckObservation observation,
        AdvisorEvidenceSnapshot evidence, params GameSettingDescriptor[] settings)
        => new RecommendationEngine().Build(new[] { observation }, settings, evidence);
}
