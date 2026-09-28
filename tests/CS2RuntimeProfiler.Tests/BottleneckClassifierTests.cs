using System;
using System.Linq;
using CS2RuntimeProfiler.Core;
using CS2RuntimeProfiler.Core.Advisor;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests
{
    [TestFixture]
    public sealed class BottleneckClassifierTests
    {
        [Test]
        public void Slow_frame_and_gpu_with_healthy_simulation_identify_rendering_pressure()
        {
            var result = Classify(Available("frame.p95.ms", 28), Available("gpu.frame.ms", 25),
                Available("simulation.efficiency", 0.98));
            Assert.That(result.Select(x => x.Category), Is.EqualTo(new[] { BottleneckCategory.RenderingGpu }));
            Assert.That(result[0].Confidence, Is.EqualTo(AdvisorConfidence.High));
        }

        [Test]
        public void Slow_simulation_with_missing_gpu_does_not_infer_gpu_pressure()
        {
            var result = Classify(Available("simulation.efficiency", 0.54),
                NamedMetricValue.Unavailable("gpu.frame.ms", "GPU timer absent"));
            Assert.That(result.Select(x => x.Category), Is.EqualTo(new[] { BottleneckCategory.SimulationCpu }));
        }

        [Test]
        public void Simultaneous_gpu_and_simulation_pressure_produce_independent_observations()
        {
            var result = Classify(Available("frame.p95.ms", 28), Available("gpu.frame.ms", 25),
                Available("simulation.efficiency", 0.45));
            Assert.That(result.Select(x => x.Category), Is.EquivalentTo(new[]
            { BottleneckCategory.RenderingGpu, BottleneckCategory.SimulationCpu }));
        }

        [Test]
        public void Indirect_gpu_timing_cannot_create_high_confidence_gpu_diagnosis()
        {
            var result = Classify(Available("frame.p95.ms", 28),
                NamedMetricValue.Available("gpu.frame.ms", 25, MetricConfidence.Indirect));
            Assert.That(result.Where(x => x.Category == BottleneckCategory.RenderingGpu)
                .All(x => x.Confidence != AdvisorConfidence.High), Is.True);
        }

        [Test]
        public void High_profiler_self_overhead_downgrades_confidence()
        {
            var result = Classify(Available("frame.p95.ms", 28), Available("gpu.frame.ms", 25),
                Available("profiler.overhead.share", 0.18));
            Assert.That(result.Single(x => x.Category == BottleneckCategory.RenderingGpu).Confidence,
                Is.EqualTo(AdvisorConfidence.Low));
        }

        [Test]
        public void Direct_slow_gpu_timer_without_total_frame_marker_supports_medium_confidence_only()
        {
            var result = Classify(NamedMetricValue.Unavailable("frame.p95.ms", "Frame marker absent"),
                Available("gpu.frame.ms", 25));
            var gpu = result.Single(x => x.Category == BottleneckCategory.RenderingGpu);
            Assert.That(gpu.Confidence, Is.EqualTo(AdvisorConfidence.Medium));
            Assert.That(gpu.EvidenceIds, Is.EqualTo(new[] { "gpu.frame.ms" }));
            Assert.That(Classify(NamedMetricValue.Unavailable("frame.p95.ms", "Frame marker absent"),
                NamedMetricValue.Available("gpu.frame.ms", 25, MetricConfidence.Indirect))
                .Any(x => x.Category == BottleneckCategory.RenderingGpu), Is.False);
        }

        [Test]
        public void Projector_converts_recorder_nanoseconds_and_leaves_absent_frame_unavailable()
        {
            var capture = new CaptureSession("capture", new CaptureTrigger(CaptureTriggerKind.Manual, 1, null), 8);
            capture.AddGlobalSample(new GlobalMetricsSnapshot(1, 1, 0.5,
                new System.Collections.Generic.Dictionary<string, RecorderReading>
                { ["Render\u001fGPU Frame Time"] = new RecorderReading(25000000, 1) },
                new System.Collections.Generic.Dictionary<string, string>
                { ["Render\u001fGPU Frame Time"] = "TimeNanoseconds" }));

            var evidence = new CaptureAdvisorEvidenceProjector().Project(capture);
            Assert.That(evidence.Find("gpu.frame.ms").Value, Is.EqualTo(25));
            Assert.That(evidence.Find("frame.p95.ms").Availability, Is.EqualTo(MetricAvailability.Unavailable));
            Assert.That(evidence.Find("simulation.efficiency").Value, Is.EqualTo(0.5));
        }

        [Test]
        public void Projector_keeps_system_timing_and_domain_metrics_as_separate_evidence()
        {
            var capture = new CaptureSession("capture", new CaptureTrigger(CaptureTriggerKind.Manual, 1, null), 8);
            var systems = new SystemTimingSnapshot();
            systems.AddSystem("TrafficSystem", 12, MetricConfidence.Managed);
            capture.SetSystemTiming(systems);
            capture.SetRuntimeSnapshots(new NamedMetricSnapshot(1, new[]
            { NamedMetricValue.Available("pathfinding.requests", 300, MetricConfidence.Indirect) }),
                new NamedMetricSnapshot(1, new[]
                { NamedMetricValue.Available("domain.citizens", 1000, MetricConfidence.Full) }));

            var evidence = new CaptureAdvisorEvidenceProjector().Project(capture);
            Assert.That(evidence.Find("system.TrafficSystem.ms").Value, Is.EqualTo(12));
            Assert.That(evidence.Find("pathfinding.requests").Confidence, Is.EqualTo(MetricConfidence.Indirect));
            Assert.That(evidence.Find("domain.citizens").Value, Is.EqualTo(1000));
        }

        private static NamedMetricValue Available(string id, double value)
            => NamedMetricValue.Available(id, value, MetricConfidence.Full);

        private static System.Collections.Generic.IReadOnlyList<BottleneckObservation> Classify(params NamedMetricValue[] metrics)
            => new BottleneckClassifier().Classify(new AdvisorEvidenceSnapshot(DateTime.UtcNow, metrics));
    }
}
