using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.Advisor;
using CS2RuntimeAssetAuditor.Core.Advisor.Experiment;
using CS2RuntimeAssetAuditor.UI;

namespace CS2RuntimeAssetAuditor.Export
{
    public static class ProfilerReportBuilder
    {
        public static Func<RuntimeReportMetadata> RuntimeMetadataProvider { get; set; }
        public static Func<CaptureConfigurationSnapshot> CaptureConfigurationProvider { get; set; }

        public static PerformanceReport Build(
            UiSnapshot snapshot,
            string gameVersion = null,
            string profilerVersion = null,
            RuntimeReportMetadata metadata = null,
            CaptureConfigurationSnapshot captureConfiguration = null)
        {
            snapshot = snapshot ?? new UiSnapshot();
            metadata = metadata ?? ResolveRuntimeMetadata();
            captureConfiguration = snapshot.Capture?.DetailConfiguration?.Clone()
                ?? captureConfiguration
                ?? ResolveCaptureConfiguration();
            var report = new PerformanceReport
            {
                GameVersion = gameVersion,
                ProfilerVersion = profilerVersion,
                BuildId = string.IsNullOrWhiteSpace(metadata?.BuildId) ? null : metadata.BuildId,
                HardwareSummary = string.IsNullOrWhiteSpace(metadata?.HardwareSummary) ? null : metadata.HardwareSummary,
                CityName = null
            };
            foreach (var mod in metadata?.EnabledMods ?? Array.Empty<string>()) if (!string.IsNullOrWhiteSpace(mod)) report.EnabledMods.Add(mod.Trim());
            report.EnabledMods = report.EnabledMods.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
            AddCaptureScope(report, snapshot);
            AddCaptureConfiguration(report, captureConfiguration);
            AddGlobal(report, snapshot.Global);

            foreach (var capture in snapshot.Captures ?? Array.Empty<CaptureSummaryUi>())
            {
                if (capture == null) continue;
                report.Captures.Add(new ReportCapture
                {
                    Id = capture.Id, TriggerKind = capture.TriggerKind, TriggeredAtSeconds = capture.TriggeredAtSeconds,
                    SessionId = capture.SessionId, StartedAtUtc = capture.StartedAtUtc, CompletedAtUtc = capture.CompletedAtUtc,
                    InterruptionReason = capture.InterruptionReason,
                    TriggerSelectedSpeed = capture.TriggerSelectedSpeed, TriggerActualSpeed = capture.TriggerActualSpeed, TriggerEfficiency = capture.TriggerEfficiency,
                    DurationSeconds = capture.DurationSeconds, DiscoveredMarkers = capture.DiscoveredMarkers, AttemptedMarkers = capture.AttemptedMarkers,
                    ActivatedMarkers = capture.ActivatedMarkers, SampledMarkers = capture.SampledMarkers, CapturedMarkers = capture.CapturedMarkers,
                    AttemptedRatio = capture.AttemptedRatio, ActivatedRatio = capture.ActivatedRatio, SampledRatio = capture.SampledRatio,
                    CoverageRatio = capture.CoverageRatio, Batched = capture.Batched, ProfilerOverheadShare = capture.ProfilerOverheadShare,
                    ProfilerMemoryBaselineBytes = capture.ProfilerMemoryBaselineBytes, ProfilerMemoryPeakBytes = capture.ProfilerMemoryPeakBytes,
                    ProfilerMemoryDeltaBytes = capture.ProfilerMemoryDeltaBytes,
                    Warnings = (capture.Warnings ?? Array.Empty<string>()).ToList()
                });
            }

            foreach (var system in snapshot.Systems ?? Array.Empty<SystemUiRow>())
            {
                if (system == null) continue;
                report.Systems.Add(new ReportSystem { SystemId = system.Id, OwnerAssembly = system.OwnerAssembly, Confidence = system.Confidence, CurrentMilliseconds = system.CurrentMilliseconds, MeanMilliseconds = system.MeanMilliseconds, MedianMilliseconds = system.MedianMilliseconds, P95Milliseconds = system.P95Milliseconds, P99Milliseconds = system.P99Milliseconds, MaxMilliseconds = system.MaxMilliseconds, TotalMilliseconds = system.TotalMilliseconds, Calls = system.Calls, PatchOwners = (system.PatchOwners ?? Array.Empty<string>()).ToList(), IsAggregateContainer = system.IsAggregateContainer, MillisecondsPerFrame = system.MillisecondsPerFrame });
            }

            foreach (var mod in snapshot.Mods ?? Array.Empty<ModUiRow>())
            {
                if (mod == null) continue;
                var detail = string.Format(CultureInfo.InvariantCulture, "directMs={0:0.###}; costBasis={3}; directSystems={1}; patchedVanillaSystems={2}", mod.DirectSystemMilliseconds, mod.DirectSystemCount, mod.PatchedVanillaSystemCount, mod.DirectCostBasis);
                report.ModAttribution.Add(new ReportNamedValue(mod.AssemblyName, detail));
            }

            foreach (var metric in snapshot.Pathfinding?.Metrics ?? Array.Empty<UiMetricRow>()) report.Pathfinding.Add(ToReportMetric(metric));
            foreach (var metric in snapshot.DomainMetrics ?? Array.Empty<UiMetricRow>()) report.DomainMetrics.Add(ToReportMetric(metric));
            foreach (var point in snapshot.Timeline ?? Array.Empty<TimelinePoint>()) if (point != null) report.Timeline.Add(new ReportTimelinePoint { TimestampSeconds = point.TimestampSeconds, Metric = point.Metric, Value = point.Value, Confidence = point.Confidence, Unit = string.IsNullOrEmpty(point.UnitType) ? null : point.UnitType });
            report.ProfilerOverhead.Add(new ReportMetric
            {
                Name = "captureOverheadShare",
                Value = snapshot.Diagnostics?.ProfilerOverheadShare ?? 0d,
                Unit = "ratio",
                Confidence = "Full",
                Availability = "Available",
                Note = "Capture controller and aggregation time relative to the sampling period; this excludes managed SystemBase timing instrumentation and is not total profiler overhead."
            });
            var unattributedJobsMilliseconds = snapshot.Diagnostics?.UnattributedJobsMilliseconds;
            report.ProfilerOverhead.Add(new ReportMetric
            {
                Name = "unattributedJobsMilliseconds",
                Value = unattributedJobsMilliseconds,
                Unit = "ms",
                Confidence = unattributedJobsMilliseconds.HasValue ? "Indirect" : "Unavailable",
                Availability = unattributedJobsMilliseconds.HasValue ? "Available" : "Unavailable",
                Note = unattributedJobsMilliseconds.HasValue
                    ? "Worker/job time is kept separate rather than assigned to a system without evidence."
                    : "Worker/job timing is not measured by this capture."
            });
            foreach (var message in snapshot.Diagnostics?.Messages ?? Array.Empty<string>()) if (!string.IsNullOrWhiteSpace(message)) report.Warnings.Add(message);
            report.Capabilities.Add(new ReportNamedValue("systemTiming", report.Systems.Count > 0 ? "available" : "unavailable"));
            report.Capabilities.Add(new ReportNamedValue("pathfinding", HasAvailableMetric(report.Pathfinding) ? "available" : "unavailable"));
            report.Capabilities.Add(new ReportNamedValue("domainMetrics", HasAvailableMetric(report.DomainMetrics) ? "available" : "unavailable"));
            try { AddAdvisor(report, snapshot.Advisor); }
            catch (Exception) { report.Advisor = new ReportAdvisor { UnavailableReason = "Advisor export unavailable" }; }
            report.Capabilities.Add(new ReportNamedValue("advisor", report.Advisor != null && report.Advisor.UnavailableReason == null ? "available" : "unavailable"));
            return report;
        }

        private static void AddAdvisor(PerformanceReport report, AdvisorState state)
        {
            if (state == null) return;
            var catalog = (state.Catalog ?? Array.Empty<GameSettingDescriptor>())
                .Where(x => x != null && x.IsUserFacing).ToArray();
            var publicIds = new HashSet<string>(catalog.Select(x => x.SettingId), StringComparer.Ordinal);
            report.Advisor = new ReportAdvisor
            {
                SelectedCaptureId = state.SelectedCaptureId,
                BaselineCaptureId = state.BaselineCaptureId,
                UnavailableReason = state.UnavailableReason,
                Evidence = (state.Evidence?.Metrics ?? Array.Empty<NamedMetricValue>())
                    .Where(x => x != null).Select(x => new ReportMetric
                    {
                        Name = x.Id, Value = x.Value, Unit = x.UnitType, Confidence = x.Confidence.ToString(),
                        Availability = x.Availability.ToString(), Note = x.Reason
                    }).ToList(),
                Diagnosis = (state.Observations ?? Array.Empty<BottleneckObservation>())
                    .Where(x => x != null).Select(x => new ReportAdvisorObservation
                    {
                        Category = x.Category.ToString(), Severity = x.Severity.ToString(),
                        Confidence = x.Confidence.ToString(), EvidenceIds = x.EvidenceIds.ToList(), Rationale = x.Rationale
                    }).ToList(),
                Recommendations = (state.Recommendations ?? Array.Empty<SettingRecommendation>())
                    .Where(x => x != null && publicIds.Contains(x.SettingId))
                    .Select(x => new ReportAdvisorRecommendation
                    {
                        SettingId = x.SettingId, CurrentValue = x.CurrentValue, RecommendedValue = x.RecommendedValue,
                        Direction = x.Direction.ToString(), Priority = x.Priority.ToString(), Confidence = x.Confidence.ToString(),
                        EvidenceIds = x.EvidenceIds.ToList(), Rationale = x.Rationale,
                        Capability = x.ApplyCapability.ToString(), ApplyBehavior = x.ApplyBehavior.ToString()
                    }).ToList(),
                Catalog = catalog.Select(x => new ReportAdvisorSetting
                {
                    SettingId = x.SettingId, Category = x.Category, CurrentValue = x.CurrentValue,
                    ValueKind = x.ValueKind.ToString(), Capability = x.CapabilityState.ToString(),
                    ApplyBehavior = x.ApplyBehavior.ToString(), Writable = x.IsWritable,
                    CurrentlyVisible = x.IsCurrentlyVisible, CurrentlyEnabled = x.IsCurrentlyEnabled
                }).ToList(),
                Changes = (state.Changes ?? Array.Empty<SettingChange>())
                    .Where(x => x != null && publicIds.Contains(x.SettingId))
                    .Select(x => new ReportAdvisorChange
                    {
                        SettingId = x.SettingId, OriginalValue = x.OriginalValue, AppliedValue = x.AppliedValue,
                        CurrentObservedValue = x.CurrentObservedValue, Status = x.Status.ToString(),
                        AppliedAtUtc = x.AppliedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                    }).ToList(),
                Comparison = ToReportComparison(state.Comparison, publicIds),
                Experiment = ToReportExperiment(state.Experiment, publicIds)
            };
        }

        private static ReportAdvisorExperiment ToReportExperiment(InvestigationExperiment experiment, HashSet<string> publicIds)
        {
            if (experiment == null || !publicIds.Contains(experiment.SettingId)) return null;
            return new ReportAdvisorExperiment
            {
                ExperimentId = experiment.ExperimentId,
                SessionId = experiment.SessionId,
                StartedAtUtc = experiment.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                CompletedAtUtc = experiment.CompletedAtUtc?.ToString("O", CultureInfo.InvariantCulture),
                BaselineCaptureId = experiment.BaselineCaptureId,
                FollowUpCaptureId = experiment.FollowUpCaptureId,
                SettingId = experiment.SettingId,
                SettingDisplayName = experiment.SettingDisplayName,
                OriginalValue = experiment.OriginalValue,
                TestedValue = experiment.TestedValue,
                ApplyBehavior = experiment.ApplyBehavior.ToString(),
                ChangeAppliedAtUtc = experiment.ChangeAppliedAtUtc?.ToString("O", CultureInfo.InvariantCulture),
                State = experiment.State.ToString(),
                Validity = experiment.Validity.ToString(),
                InvalidationReason = experiment.InvalidationReason.ToString(),
                CompletionOutcome = experiment.CompletionOutcome.ToString(),
                LastFailureReason = experiment.LastFailureReason,
                FollowUpWarnings = experiment.FollowUpWarnings.ToList(),
                FollowUpInterruption = experiment.FollowUpInterruption?.ToString(),
                Comparison = ToReportComparison(experiment.Comparison, publicIds)
            };
        }

        private static ReportAdvisorComparison ToReportComparison(AdvisorComparison comparison, HashSet<string> publicIds)
            => comparison == null ? null : new ReportAdvisorComparison
            {
                MultipleChanges = comparison.MultipleChanges,
                ChangedSettingIds = comparison.ChangedSettingIds.Where(publicIds.Contains).ToList(),
                Metrics = comparison.Metrics.Select(x => new ReportAdvisorMetricComparison
                {
                    Id = x.Id, BaselineValue = x.BaselineValue, FollowUpValue = x.FollowUpValue,
                    State = x.State.ToString(), Reason = x.Reason
                }).ToList()
            };

        private static bool HasAvailableMetric(IEnumerable<ReportMetric> metrics) => (metrics ?? Array.Empty<ReportMetric>()).Any(metric => metric != null && string.Equals(metric.Availability, "Available", StringComparison.Ordinal));
        private static RuntimeReportMetadata ResolveRuntimeMetadata() { var provider = RuntimeMetadataProvider; if (provider == null) return null; try { return provider(); } catch { return null; } }
        private static CaptureConfigurationSnapshot ResolveCaptureConfiguration() { var provider = CaptureConfigurationProvider; if (provider == null) return null; try { return provider(); } catch { return null; } }

        private static void AddCaptureScope(PerformanceReport report, UiSnapshot snapshot)
        {
            var capture = snapshot.Capture ?? new CaptureUiState();
            if (!string.IsNullOrWhiteSpace(capture.DetailCaptureId)) report.CaptureConfig.Add(new ReportNamedValue("detailCaptureId", capture.DetailCaptureId));
            report.CaptureConfig.Add(new ReportNamedValue("detailScope", string.IsNullOrWhiteSpace(capture.DetailScope) ? "live" : capture.DetailScope));
            if (snapshot.Global?.Available == true) report.CaptureConfig.Add(new ReportNamedValue("globalTimestampSeconds", snapshot.Global.TimestampSeconds.ToString("R", CultureInfo.InvariantCulture)));
        }

        private static void AddCaptureConfiguration(PerformanceReport report, CaptureConfigurationSnapshot config)
        {
            if (config == null) return;
            report.CaptureConfig.Add(new ReportNamedValue("samplingPeriodSeconds", Number(config.SamplingPeriodSeconds)));
            report.CaptureConfig.Add(new ReportNamedValue("automaticCaptureEnabled", config.AutomaticCaptureEnabled ? "true" : "false"));
            report.CaptureConfig.Add(new ReportNamedValue("efficiencyThreshold", Number(config.EfficiencyThreshold)));
            report.CaptureConfig.Add(new ReportNamedValue("lowEfficiencySustainSeconds", Number(config.LowEfficiencySustainSeconds)));
            report.CaptureConfig.Add(new ReportNamedValue("prebufferSeconds", Number(config.PrebufferSeconds)));
            report.CaptureConfig.Add(new ReportNamedValue("deepCaptureSeconds", Number(config.DeepCaptureSeconds)));
            report.CaptureConfig.Add(new ReportNamedValue("postbufferSeconds", Number(config.PostbufferSeconds)));
            report.CaptureConfig.Add(new ReportNamedValue("cooldownSeconds", Number(config.CooldownSeconds)));
            report.CaptureConfig.Add(new ReportNamedValue("maxConcurrentMarkers", config.MaxConcurrentMarkers.ToString(CultureInfo.InvariantCulture)));
            report.CaptureConfig.Add(new ReportNamedValue("profilerOverheadLimit", Number(config.ProfilerOverheadLimit)));
            report.CaptureConfig.Add(new ReportNamedValue("maxCompletedCaptures", config.MaxCompletedCaptures.ToString(CultureInfo.InvariantCulture)));
        }

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static void AddGlobal(PerformanceReport report, GlobalUiMetrics global)
        {
            if (global == null || !global.Available) return;
            report.GlobalMetrics.Add(Scalar("selectedSpeed", global.SelectedSpeed, "x")); report.GlobalMetrics.Add(Scalar("actualSpeed", global.ActualSpeed, "x")); report.GlobalMetrics.Add(Scalar("efficiency", global.Efficiency, "ratio"));
            foreach (var metric in global.RecorderMetrics ?? Array.Empty<UiMetricRow>()) report.GlobalMetrics.Add(ToReportMetric(metric));
        }
        private static ReportMetric Scalar(string name, double? value, string unit) => new ReportMetric { Name = name, Value = value, Unit = unit, Confidence = value.HasValue ? "Full" : "Unavailable", Availability = value.HasValue ? "Available" : "Unavailable" };
        private static ReportMetric ToReportMetric(UiMetricRow metric) => metric == null ? new ReportMetric { Name = "unknown", Availability = "Unavailable", Confidence = "Unavailable" } : new ReportMetric { Name = metric.Id, Value = metric.Value, Unit = metric.UnitType, Confidence = metric.Confidence, Availability = metric.Availability, Note = metric.Reason };
    }
}
