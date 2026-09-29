using System;
using System.Collections.Generic;
using System.Diagnostics;
using CS2RuntimeAssetAuditor.Core;
using CS2RuntimeAssetAuditor.Lifecycle;
using Game;
using Game.Citizens;
using Game.Common;
using Game.Pathfind;
using Game.Vehicles;
using Unity.Entities;

namespace CS2RuntimeAssetAuditor.Collectors
{
    /// <summary>
    /// Read-only runtime bridge for domain metrics. Queries are created once and sampled on a throttled cadence.
    /// No simulation data is written and no job dependency is completed solely for profiling.
    /// </summary>
    public partial class DomainMetricsSystem : GameSystemBase, IEntityDomainCountSource
    {
        private const double UpdatePeriodSeconds = 0.5d;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Dictionary<string, EntityQuery> _queries = new Dictionary<string, EntityQuery>(StringComparer.Ordinal);
        private EntityMetricsCollector _entities;
        private PathfindingCollector _pathfinding;
        private double _nextUpdateAt;

        public new EntityMetricsCollector Entities => _entities;
        public PathfindingCollector Pathfinding => _pathfinding;

        protected override void OnCreate()
        {
            base.OnCreate();

            _queries["Game.Citizens.Citizen"] = CreateReadOnlyQuery<Citizen>();
            _queries["Game.Citizens.Household"] = CreateReadOnlyQuery<Household>();
            _queries["Game.Citizens.TouristHousehold"] = CreateReadOnlyQuery<TouristHousehold>();
            _queries["Game.Vehicles.Vehicle"] = CreateReadOnlyQuery<Vehicle>();
            _queries["Game.Vehicles.PublicTransport"] = CreateReadOnlyQuery<PublicTransport>();
            _queries["Game.Vehicles.CargoTransport"] = CreateReadOnlyQuery<CargoTransport>();
            _queries["Game.Vehicles.ServiceVehicleUnion"] = GetEntityQuery(new EntityQueryDesc
            {
                Any = new[]
                {
                    ComponentType.ReadOnly<Ambulance>(),
                    ComponentType.ReadOnly<Hearse>(),
                    ComponentType.ReadOnly<MaintenanceVehicle>(),
                    ComponentType.ReadOnly<FireEngine>(),
                    ComponentType.ReadOnly<GarbageTruck>(),
                    ComponentType.ReadOnly<PoliceCar>(),
                    ComponentType.ReadOnly<PostVan>(),
                    ComponentType.ReadOnly<PrisonerTransport>()
                },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Game.Tools.Temp>() }
            });

            _entities = new EntityMetricsCollector(this, samplePeriodSeconds: 2d);
            var queueSystem = World.GetOrCreateSystemManaged<PathfindQueueSystem>();
            var resultSystem = World.GetOrCreateSystemManaged<PathfindResultSystem>();
            _pathfinding = new PathfindingCollector(
                queueSystem,
                () => ReadQueryStats(resultSystem),
                () => resultSystem.pendingRequestCount);
        }

        // PathfindResultSystem.queryStats is updated on the main thread by PathfindResultSystem itself, so reading it
        // from this UI-phase system does not race with it. It holds one entry per requesting system, query type and
        // origin/destination pair (a few dozen).
        private static IReadOnlyList<PathfindQueryStat> ReadQueryStats(PathfindResultSystem resultSystem)
        {
            var stats = resultSystem.queryStats;
            if (stats == null)
                return Array.Empty<PathfindQueryStat>();
            var result = new List<PathfindQueryStat>(stats.Count);
            foreach (var pair in stats)
            {
                result.Add(new PathfindQueryStat(
                    pair.Key.m_System?.GetType().FullName ?? "unknown",
                    pair.Key.m_QueryType.ToString(),
                    pair.Key.m_OriginType.ToString(),
                    pair.Key.m_DestinationType.ToString(),
                    pair.Value.m_QueryCount,
                    pair.Value.m_SuccessCount,
                    pair.Value.m_GraphTraversal));
            }
            return result;
        }

        protected override void OnUpdate()
        {
            var start = ModUpdateCost.Start();
            try { RunUpdate(); }
            finally { ModUpdateCost.Stop(nameof(DomainMetricsSystem), start); }
        }

        private void RunUpdate()
        {
            if (Mod.Settings != null && !Mod.Settings.EnableMonitoring)
                return;

            var now = _clock.Elapsed.TotalSeconds;
            if (now < _nextUpdateAt)
                return;

            _nextUpdateAt = now + UpdatePeriodSeconds;
            _pathfinding?.Sample(now);
            _entities?.Sample(now);
        }

        public bool TryGetCount(string componentTypeName, out int count, out string reason)
        {
            if (string.IsNullOrWhiteSpace(componentTypeName) || !_queries.TryGetValue(componentTypeName, out var query))
            {
                count = 0;
                reason = $"unsupported: no verified EntityQuery is registered for {componentTypeName}.";
                return false;
            }

            try
            {
                count = query.CalculateEntityCount();
                reason = null;
                return true;
            }
            catch (Exception ex)
            {
                count = 0;
                reason = $"EntityQuery count failed for {componentTypeName}: {ex.Message}";
                return false;
            }
        }

        private EntityQuery CreateReadOnlyQuery<T>() where T : unmanaged, IComponentData
        {
            return GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<T>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Game.Tools.Temp>() }
            });
        }
    }
}
