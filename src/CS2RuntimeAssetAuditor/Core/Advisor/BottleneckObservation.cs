using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core.Advisor
{
    public enum BottleneckCategory { RenderingGpu, SimulationCpu, MemoryGc, Pathfinding, Unknown }
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
