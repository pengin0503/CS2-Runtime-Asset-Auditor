using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeProfiler.Core;
using CS2RuntimeProfiler.Core.Advisor;
using CS2RuntimeProfiler.UI;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests
{
    [TestFixture]
    public sealed class AdvisorCoordinatorPolicyTests
    {
        [Test]
        public void Only_explicitly_selected_completed_capture_is_diagnosed()
        {
            var completed = new CaptureSession("finished", new CaptureTrigger(CaptureTriggerKind.Manual, 1, null), 8);
            completed.AddGlobalSample(new GlobalMetricsSnapshot(1, 1, 0.5, null));
            var current = new CaptureSession("active", new CaptureTrigger(CaptureTriggerKind.Manual, 2, null), 8);
            var coordinator = new AdvisorCoordinator(() => Array.Empty<GameSettingDescriptor>());

            Assert.That(coordinator.CurrentState.SelectedCaptureId, Is.Empty);
            Assert.That(coordinator.DiagnoseCompletedCapture(new[] { completed }, "active"), Is.False);
            Assert.That(coordinator.CurrentState.SelectedCaptureId, Is.Empty);
            Assert.That(coordinator.DiagnoseCompletedCapture(new[] { completed }, "finished"), Is.True);
            Assert.That(coordinator.CurrentState.SelectedCaptureId, Is.EqualTo("finished"));
            Assert.That(coordinator.CurrentState.Observations.Single().Category, Is.EqualTo(BottleneckCategory.SimulationCpu));
        }

        [Test]
        public void Failed_catalog_degrades_only_advisor_state()
        {
            var coordinator = new AdvisorCoordinator(() => throw new InvalidOperationException("catalog unavailable"));
            var state = coordinator.CurrentState;
            Assert.That(state.IsAvailable, Is.False);
            Assert.That(state.Catalog, Is.Empty);
            Assert.That(UiSnapshotBuilder.Build(new UiSnapshotInput()).Global.Available, Is.False);
        }

        [Test]
        public void Apply_policy_refuses_a_stale_recommendation_after_the_user_changes_options()
        {
            var recommendation = new SettingRecommendation("shadow", "Shadow", "High", "Medium",
                RecommendationDirection.LowerRecommended, RecommendationPriority.High, AdvisorConfidence.High,
                "measured", new[] { "gpu.frame.ms" }, SettingCapabilityState.Available,
                SettingApplyBehavior.ApplyRequired);
            Assert.That(AdvisorApplyPolicy.IsCurrentRecommendation(recommendation, "Low", "Medium"), Is.False);
            Assert.That(AdvisorApplyPolicy.IsCurrentRecommendation(recommendation, "High", "Medium"), Is.True);
        }

    }
}
