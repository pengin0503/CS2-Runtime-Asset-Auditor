using System;
using NUnit.Framework;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Core.Scanning;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    [TestFixture]
    public sealed class ScanSessionTests
    {
        private static readonly DateTimeOffset StartedAt = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);

        [Test]
        public void Phase_one_stages_advance_in_order_and_publication_opens_only_at_finalizing()
        {
            var session = ScanSession.Start(ScanKind.Census, worldGeneration: 3, startedAt: StartedAt);
            Assert.That(session.State, Is.EqualTo(ScanState.Running));
            Assert.That(session.Stage, Is.EqualTo(ScanStage.Preparing));
            Assert.That(session.CanPublish, Is.False);

            session.TransitionTo(ScanStage.CapturingCatalog);
            session.TransitionTo(ScanStage.ProcessingCatalog);
            session.TransitionTo(ScanStage.CapturingObjectCensus);
            session.TransitionTo(ScanStage.ReducingObjectCensus);
            session.TransitionTo(ScanStage.CapturingNetworkCensus);
            session.TransitionTo(ScanStage.ReducingNetworkCensus);
            session.TransitionTo(ScanStage.Finalizing);

            Assert.That(session.CanPublish, Is.True);
            session.Complete();
            Assert.That(session.State, Is.EqualTo(ScanState.Completed));
            Assert.That(session.Stage, Is.EqualTo(ScanStage.Completed));
            Assert.That(session.CanPublish, Is.False);
        }

        [Test]
        public void Asset_audit_uses_analysis_specific_stages_and_progress_count()
        {
            var session = ScanSession.Start(ScanKind.AssetAudit, 1, StartedAt);

            session.TransitionTo(ScanStage.ResolvingRenderGraph);
            Assert.That(session.Progress.StageNumber, Is.EqualTo(2));
            Assert.That(session.Progress.TotalStages, Is.EqualTo(6));
            session.TransitionTo(ScanStage.CollectingGeometry);
            session.TransitionTo(ScanStage.CollectingSurfaceTexture);
            session.TransitionTo(ScanStage.EvaluatingFindings);
            session.TransitionTo(ScanStage.Finalizing);

            Assert.That(session.CanPublish, Is.True);
            session.Complete();
            Assert.That(session.Progress.TotalStages, Is.EqualTo(6));
        }

        [Test]
        public void Deep_inspection_has_selected_asset_only_stage_sequence()
        {
            var session = ScanSession.Start(ScanKind.DeepInspection, 1, StartedAt);
            session.TransitionTo(ScanStage.DeepInspecting);
            Assert.That(session.Progress.StageNumber, Is.EqualTo(2));
            Assert.That(session.Progress.TotalStages, Is.EqualTo(3));
            session.TransitionTo(ScanStage.Finalizing);
            Assert.That(session.CanPublish, Is.True);
        }

        [Test]
        public void Backwards_stage_transition_is_rejected()
        {
            var session = ScanSession.Start(ScanKind.Census, 1, StartedAt);
            session.TransitionTo(ScanStage.CapturingCatalog);
            session.TransitionTo(ScanStage.ProcessingCatalog);
            session.TransitionTo(ScanStage.CapturingObjectCensus);
            session.TransitionTo(ScanStage.ReducingObjectCensus);

            Assert.Throws<InvalidOperationException>(() => session.TransitionTo(ScanStage.CapturingObjectCensus));
            Assert.That(session.Stage, Is.EqualTo(ScanStage.ReducingObjectCensus));
        }

        [Test]
        public void Cancellation_during_managed_reduction_blocks_publication()
        {
            var session = ScanSession.Start(ScanKind.Census, 1, StartedAt);
            session.TransitionTo(ScanStage.CapturingCatalog);
            session.TransitionTo(ScanStage.ProcessingCatalog);
            session.TransitionTo(ScanStage.CapturingObjectCensus);
            session.TransitionTo(ScanStage.ReducingObjectCensus);
            session.RequestCancellation();

            Assert.That(session.State, Is.EqualTo(ScanState.CancellationRequested));
            Assert.That(session.Stage, Is.EqualTo(ScanStage.ReducingObjectCensus));
            Assert.That(session.CanPublish, Is.False);
            session.MarkCancelled();
            Assert.That(session.State, Is.EqualTo(ScanState.Cancelled));
        }

        [Test]
        public void Cancellation_during_scheduled_capture_keeps_stage_until_safe_discard()
        {
            var session = ScanSession.Start(ScanKind.Census, 1, StartedAt);
            session.TransitionTo(ScanStage.CapturingCatalog);
            session.TransitionTo(ScanStage.ProcessingCatalog);
            session.TransitionTo(ScanStage.CapturingObjectCensus);
            session.RequestCancellation();

            Assert.That(session.State, Is.EqualTo(ScanState.CancellationRequested));
            Assert.That(session.Stage, Is.EqualTo(ScanStage.CapturingObjectCensus));
            session.MarkCancelled();
            Assert.That(session.State, Is.EqualTo(ScanState.Cancelled));
        }

        [Test]
        public void Unknown_total_uses_indeterminate_progress_without_a_percentage()
        {
            var session = ScanSession.Start(ScanKind.AssetAudit, 1, StartedAt);
            session.TransitionTo(ScanStage.ResolvingRenderGraph);
            session.ReportProgress(completedItems: null, totalItems: null);

            Assert.That(session.Progress.IsIndeterminate, Is.True);
            Assert.That(session.Progress.PercentComplete, Is.Null);
            Assert.That(session.Progress.StageNumber, Is.EqualTo(2));
        }

        [Test]
        public void Exact_progress_reports_the_measured_fraction()
        {
            var session = ScanSession.Start(ScanKind.AssetAudit, 1, StartedAt);
            session.TransitionTo(ScanStage.ResolvingRenderGraph);
            session.ReportProgress(completedItems: 3, totalItems: 4);

            Assert.That(session.Progress.IsIndeterminate, Is.False);
            Assert.That(session.Progress.PercentComplete, Is.EqualTo(75.0).Within(0.0001));
            Assert.That(session.Progress.CompletedItems, Is.EqualTo(3));
            Assert.That(session.Progress.TotalItems, Is.EqualTo(4));
        }

        [Test]
        public void Failure_and_cancellation_never_allow_publication()
        {
            var failed = ScanSession.Start(ScanKind.Census, 1, StartedAt);
            failed.Fail("APA-CEN-001");
            Assert.That(failed.State, Is.EqualTo(ScanState.Failed));
            Assert.That(failed.CanPublish, Is.False);

            var cancelled = ScanSession.Start(ScanKind.Census, 1, StartedAt);
            cancelled.RequestCancellation();
            cancelled.MarkCancelled();
            Assert.That(cancelled.CanPublish, Is.False);
        }

        [Test]
        public void Cancelled_same_world_scan_keeps_the_previous_published_snapshot()
        {
            var record = new PrefabRecord(new PrefabKey("Building.FireStation", "Building"), "Fire Station",
                PrefabTraits.Building, new AssetOriginEvidence());
            var first = new CensusReducer(new[] { record }, 4, 1, StartedAt, new ScanOptions(true, true)).BuildSnapshot();
            var working = new CensusReducer(new[] { record }, 4, 2, StartedAt.AddMinutes(1), new ScanOptions(true, true)).BuildSnapshot();
            var published = new PublishedAuditState();
            published.ResetForWorld(4);
            Assert.That(published.TryPublishCensus(first, scanSucceeded: true), Is.True);

            var session = ScanSession.Start(ScanKind.Census, 4, StartedAt);
            session.RequestCancellation();
            session.MarkCancelled();

            Assert.That(published.TryPublishCensus(working, scanSucceeded: session.State == ScanState.Completed), Is.False);
            Assert.That(published.Census, Is.SameAs(first));
        }
    }
}
