using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Colossal.UI.Binding;
using CS2RuntimeAssetAuditor.Advisor;
using CS2RuntimeAssetAuditor.Advisor.Settings;
using CS2RuntimeAssetAuditor.Core.Advisor;
using CS2RuntimeAssetAuditor.Core.Advisor.Experiment;
using CS2RuntimeAssetAuditor.Collectors;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.Lifecycle;
using CS2RuntimeAssetAuditor.Profiling;
using CS2RuntimeAssetAuditor.Assets.Export;
using CS2RuntimeAssetAuditor.Assets.UI;
using CS2RuntimeAssetAuditor.Coordination;
using Game;
using Game.Input;
using Game.UI;

namespace CS2RuntimeAssetAuditor.UI
{
    public partial class ProfilerUISystem : UISystemBase
    {
        private const string Group = Mod.Id;
        private const double DefaultUiRefreshPeriodSeconds = 0.5d;

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private GlobalMetricsCollector _global;
        private DomainMetricsSystem _domains;
        private CaptureRuntimeSystem _capture;
        private AdvisorSystem _advisor;
        private ReportExporter _exporter;
        private UiSnapshot _snapshot = new UiSnapshot();
        private RawValueBinding _snapshotBinding;
        private RawValueBinding _hudSnapshotBinding;
        private ValueBinding<bool> _panelVisibleBinding;
        private ValueBinding<int> _uiScalePercentBinding;
        private ValueBinding<string> _localeBinding;
        private ValueBinding<string> _selectedCaptureBinding;
        private ValueBinding<string> _exportResultBinding;
        private RawValueBinding _panelLayoutBinding;
        private ProxyAction? _togglePanelAction;
        private double _nextRefreshAt;
        private bool _panelVisible;
        private string _selectedCaptureId = string.Empty;

        public override GameMode gameMode => GameMode.Game;

        public bool IsPanelVisible => _panelVisible;

        protected override void OnCreate()
        {
            base.OnCreate();

            _global = World.GetOrCreateSystemManaged<GlobalMetricsCollector>();
            _domains = World.GetOrCreateSystemManaged<DomainMetricsSystem>();
            _capture = World.GetOrCreateSystemManaged<CaptureRuntimeSystem>();
            try { _advisor = World.GetOrCreateSystemManaged<AdvisorSystem>(); }
            catch (Exception) { _advisor = null; }
            _exporter = new ReportExporter();

            AddBinding(_snapshotBinding = new RawValueBinding(Group, "snapshot", WriteSnapshot));
            AddBinding(_hudSnapshotBinding = new RawValueBinding(Group, "hudSnapshot", WriteHudSnapshot));
            AddBinding(_panelVisibleBinding = new ValueBinding<bool>(Group, "panelVisible", false));
            AddBinding(_uiScalePercentBinding = new ValueBinding<int>(Group, "uiScalePercent", GetUiScalePercent()));
            // The panel follows the game's interface language; the UI resolves Japanese locales to ja and all others to en.
            AddBinding(_localeBinding = new ValueBinding<string>(Group, "locale", ActiveLocaleReader.Read()));
            AddBinding(_selectedCaptureBinding = new ValueBinding<string>(Group, "selectedCaptureId", string.Empty));
            AddBinding(_exportResultBinding = new ValueBinding<string>(Group, "exportResult", string.Empty));

            AddBinding(_panelLayoutBinding = new RawValueBinding(Group, "panelLayout", WritePanelLayout));

            AddBinding(new TriggerBinding(Group, "togglePanel", TogglePanel));
            AddBinding(new TriggerBinding<bool>(Group, "setPanelVisible", SetPanelVisible));
            AddBinding(new TriggerBinding<int, int, int, int>(Group, "setPanelLayout", SetPanelLayout));
            AddBinding(new TriggerBinding(Group, "resetPanelLayout", () => SetPanelLayout(0, 0, 0, 0)));
            AddBinding(new TriggerBinding(Group, "manualCapture", ManualCapture));
            AddBinding(new TriggerBinding<string>(Group, "selectCapture", SelectCapture));
            AddBinding(new TriggerBinding<string>(Group, "diagnoseAdvisor", DiagnoseAdvisor));
            AddBinding(new TriggerBinding<string>(Group, "advisorRediagnose", RediagnoseAdvisor));
            AddBinding(new TriggerBinding<string>(Group, "selectAdvisorBaseline", SelectAdvisorBaseline));
            AddBinding(new TriggerBinding<string, string, bool>(Group, "advisorApply", AdvisorApply));
            AddBinding(new TriggerBinding<string, bool>(Group, "advisorUndo", AdvisorUndo));
            AddBinding(new TriggerBinding<bool>(Group, "advisorUndoSession", AdvisorUndoSession));
            AddBinding(new TriggerBinding<string, bool>(Group, "advisorResolveConflict", AdvisorResolveConflict));
            AddBinding(new TriggerBinding<string, string, string>(Group, "advisorStartExperiment", AdvisorStartExperiment));
            AddBinding(new TriggerBinding<bool>(Group, "advisorApplyExperiment", AdvisorApplyExperiment));
            AddBinding(new TriggerBinding(Group, "advisorStartExperimentFollowUp", AdvisorStartExperimentFollowUp));
            AddBinding(new TriggerBinding(Group, "advisorCancelExperiment", AdvisorCancelExperiment));
            AddBinding(new TriggerBinding(Group, "advisorKeepExperiment", AdvisorKeepExperiment));
            AddBinding(new TriggerBinding<bool>(Group, "advisorUndoExperiment", AdvisorUndoExperiment));
            AddBinding(new TriggerBinding(Group, "exportReport", ExportReport));

            _togglePanelAction = EnableTogglePanelAction();
            RefreshSnapshot();
        }

        protected override void OnDestroy()
        {
            if (_togglePanelAction != null)
            {
                try { _togglePanelAction.shouldBeEnabled = false; }
                catch (Exception ex) { Mod.ReportFailure("Disabling the panel key binding failed.", ex); }
                _togglePanelAction = null;
            }

            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            var start = ModUpdateCost.Start();
            try { RunUpdate(); }
            finally { ModUpdateCost.Stop(nameof(ProfilerUISystem), start); }
        }

        private void RunUpdate()
        {
            base.OnUpdate();

            // Polled every frame, ahead of the refresh throttle, so a key press is never missed. The game masks
            // keyboard actions while a text field has focus, so typing in the panel's search box does not toggle it.
            if (_togglePanelAction != null && _togglePanelAction.WasPerformedThisFrame())
                TogglePanel();

            var now = _clock.Elapsed.TotalSeconds;
            if (now < _nextRefreshAt)
                return;

            _nextRefreshAt = now + GetUiRefreshPeriodSeconds();
            _uiScalePercentBinding.Update(GetUiScalePercent());
            _localeBinding.Update(ActiveLocaleReader.Read());
            _hudSnapshotBinding.Update();

            if (!_panelVisible)
                return;

            RefreshSnapshot();
            _snapshotBinding.Update();
        }

        private void TogglePanel() => SetPanelVisible(!_panelVisible);

        // The action exists only when the settings registered their key bindings; an unassigned binding never performs.
        private static ProxyAction? EnableTogglePanelAction()
        {
            var settings = Mod.Settings;
            if (settings == null || !settings.keyBindingRegistered)
                return null;

            try
            {
                var action = settings.GetAction(Setting.TogglePanelActionName);
                if (action != null)
                    action.shouldBeEnabled = true;
                return action;
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Enabling the panel key binding failed; the launcher still opens the panel.", ex);
                return null;
            }
        }

        // Idempotent so that a close request (for example the game's Back/Escape input action)
        // can never reopen the panel if it is delivered twice.
        private void SetPanelVisible(bool visible)
        {
            if (_panelVisible == visible)
                return;

            _panelVisible = visible;
            _panelVisibleBinding.Update(_panelVisible);
            _uiScalePercentBinding.Update(GetUiScalePercent());
            _hudSnapshotBinding.Update();

            if (_panelVisible)
            {
                RefreshSnapshot();
                _snapshotBinding.Update();
            }
        }

        private void SetPanelLayout(int left, int top, int width, int height)
        {
            var settings = Mod.Settings;
            if (settings == null)
                return;

            var reset = width <= 0 || height <= 0;
            // The UI clamps against the current viewport (the left edge may sit partly off screen while the
            // header stays grabbable); store the geometry as given so the panel does not jump after a drop.
            settings.PanelLeft = reset ? 0 : left;
            settings.PanelTop = reset ? 0 : top;
            settings.PanelWidth = reset ? 0 : width;
            settings.PanelHeight = reset ? 0 : height;
            try
            {
                settings.ApplyAndSave();
            }
            catch (Exception ex)
            {
                Mod.Log.Warn($"Saving profiler panel layout failed: {ReportPrivacy.Sanitize(ex.Message)}");
            }

            _panelLayoutBinding.Update();
        }

        private void WritePanelLayout(IJsonWriter writer)
        {
            var settings = Mod.Settings;
            var hasLayout = settings != null && settings.PanelWidth > 0 && settings.PanelHeight > 0;
            writer.TypeBegin("CS2RuntimeAssetAuditor.PanelLayout");
            writer.PropertyName("custom"); writer.Write(hasLayout);
            writer.PropertyName("left"); writer.Write(hasLayout ? settings.PanelLeft : 0);
            writer.PropertyName("top"); writer.Write(hasLayout ? settings.PanelTop : 0);
            writer.PropertyName("width"); writer.Write(hasLayout ? settings.PanelWidth : 0);
            writer.PropertyName("height"); writer.Write(hasLayout ? settings.PanelHeight : 0);
            writer.TypeEnd();
        }

        private void ManualCapture()
        {
            _capture?.RequestManualCapture();
            _hudSnapshotBinding.Update();

            if (_panelVisible)
            {
                RefreshSnapshot();
                _snapshotBinding.Update();
            }
        }

        private void DiagnoseAdvisor(string id)
        {
            _advisor?.DiagnoseCompletedCapture(id);
            if (_panelVisible) { RefreshSnapshot(); _snapshotBinding.Update(); }
        }

        private void RediagnoseAdvisor(string id)
        {
            _advisor?.Rediagnose(id);
            if (_panelVisible) { RefreshSnapshot(); _snapshotBinding.Update(); }
        }

        private void SelectAdvisorBaseline(string id)
        {
            _advisor?.SelectBaseline(id);
            if (_panelVisible) { RefreshSnapshot(); _snapshotBinding.Update(); }
        }

        private void AdvisorApply(string id, string proposed, bool confirmed)
        {
            _advisor?.ApplySetting(id, proposed, confirmed);
            if (_panelVisible) { RefreshSnapshot(); _snapshotBinding.Update(); }
        }

        private void AdvisorUndo(string id, bool confirmed)
        {
            _advisor?.UndoSetting(id, confirmed);
            if (_panelVisible) { RefreshSnapshot(); _snapshotBinding.Update(); }
        }

        private void AdvisorUndoSession(bool confirmed)
        {
            _advisor?.UndoSession(confirmed);
            if (_panelVisible) { RefreshSnapshot(); _snapshotBinding.Update(); }
        }

        private void AdvisorResolveConflict(string id, bool restoreOriginal)
        {
            _advisor?.ResolveConflict(id, restoreOriginal);
            if (_panelVisible) { RefreshSnapshot(); _snapshotBinding.Update(); }
        }

        private void AdvisorStartExperiment(string captureId, string settingId, string proposedValue)
        {
            _advisor?.StartExperiment(captureId, settingId, proposedValue);
            if (_panelVisible) { RefreshSnapshot(); _snapshotBinding.Update(); }
        }

        private void AdvisorApplyExperiment(bool confirmed)
        {
            _advisor?.ApplyExperimentChange(confirmed);
            if (_panelVisible) { RefreshSnapshot(); _snapshotBinding.Update(); }
        }

        private void AdvisorStartExperimentFollowUp()
        {
            _advisor?.StartExperimentFollowUpCapture();
            if (_panelVisible) { RefreshSnapshot(); _snapshotBinding.Update(); }
        }

        private void AdvisorCancelExperiment()
        {
            _advisor?.CancelExperiment();
            if (_panelVisible) { RefreshSnapshot(); _snapshotBinding.Update(); }
        }

        private void AdvisorKeepExperiment()
        {
            _advisor?.KeepExperimentChange();
            if (_panelVisible) { RefreshSnapshot(); _snapshotBinding.Update(); }
        }

        private void AdvisorUndoExperiment(bool confirmed)
        {
            _advisor?.UndoExperimentChange(confirmed);
            if (_panelVisible) { RefreshSnapshot(); _snapshotBinding.Update(); }
        }

        private void SelectCapture(string id)
        {
            _selectedCaptureId = id ?? string.Empty;
            _selectedCaptureBinding.Update(_selectedCaptureId);

            if (_panelVisible)
            {
                RefreshSnapshot();
                _snapshotBinding.Update();
            }
        }

        private void ExportReport()
        {
            try
            {
                var start = Stopwatch.GetTimestamp();
                var report = BuildCurrentUnifiedReport();
                var result = _exporter.Export(report, (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency);
                _exportResultBinding.Update(result.Success
                    ? ExportResultFormat.Succeeded(Path.GetFileName(result.Path))
                    : ExportResultFormat.Failed(null, result.Error));
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("UI-triggered profiler report export failed", ex);
                _exportResultBinding.Update(ExportResultFormat.Failed(null, ReportPrivacy.Sanitize(ex.Message)));
            }
        }

        public RuntimeAssetAuditReport BuildCurrentUnifiedReport(AuditReport assetSection = null)
        {
            var exportSnapshot = UiSnapshotBuilder.BuildForExport(CreateSnapshotInput());
            var runtime = ProfilerReportBuilder.Build(exportSnapshot,
                gameVersion: exportSnapshot.Diagnostics?.GameVersion,
                profilerVersion: exportSnapshot.Diagnostics?.ProfilerVersion);
            var assets = assetSection ?? World.GetExistingSystemManaged<AssetAuditUISystem>()?.BuildCurrentReport();
            var links = new List<DiagnosticEvidenceLink>();
            if (assets?.AssetSnapshotId != null
                && DateTimeOffset.TryParse(assets.StartedAtUtc, out var assetStarted)
                && DateTimeOffset.TryParse(assets.CompletedAtUtc, out var assetCompleted))
            {
                foreach (var capture in runtime.Captures)
                {
                    if (!DateTimeOffset.TryParse(capture.StartedAtUtc, out var started)
                        || !DateTimeOffset.TryParse(capture.CompletedAtUtc, out var completed)) continue;
                    var link = DiagnosticEvidenceBridge.TryLink(capture.SessionId, capture.Id, started, completed,
                        assets.SessionId, assets.AssetSnapshotId, assetStarted, assetCompleted);
                    if (link != null) links.Add(link);
                }
            }
            return RuntimeAssetAuditReportBuilder.Build(runtime, assets, Mod.SessionContext, DateTimeOffset.UtcNow, links,
                ReportLoadingTrace.FromSnapshot(Mod.LoadingTrace.Snapshot()));
        }

        private void RefreshSnapshot()
        {
            _snapshot = UiSnapshotBuilder.Build(CreateSnapshotInput());
        }

        private static int GetUiScalePercent()
        {
            return Mod.Settings?.ResolvedUiScalePercent ?? 100;
        }

        private static double GetUiRefreshPeriodSeconds()
        {
            return Mod.Settings?.ResolvedUiRefreshPeriodSeconds ?? DefaultUiRefreshPeriodSeconds;
        }

        private UiSnapshotInput CreateSnapshotInput()
        {
            var captures = new List<CaptureSession>();
            if (_capture?.CompletedSessions != null)
                captures.AddRange(_capture.CompletedSessions.Where(capture => capture != null));

            if (!string.IsNullOrWhiteSpace(_selectedCaptureId)
                && !captures.Any(capture => string.Equals(capture.Id, _selectedCaptureId, StringComparison.Ordinal)))
            {
                _selectedCaptureId = string.Empty;
                _selectedCaptureBinding.Update(string.Empty);
            }

            var currentCapture = _capture?.CurrentSession;
            CaptureSession detailCapture = null;
            if (!string.IsNullOrWhiteSpace(_selectedCaptureId))
            {
                detailCapture = captures.FirstOrDefault(capture =>
                    string.Equals(capture.Id, _selectedCaptureId, StringComparison.Ordinal));
            }
            detailCapture = detailCapture ?? currentCapture ?? captures.LastOrDefault();

            var timing = detailCapture?.SystemTiming;
            var diagnostics = new List<string>();
            if (timing == null)
                diagnostics.Add(UiDiagnosticText.SystemTimingPending);
            diagnostics.Add(UiDiagnosticText.TimelineRetainedOnly);

            var markerCapture = detailCapture ?? currentCapture ?? captures.LastOrDefault();
            var patchMapState = timing == null
                ? UiDiagnosticText.PatchMapUnavailable
                : timing.Systems.Any(system => system.PatchOwners != null && system.PatchOwners.Count > 0)
                    ? UiDiagnosticText.PatchMapDetected
                    : UiDiagnosticText.PatchMapNone;

            return new UiSnapshotInput(
                _global?.Latest,
                _capture?.State ?? CaptureState.Monitoring,
                _domains?.Pathfinding?.Latest,
                _domains?.Entities?.Latest,
                timing,
                captures,
                _capture?.LastOverheadShare ?? 0d,
                diagnostics)
            {
                CurrentCapture = currentCapture,
                SelectedCaptureId = _selectedCaptureId,
                GameVersion = GetGameVersion(),
                ProfilerVersion = typeof(Mod).Assembly.GetName().Version?.ToString() ?? string.Empty,
                DiscoveredMarkerCount = _capture?.DiscoveredMarkerCount ?? 0,
                CapturedMarkerCount = markerCapture?.MarkerCoverage.Captured ?? 0,
                MarkerBatchSize = _capture?.CurrentBatchSize ?? 0,
                SamplingStride = _capture?.SamplingStride ?? 1,
                PatchMapState = patchMapState,
                Advisor = _advisor?.CurrentState
            };
        }

        private void WriteHudSnapshot(IJsonWriter writer)
        {
            var latest = _global?.Latest;
            var state = _capture?.State ?? CaptureState.Monitoring;

            writer.TypeBegin("CS2RuntimeAssetAuditor.UiHudSnapshot");
            writer.PropertyName("selectedSpeed");
            if (latest == null) writer.WriteNull(); else writer.Write(latest.SelectedSpeed);
            writer.PropertyName("actualSpeed");
            if (latest == null) writer.WriteNull(); else writer.Write(latest.ActualSpeed);
            writer.PropertyName("state"); writer.Write(state.ToString());
            writer.PropertyName("isDeepCapture"); writer.Write(state == CaptureState.DeepCapture);
            writer.TypeEnd();
        }

        private static string GetGameVersion()
        {
            try
            {
                return Game.Version.current.fullVersion ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void WriteSnapshot(IJsonWriter writer)
        {
            var snapshot = _snapshot ?? new UiSnapshot();
            writer.TypeBegin("CS2RuntimeAssetAuditor.UiSnapshot");

            writer.PropertyName("global");
            WriteGlobal(writer, snapshot.Global);
            writer.PropertyName("capture");
            WriteCaptureState(writer, snapshot.Capture);
            writer.PropertyName("systems");
            WriteSystems(writer, snapshot.Systems);
            writer.PropertyName("mods");
            WriteMods(writer, snapshot.Mods);
            writer.PropertyName("pathfinding");
            WriteMetricsObject(writer, snapshot.Pathfinding?.Metrics);
            writer.PropertyName("domainMetrics");
            WriteMetricsArray(writer, snapshot.DomainMetrics);
            writer.PropertyName("timeline");
            WriteTimeline(writer, snapshot.Timeline);
            writer.PropertyName("captures");
            WriteCaptures(writer, snapshot.Captures);
            writer.PropertyName("diagnostics");
            WriteDiagnostics(writer, snapshot.Diagnostics);
            writer.PropertyName("advisor");
            WriteAdvisor(writer, snapshot.Advisor);

            writer.TypeEnd();
        }

        private static void WriteAdvisor(IJsonWriter writer, AdvisorState state)
        {
            state = state ?? new AdvisorState(null, null, null, "Advisor unavailable.");
            writer.TypeBegin("CS2RuntimeAssetAuditor.AdvisorUiState");
            writer.PropertyName("available"); writer.Write(state.IsAvailable);
            writer.PropertyName("unavailableReason"); writer.Write(state.UnavailableReason ?? string.Empty);
            writer.PropertyName("selectedCaptureId"); writer.Write(state.SelectedCaptureId ?? string.Empty);
            writer.PropertyName("baselineCaptureId"); writer.Write(state.BaselineCaptureId ?? string.Empty);
            writer.PropertyName("catalog");
            writer.ArrayBegin((uint)state.Catalog.Count);
            foreach (var item in state.Catalog)
            {
                writer.TypeBegin("CS2RuntimeAssetAuditor.AdvisorSetting");
                writer.PropertyName("settingId"); writer.Write(item.SettingId);
                writer.PropertyName("category"); writer.Write(item.Category);
                writer.PropertyName("displayName"); writer.Write(item.DisplayName);
                writer.PropertyName("currentValue"); writer.Write(item.CurrentValue);
                writer.PropertyName("isUserFacing"); writer.Write(item.IsUserFacing);
                writer.PropertyName("isReadable"); writer.Write(item.IsReadable);
                writer.PropertyName("isWritable"); writer.Write(item.IsWritable);
                writer.PropertyName("applyBehavior"); writer.Write(item.ApplyBehavior.ToString());
                writer.TypeEnd();
            }
            writer.ArrayEnd();
            writer.PropertyName("observations");
            writer.ArrayBegin((uint)state.Observations.Count);
            foreach (var item in state.Observations)
            {
                writer.TypeBegin("CS2RuntimeAssetAuditor.AdvisorObservation");
                writer.PropertyName("category"); writer.Write(item.Category.ToString());
                writer.PropertyName("severity"); writer.Write(item.Severity.ToString());
                writer.PropertyName("confidence"); writer.Write(item.Confidence.ToString());
                writer.PropertyName("rationale"); writer.Write(item.Rationale);
                writer.PropertyName("evidenceIds"); WriteStrings(writer, item.EvidenceIds);
                writer.TypeEnd();
            }
            writer.ArrayEnd();
            writer.PropertyName("recommendations");
            writer.ArrayBegin((uint)state.Recommendations.Count);
            foreach (var item in state.Recommendations)
            {
                writer.TypeBegin("CS2RuntimeAssetAuditor.AdvisorRecommendation");
                writer.PropertyName("settingId"); writer.Write(item.SettingId);
                writer.PropertyName("displayName"); writer.Write(item.DisplayName);
                writer.PropertyName("currentValue"); writer.Write(item.CurrentValue);
                writer.PropertyName("recommendedValue"); writer.Write(item.RecommendedValue);
                writer.PropertyName("direction"); writer.Write(item.Direction.ToString());
                writer.PropertyName("priority"); writer.Write(item.Priority.ToString());
                writer.PropertyName("confidence"); writer.Write(item.Confidence.ToString());
                writer.PropertyName("rationale"); writer.Write(item.Rationale);
                writer.PropertyName("evidenceIds"); WriteStrings(writer, item.EvidenceIds);
                writer.PropertyName("applyCapability"); writer.Write(item.ApplyCapability.ToString());
                writer.PropertyName("applyBehavior"); writer.Write(item.ApplyBehavior.ToString());
                writer.TypeEnd();
            }
            writer.ArrayEnd();
            writer.PropertyName("changes");
            writer.ArrayBegin((uint)state.Changes.Count);
            foreach (var item in state.Changes)
            {
                writer.TypeBegin("CS2RuntimeAssetAuditor.AdvisorChange");
                writer.PropertyName("settingId"); writer.Write(item.SettingId);
                writer.PropertyName("displayName"); writer.Write(item.DisplayName ?? item.SettingId);
                writer.PropertyName("originalValue"); writer.Write(item.OriginalValue ?? string.Empty);
                writer.PropertyName("appliedValue"); writer.Write(item.AppliedValue ?? string.Empty);
                writer.PropertyName("currentObservedValue"); writer.Write(item.CurrentObservedValue ?? string.Empty);
                writer.PropertyName("status"); writer.Write(item.Status.ToString());
                writer.TypeEnd();
            }
            writer.ArrayEnd();
            writer.PropertyName("lastAction");
            WriteAdvisorAction(writer, state.LastAction);
            writer.PropertyName("comparison");
            WriteAdvisorComparison(writer, state.Comparison);
            writer.PropertyName("experiment");
            WriteAdvisorExperiment(writer, state.Experiment);
            writer.TypeEnd();
        }

        private static void WriteAdvisorExperiment(IJsonWriter writer, InvestigationExperiment experiment)
        {
            if (experiment == null)
            {
                writer.WriteNull();
                return;
            }
            writer.TypeBegin("CS2RuntimeAssetAuditor.AdvisorExperiment");
            writer.PropertyName("experimentId"); writer.Write(experiment.ExperimentId);
            writer.PropertyName("state"); writer.Write(experiment.State.ToString());
            writer.PropertyName("validity"); writer.Write(experiment.Validity.ToString());
            writer.PropertyName("invalidationReason"); writer.Write(experiment.InvalidationReason.ToString());
            writer.PropertyName("baselineCaptureId"); writer.Write(experiment.BaselineCaptureId);
            writer.PropertyName("followUpCaptureId"); writer.Write(experiment.FollowUpCaptureId ?? string.Empty);
            writer.PropertyName("settingId"); writer.Write(experiment.SettingId);
            writer.PropertyName("settingDisplayName"); writer.Write(experiment.SettingDisplayName);
            writer.PropertyName("originalValue"); writer.Write(experiment.OriginalValue);
            writer.PropertyName("testedValue"); writer.Write(experiment.TestedValue);
            writer.PropertyName("changeAppliedAtUtc");
            if (experiment.ChangeAppliedAtUtc.HasValue) writer.Write(experiment.ChangeAppliedAtUtc.Value.ToString("O"));
            else writer.WriteNull();
            writer.PropertyName("stabilizationReadyAtUtc");
            if (experiment.StabilizationReadyAtUtc.HasValue) writer.Write(experiment.StabilizationReadyAtUtc.Value.ToString("O"));
            else writer.WriteNull();
            writer.PropertyName("completionOutcome"); writer.Write(experiment.CompletionOutcome.ToString());
            writer.PropertyName("lastFailureReason"); writer.Write(experiment.LastFailureReason ?? string.Empty);
            writer.PropertyName("followUpWarnings"); WriteStrings(writer, experiment.FollowUpWarnings);
            writer.PropertyName("followUpInterruption"); writer.Write(experiment.FollowUpInterruption?.ToString() ?? string.Empty);
            writer.PropertyName("comparison"); WriteAdvisorComparison(writer, experiment.Comparison);
            writer.TypeEnd();
        }

        private static void WriteAdvisorComparison(IJsonWriter writer, AdvisorComparison comparison)
        {
            if (comparison == null) { writer.WriteNull(); return; }
            writer.TypeBegin("CS2RuntimeAssetAuditor.AdvisorComparison");
            writer.PropertyName("multipleChanges"); writer.Write(comparison.MultipleChanges);
            writer.PropertyName("changedSettingIds"); WriteStrings(writer, comparison.ChangedSettingIds);
            writer.PropertyName("metrics");
            writer.ArrayBegin((uint)comparison.Metrics.Count);
            foreach (var metric in comparison.Metrics)
            {
                writer.TypeBegin("CS2RuntimeAssetAuditor.AdvisorMetricComparison");
                writer.PropertyName("id"); writer.Write(metric.Id);
                writer.PropertyName("baselineValue"); WriteNullable(writer, metric.BaselineValue);
                writer.PropertyName("followUpValue"); WriteNullable(writer, metric.FollowUpValue);
                writer.PropertyName("state"); writer.Write(metric.State.ToString());
                writer.PropertyName("reason"); writer.Write(metric.Reason ?? string.Empty);
                writer.TypeEnd();
            }
            writer.ArrayEnd();
            writer.TypeEnd();
        }

        private static void WriteAdvisorAction(IJsonWriter writer, AdvisorActionResult action)
        {
            if (action == null)
            {
                writer.WriteNull();
                return;
            }
            writer.TypeBegin("CS2RuntimeAssetAuditor.AdvisorAction");
            writer.PropertyName("kind"); writer.Write(action.Kind.ToString());
            writer.PropertyName("settingId"); writer.Write(action.SettingId ?? string.Empty);
            writer.PropertyName("displayName"); writer.Write(action.DisplayName ?? string.Empty);
            writer.PropertyName("succeeded"); writer.Write(action.Succeeded);
            writer.PropertyName("failureReason"); writer.Write(action.FailureReason ?? string.Empty);
            writer.PropertyName("succeededCount"); writer.Write(action.SucceededCount);
            writer.PropertyName("failedSettingIds"); WriteStrings(writer, action.FailedSettingIds);
            writer.PropertyName("confirmationRequiredSettingIds"); WriteStrings(writer, action.ConfirmationRequiredSettingIds);
            writer.PropertyName("atUtc"); writer.Write(action.AtUtc.ToString("O"));
            writer.TypeEnd();
        }

        private static void WriteGlobal(IJsonWriter writer, GlobalUiMetrics global)
        {
            global = global ?? new GlobalUiMetrics();
            writer.TypeBegin("CS2RuntimeAssetAuditor.GlobalUiMetrics");
            writer.PropertyName("available"); writer.Write(global.Available);
            writer.PropertyName("timestampSeconds"); writer.Write(global.TimestampSeconds);
            writer.PropertyName("selectedSpeed"); WriteNullable(writer, global.SelectedSpeed);
            writer.PropertyName("actualSpeed"); WriteNullable(writer, global.ActualSpeed);
            writer.PropertyName("efficiency"); WriteNullable(writer, global.Efficiency);
            writer.PropertyName("recorderMetrics"); WriteMetricsArray(writer, global.RecorderMetrics);
            writer.TypeEnd();
        }

        private static void WriteCaptureState(IJsonWriter writer, CaptureUiState capture)
        {
            capture = capture ?? new CaptureUiState();
            writer.TypeBegin("CS2RuntimeAssetAuditor.CaptureUiState");
            writer.PropertyName("state"); writer.Write(capture.State ?? string.Empty);
            writer.PropertyName("isDeepCapture"); writer.Write(capture.IsDeepCapture);
            writer.PropertyName("completedCount"); writer.Write(capture.CompletedCount);
            writer.PropertyName("detailCaptureId"); writer.Write(capture.DetailCaptureId ?? string.Empty);
            writer.PropertyName("detailScope"); writer.Write(capture.DetailScope ?? string.Empty);
            writer.TypeEnd();
        }

        private static void WriteSystems(IJsonWriter writer, IReadOnlyList<SystemUiRow> systems)
        {
            systems = systems ?? Array.Empty<SystemUiRow>();
            writer.ArrayBegin((uint)systems.Count);
            foreach (var row in systems)
            {
                var item = row ?? new SystemUiRow();
                writer.TypeBegin("CS2RuntimeAssetAuditor.SystemUiRow");
                writer.PropertyName("id"); writer.Write(item.Id ?? string.Empty);
                writer.PropertyName("ownerAssembly"); writer.Write(item.OwnerAssembly ?? string.Empty);
                writer.PropertyName("sourceKind"); writer.Write(item.SourceKind ?? string.Empty);
                writer.PropertyName("isAggregateContainer"); writer.Write(item.IsAggregateContainer);
                writer.PropertyName("currentMilliseconds"); writer.Write(item.CurrentMilliseconds);
                writer.PropertyName("meanMilliseconds"); WriteNullable(writer, item.MeanMilliseconds);
                writer.PropertyName("medianMilliseconds"); WriteNullable(writer, item.MedianMilliseconds);
                writer.PropertyName("p95Milliseconds"); WriteNullable(writer, item.P95Milliseconds);
                writer.PropertyName("p99Milliseconds"); WriteNullable(writer, item.P99Milliseconds);
                writer.PropertyName("maxMilliseconds"); WriteNullable(writer, item.MaxMilliseconds);
                writer.PropertyName("totalMilliseconds"); WriteNullable(writer, item.TotalMilliseconds);
                writer.PropertyName("millisecondsPerFrame"); WriteNullable(writer, item.MillisecondsPerFrame);
                writer.PropertyName("calls"); if (item.Calls.HasValue) writer.Write(item.Calls.Value); else writer.WriteNull();
                writer.PropertyName("confidence"); writer.Write(item.Confidence ?? string.Empty);
                writer.PropertyName("patchOwners"); WriteStrings(writer, item.PatchOwners);
                writer.TypeEnd();
            }
            writer.ArrayEnd();
        }

        private static void WriteMods(IJsonWriter writer, IReadOnlyList<ModUiRow> mods)
        {
            mods = mods ?? Array.Empty<ModUiRow>();
            writer.ArrayBegin((uint)mods.Count);
            foreach (var row in mods)
            {
                var item = row ?? new ModUiRow();
                writer.TypeBegin("CS2RuntimeAssetAuditor.ModUiRow");
                writer.PropertyName("assemblyName"); writer.Write(item.AssemblyName ?? string.Empty);
                writer.PropertyName("directSystemMilliseconds"); writer.Write(item.DirectSystemMilliseconds);
                writer.PropertyName("directCostBasis"); writer.Write(item.DirectCostBasis ?? string.Empty);
                writer.PropertyName("directSystemCount"); writer.Write(item.DirectSystemCount);
                writer.PropertyName("patchedVanillaSystemCount"); writer.Write(item.PatchedVanillaSystemCount);
                writer.TypeEnd();
            }
            writer.ArrayEnd();
        }

        private static void WriteMetricsObject(IJsonWriter writer, IReadOnlyList<UiMetricRow> metrics)
        {
            writer.TypeBegin("CS2RuntimeAssetAuditor.PathfindingUiMetrics");
            writer.PropertyName("metrics"); WriteMetricsArray(writer, metrics);
            writer.TypeEnd();
        }

        private static void WriteMetricsArray(IJsonWriter writer, IReadOnlyList<UiMetricRow> metrics)
        {
            metrics = metrics ?? Array.Empty<UiMetricRow>();
            writer.ArrayBegin((uint)metrics.Count);
            foreach (var row in metrics)
            {
                var item = row ?? new UiMetricRow();
                writer.TypeBegin("CS2RuntimeAssetAuditor.UiMetricRow");
                writer.PropertyName("id"); writer.Write(item.Id ?? string.Empty);
                writer.PropertyName("value"); WriteNullable(writer, item.Value);
                writer.PropertyName("unitType"); writer.Write(item.UnitType ?? string.Empty);
                writer.PropertyName("confidence"); writer.Write(item.Confidence ?? string.Empty);
                writer.PropertyName("availability"); writer.Write(item.Availability ?? string.Empty);
                writer.PropertyName("reason"); if (item.Reason == null) writer.WriteNull(); else writer.Write(item.Reason);
                writer.TypeEnd();
            }
            writer.ArrayEnd();
        }

        private static void WriteTimeline(IJsonWriter writer, IReadOnlyList<TimelinePoint> timeline)
        {
            timeline = timeline ?? Array.Empty<TimelinePoint>();
            writer.ArrayBegin((uint)timeline.Count);
            foreach (var point in timeline)
            {
                var item = point ?? new TimelinePoint();
                writer.TypeBegin("CS2RuntimeAssetAuditor.TimelinePoint");
                writer.PropertyName("timestampSeconds"); writer.Write(item.TimestampSeconds);
                writer.PropertyName("metric"); writer.Write(item.Metric ?? string.Empty);
                writer.PropertyName("value"); writer.Write(item.Value);
                writer.PropertyName("unitType"); writer.Write(item.UnitType ?? string.Empty);
                writer.PropertyName("confidence"); writer.Write(item.Confidence ?? string.Empty);
                writer.TypeEnd();
            }
            writer.ArrayEnd();
        }

        private static void WriteCaptures(IJsonWriter writer, IReadOnlyList<CaptureSummaryUi> captures)
        {
            captures = captures ?? Array.Empty<CaptureSummaryUi>();
            writer.ArrayBegin((uint)captures.Count);
            foreach (var capture in captures)
            {
                var item = capture ?? new CaptureSummaryUi();
                writer.TypeBegin("CS2RuntimeAssetAuditor.CaptureSummaryUi");
                writer.PropertyName("id"); writer.Write(item.Id ?? string.Empty);
                writer.PropertyName("triggerKind"); writer.Write(item.TriggerKind ?? string.Empty);
                writer.PropertyName("triggeredAtSeconds"); writer.Write(item.TriggeredAtSeconds);
                writer.PropertyName("durationSeconds"); writer.Write(item.DurationSeconds);
                writer.PropertyName("discoveredMarkers"); writer.Write(item.DiscoveredMarkers);
                writer.PropertyName("capturedMarkers"); writer.Write(item.CapturedMarkers);
                writer.PropertyName("batched"); writer.Write(item.Batched);
                writer.PropertyName("coverageRatio"); WriteNullable(writer, item.CoverageRatio);
                writer.PropertyName("warningCount"); writer.Write(item.WarningCount);
                writer.PropertyName("profilerOverheadShare"); writer.Write(item.ProfilerOverheadShare);
                writer.PropertyName("warnings"); WriteStrings(writer, item.Warnings);
                writer.PropertyName("correlatedChanges"); WriteCorrelatedChanges(writer, item.CorrelatedChanges);
                writer.TypeEnd();
            }
            writer.ArrayEnd();
        }

        private static void WriteCorrelatedChanges(IJsonWriter writer, IReadOnlyList<CorrelatedChangeUi> changes)
        {
            changes = changes ?? Array.Empty<CorrelatedChangeUi>();
            writer.ArrayBegin((uint)changes.Count);
            foreach (var change in changes)
            {
                var item = change ?? new CorrelatedChangeUi();
                writer.TypeBegin("CS2RuntimeAssetAuditor.CorrelatedChangeUi");
                writer.PropertyName("metric"); writer.Write(item.Metric ?? string.Empty);
                writer.PropertyName("before"); writer.Write(item.Before);
                writer.PropertyName("after"); writer.Write(item.After);
                writer.PropertyName("delta"); writer.Write(item.Delta);
                writer.PropertyName("relativeDelta"); WriteNullable(writer, item.RelativeDelta);
                writer.PropertyName("unitType"); writer.Write(item.UnitType ?? string.Empty);
                writer.PropertyName("confidence"); writer.Write(item.Confidence ?? string.Empty);
                writer.TypeEnd();
            }
            writer.ArrayEnd();
        }

        private static void WriteDiagnostics(IJsonWriter writer, DiagnosticsUi diagnostics)
        {
            diagnostics = diagnostics ?? new DiagnosticsUi();
            writer.TypeBegin("CS2RuntimeAssetAuditor.DiagnosticsUi");
            writer.PropertyName("profilerOverheadShare"); writer.Write(diagnostics.ProfilerOverheadShare);
            writer.PropertyName("unattributedJobsMilliseconds"); WriteNullable(writer, diagnostics.UnattributedJobsMilliseconds);
            writer.PropertyName("messages"); WriteStrings(writer, diagnostics.Messages);
            writer.PropertyName("gameVersion"); writer.Write(diagnostics.GameVersion ?? string.Empty);
            writer.PropertyName("profilerVersion"); writer.Write(diagnostics.ProfilerVersion ?? string.Empty);
            writer.PropertyName("discoveredMarkerCount"); writer.Write(diagnostics.DiscoveredMarkerCount);
            writer.PropertyName("capturedMarkerCount"); writer.Write(diagnostics.CapturedMarkerCount);
            writer.PropertyName("systemCount"); writer.Write(diagnostics.SystemCount);
            writer.PropertyName("markerBatchSize"); writer.Write(diagnostics.MarkerBatchSize);
            writer.PropertyName("samplingStride"); writer.Write(diagnostics.SamplingStride);
            writer.PropertyName("patchMapState"); writer.Write(diagnostics.PatchMapState ?? string.Empty);
            writer.TypeEnd();
        }

        private static void WriteStrings(IJsonWriter writer, IReadOnlyList<string> values)
        {
            values = values ?? Array.Empty<string>();
            writer.ArrayBegin((uint)values.Count);
            foreach (var value in values)
                writer.Write(value ?? string.Empty);
            writer.ArrayEnd();
        }

        private static void WriteNullable(IJsonWriter writer, double? value)
        {
            if (value.HasValue)
                writer.Write(value.Value);
            else
                writer.WriteNull();
        }
    }
}
