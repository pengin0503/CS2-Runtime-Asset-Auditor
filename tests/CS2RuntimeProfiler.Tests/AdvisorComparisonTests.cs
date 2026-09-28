using System;
using System.Linq;
using CS2RuntimeProfiler.Core;
using CS2RuntimeProfiler.Core.Advisor;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests
{
    [TestFixture]
    public sealed class AdvisorComparisonTests
    {
        [TestCase(25, 20, ComparisonState.Improved)]
        [TestCase(25, 24.8, ComparisonState.NoMaterialChange)]
        [TestCase(25, 30, ComparisonState.Regressed)]
        public void Frame_time_differences_use_a_material_threshold(double before, double after, ComparisonState expected)
        {
            var result = Compare("frame.p95.ms", before, after);
            Assert.That(result.Metrics.Single().State, Is.EqualTo(expected));
        }

        [Test]
        public void Higher_simulation_efficiency_is_an_improvement()
        {
            Assert.That(Compare("simulation.efficiency", 0.62, 0.9).Metrics.Single().State,
                Is.EqualTo(ComparisonState.Improved));
        }

        [Test]
        public void Unavailable_or_changed_metric_definition_is_not_comparable()
        {
            var baseline = Evidence(NamedMetricValue.Available("gpu.frame.ms", 25, MetricConfidence.Full, "Milliseconds"));
            var unavailable = Evidence(NamedMetricValue.Unavailable("gpu.frame.ms", "GPU timer missing"));
            var changedUnits = Evidence(NamedMetricValue.Available("gpu.frame.ms", 20000,
                MetricConfidence.Full, "Microseconds"));
            var changedCapability = Evidence(NamedMetricValue.Available("gpu.frame.ms", 20,
                MetricConfidence.Indirect, "Milliseconds"));

            foreach (var followUp in new[] { unavailable, changedUnits, changedCapability })
                Assert.That(AdvisorComparison.Compare(baseline, followUp, Array.Empty<SettingChange>())
                    .Metrics.Single().State, Is.EqualTo(ComparisonState.NotComparable));
        }

        [Test]
        public void Multiple_setting_changes_are_recorded_without_single_setting_causality()
        {
            var session = new SettingChangeSession();
            session.RecordApplied("shadow", "High", "Medium", DateTime.UtcNow);
            session.RecordApplied("volumetrics", "High", "Medium", DateTime.UtcNow);
            var result = AdvisorComparison.Compare(Evidence(NamedMetricValue.Available("frame.p95.ms", 25,
                MetricConfidence.Full, "Milliseconds")), Evidence(NamedMetricValue.Available("frame.p95.ms", 20,
                MetricConfidence.Full, "Milliseconds")), session.Changes);
            Assert.That(result.MultipleChanges, Is.True);
            Assert.That(result.ChangedSettingIds, Is.EquivalentTo(new[] { "shadow", "volumetrics" }));
            Assert.That(result.Metrics.Single().State, Is.EqualTo(ComparisonState.Improved));
        }

        private static AdvisorComparison Compare(string id, double before, double after)
            => AdvisorComparison.Compare(Evidence(NamedMetricValue.Available(id, before,
                    MetricConfidence.Full, id == "simulation.efficiency" ? "" : "Milliseconds")),
                Evidence(NamedMetricValue.Available(id, after,
                    MetricConfidence.Full, id == "simulation.efficiency" ? "" : "Milliseconds")),
                Array.Empty<SettingChange>());

        private static AdvisorEvidenceSnapshot Evidence(params NamedMetricValue[] metrics)
            => new AdvisorEvidenceSnapshot(DateTime.UtcNow, metrics);
    }
}
