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
        // Query keys the count source must answer; DomainMetricsSystem registers its ECS queries under these names.
        public const string CitizenQuery = "Game.Citizens.Citizen";
        public const string HouseholdQuery = "Game.Citizens.Household";
        public const string TouristHouseholdQuery = "Game.Citizens.TouristHousehold";
        public const string VehicleQuery = "Game.Vehicles.Vehicle";
        public const string PublicTransportQuery = "Game.Vehicles.PublicTransport";
        public const string CargoTransportQuery = "Game.Vehicles.CargoTransport";
        /// <summary>Any of the city-service vehicle components (ambulance through prisoner transport).</summary>
        public const string ServiceVehicleUnionQuery = "Game.Vehicles.ServiceVehicleUnion";

        private static readonly IReadOnlyDictionary<string, string> VerifiedDomains =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["citizens"] = CitizenQuery,
                ["households"] = HouseholdQuery,
                ["touristHouseholds"] = TouristHouseholdQuery,
                ["vehicles"] = VehicleQuery,
                ["publicTransportVehicles"] = PublicTransportQuery,
                ["cargoTransportVehicles"] = CargoTransportQuery,
                ["serviceVehicles"] = ServiceVehicleUnionQuery
            };

        /// <summary>Every query key a sample asks the count source for.</summary>
        public static IEnumerable<string> RequiredQueries => VerifiedDomains.Values;

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

        /// <summary>Forgets the latest counts so the next city starts without them; the next sample is due at once.</summary>
        public void Reset()
        {
            _nextSampleAt = double.NegativeInfinity;
            Latest = new NamedMetricSnapshot(0, Array.Empty<NamedMetricValue>());
        }

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
