using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using CS2RuntimeAssetAuditor.Assets.Export;
using CS2RuntimeAssetAuditor.Coordination;

namespace CS2RuntimeAssetAuditor.Export
{
    [DataContract]
    public sealed class RuntimeAssetAuditReport
    {
        [DataMember(Name = "schemaVersion", Order = 1)] public int SchemaVersion { get; set; } = 1;
        [DataMember(Name = "generatedAtUtc", Order = 2)] public string GeneratedAtUtc { get; set; }
        [DataMember(Name = "product", Order = 3)] public string Product { get; set; } = "CS2 Runtime Asset Auditor";
        [DataMember(Name = "session", Order = 4)] public ReportSession Session { get; set; }
        [DataMember(Name = "runtime", Order = 5, EmitDefaultValue = true)] public PerformanceReport Runtime { get; set; }
        [DataMember(Name = "advisor", Order = 6, EmitDefaultValue = true)] public ReportAdvisor Advisor { get; set; }
        [DataMember(Name = "assets", Order = 7, EmitDefaultValue = true)] public AuditReport Assets { get; set; }
        [DataMember(Name = "evidenceLinks", Order = 8)] public List<ReportEvidenceLink> EvidenceLinks { get; set; } = new List<ReportEvidenceLink>();
        [DataMember(Name = "capabilities", Order = 9)] public ReportCapabilities Capabilities { get; set; } = new ReportCapabilities();
        [DataMember(Name = "diagnostics", Order = 10)] public ReportDiagnostics Diagnostics { get; set; } = new ReportDiagnostics();
        [DataMember(Name = "privacy", Order = 11)] public string Privacy { get; set; } = "Home paths and account identifiers are redacted; review before sharing.";
    }

    [DataContract]
    public sealed class ReportSession
    {
        [DataMember(Name = "sessionId", Order = 1)] public string SessionId { get; set; }
        [DataMember(Name = "startedAtUtc", Order = 2)] public string StartedAtUtc { get; set; }
        [DataMember(Name = "gameVersion", Order = 3)] public string GameVersion { get; set; }
        [DataMember(Name = "buildIdentity", Order = 4)] public string BuildIdentity { get; set; }
    }

    [DataContract]
    public sealed class ReportEvidenceLink
    {
        [DataMember(Name = "captureId", Order = 1)] public string CaptureId { get; set; }
        [DataMember(Name = "assetSnapshotId", Order = 2)] public string AssetSnapshotId { get; set; }
        [DataMember(Name = "relativeTiming", Order = 3)] public string RelativeTiming { get; set; }
    }

    [DataContract]
    public sealed class ReportCapabilities
    {
        [DataMember(Name = "runtime", Order = 1)] public List<ReportNamedValue> Runtime { get; set; } = new List<ReportNamedValue>();
        [DataMember(Name = "assets", Order = 2, EmitDefaultValue = true)] public ReportCapabilityReport Assets { get; set; }
    }

    [DataContract]
    public sealed class ReportDiagnostics
    {
        [DataMember(Name = "runtimeWarnings", Order = 1)] public List<string> RuntimeWarnings { get; set; } = new List<string>();
        [DataMember(Name = "assets", Order = 2)] public ReportDiagnostic[] Assets { get; set; } = new ReportDiagnostic[0];
    }
}
