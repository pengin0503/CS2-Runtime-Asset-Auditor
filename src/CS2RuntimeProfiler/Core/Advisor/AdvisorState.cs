using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeProfiler.Core.Advisor
{
    public sealed class AdvisorState
    {
        public AdvisorState(AdvisorEvidenceSnapshot evidence, IEnumerable<BottleneckObservation> observations,
            IEnumerable<SettingRecommendation> recommendations, string unavailableReason = null)
        {
            Evidence = evidence;
            Observations = (observations ?? Enumerable.Empty<BottleneckObservation>()).ToArray();
            Recommendations = (recommendations ?? Enumerable.Empty<SettingRecommendation>()).ToArray();
            UnavailableReason = unavailableReason;
        }

        public AdvisorEvidenceSnapshot Evidence { get; }
        public IReadOnlyList<BottleneckObservation> Observations { get; }
        public IReadOnlyList<SettingRecommendation> Recommendations { get; }
        public string UnavailableReason { get; }
        public bool IsAvailable => UnavailableReason == null;
        public IReadOnlyList<GameSettingDescriptor> Catalog { get; set; } = new GameSettingDescriptor[0];
        public string SelectedCaptureId { get; set; } = string.Empty;
        public string BaselineCaptureId { get; set; } = string.Empty;
        public IReadOnlyList<SettingChange> Changes { get; set; } = new SettingChange[0];
        public AdvisorComparison Comparison { get; set; }
    }
}
