using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Collectors;
using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class EntityMetricsCollectorTests
{
    private sealed class FakeSource : IEntityDomainCountSource
    {
        public readonly Dictionary<string, int> Counts = new();
        public int Calls { get; private set; }

        public bool TryGetCount(string componentTypeName, out int count, out string reason)
        {
            Calls++;
            if (Counts.TryGetValue(componentTypeName, out count))
            {
                reason = null;
                return true;
            }

            count = 0;
            reason = $"unsupported: {componentTypeName}";
            return false;
        }
    }

    [Test]
    public void Known_domains_are_indirect_counts_and_sampling_is_throttled()
    {
        var source = new FakeSource();
        source.Counts["Game.Citizens.Citizen"] = 1234;
        source.Counts["Game.Citizens.Household"] = 456;
        var collector = new EntityMetricsCollector(source, samplePeriodSeconds: 2);

        collector.Sample(0);
        var callsAfterFirstSample = source.Calls;
        collector.Sample(1);

        Assert.That(collector.Latest.Get("citizens").Value, Is.EqualTo(1234));
        Assert.That(collector.Latest.Get("citizens").Confidence, Is.EqualTo(MetricConfidence.Indirect));
        Assert.That(collector.Latest.Get("households").Value, Is.EqualTo(456));
        Assert.That(source.Calls, Is.EqualTo(callsAfterFirstSample));

        collector.Sample(2);
        Assert.That(source.Calls, Is.GreaterThan(callsAfterFirstSample));
    }

    [Test]
    public void Verified_service_vehicle_union_is_exposed_as_indirect_count()
    {
        var source = new FakeSource();
        source.Counts["Game.Vehicles.ServiceVehicleUnion"] = 37;
        var collector = new EntityMetricsCollector(source, samplePeriodSeconds: 2);

        collector.Sample(0);

        var service = collector.Latest.Get("serviceVehicles");
        Assert.That(service.Availability, Is.EqualTo(MetricAvailability.Available));
        Assert.That(service.Value, Is.EqualTo(37));
        Assert.That(service.Confidence, Is.EqualTo(MetricConfidence.Indirect));
    }

    [Test]
    public void Unsupported_domain_remains_unavailable_not_zero()
    {
        var collector = new EntityMetricsCollector(new FakeSource(), samplePeriodSeconds: 2);
        collector.Sample(0);

        var citizens = collector.Latest.Get("citizens");
        Assert.That(citizens.Availability, Is.EqualTo(MetricAvailability.Unavailable));
        Assert.That(citizens.Value, Is.Null);
        Assert.That(citizens.Reason, Does.Contain("unsupported"));
    }

    [Test]
    public void Every_domain_is_requested_under_the_query_keys_the_ecs_system_registers()
    {
        // DomainMetricsSystem registers one ECS query per RequiredQueries key; a source that answers exactly those
        // keys must make every domain available, including the service-vehicle union.
        var source = new FakeSource();
        var value = 1;
        foreach (var key in EntityMetricsCollector.RequiredQueries)
            source.Counts[key] = value++;
        var collector = new EntityMetricsCollector(source);

        collector.Sample(0);

        Assert.Multiple(() =>
        {
            Assert.That(collector.Latest.Metrics.Values.All(metric => metric.Availability == MetricAvailability.Available), Is.True);
            Assert.That(collector.Latest.Get("serviceVehicles").Value, Is.EqualTo(source.Counts[EntityMetricsCollector.ServiceVehicleUnionQuery]));
            Assert.That(EntityMetricsCollector.RequiredQueries, Does.Contain("Game.Vehicles.ServiceVehicleUnion"));
        });
    }
}
