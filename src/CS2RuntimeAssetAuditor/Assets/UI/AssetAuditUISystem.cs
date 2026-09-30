using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.UI.Binding;
using CS2RuntimeAssetAuditor.Assets.Core;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Diagnostics;
using CS2RuntimeAssetAuditor.Assets.Core.Findings;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Core.Query;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using CS2RuntimeAssetAuditor.Assets.Export;
using CS2RuntimeAssetAuditor.Assets.GameIntegration;
using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.Lifecycle;
using System.IO;
using System.Text;
using Colossal.PSI.Environment;
using Game.UI;
using Unity.Entities;

namespace CS2RuntimeAssetAuditor.Assets.UI
{
    public sealed partial class AssetAuditUISystem : UISystemBase
    {
        private readonly UiSnapshotBuilder _snapshotBuilder = new UiSnapshotBuilder();
        private readonly DiagnosticAggregator _diagnostics = new DiagnosticAggregator();
        private readonly AuditReportBuilder _reportBuilder = new AuditReportBuilder(CS2RuntimeAssetAuditor.Assets.Export.PrivacySanitizer.Shared);
        private readonly CsvSummaryExporter _csvExporter = new CsvSummaryExporter();

        private ValueBinding<string>? _snapshotBinding;
        private ValueBinding<string>? _exportBinding;
        private ValueBinding<string>? _findingsBinding;
        private AssetAnalysisSnapshot? _publishedFindingsAnalysis;
        private bool _publishedFindingsOnce;
        private bool _deferredChanges;
        private AssetAuditSystem? _lastAuditSystem;
        private CensusSnapshot? _lastCensus;
        private AssetAnalysisSnapshot? _lastAnalysis;
        private long _lastCatalogGeneration = -1;
        private AssetQuery _assetQuery = new AssetQuery();
        private AssetPage _assetPage = new AssetPage(Array.Empty<AssetPageItem>(), 0, 0, 100);
        private UiScanOptions _uiSettings = new UiScanOptions();
        private DateTimeOffset _lastPublishedAt;
        private AssetStatusFingerprint _lastPublishedStatus;

        protected override void OnCreate()
        {
            base.OnCreate();
            var stored = Mod.Settings;
            if (stored != null)
                _uiSettings = NormalizeSettings(FromStoredSettings(stored));
            _snapshotBinding = new ValueBinding<string>(UiBindingContract.Group, UiBindingContract.Snapshot, "{}");
            _exportBinding = new ValueBinding<string>(UiBindingContract.Group, UiBindingContract.ExportedReport, string.Empty);
            AddBinding(_snapshotBinding);
            AddBinding(_exportBinding);
            _findingsBinding = new ValueBinding<string>(UiBindingContract.Group, UiBindingContract.Findings, "{}");
            AddBinding(_findingsBinding);
            AddBinding(new TriggerBinding<string>(UiBindingContract.Group, UiBindingContract.RequestCensus, HandleRequestCensus, new Colossal.UI.Binding.StringReader()));
            AddBinding(new TriggerBinding<string>(UiBindingContract.Group, UiBindingContract.RequestAssetAudit, HandleRequestAssetAudit, new Colossal.UI.Binding.StringReader()));
            AddBinding(new TriggerBinding<string>(UiBindingContract.Group, UiBindingContract.RequestDeepInspection, HandleRequestDeepInspection, new Colossal.UI.Binding.StringReader()));
            AddBinding(new TriggerBinding(UiBindingContract.Group, UiBindingContract.CancelCensus, HandleCancelCurrentScan));
            AddBinding(new TriggerBinding<string>(UiBindingContract.Group, UiBindingContract.QueryAssets, HandleQueryAssets, new Colossal.UI.Binding.StringReader()));
            AddBinding(new TriggerBinding<string>(UiBindingContract.Group, UiBindingContract.RequestExport, HandleRequestExport, new Colossal.UI.Binding.StringReader()));
            AddBinding(new TriggerBinding<string>(UiBindingContract.Group, UiBindingContract.UpdateSettings, HandleUpdateSettings, new Colossal.UI.Binding.StringReader()));
            RefreshAssetPage(GetAuditSystem(), force: true);
            PublishSnapshot();
        }
        protected override void OnUpdate()
        {
            var start = ModUpdateCost.Start();
            try { RunUpdate(); }
            finally { ModUpdateCost.Stop(nameof(AssetAuditUISystem), start); }
        }

        private void RunUpdate()
        {
            base.OnUpdate();
            var auditSystem = GetAuditSystem();
            // The shared shell owns panel visibility; this system only reads it.
            var shellVisible = IsPanelVisible();
            if (!shellVisible)
            {
                // Building the snapshot maps the asset page and serializes it; skip that work while nobody can see it.
                // Once a change is pending the panel publishes as soon as it is shown, and that first visible
                // update refreshes the asset page itself, so nothing more needs to be checked until then.
                if (!_deferredChanges)
                    _deferredChanges = RefreshAssetPage(auditSystem, force: false)
                        || !CaptureStatus(auditSystem).Matches(_lastPublishedStatus);
                return;
            }
            var dataChanged = RefreshAssetPage(auditSystem, force: false);
            var statusChanged = !CaptureStatus(auditSystem).Matches(_lastPublishedStatus);
            var decision = UiPublishPolicy.Decide(
                shellVisible,
                _deferredChanges,
                dataChanged,
                statusChanged,
                auditSystem?.IsScanActive == true,
                DateTimeOffset.UtcNow - _lastPublishedAt,
                TimeSpan.FromMilliseconds(_uiSettings.ProgressUpdateMs));
            if (decision == UiPublishDecision.Publish)
                PublishSnapshot();
        }

        protected override void OnDestroy()
        {
            _snapshotBinding = null;
            _exportBinding = null;
            _findingsBinding = null;
            _publishedFindingsAnalysis = null;
            _lastAuditSystem = null;
            _lastCensus = null;
            _lastAnalysis = null;
            base.OnDestroy();
        }

        private bool IsPanelVisible()
        {
            if (!World.IsCreated)
                return false;
            var shell = World.GetExistingSystemManaged<global::CS2RuntimeAssetAuditor.UI.ProfilerUISystem>();
            return shell?.IsPanelVisible ?? true;
        }

        private AssetAuditSystem? GetAuditSystem()
        {
            if (!World.IsCreated)
                return null;
            return World.GetExistingSystemManaged<AssetAuditSystem>();
        }

        private void HandleRequestCensus(string optionsJson)
        {
            try
            {
                if (UiSnapshotBuilder.TryDeserialize<UiScanOptions>(optionsJson, out var options))
                    ApplySettings(options);
                var scanOptions = new ScanOptions(_uiSettings.CollectSubordinateObjects, _uiSettings.CollectNetworkEdges);
                InvalidateExport();
                GetAuditSystem()?.RequestCensusScan(scanOptions, _uiSettings.FrameBudgetMs);
                PublishSnapshot();
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Census request was rejected", ex);
                _diagnostics.Add("APA-CEN-004", "ui_census_request_rejected");
                PublishSnapshot();
            }
        }

        private void HandleRequestAssetAudit(string optionsJson)
        {
            try
            {
                if (UiSnapshotBuilder.TryDeserialize<UiScanOptions>(optionsJson, out var options))
                    ApplySettings(options);
                InvalidateExport();
                var auditSystem = GetAuditSystem();
                if (auditSystem != null)
                {
                    auditSystem.DeepInspectionLimit = _uiSettings.DeepInspectionLimit;
                    auditSystem.RequestAssetAudit(
                        _uiSettings.FrameBudgetMs,
                        _uiSettings.RefreshCatalogAtScanStart,
                        _uiSettings.EnableHeuristicFindings,
                        _uiSettings.EnablePeerOutliers,
                        ParseEnum(_uiSettings.ComparisonPopulation, ComparisonPopulation.SameCategory),
                        _uiSettings.MetadataCacheLimit);
                }
                PublishSnapshot();
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Asset Audit request was rejected", ex);
                _diagnostics.Add("APA-AUD-004", "ui_asset_audit_request_rejected");
                PublishSnapshot();
            }
        }

        private void HandleRequestDeepInspection(string renderKeyText)
        {
            try
            {
                var auditSystem = GetAuditSystem();
                if (auditSystem != null)
                    auditSystem.DeepInspectionLimit = _uiSettings.DeepInspectionLimit;
                // An invalid key or a busy/stale audit is an expected rejection, not an error worth logging.
                if (!RenderAssetKey.TryParse(renderKeyText, out var renderKey)
                    || auditSystem == null || !auditSystem.RequestDeepInspection(renderKey))
                {
                    _diagnostics.Add("APA-DEEP-004", "ui_deep_inspection_request_rejected");
                    PublishSnapshot();
                    return;
                }
                InvalidateExport();
                PublishSnapshot();
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Deep Inspection request failed", ex);
                _diagnostics.Add("APA-DEEP-004", "ui_deep_inspection_request_rejected");
                PublishSnapshot();
            }
        }

        private void HandleCancelCurrentScan()
        {
            GetAuditSystem()?.CancelCurrentScan();
            PublishSnapshot();
        }

        private void HandleQueryAssets(string queryJson)
        {
            try
            {
                if (!UiSnapshotBuilder.TryDeserialize<UiAssetQueryRequest>(queryJson, out var request))
                    throw new ArgumentException("The asset query payload was invalid.");
                _assetQuery = CreateAssetQuery(request);
                RefreshAssetPage(GetAuditSystem(), force: true);
                PublishSnapshot();
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Asset query was rejected", ex);
                _diagnostics.Add("APA-EXP-002", "ui_asset_query_rejected");
                PublishSnapshot();
            }
        }

        private void HandleUpdateSettings(string settingsJson)
        {
            try
            {
                if (!UiSnapshotBuilder.TryDeserialize<UiScanOptions>(settingsJson, out var options))
                    throw new ArgumentException("The settings payload was invalid.");
                ApplySettings(options);
                PublishSnapshot();
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Asset settings update was rejected", ex);
                _diagnostics.Add("APA-EXP-003", "ui_scan_settings_rejected");
                PublishSnapshot();
            }
        }

        private void HandleRequestExport(string requestJson)
        {
            if (_exportBinding == null)
                return;
            var auditSystem = GetAuditSystem();
            if (auditSystem?.Capabilities == null)
            {
                // Same "ok:"/"error:" protocol as the Runtime export, so the UI can always show an outcome.
                _exportBinding.Update(ExportResultFormat.Failed("APA-EXP-004", "Asset capabilities are not available yet; load a city and try again."));
                return;
            }
            try
            {
                if (!UiSnapshotBuilder.TryDeserialize<UiExportRequest>(requestJson, out var request))
                    throw new ArgumentException("The export request payload was invalid.");
                var start = System.Diagnostics.Stopwatch.GetTimestamp();
                var scope = ParseEnum(request.Scope, ExportScope.Full);
                var includedKeys = ResolveExportKeys(auditSystem, request, scope);
                var report = _reportBuilder.BuildCurrent(
                    auditSystem.CatalogRecords,
                    auditSystem.CatalogGeneration,
                    auditSystem.CatalogGeneration > 0 ? auditSystem.CatalogCapturedAt : (DateTimeOffset?)null,
                    auditSystem.PublishedCensus,
                    auditSystem.PublishedAnalysis,
                    auditSystem.Capabilities,
                    typeof(Mod).Assembly.GetName().Version?.ToString() ?? "unknown",
                    DateTimeOffset.UtcNow,
                    _diagnostics.Snapshot(),
                    scope,
                    includedKeys);
                StampAssetReport(report, auditSystem);
                if (string.Equals(request.Format, "Csv", StringComparison.Ordinal))
                {
                    var directory = Path.Combine(EnvPath.kUserDataPath, "ModsData", Mod.Id);
                    Directory.CreateDirectory(directory);
                    var stem = $"CS2RuntimeAssetAuditor-assets-{DateTime.Now:yyyy-MM-dd_HHmmss_fff}";
                    var built = System.Diagnostics.Stopwatch.GetTimestamp();
                    var csv = _csvExporter.Export(report);
                    var formatted = System.Diagnostics.Stopwatch.GetTimestamp();
                    var path = ReportFileWriter.WriteUnique(directory, stem, stream =>
                    {
                        using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(csv);
                    }, ".csv");
                    var written = System.Diagnostics.Stopwatch.GetTimestamp();
                    // The export runs on the main thread, so these times are the length of the freeze it causes.
                    Mod.Info(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "Asset CSV export timing: file={0} buildMs={1:0.0} formatMs={2:0.0} writeMs={3:0.0} characters={4}",
                        Path.GetFileName(path),
                        ElapsedMilliseconds(start, built),
                        ElapsedMilliseconds(built, formatted),
                        ElapsedMilliseconds(formatted, written),
                        csv.Length));
                    _exportBinding.Update(ExportResultFormat.Succeeded(Path.GetFileName(path)));
                }
                else
                {
                    var profiler = World.GetExistingSystemManaged<global::CS2RuntimeAssetAuditor.UI.ProfilerUISystem>();
                    var unified = profiler != null
                        ? profiler.BuildCurrentUnifiedReport(report)
                        : RuntimeAssetAuditReportBuilder.Build(null, report, Mod.SessionContext, DateTimeOffset.UtcNow);
                    var result = new ReportExporter().Export(unified, ElapsedMilliseconds(start, System.Diagnostics.Stopwatch.GetTimestamp()));
                    _exportBinding.Update(result.Success
                        ? ExportResultFormat.Succeeded(Path.GetFileName(result.Path))
                        : ExportResultFormat.Failed("APA-EXP-001", result.Error));
                }
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Asset report export failed", ex);
                _diagnostics.Add("APA-EXP-001", "audit_report_export_failed");
                _exportBinding.Update(ExportResultFormat.Failed("APA-EXP-001", ReportPrivacy.Sanitize(ex.Message)));
            }
        }

        private static double ElapsedMilliseconds(long from, long to) => (to - from) * 1000d / System.Diagnostics.Stopwatch.Frequency;

        public AuditReport? BuildCurrentReport()
        {
            var auditSystem = GetAuditSystem();
            if (auditSystem?.Capabilities == null) return null;
            var report = _reportBuilder.BuildCurrent(auditSystem.CatalogRecords, auditSystem.CatalogGeneration,
                auditSystem.CatalogGeneration > 0 ? auditSystem.CatalogCapturedAt : (DateTimeOffset?)null,
                auditSystem.PublishedCensus, auditSystem.PublishedAnalysis, auditSystem.Capabilities,
                typeof(Mod).Assembly.GetName().Version?.ToString() ?? "unknown", DateTimeOffset.UtcNow,
                _diagnostics.Snapshot(), ExportScope.Full, null);
            StampAssetReport(report, auditSystem);
            return report;
        }

        private static void StampAssetReport(AuditReport report, AssetAuditSystem system)
        {
            var identity = system.PublishedIdentity;
            if (system.PublishedAnalysis == null || identity == null) return;
            report.SessionId = identity.SessionId;
            report.AssetSnapshotId = identity.SnapshotId;
            report.StartedAtUtc = identity.StartedAtUtc.ToString("O");
            report.CompletedAtUtc = identity.CompletedAtUtc.ToString("O");
            report.EnrichedAtUtc = identity.EnrichedAtUtc?.ToString("O");
        }

        private IEnumerable<PrefabKey>? ResolveExportKeys(AssetAuditSystem auditSystem, UiExportRequest request, ExportScope scope)
        {
            if (scope == ExportScope.Filtered)
            {
                return new AssetQueryService(auditSystem.CatalogRecords, auditSystem.PublishedCensus, auditSystem.CatalogGeneration)
                    .QueryMatchingKeys(_assetQuery);
            }
            if (scope != ExportScope.Selected)
                return null;
            if (request.SelectedKeys == null || request.SelectedKeys.Length == 0)
                throw new ArgumentException("Selected export requires at least one stable Prefab key.");
            return request.SelectedKeys
                .Where(key => key != null && !string.IsNullOrWhiteSpace(key.PrefabId) && !string.IsNullOrWhiteSpace(key.PrefabType))
                .Select(key => new PrefabKey(key.PrefabId, key.PrefabType))
                .Distinct()
                .ToArray();
        }

        private bool RefreshAssetPage(AssetAuditSystem? auditSystem, bool force)
        {
            var catalogGeneration = auditSystem?.CatalogGeneration ?? 0;
            var census = auditSystem?.PublishedCensus;
            var analysis = auditSystem?.PublishedAnalysis;
            var underlyingDataChanged = !ReferenceEquals(auditSystem, _lastAuditSystem)
                || catalogGeneration != _lastCatalogGeneration
                || !ReferenceEquals(census, _lastCensus)
                || !ReferenceEquals(analysis, _lastAnalysis);
            var changed = force || underlyingDataChanged;
            if (!changed)
                return false;
            if (underlyingDataChanged && _lastAuditSystem != null)
                InvalidateExport();
            var records = auditSystem?.CatalogRecords ?? Array.Empty<PrefabRecord>();
            var service = new AssetQueryService(records, census, catalogGeneration);
            _assetPage = service.Query(_assetQuery);
            _lastAuditSystem = auditSystem;
            _lastCatalogGeneration = catalogGeneration;
            _lastCensus = census;
            _lastAnalysis = analysis;
            return true;
        }

        private void InvalidateExport() => _exportBinding?.Update(string.Empty);

        private void PublishSnapshot()
        {
            if (_snapshotBinding == null)
                return;
            var auditSystem = GetAuditSystem();
            var snapshot = _snapshotBuilder.Build(auditSystem, _assetPage, _uiSettings,
                typeof(Mod).Assembly.GetName().Version?.ToString() ?? "unknown");
            snapshot.SessionId = Mod.SessionContext?.SessionId;
            var analysis = GetCurrentAnalysis(auditSystem);
            if (analysis != null && auditSystem?.PublishedAssetSnapshotSessionId == snapshot.SessionId)
            {
                snapshot.Summary.LatestAssetSnapshotId = auditSystem.PublishedAssetSnapshotId;
                snapshot.Summary.LatestAssetSnapshotStartedAtUtc = auditSystem.PublishedAssetSnapshotStartedAtUtc?.ToString("O");
                snapshot.Summary.LatestAssetSnapshotCompletedAtUtc = auditSystem.PublishedAssetSnapshotCompletedAtUtc?.ToString("O");
            }
            UiAnalysisProjection.ApplyDeepInspections(snapshot, analysis);
            snapshot.Diagnostics = CreateDiagnostics(auditSystem);
            _snapshotBinding.Update(UiSnapshotBuilder.Serialize(snapshot));
            PublishFindingsIfChanged(analysis);
            _deferredChanges = false;
            _lastPublishedAt = DateTimeOffset.UtcNow;
            _lastPublishedStatus = CaptureStatus(auditSystem);
        }

        // Findings are serialized only when a different analysis snapshot becomes current.
        private void PublishFindingsIfChanged(AssetAnalysisSnapshot? analysis)
        {
            if (_findingsBinding == null || (_publishedFindingsOnce && ReferenceEquals(analysis, _publishedFindingsAnalysis)))
                return;
            _findingsBinding.Update(UiSnapshotBuilder.Serialize(UiSnapshotBuilder.BuildFindings(analysis)));
            _publishedFindingsAnalysis = analysis;
            _publishedFindingsOnce = true;
        }

        // Cheap, reference-based view of every snapshot input not covered by RefreshAssetPage (catalog, census,
        // analysis) or by the request handlers, which publish on their own. Matching fingerprints mean the snapshot
        // would be unchanged, so it is not rebuilt. Taken every frame, so it allocates nothing.
        private AssetStatusFingerprint CaptureStatus(AssetAuditSystem? auditSystem)
        {
            var scan = auditSystem?.CurrentScan;
            return new AssetStatusFingerprint(
                auditSystem,
                scan,
                scan?.State ?? default,
                scan?.Stage ?? default,
                scan?.Progress,
                auditSystem?.Capabilities,
                auditSystem?.LastDiagnosticCode,
                auditSystem?.CatalogCapturedAt ?? default,
                auditSystem?.CatalogUnresolvedEntityCount ?? 0,
                auditSystem?.UnmatchedPrefabReferenceCount ?? 0,
                auditSystem?.FinalTelemetry,
                auditSystem?.HasLiveTelemetry == true,
                auditSystem?.DiagnosticWorkStatus,
                Mod.SessionContext?.SessionId,
                _diagnostics.OccurrenceCount);
        }

        private static AssetAnalysisSnapshot? GetCurrentAnalysis(AssetAuditSystem? auditSystem)
        {
            var analysis = auditSystem?.PublishedAnalysis;
            if (auditSystem == null || analysis == null)
                return null;
            return analysis.WorldGeneration == auditSystem.WorldGeneration
                && analysis.CatalogGeneration == auditSystem.CatalogGeneration
                ? analysis
                : null;
        }

        private UiDiagnostics CreateDiagnostics(AssetAuditSystem? auditSystem)
        {
            var telemetry = auditSystem?.TelemetrySnapshot;
            return new UiDiagnostics
            {
                Harmony = "not used",
                LastScanState = auditSystem?.CurrentScan?.State.ToString() ?? "Idle",
                LastDiagnosticCode = auditSystem?.LastDiagnosticCode,
                CatalogUnresolvedCount = auditSystem?.CatalogUnresolvedEntityCount ?? 0,
                UnmatchedPrefabReferenceCount = auditSystem?.UnmatchedPrefabReferenceCount ?? 0,
                DiagnosticDistinctCount = _diagnostics.DistinctCount,
                DiagnosticOccurrenceCount = _diagnostics.OccurrenceCount,
                Telemetry = telemetry == null ? null : new UiScanTelemetry
                {
                    ElapsedMilliseconds = telemetry.ElapsedMilliseconds,
                    ProcessedItems = telemetry.ProcessedItems,
                    SliceCount = telemetry.SliceCount,
                    SampleCount = telemetry.SampleCount,
                    MaxSliceMilliseconds = telemetry.MaxSliceMilliseconds,
                    P95SliceMilliseconds = telemetry.P95SliceMilliseconds
                },
                AggregatedDiagnostics = _diagnostics.Snapshot().Select(item => new UiDiagnosticEntry
                {
                    Code = item.Code.Value,
                    Message = item.Message,
                    Count = item.Count,
                    FirstSeenAt = FormatTime(item.FirstSeenAt),
                    LastSeenAt = FormatTime(item.LastSeenAt)
                }).ToArray()
            };
        }

        private static AssetQuery CreateAssetQuery(UiAssetQueryRequest request)
        {
            var traitFilter = ParseOptionalEnum<PrefabTraits>(request.TraitFilter);
            var sourceFilter = ParseEnum(request.SourceFilter, AssetSourceFilter.Any);
            var presenceFilter = ParseOptionalEnum<CensusPresence>(request.PresenceFilter);
            var sort = ParseEnum(request.Sort, AssetSort.DisplayNameAscending);
            return new AssetQuery(request.SearchText, traitFilter, sourceFilter, presenceFilter, sort, request.Offset, Math.Min(AssetQueryService.MaximumPageSize, Math.Max(1, request.Limit)));
        }

        private void ApplySettings(UiScanOptions options)
        {
            _uiSettings = NormalizeSettings(options);
            var stored = Mod.Settings;
            if (stored == null || SameSettings(FromStoredSettings(stored), _uiSettings))
                return;
            stored.CollectSubordinateObjects = _uiSettings.CollectSubordinateObjects;
            stored.CollectNetworkEdges = _uiSettings.CollectNetworkEdges;
            stored.FrameBudgetMs = _uiSettings.FrameBudgetMs;
            stored.ProgressUpdateMs = _uiSettings.ProgressUpdateMs;
            stored.RefreshCatalogAtScanStart = _uiSettings.RefreshCatalogAtScanStart;
            stored.EnableHeuristicFindings = _uiSettings.EnableHeuristicFindings;
            stored.EnablePeerOutliers = _uiSettings.EnablePeerOutliers;
            stored.ComparisonPopulation = _uiSettings.ComparisonPopulation;
            stored.ShowNoticeFindings = _uiSettings.ShowNoticeFindings;
            stored.PageSize = _uiSettings.PageSize;
            stored.MetadataCacheLimit = _uiSettings.MetadataCacheLimit;
            stored.DeepInspectionLimit = _uiSettings.DeepInspectionLimit;
            stored.UiScale = _uiSettings.UiScale;
            stored.ApplyAndSave();
        }

        private static UiScanOptions FromStoredSettings(global::CS2RuntimeAssetAuditor.Setting stored) => new UiScanOptions
        {
            CollectSubordinateObjects = stored.CollectSubordinateObjects,
            CollectNetworkEdges = stored.CollectNetworkEdges,
            FrameBudgetMs = stored.FrameBudgetMs,
            ProgressUpdateMs = stored.ProgressUpdateMs,
            RefreshCatalogAtScanStart = stored.RefreshCatalogAtScanStart,
            EnableHeuristicFindings = stored.EnableHeuristicFindings,
            EnablePeerOutliers = stored.EnablePeerOutliers,
            ComparisonPopulation = stored.ComparisonPopulation,
            ShowNoticeFindings = stored.ShowNoticeFindings,
            PageSize = stored.PageSize,
            MetadataCacheLimit = stored.MetadataCacheLimit,
            DeepInspectionLimit = stored.DeepInspectionLimit,
            UiScale = stored.UiScale
        };

        private static bool SameSettings(UiScanOptions left, UiScanOptions right)
        {
            return StringComparer.Ordinal.Equals(UiSnapshotBuilder.Serialize(left), UiSnapshotBuilder.Serialize(right));
        }

        private static UiScanOptions NormalizeSettings(UiScanOptions options)
        {
            return new UiScanOptions
            {
                CollectSubordinateObjects = options.CollectSubordinateObjects,
                CollectNetworkEdges = options.CollectNetworkEdges,
                FrameBudgetMs = Clamp(options.FrameBudgetMs, 0.25, 8.0, 1.0),
                ProgressUpdateMs = (int)Clamp(options.ProgressUpdateMs, 50, 2000, 200),
                RefreshCatalogAtScanStart = options.RefreshCatalogAtScanStart,
                EnableHeuristicFindings = options.EnableHeuristicFindings,
                EnablePeerOutliers = options.EnablePeerOutliers,
                ComparisonPopulation = NormalizePopulation(options.ComparisonPopulation),
                ShowNoticeFindings = options.ShowNoticeFindings,
                PageSize = (int)Clamp(options.PageSize, 25, AssetQueryService.MaximumPageSize, 100),
                MetadataCacheLimit = (int)Clamp(options.MetadataCacheLimit, 64, 4096, 512),
                DeepInspectionLimit = (int)Clamp(options.DeepInspectionLimit, 1, 16, 1),
                UiScale = Clamp(options.UiScale, 0.75, 1.5, 1.0)
            };
        }

        private static double Clamp(double value, double minimum, double maximum, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return fallback;
            return Math.Min(maximum, Math.Max(minimum, value));
        }

        private static string NormalizePopulation(string? value)
        {
            switch (value)
            {
                case "BuiltinDlc":
                case "Custom":
                case "SameSourcePack":
                case "SameCategory":
                    return value;
                default:
                    return "SameCategory";
            }
        }

        private static T ParseEnum<T>(string? value, T fallback) where T : struct
        {
            return !string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, ignoreCase: false, out T parsed)
                && Enum.IsDefined(typeof(T), parsed)
                ? parsed
                : fallback;
        }

        private static T? ParseOptionalEnum<T>(string? value) where T : struct
        {
            if (string.IsNullOrWhiteSpace(value) || !Enum.TryParse(value, ignoreCase: false, out T parsed) || !Enum.IsDefined(typeof(T), parsed))
                return null;
            return parsed;
        }

        private static string FormatTime(DateTimeOffset value) => value.ToUniversalTime().ToString("O");
    }
}
