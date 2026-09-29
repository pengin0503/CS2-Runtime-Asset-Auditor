using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.DiagnosticLog;
using CS2RuntimeAssetAuditor.Export;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

// The continuous diagnostic log: one CSV row per second while a city is open, used to record real play.
public class DiagnosticLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void Sample_window_matches_metric_statistics_and_ignores_unmeasured_values()
    {
        var window = new DiagnosticSampleWindow(16);
        var values = new[] { 16.7, 50d, 17.1, 33.3, 16.9, 16.6, 120d };
        foreach (var value in values)
            window.Add(value);
        window.Add(0d);
        window.Add(-1d);
        window.Add(double.NaN);
        window.Add(double.PositiveInfinity);

        var summary = window.Summarize();
        var expected = MetricStatistics.From(values);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Count, Is.EqualTo(values.Length));
            Assert.That(summary.Median, Is.EqualTo(expected.Median));
            Assert.That(summary.P95, Is.EqualTo(expected.P95));
            Assert.That(summary.Max, Is.EqualTo(120d));
        });
    }

    [Test]
    public void Sample_window_keeps_zero_only_when_zero_is_a_measurement()
    {
        var presentWait = new DiagnosticSampleWindow(8, includeZero: true);
        presentWait.Add(0d);
        presentWait.Add(0d);
        presentWait.Add(4d);

        Assert.That(presentWait.Summarize().Median, Is.EqualTo(0d));
        Assert.That(presentWait.Summarize().Count, Is.EqualTo(3));
    }

    [Test]
    public void Saturated_window_keeps_exact_count_and_max()
    {
        var window = new DiagnosticSampleWindow(2);
        window.Add(1d);
        window.Add(2d);
        window.Add(90d);

        var summary = window.Summarize();
        Assert.Multiple(() =>
        {
            Assert.That(window.IsSaturated, Is.True);
            Assert.That(summary.Count, Is.EqualTo(3));
            Assert.That(summary.Max, Is.EqualTo(90d));
        });

        window.Reset();
        Assert.That(window.Summarize().HasValue, Is.False);
    }

    [TestCase(0.5, 1)]
    [TestCase(1d, 2)]
    [TestCase(2d, 4)]
    [TestCase(4d, 8)]
    [TestCase(8d, 8)]
    [TestCase(1.25, 2)] // 2.5 rounds to even, as Mathf.RoundToInt does.
    [TestCase(0.1, 1)]
    public void Render_frame_step_cap_follows_the_game_formula(double selectedSpeed, int expected)
    {
        Assert.That(DiagnosticLogAccumulator.RenderFrameStepCap(selectedSpeed), Is.EqualTo(expected));
    }

    [Test]
    public void Low_frame_rate_at_normal_speed_shows_frames_at_the_render_cap()
    {
        // 20 fps at 1x: the game allows 2 steps per rendered frame, so 40 steps/s instead of 60.
        var accumulator = new DiagnosticLogAccumulator(null);
        uint frameIndex = 1000;
        for (var i = 0; i < 20; i++)
        {
            frameIndex += i == 0 ? 0u : 2u;
            accumulator.AddFrame(Frame(0.05, selectedSpeed: 1, actualSpeed: 0.667, frameIndex: frameIndex, stepSeconds: 0.004));
        }

        var row = accumulator.Complete(Now, 1d, default);

        Assert.Multiple(() =>
        {
            Assert.That(row.Frames, Is.EqualTo(20));
            Assert.That(row.IntervalSeconds, Is.EqualTo(1d).Within(1e-9));
            Assert.That(row.FrameMs.Median, Is.EqualTo(50d).Within(1e-9));
            Assert.That(row.SimulationSteps, Is.EqualTo(38), "The first frame only sets the baseline.");
            Assert.That(row.SimulationMaxStepsPerFrame, Is.EqualTo(2));
            Assert.That(row.SimulationFramesAtRenderCap, Is.EqualTo(19));
            Assert.That(row.SimulationFramesWithoutStep, Is.EqualTo(0));
            Assert.That(row.SimulationStepsPerSecond, Is.EqualTo(38d).Within(1e-9));
            Assert.That(row.SimulationStepMs.Median, Is.EqualTo(4d).Within(1e-9));
            Assert.That(row.EfficiencyMean, Is.EqualTo(0.667).Within(1e-9));
            Assert.That(row.PausedFrames, Is.EqualTo(0));
        });
    }

    [Test]
    public void Frame_index_jumps_and_pauses_are_not_counted_as_steps()
    {
        var accumulator = new DiagnosticLogAccumulator(null);
        accumulator.AddFrame(Frame(0.016, selectedSpeed: 1, frameIndex: 100));
        accumulator.AddFrame(Frame(0.016, selectedSpeed: 1, frameIndex: 101));
        accumulator.AddFrame(Frame(0.016, selectedSpeed: 1, frameIndex: 5000)); // Load replaced frameIndex.
        accumulator.AddFrame(Frame(0.016, selectedSpeed: 1, frameIndex: 10));   // Went backwards.
        accumulator.AddFrame(Frame(0.016, selectedSpeed: 0, frameIndex: 10));   // Paused.
        accumulator.AddFrame(Frame(0.016, selectedSpeed: 1, frameIndex: 10));   // Running but no step.

        var row = accumulator.Complete(Now, 1d, default);

        Assert.Multiple(() =>
        {
            Assert.That(row.SimulationSteps, Is.EqualTo(1));
            Assert.That(row.PausedFrames, Is.EqualTo(1));
            Assert.That(row.SimulationFramesWithoutStep, Is.EqualTo(1));
            Assert.That(row.SelectedSpeed, Is.EqualTo(1d));
        });
    }

    [Test]
    public void Pathfinding_lead_and_pending_requests_are_reported_per_interval()
    {
        var accumulator = new DiagnosticLogAccumulator(null);
        accumulator.AddFrame(Frame(0.016, selectedSpeed: 1, frameIndex: 100, pathfindPending: uint.MaxValue, pendingRequests: 0));
        accumulator.AddFrame(Frame(0.016, selectedSpeed: 1, frameIndex: 101, pathfindPending: 200, pendingRequests: 30));
        accumulator.AddFrame(Frame(0.016, selectedSpeed: 1, frameIndex: 102, pathfindPending: 130, pendingRequests: 12));

        var row = accumulator.Complete(Now, 1d, default);

        Assert.Multiple(() =>
        {
            Assert.That(row.PathfindLeadFramesMin, Is.EqualTo(27));
            Assert.That(row.PathfindLowLeadFrames, Is.EqualTo(1));
            Assert.That(row.PathfindPendingRequestsMax, Is.EqualTo(30));
        });

        var empty = accumulator.Complete(Now, 2d, default);
        Assert.That(empty.PathfindLeadFramesMin, Is.Null, "Each row starts a new interval.");
    }

    [Test]
    public void Recorder_columns_keep_the_latest_reading_and_ignore_other_recorders()
    {
        var columns = new[] { new DiagnosticRecorderColumn("Internal\u001fMain Thread", "Main Thread", "TimeNanoseconds") };
        var accumulator = new DiagnosticLogAccumulator(columns);
        accumulator.AddRecorderReadings(new Dictionary<string, RecorderReading>
        {
            ["Internal\u001fMain Thread"] = new RecorderReading(8_000_000, 1),
            ["Other\u001fMarker"] = new RecorderReading(5, 1)
        });
        accumulator.AddRecorderReadings(new Dictionary<string, RecorderReading>
        {
            ["Internal\u001fMain Thread"] = new RecorderReading(9_000_000, 1)
        });
        accumulator.AddFrame(Frame(0.016, selectedSpeed: 1, frameIndex: 1));

        var row = accumulator.Complete(Now, 1d, default);
        var next = accumulator.Complete(Now, 2d, default);

        Assert.That(row.RecorderValues, Is.EqualTo(new double?[] { 9_000_000 }));
        Assert.That(next.RecorderValues, Is.EqualTo(new double?[] { null }));
    }

    [Test]
    public void Gc_collections_are_reported_as_the_change_since_the_previous_row()
    {
        var accumulator = new DiagnosticLogAccumulator(null);
        var first = accumulator.Complete(Now, 1d, new DiagnosticIntervalContext("Monitoring", null, null, 512L * 1024 * 1024, 40));
        var second = accumulator.Complete(Now, 2d, new DiagnosticIntervalContext("Monitoring", null, null, 512L * 1024 * 1024, 43));

        Assert.Multiple(() =>
        {
            Assert.That(first.GcCollections, Is.Null);
            Assert.That(second.GcCollections, Is.EqualTo(3));
            Assert.That(second.ManagedHeapMiB, Is.EqualTo(512d));
        });
    }

    [Test]
    public void Csv_rows_have_one_field_per_header_column_and_use_the_invariant_culture()
    {
        var columns = new[] { new DiagnosticRecorderColumn("Internal\u001fMain Thread", "Main Thread", "TimeNanoseconds") };
        var accumulator = new DiagnosticLogAccumulator(columns);
        accumulator.AddFrame(Frame(0.0165, selectedSpeed: 1, actualSpeed: 0.95, frameIndex: 1, hasTiming: true));
        accumulator.AddFrame(Frame(0.0171, selectedSpeed: 1, actualSpeed: 0.95, frameIndex: 2, hasTiming: true));
        var row = accumulator.Complete(Now, 1.25, new DiagnosticIntervalContext("DeepCapture", "Manual", "abc", null, null));

        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var header = DiagnosticLogCsv.FormatHeader(columns).Split(',');
            var fields = DiagnosticLogCsv.FormatRow(row).Split(',');
            var values = header.Zip(fields).ToDictionary(pair => pair.First, pair => pair.Second);

            Assert.Multiple(() =>
            {
                Assert.That(fields, Has.Length.EqualTo(header.Length));
                Assert.That(header.Last(), Is.EqualTo("recorder:Main Thread [TimeNanoseconds]"));
                Assert.That(values["utcTime"], Is.EqualTo("2026-09-29T12:00:00.000Z"));
                Assert.That(values["elapsedSeconds"], Is.EqualTo("1.25"));
                Assert.That(values["frameMsMedian"], Is.EqualTo("16.8"));
                Assert.That(values["efficiencyMean"], Is.EqualTo("0.95"));
                Assert.That(values["cpuMainMsMedian"], Is.EqualTo("10"));
                Assert.That(values["captureState"], Is.EqualTo("DeepCapture"));
                Assert.That(values["captureTrigger"], Is.EqualTo("Manual"));
                Assert.That(values["pathfindLeadFramesMin"], Is.Empty, "Absent values are empty, never 0.");
                Assert.That(values["gcCollections"], Is.Empty);
                Assert.That(values["recorder:Main Thread [TimeNanoseconds]"], Is.Empty);
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Test]
    public void Csv_escapes_fields_with_separators_and_writes_the_marker_inventory()
    {
        var descriptor = new RecorderDescriptor("Render\u001fDraw, \"Opaque\"", "Render", "Draw, \"Opaque\"", "TimeNanoseconds", "Int64");

        Assert.Multiple(() =>
        {
            Assert.That(DiagnosticLogCsv.FormatMarkerInventoryHeader(), Is.EqualTo("category,name,unit,dataType,normalMonitoring"));
            Assert.That(DiagnosticLogCsv.FormatMarkerInventoryRow(descriptor, normalMonitoring: false),
                Is.EqualTo("Render,\"Draw, \"\"Opaque\"\"\",TimeNanoseconds,Int64,false"));
            Assert.That(DiagnosticLogCsv.Escape("a\nb"), Is.EqualTo("\"a\nb\""));
        });
    }

    [Test]
    public void Writer_drops_lines_once_the_size_cap_would_be_passed()
    {
        var text = new StringWriter { NewLine = "\n" };
        var writer = new DiagnosticLogWriter(text, new UTF8Encoding(false), maxBytes: 10);

        Assert.Multiple(() =>
        {
            Assert.That(writer.TryWriteLine("abcd"), Is.True);   // 5 bytes
            Assert.That(writer.TryWriteLine("efgh"), Is.True);   // 10 bytes: exactly at the cap
            Assert.That(writer.TryWriteLine("i"), Is.False);
            Assert.That(writer.TryWriteLine(""), Is.False, "Nothing is written after the cap is reached.");
            Assert.That(writer.IsLimitReached, Is.True);
            Assert.That(writer.WrittenBytes, Is.EqualTo(10));
            Assert.That(text.ToString(), Is.EqualTo("abcd\nefgh\n"));
        });
    }

    [Test]
    public void Report_file_writer_claims_a_new_streaming_file_without_overwriting()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cs2raa-diaglog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "log.csv"), "existing");
            using (var stream = ReportFileWriter.CreateUnique(directory, "log", ".csv", out var path))
            {
                Assert.That(Path.GetFileName(path), Is.EqualTo("log-1.csv"));
                stream.WriteByte((byte)'x');
            }
            Assert.That(File.ReadAllText(Path.Combine(directory, "log.csv")), Is.EqualTo("existing"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Capture_start_line_names_the_capture_trigger_and_efficiency()
    {
        var automatic = new CaptureSession("cap-1", new CaptureTrigger(CaptureTriggerKind.AutomaticLowEfficiency, 3d, 0.6667), 16);
        var manual = new CaptureSession("cap-2", new CaptureTrigger(CaptureTriggerKind.Manual, 3d, null), 16);

        Assert.Multiple(() =>
        {
            Assert.That(CaptureCompletionLogFormatter.FormatStarted(automatic),
                Is.EqualTo("Capture started: capture=cap-1 trigger=AutomaticLowEfficiency efficiency=0.667"));
            Assert.That(CaptureCompletionLogFormatter.FormatStarted(manual),
                Is.EqualTo("Capture started: capture=cap-2 trigger=Manual efficiency=unavailable"));
        });
    }

    [Test]
    public void Diagnostic_log_is_off_by_default_and_registered_after_capture_state_is_updated()
    {
        var setting = ReadSource("Setting.cs");
        var mod = ReadSource("Mod.cs");
        var capture = mod.IndexOf("updateSystem.UpdateAt<CaptureRuntimeSystem>(SystemUpdatePhase.UIUpdate);", StringComparison.Ordinal);
        var log = mod.IndexOf("updateSystem.UpdateAt<DiagnosticLogSystem>(SystemUpdatePhase.UIUpdate);", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(setting, Does.Match(@"\[SettingsUISection\(MainTab, MonitoringGroup\)\]\s+public bool EnableDiagnosticLog \{ get; set; \}"));
            Assert.That(setting, Does.Contain("EnableDiagnosticLog = false;"));
            Assert.That(capture, Is.GreaterThan(0));
            Assert.That(log, Is.GreaterThan(capture));
        });
    }

    [Test]
    public void Diagnostic_log_option_is_localized_in_english_and_japanese()
    {
        foreach (var locale in new[] { "Localization/LocaleEN.cs", "Localization/LocaleJA.cs" })
        {
            var source = ReadSource(locale);
            Assert.That(source, Does.Contain("GetOptionLabelLocaleID(nameof(Setting.EnableDiagnosticLog))"), locale);
            Assert.That(source, Does.Contain("GetOptionDescLocaleID(nameof(Setting.EnableDiagnosticLog))"), locale);
        }
    }

    [Test]
    public void Diagnostic_log_system_logs_only_the_file_name()
    {
        var system = ReadSource("Lifecycle/DiagnosticLogSystem.cs");

        Assert.That(system, Does.Contain("_fileName = Path.GetFileName(path);"));
        Assert.That(Regex.IsMatch(system, @"Mod\.Log\.Info\([^;]*\bpath\b"), Is.False, "The full path contains the user's profile folder.");
    }

    private static DiagnosticFrameInput Frame(
        double deltaSeconds,
        double selectedSpeed,
        uint frameIndex,
        double actualSpeed = 1d,
        double stepSeconds = 0d,
        uint pathfindPending = uint.MaxValue,
        int pendingRequests = -1,
        bool hasTiming = false)
        => new(
            deltaSeconds,
            hasTiming,
            hasTiming ? 10d : 0d,
            hasTiming ? 6d : 0d,
            hasTiming ? 12d : 0d,
            0d,
            selectedSpeed,
            actualSpeed,
            frameIndex,
            stepSeconds,
            "Balanced",
            pathfindPending,
            pendingRequests);

    private static string ReadSource(string relativePath)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CS2RuntimeAssetAuditor.sln")))
            directory = directory.Parent;
        Assert.That(directory, Is.Not.Null);
        return File.ReadAllText(Path.Combine(directory!.FullName, "src", "CS2RuntimeAssetAuditor", relativePath));
    }
}
