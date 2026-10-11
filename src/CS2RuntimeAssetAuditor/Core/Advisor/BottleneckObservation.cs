using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core.Advisor
{
    public enum BottleneckCategory
    {
        RenderingGpu, SimulationCpu, MemoryGc, Pathfinding, Unknown, MainThreadCpu, FrameRateLimit,
        /// <summary>The render thread (issuing draw calls to the graphics API) limits the frame rate.</summary>
        RenderThreadCpu,
        /// <summary>A frame-rate cap (VSync or a target frame rate), not a workload, sets the frame time.</summary>
        PresentLimited
    }
    public enum BottleneckSeverity { Low, Medium, High }
    public enum AdvisorConfidence { InsufficientEvidence, Low, Medium, High }

    public sealed class BottleneckObservation
    {
        public BottleneckObservation(BottleneckCategory category, BottleneckSeverity severity,
            AdvisorConfidence confidence, IEnumerable<string> evidenceIds, string rationale)
        {
            Category = category;
            Severity = severity;
            Confidence = confidence;
            EvidenceIds = (evidenceIds ?? Enumerable.Empty<string>()).ToArray();
            Rationale = rationale ?? string.Empty;
        }

        public BottleneckCategory Category { get; }
        public BottleneckSeverity Severity { get; }
        public AdvisorConfidence Confidence { get; }
        public IReadOnlyList<string> EvidenceIds { get; }
        public string Rationale { get; }
    }
}
