using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;

namespace CS2RuntimeAssetAuditor.Assets.Core.Findings
{
    public sealed class GeometryFindingInput
    {
        public GeometryFindingInput(string assetId, string peerCategory, bool lowerLodPresent, bool requiredRenderReferenceBroken, long? lod0VertexCount, long? lod1VertexCount, bool renderStructureResolved = true)
        {
            if (string.IsNullOrWhiteSpace(assetId)) throw new ArgumentException("Asset ID is required.", nameof(assetId));
            if (string.IsNullOrWhiteSpace(peerCategory)) throw new ArgumentException("Peer category is required.", nameof(peerCategory));
            AssetId = assetId; PeerCategory = peerCategory; LowerLodPresent = lowerLodPresent; RequiredRenderReferenceBroken = requiredRenderReferenceBroken; Lod0VertexCount = lod0VertexCount; Lod1VertexCount = lod1VertexCount; RenderStructureResolved = renderStructureResolved;
        }
        public string AssetId { get; }
        public string PeerCategory { get; }
        public bool LowerLodPresent { get; }
        public bool RequiredRenderReferenceBroken { get; }
        public long? Lod0VertexCount { get; }
        public long? Lod1VertexCount { get; }
        // False when the render structure was not resolved (unknown, not applicable, or failed coverage);
        // LOD rules then have no evidence to evaluate and must not report an absent LOD.
        public bool RenderStructureResolved { get; }
    }

    public sealed class ExposureFindingInput
    {
        public ExposureFindingInput(string assetId, long liveReferences, long subordinateReferences)
        {
            if (string.IsNullOrWhiteSpace(assetId)) throw new ArgumentException("Asset ID is required.", nameof(assetId));
            if (liveReferences < 0 || subordinateReferences < 0) throw new ArgumentOutOfRangeException(nameof(liveReferences));
            AssetId = assetId; LiveReferences = liveReferences; SubordinateReferences = subordinateReferences;
        }
        public string AssetId { get; }
        public long LiveReferences { get; }
        public long SubordinateReferences { get; }
    }

    public sealed class TextureFindingInput
    {
        public TextureFindingInput(string assetId, string textureId, Availability availability, string? diagnosticCode)
        {
            if (string.IsNullOrWhiteSpace(assetId)) throw new ArgumentException("Asset ID is required.", nameof(assetId));
            if (string.IsNullOrWhiteSpace(textureId)) throw new ArgumentException("Texture ID is required.", nameof(textureId));
            AssetId = assetId; TextureId = textureId; Availability = availability; DiagnosticCode = diagnosticCode;
        }
        public string AssetId { get; }
        public string TextureId { get; }
        public Availability Availability { get; }
        public string? DiagnosticCode { get; }
    }

    public sealed class FindingEngine
    {
        public IReadOnlyList<Finding> EvaluateGeometryLod(GeometryFindingInput input, DateTimeOffset capturedAt)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var findings = new List<Finding>();
            if (input.RequiredRenderReferenceBroken)
                findings.Add(new Finding("APA-INT-001", FindingStatus.Warning, FindingCategory.Integrity, "Required render reference is unresolved", "A required render reference could not be resolved; this is structural evidence rather than a performance heuristic.", new[] { "asset=" + input.AssetId }, FindingBasis.Deterministic, RuleSetInfo.Version));

            if (!input.RenderStructureResolved)
                return findings.AsReadOnly();

            if (!input.LowerLodPresent)
            {
                findings.Add(new Finding("APA-LOD-001", FindingStatus.Notice, FindingCategory.Lod, "No lower LOD observed", "No lower LOD was observed. This is an observation and is not automatically a performance defect.", new[] { "asset=" + input.AssetId }, FindingBasis.Observation, RuleSetInfo.Version));
                return findings.AsReadOnly();
            }

            if (input.Lod0VertexCount.HasValue && input.Lod1VertexCount.HasValue)
            {
                var retention = LodMetrics.RetentionPercent(input.Lod0VertexCount.Value, input.Lod1VertexCount.Value, capturedAt);
                if (retention.HasValue && retention.Value >= RuleSetInfo.WeakLodRetentionPercent)
                    findings.Add(new Finding("APA-LOD-002", FindingStatus.PotentialIssue, FindingCategory.Lod, "Weak LOD vertex reduction", "The lower LOD retains an unusually large share of LOD0 vertices under the versioned project heuristic; this does not prove a runtime bottleneck.", new[] { "asset=" + input.AssetId, "vertexRetentionPercent=" + retention.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), "heuristicThresholdPercent=" + RuleSetInfo.WeakLodRetentionPercent.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) }, FindingBasis.Heuristic, RuleSetInfo.Version));
            }
            return findings.AsReadOnly();
        }

        public IReadOnlyList<Finding> EvaluateExposure(ExposureFindingInput input, DateTimeOffset capturedAt)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.LiveReferences < RuleSetInfo.HighExposureReferenceCount && input.SubordinateReferences < RuleSetInfo.HighExposureReferenceCount)
                return Array.Empty<Finding>();
            return Array.AsReadOnly(new[] { new Finding("APA-EXP-001", FindingStatus.Observed, FindingCategory.Exposure, "High snapshot exposure", "This asset appears many times in the current city snapshot. Exposure can amplify cost if the asset is expensive, but exposure alone does not prove render cost or a bottleneck.", new[] { "asset=" + input.AssetId, "liveReferences=" + input.LiveReferences, "subordinateReferences=" + input.SubordinateReferences }, FindingBasis.Observation, RuleSetInfo.Version) });
        }

        public IReadOnlyList<Finding> EvaluateTextureRead(TextureFindingInput input, DateTimeOffset capturedAt)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Availability == Availability.Available) return Array.Empty<Finding>();
            if (input.Availability != Availability.Failed && input.Availability != Availability.Unsupported) return Array.Empty<Finding>();
            var evidence = new List<string> { "asset=" + input.AssetId, "texture=" + input.TextureId, "availability=" + input.Availability };
            if (!string.IsNullOrWhiteSpace(input.DiagnosticCode)) evidence.Add("diagnostic=" + input.DiagnosticCode);
            return Array.AsReadOnly(new[] { new Finding("APA-TEX-001", FindingStatus.Unknown, FindingCategory.Texture, "Texture metadata unavailable", "Texture metadata could not be read reliably. The failure is scoped to this texture and does not imply that unrelated assets failed analysis.", evidence, FindingBasis.Observation, RuleSetInfo.Version) });
        }
    }
}
