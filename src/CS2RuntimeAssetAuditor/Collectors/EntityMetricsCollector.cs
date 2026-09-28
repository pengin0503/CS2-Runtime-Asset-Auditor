using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Core;

namespace CS2RuntimeAssetAuditor.Collectors
{
    public interface IEntityDomainCountSource
    {
        bool TryGetCount(string componentTypeName, out int count, out string reason);
    }

    public sealed class EntityMetricsCollector : IMetricCollector
    {
        private static readonly IReadOnlyDictionary<string, string> VerifiedDomains =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["citizens"] = "Game.Citizens.Citizen",
                ["households"] = "Game.Citizens.Household",
                ["touristHouseholds"] = "Game.Citizens.TouristHousehold",
                ["vehicles"] = "Game.Vehicles.Vehicle",
                ["publicTransportVehicles"] = "Game.Vehicles.PublicTransport",
                ["cargoTransportVehicles"] = "Game.Vehicles.CargoTransport",
                ["serviceVehicles"] = "Game.Vehicles.ServiceVehicleUnion"
            };

        private readonly IEntityDomainCountSource _source;
        private readonly double _samplePeriodSeconds;
        private double _nextSampleAt = double.NegativeInfinity;

        public EntityMetricsCollector(IEntityDomainCountSource source, double samplePeriodSeconds = 2d)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            if (samplePeriodSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(samplePeriodSeconds));
            _samplePeriodSeconds = samplePeriodSeconds;
            Latest = new NamedMetricSnapshot(0, Array.Empty<NamedMetricValue>());
        }

        public string Name => "EntityDomains";
        public NamedMetricSnapshot Latest { get; private set; }

        public void Sample(double timestampSeconds)
        {
            if (timestampSeconds < _nextSampleAt)
                return;
            _nextSampleAt = timestampSeconds + _samplePeriodSeconds;

            var metrics = new List<NamedMetricValue>();
            foreach (var pair in VerifiedDomains)
                metrics.Add(ReadCount(pair.Key, pair.Value));

            Latest = new NamedMetricSnapshot(timestampSeconds, metrics);
        }

        private NamedMetricValue ReadCount(string id, string componentTypeName)
        {
            try
            {
                if (_source.TryGetCount(componentTypeName, out var count, out var reason))
                    return NamedMetricValue.Available(id, Math.Max(0, count), MetricConfidence.Indirect);
                return NamedMetricValue.Unavailable(id, reason ?? $"unsupported: {componentTypeName}");
            }
            catch (Exception ex)
            {
                return NamedMetricValue.Unavailable(id, $"Counting {componentTypeName} failed: {ex.Message}");
            }
        }
    }
}
