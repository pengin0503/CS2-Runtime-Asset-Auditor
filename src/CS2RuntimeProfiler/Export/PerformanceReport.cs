using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace CS2RuntimeProfiler.Export
{
    [DataContract]
    public sealed class ReportNamedValue
    {
        public ReportNamedValue() { }
        public ReportNamedValue(string name, string value) { Name = name; Value = value; }
        [DataMember(Name = "name", Order = 1)] public string Name { get; set; }
        [DataMember(Name = "value", Order = 2, EmitDefaultValue = false)] public string Value { get; set; }
        internal ReportNamedValue SanitizedCopy() => new ReportNamedValue(PrivacySanitizer.Sanitize(Name), PrivacySanitizer.Sanitize(Value));
    }

    [DataContract]
    public sealed class ReportMetric
    {
        [DataMember(Name = "name", Order = 1)] public string Name { get; set; }
        [DataMember(Name = "value", Order = 2, EmitDefaultValue = false)] public double? Value { get; set; }
        [DataMember(Name = "unit", Order = 3, EmitDefaultValue = false)] public string Unit { get; set; }
        [DataMember(Name = "confidence", Order = 4, EmitDefaultValue = false)] public string Confidence { get; set; }
        [DataMember(Name = "availability", Order = 5, EmitDefaultValue = false)] public string Availability { get; set; }
        [DataMember(Name = "note", Order = 6, EmitDefaultValue = false)] public string Note { get; set; }
        internal ReportMetric SanitizedCopy() => new ReportMetric { Name = PrivacySanitizer.Sanitize(Name), Value = Value, Unit = PrivacySanitizer.Sanitize(Unit), Confidence = PrivacySanitizer.Sanitize(Confidence), Availability = PrivacySanitizer.Sanitize(Availability), Note = PrivacySanitizer.Sanitize(Note) };
    }

    [DataContract]
    public sealed class ReportSystem
    {
        public ReportSystem() { PatchOwners = new List<string>(); }
        [DataMember(Name = "systemId", Order = 1)] public string SystemId { get; set; }
        [DataMember(Name = "ownerAssembly", Order = 2, EmitDefaultValue = false)] public string OwnerAssembly { get; set; }
        [DataMember(Name = "modName", Order = 3, EmitDefaultValue = false)] public string ModName { get; set; }
        [DataMember(Name = "confidence", Order = 4, EmitDefaultValue = false)] public string Confidence { get; set; }
        [DataMember(Name = "currentMilliseconds", Order = 5, EmitDefaultValue = false)] public double? CurrentMilliseconds { get; set; }
        [DataMember(Name = "meanMilliseconds", Order = 6, EmitDefaultValue = false)] public double? MeanMilliseconds { get; set; }
        [DataMember(Name = "medianMilliseconds", Order = 7, EmitDefaultValue = false)] public double? MedianMilliseconds { get; set; }
        [DataMember(Name = "p95Milliseconds", Order = 8, EmitDefaultValue = false)] public double? P95Milliseconds { get; set; }
        [DataMember(Name = "p99Milliseconds", Order = 9, EmitDefaultValue = false)] public double? P99Milliseconds { get; set; }
        [DataMember(Name = "maxMilliseconds", Order = 10, EmitDefaultValue = false)] public double? MaxMilliseconds { get; set; }
        [DataMember(Name = "totalMilliseconds", Order = 11, EmitDefaultValue = false)] public double? TotalMilliseconds { get; set; }
        [DataMember(Name = "calls", Order = 12, EmitDefaultValue = false)] public int? Calls { get; set; }
        [DataMember(Name = "patchOwners", Order = 13)] public List<string> PatchOwners { get; set; }
        [DataMember(Name = "isAggregateContainer", Order = 14, EmitDefaultValue = false)] public bool IsAggregateContainer { get; set; }
        [DataMember(Name = "millisecondsPerFrame", Order = 15, EmitDefaultValue = false)] public double? MillisecondsPerFrame { get; set; }
        internal ReportSystem SanitizedCopy() => new ReportSystem { MillisecondsPerFrame = MillisecondsPerFrame, SystemId = PrivacySanitizer.Sanitize(SystemId), OwnerAssembly = PrivacySanitizer.Sanitize(OwnerAssembly), ModName = PrivacySanitizer.Sanitize(ModName), Confidence = PrivacySanitizer.Sanitize(Confidence), CurrentMilliseconds = CurrentMilliseconds, MeanMilliseconds = MeanMilliseconds, MedianMilliseconds = MedianMilliseconds, P95Milliseconds = P95Milliseconds, P99Milliseconds = P99Milliseconds, MaxMilliseconds = MaxMilliseconds, TotalMilliseconds = TotalMilliseconds, Calls = Calls, PatchOwners = (PatchOwners ?? new List<string>()).Select(PrivacySanitizer.Sanitize).ToList(), IsAggregateContainer = IsAggregateContainer };
    }

    [DataContract]
    public sealed class ReportCapture
    {
        public ReportCapture() { Warnings = new List<string>(); }
        [DataMember(Name = "id", Order = 1)] public string Id { get; set; }
        [DataMember(Name = "triggerKind", Order = 2, EmitDefaultValue = false)] public string TriggerKind { get; set; }
        [DataMember(Name = "triggeredAtSeconds", Order = 3)] public double TriggeredAtSeconds { get; set; }
        [DataMember(Name = "durationSeconds", Order = 4)] public double DurationSeconds { get; set; }
        [DataMember(Name = "discoveredMarkers", Order = 5)] public int DiscoveredMarkers { get; set; }
        [DataMember(Name = "capturedMarkers", Order = 6)] public int CapturedMarkers { get; set; }
        [DataMember(Name = "coverageRatio", Order = 7, EmitDefaultValue = false)] public double? CoverageRatio { get; set; }
        [DataMember(Name = "batched", Order = 8)] public bool Batched { get; set; }
        [DataMember(Name = "profilerOverheadShare", Order = 9)] public double ProfilerOverheadShare { get; set; }
        [DataMember(Name = "warnings", Order = 10)] public List<string> Warnings { get; set; }
        [DataMember(Name = "attemptedMarkers", Order = 11)] public int AttemptedMarkers { get; set; }
        [DataMember(Name = "activatedMarkers", Order = 12)] public int ActivatedMarkers { get; set; }
        [DataMember(Name = "sampledMarkers", Order = 13)] public int SampledMarkers { get; set; }
        [DataMember(Name = "attemptedRatio", Order = 14, EmitDefaultValue = false)] public double? AttemptedRatio { get; set; }
        [DataMember(Name = "activatedRatio", Order = 15, EmitDefaultValue = false)] public double? ActivatedRatio { get; set; }
        [DataMember(Name = "sampledRatio", Order = 16, EmitDefaultValue = false)] public double? SampledRatio { get; set; }
        [DataMember(Name = "triggerSelectedSpeed", Order = 17, EmitDefaultValue = false)] public double? TriggerSelectedSpeed { get; set; }
        [DataMember(Name = "triggerActualSpeed", Order = 18, EmitDefaultValue = false)] public double? TriggerActualSpeed { get; set; }
        [DataMember(Name = "triggerEfficiency", Order = 19, EmitDefaultValue = false)] public double? TriggerEfficiency { get; set; }
        [DataMember(Name = "profilerMemoryBaselineBytes", Order = 20, EmitDefaultValue = false)] public double? ProfilerMemoryBaselineBytes { get; set; }
        [DataMember(Name = "profilerMemoryPeakBytes", Order = 21, EmitDefaultValue = false)] public double? ProfilerMemoryPeakBytes { get; set; }
        [DataMember(Name = "profilerMemoryDeltaBytes", Order = 22, EmitDefaultValue = false)] public double? ProfilerMemoryDeltaBytes { get; set; }
        internal ReportCapture SanitizedCopy() => new ReportCapture { Id = PrivacySanitizer.Sanitize(Id), TriggerKind = PrivacySanitizer.Sanitize(TriggerKind), TriggeredAtSeconds = TriggeredAtSeconds, DurationSeconds = DurationSeconds, DiscoveredMarkers = DiscoveredMarkers, CapturedMarkers = CapturedMarkers, CoverageRatio = CoverageRatio, Batched = Batched, ProfilerOverheadShare = ProfilerOverheadShare, Warnings = (Warnings ?? new List<string>()).Select(PrivacySanitizer.Sanitize).ToList(), AttemptedMarkers = AttemptedMarkers, ActivatedMarkers = ActivatedMarkers, SampledMarkers = SampledMarkers, AttemptedRatio = AttemptedRatio, ActivatedRatio = ActivatedRatio, SampledRatio = SampledRatio, TriggerSelectedSpeed = TriggerSelectedSpeed, TriggerActualSpeed = TriggerActualSpeed, TriggerEfficiency = TriggerEfficiency, ProfilerMemoryBaselineBytes = ProfilerMemoryBaselineBytes, ProfilerMemoryPeakBytes = ProfilerMemoryPeakBytes, ProfilerMemoryDeltaBytes = ProfilerMemoryDeltaBytes };
    }

    [DataContract]
    public sealed class ReportTimelinePoint
    {
        [DataMember(Name = "timestampSeconds", Order = 1)] public double TimestampSeconds { get; set; }
        [DataMember(Name = "metric", Order = 2)] public string Metric { get; set; }
        [DataMember(Name = "value", Order = 3)] public double Value { get; set; }
        [DataMember(Name = "confidence", Order = 4, EmitDefaultValue = false)] public string Confidence { get; set; }
        [DataMember(Name = "unit", Order = 5, EmitDefaultValue = false)] public string Unit { get; set; }
        internal ReportTimelinePoint SanitizedCopy() => new ReportTimelinePoint { Unit = PrivacySanitizer.Sanitize(Unit), TimestampSeconds = TimestampSeconds, Metric = PrivacySanitizer.Sanitize(Metric), Value = Value, Confidence = PrivacySanitizer.Sanitize(Confidence) };
    }

    [DataContract]
    public sealed class ReportAdvisorObservation
    {
        [DataMember(Name = "category", Order = 1)] public string Category { get; set; }
        [DataMember(Name = "severity", Order = 2)] public string Severity { get; set; }
        [DataMember(Name = "confidence", Order = 3)] public string Confidence { get; set; }
        [DataMember(Name = "evidenceIds", Order = 4)] public List<string> EvidenceIds { get; set; } = new List<string>();
        [DataMember(Name = "rationale", Order = 5)] public string Rationale { get; set; }
        internal ReportAdvisorObservation SanitizedCopy() => new ReportAdvisorObservation { Category = PrivacySanitizer.Sanitize(Category), Severity = PrivacySanitizer.Sanitize(Severity), Confidence = PrivacySanitizer.Sanitize(Confidence), EvidenceIds = (EvidenceIds ?? new List<string>()).Select(PrivacySanitizer.Sanitize).ToList(), Rationale = PrivacySanitizer.Sanitize(Rationale) };
    }

    [DataContract]
    public sealed class ReportAdvisorRecommendation
    {
        [DataMember(Name = "settingId", Order = 1)] public string SettingId { get; set; }
        [DataMember(Name = "currentValue", Order = 2)] public string CurrentValue { get; set; }
        [DataMember(Name = "recommendedValue", Order = 3)] public string RecommendedValue { get; set; }
        [DataMember(Name = "direction", Order = 4)] public string Direction { get; set; }
        [DataMember(Name = "priority", Order = 5)] public string Priority { get; set; }
        [DataMember(Name = "confidence", Order = 6)] public string Confidence { get; set; }
        [DataMember(Name = "evidenceIds", Order = 7)] public List<string> EvidenceIds { get; set; } = new List<string>();
        [DataMember(Name = "rationale", Order = 8)] public string Rationale { get; set; }
        [DataMember(Name = "capability", Order = 9)] public string Capability { get; set; }
        [DataMember(Name = "applyBehavior", Order = 10)] public string ApplyBehavior { get; set; }
        internal ReportAdvisorRecommendation SanitizedCopy() => new ReportAdvisorRecommendation { SettingId = PrivacySanitizer.Sanitize(SettingId), CurrentValue = PrivacySanitizer.Sanitize(CurrentValue), RecommendedValue = PrivacySanitizer.Sanitize(RecommendedValue), Direction = PrivacySanitizer.Sanitize(Direction), Priority = PrivacySanitizer.Sanitize(Priority), Confidence = PrivacySanitizer.Sanitize(Confidence), EvidenceIds = (EvidenceIds ?? new List<string>()).Select(PrivacySanitizer.Sanitize).ToList(), Rationale = PrivacySanitizer.Sanitize(Rationale), Capability = PrivacySanitizer.Sanitize(Capability), ApplyBehavior = PrivacySanitizer.Sanitize(ApplyBehavior) };
    }

    [DataContract]
    public sealed class ReportAdvisorSetting
    {
        [DataMember(Name = "settingId", Order = 1)] public string SettingId { get; set; }
        [DataMember(Name = "category", Order = 2)] public string Category { get; set; }
        [DataMember(Name = "currentValue", Order = 3)] public string CurrentValue { get; set; }
        [DataMember(Name = "valueKind", Order = 4)] public string ValueKind { get; set; }
        [DataMember(Name = "capability", Order = 5)] public string Capability { get; set; }
        [DataMember(Name = "applyBehavior", Order = 6)] public string ApplyBehavior { get; set; }
        [DataMember(Name = "writable", Order = 7)] public bool Writable { get; set; }
        [DataMember(Name = "currentlyVisible", Order = 8, EmitDefaultValue = false)] public bool? CurrentlyVisible { get; set; }
        [DataMember(Name = "currentlyEnabled", Order = 9, EmitDefaultValue = false)] public bool? CurrentlyEnabled { get; set; }
        internal ReportAdvisorSetting SanitizedCopy() => new ReportAdvisorSetting { SettingId = PrivacySanitizer.Sanitize(SettingId), Category = PrivacySanitizer.Sanitize(Category), CurrentValue = PrivacySanitizer.Sanitize(CurrentValue), ValueKind = PrivacySanitizer.Sanitize(ValueKind), Capability = PrivacySanitizer.Sanitize(Capability), ApplyBehavior = PrivacySanitizer.Sanitize(ApplyBehavior), Writable = Writable, CurrentlyVisible = CurrentlyVisible, CurrentlyEnabled = CurrentlyEnabled };
    }

    [DataContract]
    public sealed class ReportAdvisorChange
    {
        [DataMember(Name = "settingId", Order = 1)] public string SettingId { get; set; }
        [DataMember(Name = "originalValue", Order = 2)] public string OriginalValue { get; set; }
        [DataMember(Name = "appliedValue", Order = 3)] public string AppliedValue { get; set; }
        [DataMember(Name = "currentObservedValue", Order = 4)] public string CurrentObservedValue { get; set; }
        [DataMember(Name = "status", Order = 5)] public string Status { get; set; }
        [DataMember(Name = "appliedAtUtc", Order = 6)] public string AppliedAtUtc { get; set; }
        internal ReportAdvisorChange SanitizedCopy() => new ReportAdvisorChange { SettingId = PrivacySanitizer.Sanitize(SettingId), OriginalValue = PrivacySanitizer.Sanitize(OriginalValue), AppliedValue = PrivacySanitizer.Sanitize(AppliedValue), CurrentObservedValue = PrivacySanitizer.Sanitize(CurrentObservedValue), Status = PrivacySanitizer.Sanitize(Status), AppliedAtUtc = PrivacySanitizer.Sanitize(AppliedAtUtc) };
    }

    [DataContract]
    public sealed class ReportAdvisorMetricComparison
    {
        [DataMember(Name = "id", Order = 1)] public string Id { get; set; }
        [DataMember(Name = "baselineValue", Order = 2, EmitDefaultValue = false)] public double? BaselineValue { get; set; }
        [DataMember(Name = "followUpValue", Order = 3, EmitDefaultValue = false)] public double? FollowUpValue { get; set; }
        [DataMember(Name = "state", Order = 4)] public string State { get; set; }
        [DataMember(Name = "reason", Order = 5, EmitDefaultValue = false)] public string Reason { get; set; }
        internal ReportAdvisorMetricComparison SanitizedCopy() => new ReportAdvisorMetricComparison { Id = PrivacySanitizer.Sanitize(Id), BaselineValue = BaselineValue, FollowUpValue = FollowUpValue, State = PrivacySanitizer.Sanitize(State), Reason = PrivacySanitizer.Sanitize(Reason) };
    }

    [DataContract]
    public sealed class ReportAdvisorComparison
    {
        [DataMember(Name = "metrics", Order = 1)] public List<ReportAdvisorMetricComparison> Metrics { get; set; } = new List<ReportAdvisorMetricComparison>();
        [DataMember(Name = "changedSettingIds", Order = 2)] public List<string> ChangedSettingIds { get; set; } = new List<string>();
        [DataMember(Name = "multipleChanges", Order = 3)] public bool MultipleChanges { get; set; }
        internal ReportAdvisorComparison SanitizedCopy() => new ReportAdvisorComparison { Metrics = (Metrics ?? new List<ReportAdvisorMetricComparison>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), ChangedSettingIds = (ChangedSettingIds ?? new List<string>()).Select(PrivacySanitizer.Sanitize).ToList(), MultipleChanges = MultipleChanges };
    }

    [DataContract]
    public sealed class ReportAdvisor
    {
        [DataMember(Name = "selectedCaptureId", Order = 1)] public string SelectedCaptureId { get; set; }
        [DataMember(Name = "baselineCaptureId", Order = 2)] public string BaselineCaptureId { get; set; }
        [DataMember(Name = "unavailableReason", Order = 3, EmitDefaultValue = false)] public string UnavailableReason { get; set; }
        [DataMember(Name = "evidence", Order = 4)] public List<ReportMetric> Evidence { get; set; } = new List<ReportMetric>();
        [DataMember(Name = "diagnosis", Order = 5)] public List<ReportAdvisorObservation> Diagnosis { get; set; } = new List<ReportAdvisorObservation>();
        [DataMember(Name = "recommendations", Order = 6)] public List<ReportAdvisorRecommendation> Recommendations { get; set; } = new List<ReportAdvisorRecommendation>();
        [DataMember(Name = "catalog", Order = 7)] public List<ReportAdvisorSetting> Catalog { get; set; } = new List<ReportAdvisorSetting>();
        [DataMember(Name = "changes", Order = 8)] public List<ReportAdvisorChange> Changes { get; set; } = new List<ReportAdvisorChange>();
        [DataMember(Name = "comparison", Order = 9, EmitDefaultValue = false)] public ReportAdvisorComparison Comparison { get; set; }
        internal ReportAdvisor SanitizedCopy() => new ReportAdvisor { SelectedCaptureId = PrivacySanitizer.Sanitize(SelectedCaptureId), BaselineCaptureId = PrivacySanitizer.Sanitize(BaselineCaptureId), UnavailableReason = PrivacySanitizer.Sanitize(UnavailableReason), Evidence = (Evidence ?? new List<ReportMetric>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), Diagnosis = (Diagnosis ?? new List<ReportAdvisorObservation>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), Recommendations = (Recommendations ?? new List<ReportAdvisorRecommendation>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), Catalog = (Catalog ?? new List<ReportAdvisorSetting>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), Changes = (Changes ?? new List<ReportAdvisorChange>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), Comparison = Comparison?.SanitizedCopy() };
    }

    [DataContract]
    public sealed class PerformanceReport
    {
        public const int CurrentSchemaVersion = 2;
        public PerformanceReport() { SchemaVersion = CurrentSchemaVersion; EnabledMods = new List<string>(); CaptureConfig = new List<ReportNamedValue>(); Capabilities = new List<ReportNamedValue>(); GlobalMetrics = new List<ReportMetric>(); Systems = new List<ReportSystem>(); ModAttribution = new List<ReportNamedValue>(); Pathfinding = new List<ReportMetric>(); DomainMetrics = new List<ReportMetric>(); Timeline = new List<ReportTimelinePoint>(); Captures = new List<ReportCapture>(); ProfilerOverhead = new List<ReportMetric>(); Warnings = new List<string>(); }
        [DataMember(Name = "schemaVersion", Order = 1)] public int SchemaVersion { get; set; }
        [DataMember(Name = "gameVersion", Order = 2, EmitDefaultValue = false)] public string GameVersion { get; set; }
        [DataMember(Name = "profilerVersion", Order = 3, EmitDefaultValue = false)] public string ProfilerVersion { get; set; }
        [DataMember(Name = "hardwareSummary", Order = 4, EmitDefaultValue = false)] public string HardwareSummary { get; set; }
        [DataMember(Name = "enabledMods", Order = 5)] public List<string> EnabledMods { get; set; }
        [DataMember(Name = "captureConfig", Order = 6)] public List<ReportNamedValue> CaptureConfig { get; set; }
        [DataMember(Name = "capabilities", Order = 7)] public List<ReportNamedValue> Capabilities { get; set; }
        [DataMember(Name = "globalMetrics", Order = 8)] public List<ReportMetric> GlobalMetrics { get; set; }
        [DataMember(Name = "systems", Order = 9)] public List<ReportSystem> Systems { get; set; }
        [DataMember(Name = "modAttribution", Order = 10)] public List<ReportNamedValue> ModAttribution { get; set; }
        [DataMember(Name = "pathfinding", Order = 11)] public List<ReportMetric> Pathfinding { get; set; }
        [DataMember(Name = "domainMetrics", Order = 12)] public List<ReportMetric> DomainMetrics { get; set; }
        [DataMember(Name = "timeline", Order = 13)] public List<ReportTimelinePoint> Timeline { get; set; }
        [DataMember(Name = "profilerOverhead", Order = 14)] public List<ReportMetric> ProfilerOverhead { get; set; }
        [DataMember(Name = "warnings", Order = 15)] public List<string> Warnings { get; set; }
        [DataMember(Name = "captures", Order = 16)] public List<ReportCapture> Captures { get; set; }
        [DataMember(Name = "buildId", Order = 17, EmitDefaultValue = false)] public string BuildId { get; set; }
        [DataMember(Name = "advisor", Order = 18, EmitDefaultValue = false)] public ReportAdvisor Advisor { get; set; }
        [DataMember(Name = "cityName", Order = 100, EmitDefaultValue = false)] public string CityName { get; set; }
        public static PerformanceReport CreateForTest() => new PerformanceReport { ProfilerVersion = "test", CityName = null };
        internal PerformanceReport SanitizedCopy() => new PerformanceReport { SchemaVersion = SchemaVersion <= 0 ? CurrentSchemaVersion : SchemaVersion, GameVersion = PrivacySanitizer.Sanitize(GameVersion), ProfilerVersion = PrivacySanitizer.Sanitize(ProfilerVersion), HardwareSummary = PrivacySanitizer.Sanitize(HardwareSummary), EnabledMods = (EnabledMods ?? new List<string>()).Select(PrivacySanitizer.Sanitize).ToList(), CaptureConfig = (CaptureConfig ?? new List<ReportNamedValue>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), Capabilities = (Capabilities ?? new List<ReportNamedValue>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), GlobalMetrics = (GlobalMetrics ?? new List<ReportMetric>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), Systems = (Systems ?? new List<ReportSystem>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), ModAttribution = (ModAttribution ?? new List<ReportNamedValue>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), Pathfinding = (Pathfinding ?? new List<ReportMetric>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), DomainMetrics = (DomainMetrics ?? new List<ReportMetric>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), Timeline = (Timeline ?? new List<ReportTimelinePoint>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), ProfilerOverhead = (ProfilerOverhead ?? new List<ReportMetric>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), Warnings = (Warnings ?? new List<string>()).Select(PrivacySanitizer.Sanitize).ToList(), Captures = (Captures ?? new List<ReportCapture>()).Where(x => x != null).Select(x => x.SanitizedCopy()).ToList(), BuildId = PrivacySanitizer.Sanitize(BuildId), Advisor = Advisor?.SanitizedCopy(), CityName = string.IsNullOrWhiteSpace(CityName) ? null : PrivacySanitizer.Sanitize(CityName) };
    }
}
