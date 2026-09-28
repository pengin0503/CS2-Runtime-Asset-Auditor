using System;
using CS2RuntimeAssetAuditor.Assets.Core.Diagnostics;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    public sealed class ScanTelemetryTests
    {
        private static readonly DateTimeOffset StartedAt = new DateTimeOffset(2026, 9, 27, 8, 0, 0, TimeSpan.Zero);

        [Test]
        public void SnapshotTracksProcessedElapsedMaxAndNearestRankP95()
        {
            var telemetry = new ScanTelemetry(StartedAt);
            for (var milliseconds = 1; milliseconds <= 20; milliseconds++)
                telemetry.RecordManagedSlice(TimeSpan.FromMilliseconds(milliseconds), processedItems: milliseconds);

            var snapshot = telemetry.Snapshot(StartedAt.AddSeconds(3));

            Assert.That(snapshot.ProcessedItems, Is.EqualTo(210));
            Assert.That(snapshot.SliceCount, Is.EqualTo(20));
            Assert.That(snapshot.ElapsedMilliseconds, Is.EqualTo(3000).Within(0.001));
            Assert.That(snapshot.MaxSliceMilliseconds, Is.EqualTo(20).Within(0.001));
            Assert.That(snapshot.P95SliceMilliseconds, Is.EqualTo(19).Within(0.001));
        }

        [Test]
        public void SampleWindowIsBoundedWithoutChangingLifetimeMaxOrProcessedCount()
        {
            var telemetry = new ScanTelemetry(StartedAt, sampleCapacity: 4);
            for (var milliseconds = 1; milliseconds <= 6; milliseconds++)
                telemetry.RecordManagedSlice(TimeSpan.FromMilliseconds(milliseconds), processedItems: 1);

            var snapshot = telemetry.Snapshot(StartedAt.AddSeconds(1));

            Assert.That(snapshot.ProcessedItems, Is.EqualTo(6));
            Assert.That(snapshot.SliceCount, Is.EqualTo(6));
            Assert.That(snapshot.SampleCount, Is.EqualTo(4));
            Assert.That(snapshot.MaxSliceMilliseconds, Is.EqualTo(6).Within(0.001));
            Assert.That(snapshot.P95SliceMilliseconds, Is.EqualTo(6).Within(0.001));
        }

        [Test]
        public void DiagnosticAggregatorStillAggregatesRepeatedIdenticalFailures()
        {
            var diagnostics = new DiagnosticAggregator();
            diagnostics.Add("APA-GEO-001", "metadata_read_failed", StartedAt);
            diagnostics.Add("APA-GEO-001", "metadata_read_failed", StartedAt.AddSeconds(1));
            diagnostics.Add("APA-TEX-001", "texture_read_failed", StartedAt.AddSeconds(2));

            var snapshot = diagnostics.Snapshot();

            Assert.That(snapshot, Has.Count.EqualTo(2));
            Assert.That(snapshot[0].Count, Is.EqualTo(2));
            Assert.That(snapshot[0].FirstSeenAt, Is.EqualTo(StartedAt));
            Assert.That(snapshot[0].LastSeenAt, Is.EqualTo(StartedAt.AddSeconds(1)));
            Assert.That(diagnostics.DistinctCount, Is.EqualTo(2));
            Assert.That(diagnostics.OccurrenceCount, Is.EqualTo(3));
        }
    }
}
