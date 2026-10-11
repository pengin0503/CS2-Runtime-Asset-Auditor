using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Core.Advisor;
using CS2RuntimeAssetAuditor.Core.Frames;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

// Issue #55: the render thread is part of the frame-time classification, and contradicting or missing timings
// lower the confidence instead of producing a confident GPU diagnosis. Values are a 30 ms median frame.
public class RenderThreadBottleneckTests
{
    private static NamedMetricValue[] Frame(double main, double render, double gpu, double wait, double median = 30, double p95 = 34)
        => new[]
        {
            Available("frame.p95.ms", p95), Available("frame.median.ms", median), Available("cpu.main.ms", main),
            Available("cpu.render.ms", render), Available("gpu.frame.ms", gpu), Available("present.wait.ms", wait)
        };

    [Test]
    public void Gpu_bound_frame_with_an_idle_render_thread_is_a_confident_gpu_diagnosis()
    {
        var result = Classify(Frame(main: 29, render: 8, gpu: 29.5, wait: 12));
        var gpu = result.Single();
        Assert.That(gpu.Category, Is.EqualTo(BottleneckCategory.RenderingGpu));
        Assert.That(gpu.Confidence, Is.EqualTo(AdvisorConfidence.High));
        Assert.That(gpu.EvidenceIds, Does.Contain("cpu.render.ms"));
    }

    [Test]
    public void Render_thread_bound_frame_waiting_for_present_is_not_called_gpu_bound()
    {
        // Draw-call bound: the main thread waits for the render thread's present, the GPU is not busy.
        var result = Classify(Frame(main: 29, render: 27, gpu: 15, wait: 12));
        var render = result.Single();
        Assert.That(render.Category, Is.EqualTo(BottleneckCategory.RenderThreadCpu));
        Assert.That(render.Confidence, Is.EqualTo(AdvisorConfidence.High));
        Assert.That(render.EvidenceIds, Is.EqualTo(new[] { "frame.p95.ms", "cpu.render.ms", "present.wait.ms" }));
    }

    [Test]
    public void Render_thread_and_gpu_both_near_the_frame_time_lower_the_confidence()
    {
        var result = Classify(Frame(main: 29, render: 27, gpu: 29, wait: 12));
        var render = result.Single();
        Assert.That(render.Category, Is.EqualTo(BottleneckCategory.RenderThreadCpu));
        Assert.That(render.Confidence, Is.EqualTo(AdvisorConfidence.Medium));
        Assert.That(render.EvidenceIds, Does.Contain("gpu.frame.ms"));
    }

    [Test]
    public void Main_thread_bound_frame_stays_main_thread()
    {
        var result = Classify(Frame(main: 29, render: 7, gpu: 28, wait: 0.01));
        Assert.That(result.Single().Category, Is.EqualTo(BottleneckCategory.MainThreadCpu));
        Assert.That(result.Single().Confidence, Is.EqualTo(AdvisorConfidence.High));
    }

    [Test]
    public void Present_wait_with_spare_gpu_and_threads_is_a_frame_rate_cap_not_rendering_load()
    {
        var uncapped = Classify(Frame(main: 29, render: 6, gpu: 10, wait: 20)).Single();
        Assert.That(uncapped.Category, Is.EqualTo(BottleneckCategory.PresentLimited));
        Assert.That(uncapped.Confidence, Is.EqualTo(AdvisorConfidence.Medium));

        var vsync = Classify(Append(Frame(main: 29, render: 6, gpu: 10, wait: 20), Available("display.frame.cap", 1))).Single();
        Assert.That(vsync.Category, Is.EqualTo(BottleneckCategory.PresentLimited));
        Assert.That(vsync.Confidence, Is.EqualTo(AdvisorConfidence.High));
        Assert.That(vsync.EvidenceIds, Does.Contain("display.frame.cap"));

        var recommendations = new RecommendationEngine().Build(new[] { vsync }, new[] { QualitySetting() });
        Assert.That(recommendations.Single().Direction, Is.EqualTo(RecommendationDirection.NoRecommendation));
    }

    [Test]
    public void Missing_render_timing_keeps_a_present_wait_diagnosis_at_medium_confidence()
    {
        var metrics = Frame(main: 29, render: 0, gpu: 29.5, wait: 12);
        metrics[3] = NamedMetricValue.Unavailable("cpu.render.ms", "Render-thread frame timing unavailable");
        var gpu = Classify(metrics).Single();
        Assert.That(gpu.Category, Is.EqualTo(BottleneckCategory.RenderingGpu));
        Assert.That(gpu.Confidence, Is.EqualTo(AdvisorConfidence.Medium));
        Assert.That(gpu.EvidenceIds, Does.Not.Contain("cpu.render.ms"));
    }

    [Test]
    public void Missing_gpu_timing_never_gives_a_confident_gpu_diagnosis()
    {
        var metrics = Frame(main: 29, render: 8, gpu: 0, wait: 12);
        metrics[4] = NamedMetricValue.Unavailable("gpu.frame.ms", "GPU timer unavailable");
        var gpu = Classify(metrics).Single();
        Assert.That(gpu.Category, Is.EqualTo(BottleneckCategory.RenderingGpu));
        Assert.That(gpu.Confidence, Is.EqualTo(AdvisorConfidence.Medium));
    }

    [Test]
    public void Mixed_load_without_a_dominant_thread_is_unknown_with_low_confidence()
    {
        var result = Classify(Frame(main: 20, render: 15, gpu: 18, wait: 2)).Single();
        Assert.That(result.Category, Is.EqualTo(BottleneckCategory.Unknown));
        Assert.That(result.Confidence, Is.EqualTo(AdvisorConfidence.Low));
    }

    [Test]
    public void Short_spikes_are_not_attributed_with_confidence()
    {
        // Typical frames take 12 ms; only the P95 is slow.
        var result = Classify(Frame(main: 11.5, render: 3, gpu: 11.8, wait: 5, median: 12, p95: 26)).Single();
        Assert.That(result.Severity, Is.EqualTo(BottleneckSeverity.Medium));
        Assert.That(result.Confidence, Is.EqualTo(AdvisorConfidence.Low));
        Assert.That(result.Rationale, Does.EndWith(BottleneckClassifier.IntermittentSuffix));
    }

    [Test]
    public void High_profiler_overhead_lowers_a_render_thread_diagnosis()
    {
        var result = Classify(Append(Frame(main: 29, render: 27, gpu: 15, wait: 12), Available("profiler.overhead.share", 0.2)));
        Assert.That(result.Single().Confidence, Is.EqualTo(AdvisorConfidence.Low));
    }

    [Test]
    public void Render_thread_diagnosis_does_not_recommend_lowering_gpu_quality()
    {
        var render = Classify(Frame(main: 29, render: 27, gpu: 15, wait: 12)).Single();
        var recommendations = new RecommendationEngine().Build(new[] { render }, new[] { QualitySetting() });
        Assert.That(recommendations.Single().Direction, Is.Not.EqualTo(RecommendationDirection.LowerRecommended));
    }

    [Test]
    public void Projector_reports_render_thread_time_and_the_frame_rate_cap()
    {
        var capture = new CaptureSession("capture", new CaptureTrigger(CaptureTriggerKind.Manual, 1, null), 8);
        capture.AddGlobalSample(new GlobalMetricsSnapshot(1, 1, 1, null, null, new RuntimeInterval
        {
            IntervalSeconds = 0.5,
            Frames = 15,
            FrameMs = new SampleSummary(15, 33, 34, 35),
            FrameTimingSamples = 15,
            CpuMainThreadMs = new SampleSummary(15, 32, 33, 33),
            CpuRenderThreadMs = new SampleSummary(15, 7, 8, 9),
            GpuMs = new SampleSummary(15, 12, 13, 13),
            PresentWaitMs = new SampleSummary(15, 20, 21, 21),
            VSyncCount = 1,
            TargetFrameRate = -1
        }));

        var evidence = new CaptureAdvisorEvidenceProjector().Project(capture);
        Assert.That(evidence.Find("cpu.render.ms").Value, Is.EqualTo(7));
        Assert.That(evidence.Find("display.frame.cap").Value, Is.EqualTo(1));

        var empty = new CaptureSession("empty", new CaptureTrigger(CaptureTriggerKind.Manual, 1, null), 8);
        empty.AddGlobalSample(new GlobalMetricsSnapshot(1, 1, 1, null, null, new RuntimeInterval { FrameTimingSamples = 1 }));
        var missing = new CaptureAdvisorEvidenceProjector().Project(empty);
        Assert.That(missing.Find("cpu.render.ms").Availability, Is.EqualTo(MetricAvailability.Unavailable));
        Assert.That(missing.Find("display.frame.cap").Availability, Is.EqualTo(MetricAvailability.Unavailable));
    }

    private static GameSettingDescriptor QualitySetting() => new()
    {
        SettingId = "Game.Settings.GraphicsSettings::qualityPreset",
        ValueKind = SettingValueKind.Enumeration,
        CurrentValue = "High",
        AllowedValues = new[] { "Low", "Medium", "High" },
        SemanticTags = new[] { "rendering.ordered-quality" },
        IsUserFacing = true,
        IsReadable = true
    };

    private static NamedMetricValue[] Append(NamedMetricValue[] metrics, NamedMetricValue extra)
        => metrics.Concat(new[] { extra }).ToArray();

    private static NamedMetricValue Available(string id, double value) => NamedMetricValue.Available(id, value, MetricConfidence.Full);

    private static IReadOnlyList<BottleneckObservation> Classify(params NamedMetricValue[] metrics)
        => new BottleneckClassifier().Classify(new AdvisorEvidenceSnapshot(DateTime.UtcNow, metrics));
}
