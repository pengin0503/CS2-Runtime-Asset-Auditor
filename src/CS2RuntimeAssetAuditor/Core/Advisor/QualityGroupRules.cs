using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core.Advisor
{
    /// <summary>
    /// A detailed graphics-quality group of the Options screen (shadows, level of detail, dynamic resolution, texture)
    /// offered as a trial candidate: lower it one step, then measure again. A group is only proposed for the bottleneck
    /// it can plausibly relieve, never with more than medium confidence, and never on supporting counters alone.
    /// </summary>
    public sealed class QualityGroupRule
    {
        public QualityGroupRule(string semanticTag, string settingTypeName, string displayName, IReadOnlyList<BottleneckCategory> categories,
            IReadOnlyList<string> supportingEvidenceIds, string requiredEvidenceId, AdvisorConfidence maximumConfidence,
            string rationale)
        {
            SemanticTag = semanticTag;
            SettingTypeName = settingTypeName;
            DisplayName = displayName;
            Categories = categories;
            SupportingEvidenceIds = supportingEvidenceIds;
            RequiredEvidenceId = requiredEvidenceId;
            MaximumConfidence = maximumConfidence;
            Rationale = rationale;
        }

        public string SemanticTag { get; }

        /// <summary>The Game.Settings QualitySetting subclass (1.6.2f1) whose level the setting changes.</summary>
        public string SettingTypeName { get; }

        /// <summary>The name shown in the Advisor, like the overall preset's "Graphics quality preset".</summary>
        public string DisplayName { get; }

        public string SettingId => "Game.Settings." + SettingTypeName + "::" + QualityGroupRules.LevelMember;

        /// <summary>The frame-time bottlenecks this group can relieve; any other one (or a mixed one) gets no proposal.</summary>
        public IReadOnlyList<BottleneckCategory> Categories { get; }

        /// <summary>Counters shown with the proposal when they were measured; they never trigger it.</summary>
        public IReadOnlyList<string> SupportingEvidenceIds { get; }

        /// <summary>Evidence that must be available and above its neutral value for a proposal; empty when none is needed.</summary>
        public string RequiredEvidenceId { get; }

        public AdvisorConfidence MaximumConfidence { get; }

        /// <summary>Why a step down is worth trying, including the visible cost.</summary>
        public string Rationale { get; }
    }

    public static class QualityGroupRules
    {
        /// <summary>The setting member name used for a group's level (the Options dropdown has no property of its own).</summary>
        public const string LevelMember = "QualityLevel";

        public static readonly QualityGroupRule Shadows = new QualityGroupRule(
            "rendering.quality-group.shadows", "ShadowsQualitySettings", "Shadow quality",
            new[] { BottleneckCategory.RenderingGpu, BottleneckCategory.RenderThreadCpu },
            new[] { "render.shadowCasters" }, string.Empty, AdvisorConfidence.Medium,
            "Rendering limits the frame rate, and shadows are drawn again for every shadow caster in view, which costs GPU time and render-thread draw calls. Lowering shadow quality by one step is worth trying and measuring again; shadows become less detailed.");

        public static readonly QualityGroupRule LevelOfDetail = new QualityGroupRule(
            "rendering.quality-group.level-of-detail", "LevelOfDetailQualitySettings", "Level of detail",
            new[] { BottleneckCategory.RenderingGpu, BottleneckCategory.RenderThreadCpu },
            new[] { "render.triangles", "render.drawCalls" }, string.Empty, AdvisorConfidence.Medium,
            "Rendering limits the frame rate. Lowering the level-of-detail quality by one step switches objects to simpler meshes closer to the camera, which reduces the triangles and objects drawn; it is worth trying and measuring again. Distant objects lose detail sooner.");

        public static readonly QualityGroupRule DynamicResolution = new QualityGroupRule(
            "rendering.quality-group.dynamic-resolution", "DynamicResolutionScaleSettings", "Dynamic resolution",
            new[] { BottleneckCategory.RenderingGpu },
            Array.Empty<string>(), string.Empty, AdvisorConfidence.Medium,
            "The GPU limits the frame rate. Letting the game render at a lower resolution reduces the pixels the GPU shades each frame; it is worth trying and measuring again. The image looks softer while the resolution is reduced, and it does not help when the CPU is the limit.");

        // VT pressure alone does not justify a texture change: the GPU must also limit the frame rate, and the game must
        // already apply a working-set LOD bias (factor above 1, the neutral value), i.e. sample coarser mips to fit.
        public static readonly QualityGroupRule Texture = new QualityGroupRule(
            "rendering.quality-group.texture", "TextureQualitySettings", "Texture quality",
            new[] { BottleneckCategory.RenderingGpu },
            Array.Empty<string>(), "vt.workingSetLodBias", AdvisorConfidence.Low,
            "The GPU limits the frame rate and the game is already sampling coarser virtual-texture mips to fit its working set. Lowering texture quality by one step is worth trying and measuring again; textures look less sharp, and the change rebuilds the virtual-texture cache, which can stall the game briefly. Whether it helps has not been measured in this game yet.");

        public static readonly IReadOnlyList<QualityGroupRule> All = new[] { Shadows, LevelOfDetail, DynamicResolution, Texture };

        public static QualityGroupRule? ForTags(IReadOnlyList<string>? tags)
            => tags == null ? null : All.FirstOrDefault(rule => tags.Contains(rule.SemanticTag, StringComparer.Ordinal));

        /// <summary>The neutral value of a required evidence id: the proposal needs the measured value above it.</summary>
        public static double NeutralValue(string evidenceId)
            => string.Equals(evidenceId, "vt.workingSetLodBias", StringComparison.Ordinal) ? 1d : 0d;
    }
}
