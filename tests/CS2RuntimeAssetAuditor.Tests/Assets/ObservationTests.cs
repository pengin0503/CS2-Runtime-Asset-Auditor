using System;
using NUnit.Framework;
using CS2RuntimeAssetAuditor.Assets.Core.Capabilities;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    [TestFixture]
    public sealed class ObservationTests
    {
        private static readonly DateTimeOffset CapturedAt = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);

        [Test]
        public void Available_zero_is_distinct_from_not_scanned()
        {
            var zero = Observation<int>.FromValue(0, ObservationOrigin.Ecs, CapturedAt);
            var omitted = Observation<int>.Unavailable(Availability.NotScanned, ObservationOrigin.Ecs, CapturedAt);

            Assert.That(zero.Availability, Is.EqualTo(Availability.Available));
            Assert.That(zero.HasValue, Is.True);
            Assert.That(zero.Value, Is.EqualTo(0));
            Assert.That(omitted.Availability, Is.EqualTo(Availability.NotScanned));
            Assert.That(omitted.HasValue, Is.False);
            Assert.Throws<InvalidOperationException>(() => _ = omitted.Value);
        }

        [Test]
        public void Unsupported_observation_does_not_expose_a_fabricated_value()
        {
            var unsupported = Observation<int>.Unavailable(
                Availability.Unsupported,
                ObservationOrigin.GameAssembly,
                CapturedAt);

            Assert.That(unsupported.HasValue, Is.False);
            Assert.That(unsupported.Availability, Is.EqualTo(Availability.Unsupported));
            Assert.Throws<InvalidOperationException>(() => _ = unsupported.Value);
        }

        [Test]
        public void Capability_report_keeps_per_feature_states_independent()
        {
            var report = new CapabilityReport(
                "1.6.2f1",
                CompatibilityState.Untested,
                new[]
                {
                    new CapabilityStatus(CapabilityId.PrefabCatalog, CapabilityState.Supported),
                    new CapabilityStatus(CapabilityId.NetworkEdgeCensus, CapabilityState.Degraded, "network exclusions unavailable"),
                    new CapabilityStatus(CapabilityId.RuntimeGpuResidency, CapabilityState.Unsupported)
                });

            Assert.That(report.GameVersion, Is.EqualTo("1.6.2f1"));
            Assert.That(report.Compatibility, Is.EqualTo(CompatibilityState.Untested));
            Assert.That(report.TryGet(CapabilityId.PrefabCatalog, out var prefab), Is.True);
            Assert.That(prefab.State, Is.EqualTo(CapabilityState.Supported));
            Assert.That(report.TryGet(CapabilityId.NetworkEdgeCensus, out var network), Is.True);
            Assert.That(network.State, Is.EqualTo(CapabilityState.Degraded));
            Assert.That(report.TryGet(CapabilityId.RuntimeGpuResidency, out var gpu), Is.True);
            Assert.That(gpu.State, Is.EqualTo(CapabilityState.Unsupported));
        }
    }
}
