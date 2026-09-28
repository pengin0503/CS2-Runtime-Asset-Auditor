using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeProfiler.Core.Advisor
{
    public sealed class AdvisorCoordinator
    {
        private readonly Func<IReadOnlyList<GameSettingDescriptor>> _catalog;
        private readonly CaptureAdvisorEvidenceProjector _projector = new CaptureAdvisorEvidenceProjector();
        private readonly BottleneckClassifier _classifier = new BottleneckClassifier();
        private readonly RecommendationEngine _recommendations = new RecommendationEngine();
        private AdvisorState _state;

        public AdvisorCoordinator(Func<IReadOnlyList<GameSettingDescriptor>> catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _state = Build(null, string.Empty, string.Empty);
        }

        public AdvisorState CurrentState => _state;

        public bool DiagnoseCompletedCapture(IReadOnlyList<CaptureSession> completed, string captureId)
        {
            if (string.IsNullOrWhiteSpace(captureId)) return false;
            var capture = completed?.FirstOrDefault(item => item != null &&
                string.Equals(item.Id, captureId, StringComparison.Ordinal));
            if (capture == null) return false;
            _state = Build(capture, captureId, _state.BaselineCaptureId);
            return true;
        }

        public bool SelectBaseline(IReadOnlyList<CaptureSession> completed, string captureId)
        {
            if (string.IsNullOrWhiteSpace(captureId) || completed?.Any(item => item != null && item.Id == captureId) != true)
                return false;
            _state.BaselineCaptureId = captureId;
            return true;
        }

        private AdvisorState Build(CaptureSession capture, string selectedId, string baselineId)
        {
            IReadOnlyList<GameSettingDescriptor> catalog;
            string reason = null;
            try { catalog = _catalog() ?? Array.Empty<GameSettingDescriptor>(); }
            catch (Exception ex)
            {
                catalog = Array.Empty<GameSettingDescriptor>();
                reason = "Standard Options catalog unavailable: " + ex.GetType().Name;
            }

            AdvisorEvidenceSnapshot evidence = null;
            IReadOnlyList<BottleneckObservation> observations = Array.Empty<BottleneckObservation>();
            IReadOnlyList<SettingRecommendation> recommendations = Array.Empty<SettingRecommendation>();
            if (capture != null)
            {
                try
                {
                    evidence = _projector.Project(capture);
                    observations = _classifier.Classify(evidence);
                    recommendations = _recommendations.Build(observations, catalog);
                }
                catch (Exception ex)
                {
                    reason = "Advisor diagnosis unavailable: " + ex.GetType().Name;
                }
            }
            return new AdvisorState(evidence, observations, recommendations, reason)
            { Catalog = catalog, SelectedCaptureId = selectedId, BaselineCaptureId = baselineId };
        }
    }
}
