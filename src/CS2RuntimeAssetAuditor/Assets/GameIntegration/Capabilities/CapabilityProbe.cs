using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Assets.Core;
using CS2RuntimeAssetAuditor.Assets.Core.Capabilities;
using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Game.Vehicles;
using Unity.Entities;
using UnityEngine;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Capabilities
{
    public static class CapabilityProbe
    {
        public static CapabilityReport Probe(World world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var capabilities = new List<CapabilityStatus>
            {
                ProbeIndependently(CapabilityId.PrefabCatalog, () => ProbePrefabCatalog(world)),
                ProbeIndependently(CapabilityId.ObjectCensus, () => ProbeObjectCensus(world)),
                ProbeIndependently(CapabilityId.NetworkEdgeCensus, () => ProbeNetworkEdgeCensus(world)),
                new CapabilityStatus(CapabilityId.GeometryMetadata, CapabilityState.Supported, "geometry_metadata_reader_available"),
                new CapabilityStatus(CapabilityId.SubmeshMetadata, CapabilityState.Supported, "topology_aware_submesh_reader_available"),
                new CapabilityStatus(CapabilityId.SurfaceMetadata, CapabilityState.Supported, "surface_metadata_reader_available"),
                new CapabilityStatus(CapabilityId.TextureMetadata, CapabilityState.Supported, "texture_metadata_reader_available"),
                new CapabilityStatus(CapabilityId.ShaderDeepInspection, CapabilityState.Supported, "selected_render_prefab_deep_inspection_available"),
                new CapabilityStatus(CapabilityId.RuntimeGpuResidency, CapabilityState.Unsupported, "runtime_gpu_residency_is_out_of_scope")
            };
            var version = Application.version;
            if (string.IsNullOrWhiteSpace(version)) version = ProjectInfo.TargetGameVersion;
            return new CapabilityReport(version, CompatibilityState.Untested, capabilities);
        }

        private static CapabilityStatus ProbePrefabCatalog(World world) =>
            world.GetExistingSystemManaged<PrefabSystem>() != null
                ? new CapabilityStatus(CapabilityId.PrefabCatalog, CapabilityState.Supported, "prefab_system_available")
                : new CapabilityStatus(CapabilityId.PrefabCatalog, CapabilityState.Degraded, "prefab_system_not_present_in_current_world");

        private static CapabilityStatus ProbeObjectCensus(World world)
        {
            var excluded = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Overridden>() };
            var common = new[] { ComponentType.ReadOnly<Game.Objects.Object>(), ComponentType.ReadOnly<PrefabRef>() };
            ProbeQuery(world, common, null, new[] { excluded[0], excluded[1], excluded[2], ComponentType.ReadOnly<Owner>(), ComponentType.ReadOnly<Controller>() });
            ProbeQuery(world, common, new[] { ComponentType.ReadOnly<Owner>(), ComponentType.ReadOnly<Controller>() }, excluded);
            return new CapabilityStatus(CapabilityId.ObjectCensus, CapabilityState.Supported, "object_profile_v1_queries_available");
        }

        private static CapabilityStatus ProbeNetworkEdgeCensus(World world)
        {
            ProbeQuery(world, new[] { ComponentType.ReadOnly<Game.Net.Edge>(), ComponentType.ReadOnly<PrefabRef>() }, null,
                new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Overridden>(), ComponentType.ReadOnly<Owner>(), ComponentType.ReadOnly<Controller>() });
            return new CapabilityStatus(CapabilityId.NetworkEdgeCensus, CapabilityState.Supported, "network_profile_v1_query_available");
        }

        private static void ProbeQuery(World world, ComponentType[] all, ComponentType[]? any, ComponentType[] none)
        {
            var descriptor = new EntityQueryDesc { All = all, Any = any, None = none };
            var query = world.EntityManager.CreateEntityQuery(new[] { descriptor });
            query.Dispose();
        }

        private static CapabilityStatus ProbeIndependently(CapabilityId capability, Func<CapabilityStatus> probe)
        {
            try { return probe(); }
            catch (Exception exception) { return new CapabilityStatus(capability, CapabilityState.Degraded, "probe_failed_" + exception.GetType().Name); }
        }
    }
}
