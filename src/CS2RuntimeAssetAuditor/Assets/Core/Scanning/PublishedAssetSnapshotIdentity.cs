using System;

namespace CS2RuntimeAssetAuditor.Assets.Core.Scanning
{
    /// <summary>
    /// Identity and collection interval of a published Asset Audit snapshot. The interval spans the whole
    /// audit, from the request start to the frame the analysis was published. Deep Inspection enriches one
    /// render asset of an existing snapshot; it keeps the snapshot identity and interval, because the bulk of
    /// the evidence still comes from the original audit, and records the enrichment time separately.
    /// </summary>
    public sealed class PublishedAssetSnapshotIdentity
    {
        private PublishedAssetSnapshotIdentity(string sessionId, string snapshotId,
            DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc, DateTimeOffset? enrichedAtUtc)
        {
            SessionId = sessionId;
            SnapshotId = snapshotId;
            StartedAtUtc = startedAtUtc;
            CompletedAtUtc = completedAtUtc;
            EnrichedAtUtc = enrichedAtUtc;
        }

        public string SessionId { get; }
        public string SnapshotId { get; }
        public DateTimeOffset StartedAtUtc { get; }
        public DateTimeOffset CompletedAtUtc { get; }
        public DateTimeOffset? EnrichedAtUtc { get; }

        public static PublishedAssetSnapshotIdentity ForAudit(string sessionId, long analysisGeneration,
            DateTimeOffset auditStartedAt, DateTimeOffset publishedAt)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("A session ID is required.", nameof(sessionId));
            if (analysisGeneration <= 0) throw new ArgumentOutOfRangeException(nameof(analysisGeneration));
            var started = auditStartedAt.ToUniversalTime();
            var completed = publishedAt.ToUniversalTime();
            if (completed < started)
                completed = started;
            return new PublishedAssetSnapshotIdentity(sessionId, sessionId + "/analysis/" + analysisGeneration, started, completed, null);
        }

        public PublishedAssetSnapshotIdentity WithEnrichment(DateTimeOffset enrichedAt)
            => new PublishedAssetSnapshotIdentity(SessionId, SnapshotId, StartedAtUtc, CompletedAtUtc, enrichedAt.ToUniversalTime());
    }
}
