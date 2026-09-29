using CS2RuntimeAssetAuditor.Assets.Core.Scanning;
using CS2RuntimeAssetAuditor.Coordination;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets;

public class PublishedAssetSnapshotIdentityTests
{
    private static readonly DateTimeOffset AuditStart = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void Audit_interval_spans_request_start_to_publication()
    {
        var published = AuditStart.AddMinutes(3);
        var identity = PublishedAssetSnapshotIdentity.ForAudit("session", 4, AuditStart, published);

        Assert.Multiple(() =>
        {
            Assert.That(identity.SnapshotId, Is.EqualTo("session/analysis/4"));
            Assert.That(identity.StartedAtUtc, Is.EqualTo(AuditStart));
            Assert.That(identity.CompletedAtUtc, Is.EqualTo(published));
            Assert.That(identity.EnrichedAtUtc, Is.Null);
        });
    }

    [Test]
    public void Deep_inspection_enrichment_keeps_identity_and_audit_interval()
    {
        var identity = PublishedAssetSnapshotIdentity.ForAudit("session", 4, AuditStart, AuditStart.AddMinutes(3));
        var enriched = identity.WithEnrichment(AuditStart.AddHours(1));

        Assert.Multiple(() =>
        {
            Assert.That(enriched.SnapshotId, Is.EqualTo(identity.SnapshotId));
            Assert.That(enriched.StartedAtUtc, Is.EqualTo(identity.StartedAtUtc));
            Assert.That(enriched.CompletedAtUtc, Is.EqualTo(identity.CompletedAtUtc));
            Assert.That(enriched.EnrichedAtUtc, Is.EqualTo(AuditStart.AddHours(1)));
        });
    }

    [Test]
    public void Capture_taken_during_a_long_audit_links_as_overlapping_not_after()
    {
        // Regression: the completion time used to be the analysis start, which excluded the collection period.
        var identity = PublishedAssetSnapshotIdentity.ForAudit("session", 1, AuditStart, AuditStart.AddMinutes(5));
        var link = DiagnosticEvidenceBridge.TryLink("session", "capture", AuditStart.AddMinutes(2), AuditStart.AddMinutes(2.5),
            identity.SessionId, identity.SnapshotId, identity.StartedAtUtc, identity.CompletedAtUtc);

        Assert.That(link!.RelativeTiming, Is.EqualTo(EvidenceRelativeTiming.Overlapping));
    }

    [Test]
    public void Clock_skew_never_produces_an_inverted_interval()
    {
        var identity = PublishedAssetSnapshotIdentity.ForAudit("session", 1, AuditStart, AuditStart.AddSeconds(-1));
        Assert.That(identity.CompletedAtUtc, Is.EqualTo(identity.StartedAtUtc));
    }
}
