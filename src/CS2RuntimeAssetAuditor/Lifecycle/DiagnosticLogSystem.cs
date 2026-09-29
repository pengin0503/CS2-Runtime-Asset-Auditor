using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Colossal.PSI.Environment;
using CS2RuntimeAssetAuditor.Collectors;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.DiagnosticLog;
using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.Profiling;
using Game;
using Game.Pathfind;
using Game.Simulation;

namespace CS2RuntimeAssetAuditor.Lifecycle
{
    /// <summary>
    /// Writes the continuous diagnostic log: while the option is on and a city is loaded, one CSV row per
    /// second with the frame, simulation and pathfinding values needed to tell why the game ran slow, plus one
    /// inventory file of the profiler markers available in this build. A file covers one city session.
    /// Start, stop and failure lines go to the mod's normal log.
    /// </summary>
    public sealed partial class DiagnosticLogSystem : GameSystemBase
    {
        private const double RowIntervalSeconds = 1d;
        private const string FilePrefix = "CS2RuntimeAssetAuditor-diagnostic-";

        private readonly Stopwatch _clock = new Stopwatch();
        private RuntimeFrameSampleReader _frames;
        private GlobalMetricsCollector _global;
        private CaptureRuntimeSystem _capture;

        private DiagnosticLogAccumulator? _accumulator;
        private DiagnosticLogWriter? _writer;
        private string? _fileName;
        private long _fileGeneration = -1;
        private long _stoppedGeneration = -1;
        private int _rows;
        private double _nextRowAt;
        private GlobalMetricsSnapshot? _lastSnapshot;

        // Game 1.6.2f1: AutoSaveSystem keeps the time of the last autosave check in this private field.
        private static readonly FieldInfo? AutoSaveCheckField =
            typeof(AutoSaveSystem).GetField("m_LastAutoSaveCheck", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly AutoSaveTriggerCounter _autoSaves = new AutoSaveTriggerCounter();
        private AutoSaveSystem? _autoSaveSystem;
        private int _autoSaveStarts;

        protected override void OnCreate()
        {
            base.OnCreate();
            _global = World.GetOrCreateSystemManaged<GlobalMetricsCollector>();
            // One reader per frame for both systems, so FrameTimingManager is captured once a frame.
            _frames = _global.FrameSamples ?? new RuntimeFrameSampleReader(
                World.GetOrCreateSystemManaged<SimulationSystem>(),
                World.GetOrCreateSystemManaged<PathfindResultSystem>());
            _capture = World.GetOrCreateSystemManaged<CaptureRuntimeSystem>();
            _autoSaveSystem = World.GetExistingSystemManaged<AutoSaveSystem>();
        }
        protected override void OnUpdate()
        {
            var start = ModUpdateCost.Start();
            try { RunUpdate(); }
            finally { ModUpdateCost.Stop(nameof(DiagnosticLogSystem), start); }
        }

        private void RunUpdate()
        {
            if (Mod.Settings?.EnableDiagnosticLog != true)
            {
                Stop("disabled");
                return;
            }
            if (!Mod.Sessions.IsActive)
            {
                Stop("citySessionEnded");
                return;
            }

            var generation = Mod.Sessions.Generation;
            if (_writer != null && generation != _fileGeneration)
                Stop("citySessionChanged");
            if (_writer == null)
            {
                // A session whose file failed or reached its size cap is not reopened: a new file every frame
                // would defeat the cap and repeat the same failure.
                if (generation == _stoppedGeneration || !TryStart(generation))
                    return;
            }

            try
            {
                SampleFrame();
            }
            catch (Exception ex)
            {
                Mod.ReportFailure($"Diagnostic log sampling failed; the log for this city stops: file={_fileName}", ex);
                Stop("error", writePendingRow: false);
                _stoppedGeneration = generation;
            }
        }

        protected override void OnDestroy()
        {
            Stop("modUnloaded");
            base.OnDestroy();
        }

        private bool TryStart(long generation)
        {
            try
            {
                var directory = Path.Combine(EnvPath.kUserDataPath, "ModsData", Mod.Id);
                Directory.CreateDirectory(directory);
                var stem = FilePrefix + DateTime.Now.ToString("yyyy-MM-dd_HHmmss_fff", System.Globalization.CultureInfo.InvariantCulture);

                var columns = ResolveRecorderColumns();
                var markerCount = WriteMarkerInventory(directory, stem + "-markers", columns);

                var encoding = new UTF8Encoding(false);
                var stream = ReportFileWriter.CreateUnique(directory, stem, ".csv", out var path);
                _writer = new DiagnosticLogWriter(new StreamWriter(stream, encoding), encoding);
                _fileName = Path.GetFileName(path);
                _accumulator = new DiagnosticLogAccumulator(columns);
                _writer.TryWriteLine(DiagnosticLogCsv.FormatHeader(columns));
                _fileGeneration = generation;
                _rows = 0;
                _lastSnapshot = null;
                _autoSaves.Reset();
                _autoSaveStarts = 0;
                _clock.Restart();
                _nextRowAt = RowIntervalSeconds;

                Mod.Info(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Diagnostic log started: file={0} session={1} frameTimingFeature={2} recorderColumns={3} markers={4}",
                    _fileName,
                    Mod.SessionContext?.SessionId ?? "unknown",
                    RuntimeFrameSampleReader.FrameTimingFeatureEnabled,
                    columns.Count,
                    markerCount.HasValue ? markerCount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "unavailable"));
                return true;
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Diagnostic log could not be started; it stays off for this city.", ex);
                CloseWriter();
                _stoppedGeneration = generation;
                return false;
            }
        }

        private void SampleFrame()
        {
            var accumulator = _accumulator;
            if (accumulator == null)
                return;

            accumulator.AddFrame(_frames.Read());
            accumulator.AddModFrameCost(ModUpdateCost.LastCompletedFrame);
            ObserveAutoSave();

            var latest = _global?.Latest;
            if (latest != null && !ReferenceEquals(latest, _lastSnapshot))
            {
                _lastSnapshot = latest;
                accumulator.AddRecorderReadings(latest.RecorderReadings);
            }

            var elapsed = _clock.Elapsed.TotalSeconds;
            if (elapsed < _nextRowAt)
                return;
            _nextRowAt += RowIntervalSeconds;
            // After a long hitch the next row is due one interval from now, not several rows at once.
            if (_nextRowAt <= elapsed)
                _nextRowAt = elapsed + RowIntervalSeconds;
            WriteRow(accumulator, elapsed);
        }

        // The game's autosave stalls a frame for about a second; marking it keeps that stall from being blamed
        // on the simulation or on this mod.
        private void ObserveAutoSave()
        {
            if (AutoSaveCheckField == null)
                return;
            _autoSaveSystem ??= World.GetExistingSystemManaged<AutoSaveSystem>();
            if (_autoSaveSystem == null)
                return;
            if (!_autoSaves.Observe(AutoSaveCheckField.GetValue(_autoSaveSystem) as float?))
                return;
            _autoSaveStarts++;
            Mod.Info("Game autosave started: file=" + _fileName);
        }

        private void WriteRow(DiagnosticLogAccumulator accumulator, double elapsed)
        {
            var writer = _writer;
            if (writer == null)
                return;
            var captureState = _capture?.State ?? CaptureState.Monitoring;
            var capture = captureState == CaptureState.Monitoring ? null : _capture?.CurrentSession;
            var context = new DiagnosticIntervalContext(
                captureState.ToString(),
                capture?.Trigger?.Kind.ToString(),
                capture?.Id,
                GC.GetTotalMemory(false),
                GC.CollectionCount(0),
                AutoSaveCheckField != null && _autoSaveSystem != null ? _autoSaveStarts : (int?)null);
            _autoSaveStarts = 0;
            var row = accumulator.Complete(DateTimeOffset.UtcNow, elapsed, context);
            if (writer.TryWriteLine(DiagnosticLogCsv.FormatRow(row)))
            {
                _rows++;
                return;
            }

            Mod.Info(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Diagnostic log reached its size limit ({0} MiB); later rows for this city are not written: file={1}",
                DiagnosticLogWriter.DefaultMaxBytes / (1024 * 1024),
                _fileName));
            var generation = _fileGeneration;
            Stop("sizeLimit", writePendingRow: false);
            _stoppedGeneration = generation;
        }

        private void Stop(string reason, bool writePendingRow = true)
        {
            if (_writer == null)
                return;

            try
            {
                var accumulator = _accumulator;
                if (writePendingRow && accumulator != null && accumulator.FrameCount > 0 && !_writer.IsLimitReached)
                    WriteRow(accumulator, _clock.Elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                Mod.ReportFailure($"Diagnostic log could not write its last row: file={_fileName}", ex);
            }

            var writer = _writer;
            if (writer == null)
                return;
            var fileName = _fileName;
            var rows = _rows;
            var bytes = writer.WrittenBytes;
            CloseWriter();
            Mod.Info(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Diagnostic log stopped: file={0} reason={1} rows={2} bytes={3}",
                fileName,
                reason,
                rows,
                bytes));
        }

        private void CloseWriter()
        {
            try { _writer?.Dispose(); }
            catch (Exception ex) { Mod.ReportFailure($"Diagnostic log could not be closed: file={_fileName}", ex); }
            _writer = null;
            _accumulator = null;
            _fileGeneration = -1;
            _clock.Reset();
        }

        // The same recorders normal monitoring activates, resolved by the same preference order, so each
        // column shows the value the advisor and automatic capture read.
        private IReadOnlyList<DiagnosticRecorderColumn> ResolveRecorderColumns()
        {
            var columns = new List<DiagnosticRecorderColumn>();
            var recorders = _global?.Recorders;
            if (recorders == null)
                return columns;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var names in NormalMonitoringRecorderPolicy.GetPreferredRecorderNameGroups())
            {
                var descriptor = recorders.FindFirstByName(names);
                if (descriptor != null && seen.Add(descriptor.Id))
                    columns.Add(new DiagnosticRecorderColumn(descriptor.Id, descriptor.Name, descriptor.UnitType));
            }
            return columns;
        }

        // A fresh discovery so markers the game registered after the main menu (when the collector discovered)
        // are listed too. A failure here leaves the time series running without the inventory.
        private static int? WriteMarkerInventory(string directory, string stem, IReadOnlyList<DiagnosticRecorderColumn> columns)
        {
            try
            {
                var descriptors = new UnityRecorderBackend().Discover();
                var monitored = new HashSet<string>(StringComparer.Ordinal);
                foreach (var column in columns)
                    monitored.Add(column.Id);

                ReportFileWriter.WriteUnique(directory, stem, stream =>
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.WriteLine(DiagnosticLogCsv.FormatMarkerInventoryHeader());
                        foreach (var descriptor in descriptors)
                            writer.WriteLine(DiagnosticLogCsv.FormatMarkerInventoryRow(descriptor, monitored.Contains(descriptor.Id)));
                    }
                }, ".csv");
                return descriptors.Count;
            }
            catch (Exception ex)
            {
                Mod.ReportFailure("Diagnostic log could not write the profiler marker inventory.", ex);
                return null;
            }
        }
    }
}
