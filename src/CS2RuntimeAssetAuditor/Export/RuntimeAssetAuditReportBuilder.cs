using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Export;
using CS2RuntimeAssetAuditor.Coordination;

namespace CS2RuntimeAssetAuditor.Export
{
    public static class RuntimeAssetAuditReportBuilder
    {
        public static RuntimeAssetAuditReport Build(PerformanceReport runtime, AuditReport assets,
            DiagnosticSessionContext session, DateTimeOffset generatedAtUtc,
            IEnumerable<DiagnosticEvidenceLink> evidenceLinks = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var runtimeCopy = runtime?.SanitizedCopy();
            var advisor = runtimeCopy?.Advisor;
            if (runtimeCopy != null) runtimeCopy.Advisor = null;
            return new RuntimeAssetAuditReport
            {
                GeneratedAtUtc = generatedAtUtc.ToUniversalTime().ToString("O"),
                Session = new ReportSession
                {
                    SessionId = session.SessionId,
                    StartedAtUtc = session.StartedAtUtc.ToString("O"),
                    GameVersion = session.GameVersion,
                    BuildIdentity = session.BuildIdentity
                },
                Runtime = runtimeCopy,
                Advisor = advisor,
                Assets = assets,
                EvidenceLinks = (evidenceLinks ?? Enumerable.Empty<DiagnosticEvidenceLink>())
                    .Where(link => link != null && link.SessionId == session.SessionId)
                    .Select(link => new ReportEvidenceLink { CaptureId = link.CaptureId,
                        AssetSnapshotId = link.AssetSnapshotId, RelativeTiming = link.RelativeTiming.ToString() }).ToList(),
                Capabilities = new ReportCapabilities
                {
                    Runtime = runtimeCopy?.Capabilities ?? new List<ReportNamedValue>(),
                    Assets = assets?.CapabilityReport
                },
                Diagnostics = new ReportDiagnostics
                {
                    RuntimeWarnings = runtimeCopy?.Warnings ?? new List<string>(),
                    Assets = assets?.Diagnostics ?? new ReportDiagnostic[0]
                }
            };
        }
    }
}
