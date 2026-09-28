using System;

namespace CS2RuntimeAssetAuditor.Coordination
{
    public static class DiagnosticEvidenceBridge
    {
        public static DiagnosticEvidenceLink TryLink(string captureSessionId, string captureId,
            DateTimeOffset? captureStartedAtUtc, DateTimeOffset? captureCompletedAtUtc,
            string assetSessionId, string assetSnapshotId,
            DateTimeOffset? assetStartedAtUtc, DateTimeOffset? assetCompletedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(captureSessionId)
                || !string.Equals(captureSessionId, assetSessionId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(captureId) || string.IsNullOrWhiteSpace(assetSnapshotId)
                || !captureStartedAtUtc.HasValue || !captureCompletedAtUtc.HasValue
                || !assetStartedAtUtc.HasValue || !assetCompletedAtUtc.HasValue
                || captureCompletedAtUtc.Value < captureStartedAtUtc.Value
                || assetCompletedAtUtc.Value < assetStartedAtUtc.Value)
                return null;
            return DiagnosticEvidenceLink.TryCreate(
                new DiagnosticEvidenceInterval(captureSessionId, captureId, captureStartedAtUtc.Value, captureCompletedAtUtc.Value),
                new DiagnosticEvidenceInterval(assetSessionId, assetSnapshotId, assetStartedAtUtc.Value, assetCompletedAtUtc.Value));
        }
    }
}
