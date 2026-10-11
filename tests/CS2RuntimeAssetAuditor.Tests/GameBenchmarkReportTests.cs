using System;
using System.Linq;
using System.Text.Json;
using CS2RuntimeAssetAuditor.Export;
using CS2RuntimeAssetAuditor.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

// Issue #58: the game's own latest benchmark result goes into the report's runtime information.
public class GameBenchmarkReportTests
{
    [TestCase(false, 5400, TestName = "No data")]
    [TestCase(true, 0, TestName = "No frame rendered")]
    public void A_default_or_empty_stored_result_is_not_reported(bool hasData, int frames)
    {
        Assert.That(ReportGameBenchmark.Create(hasData, frames, DateTime.UtcNow.Ticks), Is.Null);
    }

    [Test]
    public void The_recorded_time_comes_from_the_utc_ticks_the_game_stores()
    {
        var recorded = new DateTime(2026, 10, 11, 6, 30, 15, DateTimeKind.Utc);
        Assert.That(ReportGameBenchmark.Create(true, 5400, recorded.Ticks)!.RecordedAtUtc,
            Is.EqualTo("2026-10-11T06:30:15.0000000Z"));
        Assert.That(ReportGameBenchmark.Create(true, 5400, 0)!.RecordedAtUtc, Is.Null, "never saved");
        Assert.That(ReportGameBenchmark.Create(true, 5400, -1)!.RecordedAtUtc, Is.Null);
    }

    [Test]
    public void Stats_that_the_game_left_at_zero_are_absent()
    {
        Assert.That(ReportBenchmarkStats.From(0, 0, 0, 0), Is.Null);
        Assert.That(ReportBenchmarkStats.From(1, double.NaN, 2, 3), Is.Null);
        var stats = ReportBenchmarkStats.From(8.1, 11.4, 14.2, 31);
        Assert.That(stats!.Average, Is.EqualTo(11.4));
        Assert.That(stats.P95, Is.EqualTo(14.2));
    }

    [Test]
    public void The_report_carries_the_result_with_its_meaning_and_serializes_it()
    {
        var benchmark = Benchmark("1.6.2f1");
        var report = ProfilerReportBuilder.Build(new UiSnapshot(), gameVersion: "1.6.2f1",
            metadata: new RuntimeReportMetadata { GameBenchmark = benchmark });

        Assert.That(report.GameBenchmark, Is.SameAs(benchmark));
        Assert.That(report.GameBenchmark.Note, Is.EqualTo(ReportGameBenchmark.Meaning));
        Assert.That(report.GameBenchmark.Note, Does.Contain("not of the city"));
        Assert.That(report.Warnings, Does.Not.Contain(ReportGameBenchmark.DifferentGameVersionWarning));

        using var document = JsonDocument.Parse(PerformanceReportSerializer.Serialize(report));
        var json = document.RootElement.GetProperty("gameBenchmark");
        Assert.That(json.GetProperty("averageFps").GetDouble(), Is.EqualTo(48.5).Within(1e-9));
        Assert.That(json.GetProperty("gpuBoundPercent").GetDouble(), Is.EqualTo(62.5).Within(1e-9));
        Assert.That(json.GetProperty("gpuMs").GetProperty("p95").GetDouble(), Is.EqualTo(24).Within(1e-9));
        Assert.That(json.GetProperty("recordedAtUtc").GetString(), Is.EqualTo("2026-10-11T06:30:15.0000000Z"));
        Assert.That(json.TryGetProperty("cpuRenderMs", out _), Is.False, "absent stats are not written as zeros");
    }

    [Test]
    public void A_result_from_another_game_version_is_kept_with_a_warning()
    {
        var report = ProfilerReportBuilder.Build(new UiSnapshot(), gameVersion: "1.6.3f1",
            metadata: new RuntimeReportMetadata { GameBenchmark = Benchmark("1.6.2f1") });
        Assert.That(report.GameBenchmark, Is.Not.Null);
        Assert.That(report.Warnings, Does.Contain(ReportGameBenchmark.DifferentGameVersionWarning));
    }

    [Test]
    public void No_stored_result_leaves_the_field_out()
    {
        var report = ProfilerReportBuilder.Build(new UiSnapshot(), metadata: new RuntimeReportMetadata());
        Assert.That(report.GameBenchmark, Is.Null);
        using var document = JsonDocument.Parse(PerformanceReportSerializer.Serialize(report));
        Assert.That(document.RootElement.TryGetProperty("gameBenchmark", out _), Is.False);
    }

    private static ReportGameBenchmark Benchmark(string gameVersion)
    {
        var benchmark = ReportGameBenchmark.Create(true, 4365,
            new DateTime(2026, 10, 11, 6, 30, 15, DateTimeKind.Utc).Ticks)!;
        benchmark.GameVersion = gameVersion;
        benchmark.GraphicsQuality = "High";
        benchmark.AverageFps = 48.5;
        benchmark.GpuMs = ReportBenchmarkStats.From(12, 20, 24, 40);
        benchmark.GpuBoundPercent = 62.5;
        benchmark.CpuRenderMs = ReportBenchmarkStats.From(0, 0, 0, 0);
        return benchmark;
    }
}
