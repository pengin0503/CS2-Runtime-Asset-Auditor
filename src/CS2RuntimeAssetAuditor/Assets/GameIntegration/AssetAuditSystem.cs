using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using CS2RuntimeAssetAuditor.Coordination;
using CS2RuntimeAssetAuditor.Assets.Core.Capabilities;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Diagnostics;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using CS2RuntimeAssetAuditor.Assets.Core.Scanning;
using CS2RuntimeAssetAuditor.Assets.GameIntegration.Capabilities;
using CS2RuntimeAssetAuditor.Assets.GameIntegration.Census;
using CS2RuntimeAssetAuditor.Assets.GameIntegration.Prefabs;
using CS2RuntimeAssetAuditor.Assets.GameIntegration.Rendering;
using Game;
using Game.Prefabs;
using Unity.Entities;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration
{
    public sealed partial class AssetAuditSystem : GameSystemBase
    {
        private const int CatalogSliceSize = 64;
        private const int CensusReductionSliceSize = 512;
        private static long _nextWorldGeneration;

        private IPrefabCatalogAccess? _catalog;
        private CensusAccess? _censusAccess;
        private readonly PublishedAuditState _publishedState = new PublishedAuditState();
        private readonly DeepInspectionReader _deepInspectionReader = new DeepInspectionReader();
        private CensusReducer? _censusReducer;
        private AssetAnalysisCollector? _analysisCollector;
        private RenderGraphSnapshot? _publishedRuntimeRenderGraph;
        private ScanTelemetry? _scanTelemetry;
        private ScanTelemetrySnapshot? _finalTelemetry;
        private bool _catalogScanRequested;
        private bool _catalogCaptureActive;
        private bool _censusScanRequested;
        private bool _censusCleanupRequested;
        private bool _networkCaptureStarted;
        private bool _assetAuditRequested;
        private bool _assetAuditWaitingForCatalog;
        private bool _assetAuditRefreshCatalog = true;
        private bool _assetAuditEnableHeuristics = true;
        private bool _deepInspectionRequested;
        private bool _interruptedByRuntimeCapture;
        private RenderAssetKey _requestedDeepInspectionKey;
        private RenderAssetKey _activeDeepInspectionKey;
        private double _assetAuditFrameBudgetMs = 1.0;
        private long _completedCatalogCapturesBeforeCapture;
        private long _nextAnalysisGeneration;
        private long _observedSessionGeneration = -1;
        private ScanOptions _requestedOptions = ScanOptions.Default;
        private PublishedAssetSnapshotIdentity? _publishedIdentity;

        public CapabilityReport? Capabilities { get; private set; }
        public long WorldGeneration { get; private set; }
        public long CatalogGeneration => _catalog?.CatalogGeneration ?? 0;
        public DateTimeOffset CatalogCapturedAt => _catalog?.CatalogCapturedAt ?? DateTimeOffset.MinValue;
        public IReadOnlyList<PrefabRecord> CatalogRecords => _catalog?.PublishedRecords ?? Array.Empty<PrefabRecord>();
        public int CatalogCapturedEntityCount => _catalog?.CapturedEntityCount ?? 0;
        public int CatalogProcessedEntityCount => _catalog?.ProcessedEntityCount ?? 0;
        public int CatalogUnresolvedEntityCount => _catalog?.UnresolvedEntityCount ?? 0;
        public IReadOnlyDictionary<Entity, PrefabKey> RuntimeEntityKeys => _catalog?.RuntimeEntityKeys ?? new Dictionary<Entity, PrefabKey>();
        public ScanSession? CurrentScan { get; private set; }
        public bool IsScanActive => CurrentScan != null && (CurrentScan.State == ScanState.Running || CurrentScan.State == ScanState.CancellationRequested);
        public bool IsCensusScanActive => IsScanActive && CurrentScan?.Kind == ScanKind.Census;
        public string DiagnosticWorkStatus { get; private set; } = "Idle";
        public CensusSnapshot? PublishedCensus => _publishedState.Census;
        public AssetAnalysisSnapshot? PublishedAnalysis => _publishedState.Analysis;
        public PublishedAssetSnapshotIdentity? PublishedIdentity => _publishedIdentity;
        public string? PublishedAssetSnapshotId => _publishedIdentity?.SnapshotId;
        public string? PublishedAssetSnapshotSessionId => _publishedIdentity?.SessionId;
        public DateTimeOffset? PublishedAssetSnapshotStartedAtUtc => _publishedIdentity?.StartedAtUtc;
        public DateTimeOffset? PublishedAssetSnapshotCompletedAtUtc => _publishedIdentity?.CompletedAtUtc;
        public DateTimeOffset? PublishedAssetSnapshotEnrichedAtUtc => _publishedIdentity?.EnrichedAtUtc;
        public RenderGraphSnapshot? PublishedRuntimeRenderGraph => _publishedRuntimeRenderGraph;
        public string? LastDiagnosticCode { get; private set; }
        public int UnmatchedPrefabReferenceCount => _censusAccess?.UnmatchedPrefabReferenceCount ?? 0;
        // While a scan runs, elapsed time is live; once it ends the snapshot is frozen at the frame the scan ended.
        public ScanTelemetrySnapshot? TelemetrySnapshot => _finalTelemetry ?? _scanTelemetry?.Snapshot(DateTimeOffset.UtcNow);

        // Scans describe the loaded city; without an active city session there is nothing to audit.
        private static bool CitySessionActive => Mod.Sessions.IsActive;

        public void RequestCatalogScan()
        {
            if (CitySessionActive && !IsScanActive && !_catalogCaptureActive && !_assetAuditRequested && !_deepInspectionRequested)
                _catalogScanRequested = true;
        }

        public void RequestCensusScan(ScanOptions? scanOptions = null)
        {
            if (!CitySessionActive || IsScanActive || _censusScanRequested || _censusCleanupRequested || _assetAuditRequested || _assetAuditWaitingForCatalog || _deepInspectionRequested)
                return;
            _requestedOptions = scanOptions ?? ScanOptions.Default;
            _censusScanRequested = true;
        }

        public void RequestAssetAudit(double frameBudgetMilliseconds = 1.0, bool refreshCatalogAtScanStart = true, bool enableHeuristicFindings = true)
        {
            if (!CitySessionActive || IsScanActive || _assetAuditRequested || _assetAuditWaitingForCatalog || _censusScanRequested || _censusCleanupRequested || _catalogCaptureActive || _deepInspectionRequested)
                return;
            _assetAuditFrameBudgetMs = NormalizeFrameBudget(frameBudgetMilliseconds);
            _assetAuditRefreshCatalog = refreshCatalogAtScanStart;
            _assetAuditEnableHeuristics = enableHeuristicFindings;
            _assetAuditRequested = true;
        }

        public bool RequestDeepInspection(RenderAssetKey key)
        {
            if (!CitySessionActive || !key.IsValid || IsScanActive || _catalogCaptureActive || _catalogScanRequested || _censusScanRequested || _censusCleanupRequested || _assetAuditRequested || _assetAuditWaitingForCatalog || _deepInspectionRequested)
                return false;
            var analysis = PublishedAnalysis;
            var graph = _publishedRuntimeRenderGraph;
            if (analysis == null || graph == null || analysis.WorldGeneration != WorldGeneration || analysis.CatalogGeneration != CatalogGeneration)
                return false;
            if (!analysis.TryGetRenderAsset(key, out _) || !graph.TryGetRuntimeAsset(key, out var runtime) || !(runtime is RenderPrefab))
                return false;
            _requestedDeepInspectionKey = key;
            _deepInspectionRequested = true;
            return true;
        }

        public void CancelCensusScan()
        {
            if (CurrentScan?.Kind == ScanKind.Census && CurrentScan.State == ScanState.Running)
                CurrentScan.RequestCancellation();
        }

        public void CancelCurrentScan()
        {
            if (CurrentScan?.State == ScanState.Running)
                CurrentScan.RequestCancellation();
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            WorldGeneration = Interlocked.Increment(ref _nextWorldGeneration);
            _publishedState.ResetForWorld(WorldGeneration);
            Capabilities = CapabilityProbe.Probe(World);
            _catalog = new PrefabCatalogAccess(World);
            _censusAccess = new CensusAccess(World);
        }

        protected override void OnUpdate()
        {
            ObserveSessionChange();
            if (Mod.WorkCoordinator.ConsumeAssetInterruptionRequest())
                InterruptForRuntimeCapture();
            try
            {
                UpdateScans();
                FreezeTelemetryAfterScanEnds();
            }
            finally
            {
                if (!IsScanActive && !_censusCleanupRequested && !_catalogCaptureActive
                    && !_catalogScanRequested && !_censusScanRequested && !_assetAuditRequested && !_deepInspectionRequested)
                {
                    Mod.WorkCoordinator.Complete(DiagnosticWorkKind.AssetHeavyScan);
                    if (DiagnosticWorkStatus == "Running") DiagnosticWorkStatus = "Idle";
                }
            }
        }

        // The game keeps this World across city loads. Entities, the census and render evidence belong to the
        // city that produced them, so a session change cancels in-flight work and discards published state.
        // Advancing WorldGeneration also makes any scan still unwinding fail its publish checks.
        private void ObserveSessionChange()
        {
            var generation = Mod.Sessions.Generation;
            if (generation == _observedSessionGeneration)
                return;
            var firstObservation = _observedSessionGeneration < 0;
            _observedSessionGeneration = generation;
            if (firstObservation)
                return;

            _catalogScanRequested = false;
            _censusScanRequested = false;
            _assetAuditRequested = false;
            _deepInspectionRequested = false;
            _assetAuditWaitingForCatalog = false;
            if (CurrentScan?.State == ScanState.Running)
                CurrentScan.RequestCancellation();
            if (_catalogCaptureActive && CurrentScan == null)
                _catalogCaptureActive = false;
            // Resetting the catalog cancels its working and staged captures; a census scan that is still
            // unwinding keeps its own references and finishes cleanup through the normal cancellation path.
            _catalog?.ResetForWorld();

            WorldGeneration = Interlocked.Increment(ref _nextWorldGeneration);
            _publishedState.ResetForWorld(WorldGeneration);
            _publishedIdentity = null;
            _publishedRuntimeRenderGraph = null;
            _analysisCollector = null;
            _requestedDeepInspectionKey = default;
            _activeDeepInspectionKey = default;
            LastDiagnosticCode = null;
            DiagnosticWorkStatus = "Idle";
        }

        private bool TryBeginHeavyWork()
        {
            var decision = Mod.WorkCoordinator.Request(DiagnosticWorkKind.AssetHeavyScan);
            if (decision == DiagnosticWorkDecision.Queued)
            {
                DiagnosticWorkStatus = "WaitingForRuntimeCapture";
                return false;
            }
            if (decision == DiagnosticWorkDecision.Started)
                _interruptedByRuntimeCapture = false;
            DiagnosticWorkStatus = "Running";
            return true;
        }

        private void InterruptForRuntimeCapture()
        {
            _interruptedByRuntimeCapture = true;
            DiagnosticWorkStatus = "InterruptedByRuntimeCapture";
            LastDiagnosticCode = "InterruptedByRuntimeCapture";
            _catalogScanRequested = false;
            _censusScanRequested = false;
            _assetAuditRequested = false;
            _deepInspectionRequested = false;
            _assetAuditWaitingForCatalog = false;
            if (CurrentScan?.State == ScanState.Running)
                CurrentScan.RequestCancellation();
            if (_catalogCaptureActive && CurrentScan == null)
            {
                _catalog?.CancelCapture();
                _catalogCaptureActive = false;
            }
        }

        private void UpdateScans()
        {
            if (_censusCleanupRequested)
            {
                PollCensusCleanup();
                return;
            }

            if (CurrentScan?.State == ScanState.CancellationRequested)
            {
                if (CurrentScan.Kind == ScanKind.Census)
                    BeginCensusCancellation();
                else
                    CancelManagedScan();
                return;
            }

            if (_censusScanRequested && !IsScanActive && !_catalogCaptureActive)
            {
                if (!TryBeginHeavyWork()) return;
                StartCensusScan();
                return;
            }

            if (_assetAuditRequested && !IsScanActive && !_catalogCaptureActive)
            {
                if (!TryBeginHeavyWork()) return;
                StartAssetAudit();
                return;
            }

            if (_deepInspectionRequested && !IsScanActive && !_catalogCaptureActive)
            {
                if (!TryBeginHeavyWork()) return;
                StartDeepInspection();
                return;
            }

            if (_catalogScanRequested && !_catalogCaptureActive)
            {
                if (!TryBeginHeavyWork()) return;
                StartCatalogCapture();
                return;
            }

            if (_catalogCaptureActive)
            {
                ProcessCatalogCapture();
                return;
            }

            if (!IsScanActive)
                return;

            if (CurrentScan!.Kind == ScanKind.Census)
                AdvanceCensusScan();
            else if (CurrentScan.Kind == ScanKind.AssetAudit)
                AdvanceAssetAudit();
            else if (CurrentScan.Kind == ScanKind.DeepInspection)
                AdvanceDeepInspection();
        }

        protected override void OnDestroy()
        {
            Mod.WorkCoordinator.Complete(DiagnosticWorkKind.AssetHeavyScan);
            if (CurrentScan?.State == ScanState.Running)
                CurrentScan.RequestCancellation();
            _catalog?.CancelCapture();
            _censusAccess?.Dispose();
            _analysisCollector = null;
            _publishedRuntimeRenderGraph = null;
            _deepInspectionRequested = false;
            _requestedDeepInspectionKey = default;
            _activeDeepInspectionKey = default;
            if (CurrentScan?.State == ScanState.CancellationRequested)
                CurrentScan.MarkCancelled();
            _publishedState.ResetForWorld(WorldGeneration + 1);
            _publishedIdentity = null;
            base.OnDestroy();
        }

        private void StartCensusScan()
        {
            _censusScanRequested = false;
            LastDiagnosticCode = null;
            var startedAt = DateTimeOffset.UtcNow;
            StartTelemetry(startedAt);
            CurrentScan = ScanSession.Start(ScanKind.Census, WorldGeneration, startedAt);
            CurrentScan.TransitionTo(ScanStage.CapturingCatalog);
            _catalogScanRequested = true;
        }

        private void StartAssetAudit()
        {
            _assetAuditRequested = false;
            LastDiagnosticCode = null;
            var startedAt = DateTimeOffset.UtcNow;
            StartTelemetry(startedAt);
            CurrentScan = ScanSession.Start(ScanKind.AssetAudit, WorldGeneration, startedAt);

            if (_assetAuditRefreshCatalog || CatalogGeneration == 0)
            {
                _assetAuditWaitingForCatalog = true;
                _catalogScanRequested = true;
                return;
            }

            BeginAssetAnalysis();
        }

        private void StartDeepInspection()
        {
            _deepInspectionRequested = false;
            var analysis = PublishedAnalysis;
            var graph = _publishedRuntimeRenderGraph;
            if (!_requestedDeepInspectionKey.IsValid || analysis == null || graph == null || analysis.WorldGeneration != WorldGeneration || analysis.CatalogGeneration != CatalogGeneration)
            {
                LastDiagnosticCode = "APA-DEEP-003";
                _requestedDeepInspectionKey = default;
                return;
            }
            if (!analysis.TryGetRenderAsset(_requestedDeepInspectionKey, out _) || !graph.TryGetRuntimeAsset(_requestedDeepInspectionKey, out var runtime) || !(runtime is RenderPrefab))
            {
                LastDiagnosticCode = "APA-DEEP-003";
                _requestedDeepInspectionKey = default;
                return;
            }

            LastDiagnosticCode = null;
            var startedAt = DateTimeOffset.UtcNow;
            StartTelemetry(startedAt);
            CurrentScan = ScanSession.Start(ScanKind.DeepInspection, WorldGeneration, startedAt);
            _activeDeepInspectionKey = _requestedDeepInspectionKey;
            _requestedDeepInspectionKey = default;
            CurrentScan.TransitionTo(ScanStage.DeepInspecting);
        }

        private void StartCatalogCapture()
        {
            _catalogScanRequested = false;
            if (_catalog == null)
                return;
            _completedCatalogCapturesBeforeCapture = _catalog.CompletedCaptureCount;
            var deferPublication = CurrentScan?.Kind == ScanKind.Census
                && CurrentScan.State == ScanState.Running
                && CurrentScan.Stage == ScanStage.CapturingCatalog;
            try
            {
                _catalog.BeginCapture(deferPublication);
                _catalogCaptureActive = true;
            }
            catch
            {
                LastDiagnosticCode = "APA-CAT-001";
                DegradeCapability(CapabilityId.PrefabCatalog, "catalog_capture_failed");
                if (CurrentScan?.State == ScanState.Running)
                {
                    if (CurrentScan.Kind == ScanKind.Census)
                        FailCensusScan("APA-CAT-001", CapabilityId.PrefabCatalog, "catalog_capture_failed");
                    else if (CurrentScan.Kind == ScanKind.AssetAudit)
                        FailAssetAudit("APA-CAT-001", CapabilityId.PrefabCatalog, "catalog_capture_failed");
                }
            }
        }

        private void ProcessCatalogCapture()
        {
            if (_catalog == null)
            {
                _catalogCaptureActive = false;
                return;
            }

            if (_catalog.IsWorking)
            {
                var before = _catalog.ProcessedEntityCount;
                var stopwatch = Stopwatch.StartNew();
                _catalog.ProcessNextSlice(CatalogSliceSize);
                stopwatch.Stop();
                RecordManagedSlice(stopwatch.Elapsed, Math.Max(0, _catalog.ProcessedEntityCount - before));
                if (CurrentScan?.Kind == ScanKind.Census && CurrentScan.Stage == ScanStage.CapturingCatalog && CurrentScan.State == ScanState.Running)
                    ReportExactProgress(CurrentScan, _catalog.ProcessedEntityCount, _catalog.CapturedEntityCount);
                return;
            }

            _catalogCaptureActive = false;
            if (CurrentScan?.State == ScanState.Running && CurrentScan.Kind == ScanKind.Census && CurrentScan.Stage == ScanStage.CapturingCatalog)
            {
                if (!_catalog.HasPendingPublication)
                {
                    LastDiagnosticCode = "APA-CAT-002";
                    DegradeCapability(CapabilityId.PrefabCatalog, "catalog_capture_not_staged");
                    FailCensusScan("APA-CAT-002", CapabilityId.PrefabCatalog, "catalog_capture_not_staged");
                    return;
                }
                CurrentScan.TransitionTo(ScanStage.ProcessingCatalog);
                return;
            }

            if (CurrentScan?.State == ScanState.Running && CurrentScan.Kind == ScanKind.AssetAudit && _assetAuditWaitingForCatalog)
            {
                _assetAuditWaitingForCatalog = false;
                if (_catalog.CompletedCaptureCount <= _completedCatalogCapturesBeforeCapture)
                {
                    FailAssetAudit("APA-CAT-004", CapabilityId.PrefabCatalog, "catalog_refresh_failed_before_asset_audit");
                    return;
                }
                BeginAssetAnalysis();
                return;
            }

            if (_catalog.CompletedCaptureCount <= _completedCatalogCapturesBeforeCapture)
            {
                LastDiagnosticCode = "APA-CAT-002";
                DegradeCapability(CapabilityId.PrefabCatalog, "catalog_capture_not_published");
            }
        }

        private void BeginAssetAnalysis()
        {
            var session = CurrentScan;
            if (session == null || session.Kind != ScanKind.AssetAudit || session.State != ScanState.Running || session.Stage != ScanStage.Preparing)
                return;
            if (CatalogGeneration <= 0)
            {
                FailAssetAudit("APA-AUD-001", CapabilityId.PrefabCatalog, "asset_audit_requires_published_catalog");
                return;
            }

            var previousGeneration = PublishedAnalysis?.AnalysisGeneration ?? 0;
            _nextAnalysisGeneration = Math.Max(_nextAnalysisGeneration + 1, previousGeneration + 1);
            _analysisCollector = new AssetAnalysisCollector(
                World,
                CatalogRecords,
                RuntimeEntityKeys,
                PublishedCensus,
                WorldGeneration,
                CatalogGeneration,
                _nextAnalysisGeneration,
                DateTimeOffset.UtcNow,
                _assetAuditEnableHeuristics);
            session.TransitionTo(ScanStage.ResolvingRenderGraph);
        }

        private void AdvanceAssetAudit()
        {
            var session = CurrentScan;
            var collector = _analysisCollector;
            if (session == null || collector == null || session.Kind != ScanKind.AssetAudit || session.State != ScanState.Running)
                return;

            try
            {
                switch (session.Stage)
                {
                    case ScanStage.ResolvingRenderGraph:
                    {
                        var before = collector.RenderProcessedCount;
                        var stopwatch = Stopwatch.StartNew();
                        var completed = collector.ProcessRenderGraphSlice(_assetAuditFrameBudgetMs);
                        stopwatch.Stop();
                        RecordManagedSlice(stopwatch.Elapsed, Math.Max(0, collector.RenderProcessedCount - before));
                        ReportExactProgress(session, collector.RenderProcessedCount, collector.RenderTargetCount);
                        if (completed)
                            session.TransitionTo(ScanStage.CollectingGeometry);
                        return;
                    }
                    case ScanStage.CollectingGeometry:
                    {
                        var before = collector.GeometryProcessedCount;
                        var stopwatch = Stopwatch.StartNew();
                        var completed = collector.ProcessGeometrySlice(_assetAuditFrameBudgetMs);
                        stopwatch.Stop();
                        RecordManagedSlice(stopwatch.Elapsed, Math.Max(0, collector.GeometryProcessedCount - before));
                        ReportExactProgress(session, collector.GeometryProcessedCount, collector.GeometryTargetCount);
                        if (completed)
                            session.TransitionTo(ScanStage.CollectingSurfaceTexture);
                        return;
                    }
                    case ScanStage.CollectingSurfaceTexture:
                    {
                        var before = collector.SurfaceTextureProcessedCount;
                        var stopwatch = Stopwatch.StartNew();
                        var completed = collector.ProcessSurfaceTextureSlice(_assetAuditFrameBudgetMs);
                        stopwatch.Stop();
                        RecordManagedSlice(stopwatch.Elapsed, Math.Max(0, collector.SurfaceTextureProcessedCount - before));
                        ReportExactProgress(session, collector.SurfaceTextureProcessedCount, collector.SurfaceTextureTargetCount);
                        if (completed)
                            session.TransitionTo(ScanStage.EvaluatingFindings);
                        return;
                    }
                    case ScanStage.EvaluatingFindings:
                    {
                        var before = collector.FindingsProcessedCount;
                        var stopwatch = Stopwatch.StartNew();
                        var completed = collector.ProcessFindingSlice(_assetAuditFrameBudgetMs);
                        stopwatch.Stop();
                        RecordManagedSlice(stopwatch.Elapsed, Math.Max(0, collector.FindingsProcessedCount - before));
                        ReportExactProgress(session, collector.FindingsProcessedCount, collector.FindingsTargetCount);
                        if (completed)
                            PublishAssetAnalysis(session, collector);
                        return;
                    }
                }
            }
            catch
            {
                var capability = session.Stage == ScanStage.CollectingSurfaceTexture
                    ? CapabilityId.SurfaceMetadata
                    : CapabilityId.GeometryMetadata;
                FailAssetAudit("APA-AUD-002", capability, "asset_analysis_stage_failed");
            }
        }

        private void AdvanceDeepInspection()
        {
            var session = CurrentScan;
            if (session == null || session.Kind != ScanKind.DeepInspection || session.State != ScanState.Running || session.Stage != ScanStage.DeepInspecting)
                return;
            var analysis = PublishedAnalysis;
            var graph = _publishedRuntimeRenderGraph;
            if (analysis == null || graph == null || analysis.WorldGeneration != WorldGeneration || analysis.CatalogGeneration != CatalogGeneration || !_activeDeepInspectionKey.IsValid)
            {
                FailDeepInspection("APA-DEEP-003", "deep_inspection_context_changed");
                return;
            }
            if (!graph.TryGetRuntimeAsset(_activeDeepInspectionKey, out var runtime) || !(runtime is RenderPrefab renderPrefab))
            {
                FailDeepInspection("APA-DEEP-003", "deep_inspection_runtime_render_asset_unavailable");
                return;
            }

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var observation = _deepInspectionReader.Read(_activeDeepInspectionKey, renderPrefab, DateTimeOffset.UtcNow);
                stopwatch.Stop();
                RecordManagedSlice(stopwatch.Elapsed, 1);
                _nextAnalysisGeneration = Math.Max(_nextAnalysisGeneration + 1, analysis.AnalysisGeneration + 1);
                var enriched = analysis.WithDeepInspection(_activeDeepInspectionKey, observation, _nextAnalysisGeneration);
                session.TransitionTo(ScanStage.Finalizing);
                session.ReportProgress(1, 1);
                if (!_publishedState.TryPublishAnalysis(enriched, scanSucceeded: session.CanPublish))
                {
                    FailDeepInspection("APA-DEEP-003", "deep_inspection_publish_world_mismatch");
                    return;
                }
                // Enrichment keeps the audit's identity and interval; only the enrichment time is new.
                if (_publishedIdentity != null)
                    _publishedIdentity = _publishedIdentity.WithEnrichment(observation.CapturedAt);
                session.Complete();
                LastDiagnosticCode = observation.Availability == Core.Observations.Availability.Failed ? observation.DiagnosticCode : null;
                _activeDeepInspectionKey = default;
            }
            catch
            {
                FailDeepInspection("APA-DEEP-002", "deep_inspection_failed");
            }
        }

        private void PublishAssetAnalysis(ScanSession session, AssetAnalysisCollector collector)
        {
            if (WorldGeneration != session.WorldGeneration || CatalogGeneration <= 0)
            {
                FailAssetAudit("APA-AUD-003", CapabilityId.GeometryMetadata, "world_or_catalog_changed_before_analysis_publish");
                return;
            }

            var snapshot = collector.BuildSnapshot();
            if (snapshot.WorldGeneration != WorldGeneration || snapshot.CatalogGeneration != CatalogGeneration)
            {
                FailAssetAudit("APA-AUD-003", CapabilityId.GeometryMetadata, "analysis_generation_context_changed_before_publish");
                return;
            }

            session.TransitionTo(ScanStage.Finalizing);
            session.ReportProgress(1, 1);
            if (!_publishedState.TryPublishAnalysis(snapshot, scanSucceeded: session.CanPublish))
            {
                FailAssetAudit("APA-AUD-003", CapabilityId.GeometryMetadata, "analysis_publish_world_mismatch");
                return;
            }

            StampPublishedAnalysis(session, snapshot, DateTimeOffset.UtcNow);

            _publishedRuntimeRenderGraph = collector.RenderGraph;
            session.Complete();
            LastDiagnosticCode = null;
            _analysisCollector = null;
        }

        // The interval spans the whole audit: from the request start (including any catalog refresh) to the
        // frame the analysis is published. snapshot.CapturedAt is the analysis start and must not be used here.
        private void StampPublishedAnalysis(ScanSession session, AssetAnalysisSnapshot snapshot, DateTimeOffset publishedAt)
        {
            var context = Mod.SessionContext;
            _publishedIdentity = context == null
                ? null
                : PublishedAssetSnapshotIdentity.ForAudit(context.SessionId, snapshot.AnalysisGeneration, session.StartedAt, publishedAt);
        }

        private void AdvanceCensusScan()
        {
            var session = CurrentScan;
            var census = _censusAccess;
            var catalog = _catalog;
            if (session == null || census == null || catalog == null || session.State != ScanState.Running)
                return;

            try
            {
                switch (session.Stage)
                {
                    case ScanStage.ProcessingCatalog:
                        if (!catalog.HasPendingPublication)
                        {
                            FailCensusScan("APA-CAT-003", CapabilityId.PrefabCatalog, "catalog_capture_not_pending");
                            return;
                        }
                        _censusReducer = new CensusReducer(catalog.PendingRecords, WorldGeneration, catalog.PendingCatalogGeneration, DateTimeOffset.UtcNow, _requestedOptions);
                        session.TransitionTo(ScanStage.CapturingObjectCensus);
                        census.BeginObjectCapture(catalog.PendingRuntimeEntityKeys, _censusReducer, _requestedOptions);
                        return;

                    case ScanStage.CapturingObjectCensus:
                        if (!census.ObjectCaptureJobsCompleted)
                            return;
                        census.CompleteObjectCapture();
                        session.TransitionTo(ScanStage.ReducingObjectCensus);
                        ReportExactProgress(session, 0, census.CapturedObjectReferenceCount);
                        return;

                    case ScanStage.ReducingObjectCensus:
                        var objectBefore = census.ProcessedObjectReferenceCount;
                        var objectStopwatch = Stopwatch.StartNew();
                        census.ReduceObjectSlice(CensusReductionSliceSize);
                        objectStopwatch.Stop();
                        RecordManagedSlice(objectStopwatch.Elapsed, Math.Max(0, census.ProcessedObjectReferenceCount - objectBefore));
                        ReportExactProgress(session, census.ProcessedObjectReferenceCount, census.CapturedObjectReferenceCount);
                        if (!census.ObjectReductionCompleted)
                            return;
                        census.ReleaseObjectCapture();
                        session.TransitionTo(ScanStage.CapturingNetworkCensus);
                        return;

                    case ScanStage.CapturingNetworkCensus:
                        if (!_networkCaptureStarted)
                        {
                            census.BeginNetworkCapture();
                            _networkCaptureStarted = true;
                            return;
                        }
                        if (!census.NetworkCaptureJobsCompleted)
                            return;
                        if (_requestedOptions.CollectNetworkEdges)
                            census.CompleteNetworkCapture();
                        session.TransitionTo(ScanStage.ReducingNetworkCensus);
                        ReportExactProgress(session, 0, census.CapturedNetworkEdgeCount);
                        return;

                    case ScanStage.ReducingNetworkCensus:
                        var networkBefore = census.ProcessedNetworkEdgeCount;
                        var networkStopwatch = Stopwatch.StartNew();
                        census.ReduceNetworkSlice(CensusReductionSliceSize);
                        networkStopwatch.Stop();
                        RecordManagedSlice(networkStopwatch.Elapsed, Math.Max(0, census.ProcessedNetworkEdgeCount - networkBefore));
                        ReportExactProgress(session, census.ProcessedNetworkEdgeCount, census.CapturedNetworkEdgeCount);
                        if (!census.NetworkReductionCompleted)
                            return;
                        census.ReleaseNetworkCapture();
                        PublishCensus(session, census);
                        return;
                }
            }
            catch
            {
                var failedCapability = session.Stage == ScanStage.CapturingNetworkCensus || session.Stage == ScanStage.ReducingNetworkCensus
                    ? CapabilityId.NetworkEdgeCensus
                    : CapabilityId.ObjectCensus;
                var diagnostic = failedCapability == CapabilityId.NetworkEdgeCensus ? "APA-CEN-002" : "APA-CEN-001";
                FailCensusScan(diagnostic, failedCapability, "census_capture_or_reduction_failed");
            }
        }

        private void PublishCensus(ScanSession session, CensusAccess census)
        {
            var catalog = _catalog;
            if (catalog == null || !catalog.HasPendingPublication || WorldGeneration != session.WorldGeneration || _publishedState.WorldGeneration != session.WorldGeneration)
            {
                FailCensusScan("APA-CEN-003", CapabilityId.ObjectCensus, "world_generation_changed_before_publish");
                return;
            }

            var snapshot = census.BuildSnapshot();
            if (snapshot.CatalogGeneration != catalog.PendingCatalogGeneration)
            {
                FailCensusScan("APA-CEN-003", CapabilityId.ObjectCensus, "catalog_generation_changed_before_publish");
                return;
            }

            session.TransitionTo(ScanStage.Finalizing);
            session.ReportProgress(1, 1);
            // Publish before committing the staged catalog: a rejected publish must fail the scan and discard
            // the staged capture, leaving the last good catalog and census untouched.
            if (!_publishedState.TryPublishCensus(snapshot, scanSucceeded: session.CanPublish))
            {
                FailCensusScan("APA-CEN-003", CapabilityId.ObjectCensus, "census_publish_world_mismatch");
                return;
            }
            catalog.CommitPendingCapture();

            session.Complete();
            LastDiagnosticCode = null;
            census.FinishScan();
            _censusReducer = null;
            _networkCaptureStarted = false;
        }

        private void BeginCensusCancellation()
        {
            if (_catalogCaptureActive || _catalog?.HasPendingPublication == true)
            {
                _catalog?.CancelCapture();
                _catalogCaptureActive = false;
            }
            _catalogScanRequested = false;
            _censusAccess?.RequestCancellation();
            _censusCleanupRequested = true;
            PollCensusCleanup();
        }

        private void CancelManagedScan()
        {
            if (CurrentScan == null || CurrentScan.State != ScanState.CancellationRequested)
                return;
            if (_catalogCaptureActive)
            {
                _catalog?.CancelCapture();
                _catalogCaptureActive = false;
            }
            _catalogScanRequested = false;
            _assetAuditWaitingForCatalog = false;
            _analysisCollector = null;
            _activeDeepInspectionKey = default;
            if (_interruptedByRuntimeCapture)
                CurrentScan.MarkInterruptedByRuntimeCapture();
            else
                CurrentScan.MarkCancelled();
            _interruptedByRuntimeCapture = false;
        }

        private void FailCensusScan(string diagnosticCode, CapabilityId capability, string capabilityDetail)
        {
            LastDiagnosticCode = diagnosticCode;
            DegradeCapability(capability, capabilityDetail);
            _catalogScanRequested = false;
            if (_catalogCaptureActive || _catalog?.HasPendingPublication == true)
            {
                _catalog?.CancelCapture();
                _catalogCaptureActive = false;
            }
            if (CurrentScan?.State == ScanState.Running || CurrentScan?.State == ScanState.CancellationRequested)
                CurrentScan.Fail(diagnosticCode);
            _censusAccess?.RequestCancellation();
            _censusCleanupRequested = true;
            PollCensusCleanup();
        }

        private void FailAssetAudit(string diagnosticCode, CapabilityId capability, string capabilityDetail)
        {
            LastDiagnosticCode = diagnosticCode;
            DegradeCapability(capability, capabilityDetail);
            _catalogScanRequested = false;
            _assetAuditWaitingForCatalog = false;
            if (_catalogCaptureActive)
            {
                _catalog?.CancelCapture();
                _catalogCaptureActive = false;
            }
            if (CurrentScan?.State == ScanState.Running || CurrentScan?.State == ScanState.CancellationRequested)
                CurrentScan.Fail(diagnosticCode);
            _analysisCollector = null;
        }

        private void FailDeepInspection(string diagnosticCode, string capabilityDetail)
        {
            LastDiagnosticCode = diagnosticCode;
            DegradeCapability(CapabilityId.ShaderDeepInspection, capabilityDetail);
            if (CurrentScan?.State == ScanState.Running || CurrentScan?.State == ScanState.CancellationRequested)
                CurrentScan.Fail(diagnosticCode);
            _activeDeepInspectionKey = default;
        }

        private void PollCensusCleanup()
        {
            if (_censusAccess == null)
            {
                _censusCleanupRequested = false;
                return;
            }
            if (_censusAccess.CleanupPending)
                return;
            _censusAccess.CompleteCleanup();
            _censusCleanupRequested = false;
            _censusReducer = null;
            _networkCaptureStarted = false;
            if (CurrentScan?.State == ScanState.CancellationRequested)
            {
                if (_interruptedByRuntimeCapture)
                    CurrentScan.MarkInterruptedByRuntimeCapture();
                else
                    CurrentScan.MarkCancelled();
                _interruptedByRuntimeCapture = false;
            }
        }

        private void StartTelemetry(DateTimeOffset startedAt)
        {
            _scanTelemetry = new ScanTelemetry(startedAt);
            _finalTelemetry = null;
        }

        private void FreezeTelemetryAfterScanEnds()
        {
            if (_scanTelemetry != null && _finalTelemetry == null && CurrentScan != null && !IsScanActive)
                _finalTelemetry = _scanTelemetry.Snapshot(DateTimeOffset.UtcNow);
        }

        private void RecordManagedSlice(TimeSpan elapsed, long processedItems)
        {
            _scanTelemetry?.RecordManagedSlice(elapsed, processedItems);
        }

        private static void ReportExactProgress(ScanSession session, long completed, long total)
        {
            if (total > 0)
                session.ReportProgress(Math.Min(completed, total), total);
            else
                session.ReportProgress(null, null);
        }

        private void DegradeCapability(CapabilityId id, string detail)
        {
            if (Capabilities == null)
                return;
            var statuses = new List<CapabilityStatus>();
            var found = false;
            foreach (var status in Capabilities.Capabilities)
            {
                if (status.Id == id)
                {
                    statuses.Add(new CapabilityStatus(id, CapabilityState.Degraded, detail));
                    found = true;
                }
                else
                {
                    statuses.Add(status);
                }
            }
            if (!found)
                statuses.Add(new CapabilityStatus(id, CapabilityState.Degraded, detail));
            Capabilities = new CapabilityReport(Capabilities.GameVersion, Capabilities.Compatibility, statuses);
        }

        private static double NormalizeFrameBudget(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 1.0;
            return Math.Min(8.0, Math.Max(0.25, value));
        }
    }
}
