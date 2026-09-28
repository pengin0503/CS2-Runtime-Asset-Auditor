using System;
using System.Collections.Generic;

namespace CS2RuntimeProfiler.Core.Advisor
{
    public sealed class BottleneckClassifier
    {
        private const double SlowFrameMilliseconds = 20;
        private const double SlowGpuMilliseconds = 18;
        private const double LowSimulationEfficiency = 0.8;
        private const double HighProfilerOverheadShare = 0.1;

        public IReadOnlyList<BottleneckObservation> Classify(AdvisorEvidenceSnapshot evidence)
        {
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            var result = new List<BottleneckObservation>();
            var gpu = evidence.Find("gpu.frame.ms");
            var frame = evidence.Find("frame.p95.ms");
            var simulation = evidence.Find("simulation.efficiency");
            var triggerSimulation = evidence.Find("simulation.trigger.efficiency");
            var overhead = evidence.Find("profiler.overhead.share");
            var overheadHigh = overhead.Availability == MetricAvailability.Available
                && overhead.Value >= HighProfilerOverheadShare;

            if (gpu.Availability == MetricAvailability.Available && gpu.Value >= SlowGpuMilliseconds
                && frame.Availability == MetricAvailability.Available && frame.Value >= SlowFrameMilliseconds)
            {
                var confidence = gpu.Confidence == MetricConfidence.Indirect ? AdvisorConfidence.Low
                    : gpu.Confidence == MetricConfidence.Full && frame.Confidence == MetricConfidence.Full
                        ? AdvisorConfidence.High : AdvisorConfidence.Medium;
                if (overheadHigh) confidence = AdvisorConfidence.Low;
                result.Add(new BottleneckObservation(BottleneckCategory.RenderingGpu, BottleneckSeverity.High,
                    confidence, new[] { "frame.p95.ms", "gpu.frame.ms" }, "フレーム時間とGPU時間の両方で描画負荷の上昇を確認しました。"));
            }
            else if (gpu.Availability == MetricAvailability.Available && gpu.Value >= SlowGpuMilliseconds
                && gpu.Confidence == MetricConfidence.Full && frame.Availability == MetricAvailability.Unavailable)
            {
                result.Add(new BottleneckObservation(BottleneckCategory.RenderingGpu, BottleneckSeverity.High,
                    overheadHigh ? AdvisorConfidence.Low : AdvisorConfidence.Medium,
                    new[] { "gpu.frame.ms" }, "直接GPU時間が高い状態です。総フレーム時間は取得できないため確信度を抑えています。"));
            }

            if (simulation.Availability == MetricAvailability.Available && simulation.Value < LowSimulationEfficiency)
            {
                var confidence = overheadHigh ? AdvisorConfidence.Low : simulation.Confidence == MetricConfidence.Full
                    ? AdvisorConfidence.High : AdvisorConfidence.Medium;
                result.Add(new BottleneckObservation(BottleneckCategory.SimulationCpu, BottleneckSeverity.High,
                    confidence, new[] { "simulation.efficiency" }, "キャプチャ後半を含む計測窓でシミュレーション効率の低下が継続しています。"));
            }
            else if (triggerSimulation.Availability == MetricAvailability.Available
                && triggerSimulation.Value < LowSimulationEfficiency)
            {
                var evidenceIds = simulation.Availability == MetricAvailability.Available
                    ? new[] { "simulation.trigger.efficiency", "simulation.efficiency" }
                    : new[] { "simulation.trigger.efficiency" };
                result.Add(new BottleneckObservation(BottleneckCategory.SimulationCpu, BottleneckSeverity.Medium,
                    overheadHigh ? AdvisorConfidence.Low : AdvisorConfidence.Medium,
                    evidenceIds,
                    "トリガー時にはシミュレーション効率が低下していましたが、計測窓では継続的な低下を確認できませんでした。"));
            }

            // Low pressure is evidence of potential headroom, not proof that increasing quality is safe.
            if (result.Count == 0 && gpu.Availability == MetricAvailability.Available && gpu.Value < 10
                && frame.Availability == MetricAvailability.Available && frame.Value < 12
                && simulation.Availability == MetricAvailability.Available && simulation.Value >= 0.95)
            {
                result.Add(new BottleneckObservation(BottleneckCategory.RenderingGpu, BottleneckSeverity.Low,
                    overheadHigh ? AdvisorConfidence.Low : AdvisorConfidence.Medium,
                    new[] { "gpu.frame.ms", "frame.p95.ms", "simulation.efficiency" },
                    "現在の計測条件では描画負荷に余裕があります。設定変更後は再診断してください。"));
            }
            return result;
        }
    }
}
