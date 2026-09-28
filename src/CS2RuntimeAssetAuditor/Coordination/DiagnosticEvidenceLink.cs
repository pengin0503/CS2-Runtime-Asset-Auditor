#nullable enable
using System;

namespace CS2RuntimeAssetAuditor.Coordination
{
    public enum EvidenceRelativeTiming { Before, Overlapping, After }

    public sealed class DiagnosticEvidenceInterval
    {
        public string SessionId { get; }
        public string EvidenceId { get; }
        public DateTimeOffset StartedAtUtc { get; }
        public DateTimeOffset CompletedAtUtc { get; }

        public DiagnosticEvidenceInterval(string sessionId, string evidenceId, DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("A session ID is required.", nameof(sessionId));
            if (string.IsNullOrWhiteSpace(evidenceId)) throw new ArgumentException("An evidence ID is required.", nameof(evidenceId));
            if (completedAtUtc < startedAtUtc) throw new ArgumentOutOfRangeException(nameof(completedAtUtc));
            SessionId = sessionId;
            EvidenceId = evidenceId;
            StartedAtUtc = startedAtUtc.ToUniversalTime();
            CompletedAtUtc = completedAtUtc.ToUniversalTime();
        }
    }

    public sealed class DiagnosticEvidenceLink
    {
        public string SessionId { get; }
        public string CaptureId { get; }
        public string AssetSnapshotId { get; }
        public DateTimeOffset CaptureStartedAtUtc { get; }
        public DateTimeOffset CaptureCompletedAtUtc { get; }
        public DateTimeOffset AssetStartedAtUtc { get; }
        public DateTimeOffset AssetCompletedAtUtc { get; }
        public EvidenceRelativeTiming RelativeTiming { get; }

        private DiagnosticEvidenceLink(DiagnosticEvidenceInterval runtime, DiagnosticEvidenceInterval asset, EvidenceRelativeTiming relativeTiming)
        {
            SessionId = runtime.SessionId;
            CaptureId = runtime.EvidenceId;
            AssetSnapshotId = asset.EvidenceId;
            CaptureStartedAtUtc = runtime.StartedAtUtc;
            CaptureCompletedAtUtc = runtime.CompletedAtUtc;
            AssetStartedAtUtc = asset.StartedAtUtc;
            AssetCompletedAtUtc = asset.CompletedAtUtc;
            RelativeTiming = relativeTiming;
        }

        public static DiagnosticEvidenceLink? TryCreate(DiagnosticEvidenceInterval runtime, DiagnosticEvidenceInterval asset)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (!string.Equals(runtime.SessionId, asset.SessionId, StringComparison.Ordinal)) return null;
            var timing = asset.CompletedAtUtc < runtime.StartedAtUtc ? EvidenceRelativeTiming.Before
                : asset.StartedAtUtc > runtime.CompletedAtUtc ? EvidenceRelativeTiming.After
                : EvidenceRelativeTiming.Overlapping;
            return new DiagnosticEvidenceLink(runtime, asset, timing);
        }
    }
}
