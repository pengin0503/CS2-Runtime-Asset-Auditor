using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Attribution;
using CS2RuntimeAssetAuditor.Core;
using Unity.Entities;

namespace CS2RuntimeAssetAuditor.Profiling
{
    public sealed class ProfilerCatalog
    {
        private readonly HarmonyPatchInspector _patchInspector;
        private readonly ModAttributor _modAttributor;
        private readonly World _world;

        public ProfilerCatalog(
            ModAttributor modAttributor = null,
            HarmonyPatchInspector patchInspector = null,
            World world = null)
        {
            _modAttributor = modAttributor ?? new ModAttributor(null);
            _patchInspector = patchInspector ?? HarmonyPatchInspector.TryCreate();
            _world = world;
        }

        public SystemCatalogDiscoveryResult Discover()
        {
            var types = RuntimeTypeDiscovery.EnumerateWithStatus(
                AppDomain.CurrentDomain.GetAssemblies(),
                out var isComplete,
                out var error);
            var systemBase = typeof(ComponentSystemBase);

            var systems = types
                .Where(type => type != null && !type.IsAbstract && systemBase.IsAssignableFrom(type))
                .Select(CreateDescriptor)
                .OrderBy(descriptor => descriptor.AssemblyName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(descriptor => descriptor.FullTypeName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return isComplete
                ? SystemCatalogDiscoveryResult.Complete(systems)
                : SystemCatalogDiscoveryResult.Incomplete(systems, error);
        }

        private SystemDescriptor CreateDescriptor(Type type)
        {
            var assemblyName = type.Assembly.GetName().Name ?? string.Empty;
            var sourceKind = AssemblyAttributor.ClassifyName(assemblyName);
            var modName = sourceKind == SystemSourceKind.Mod ? _modAttributor.Resolve(assemblyName) : null;

            // Patches are method-specific; their discovery is populated when the parameterless ECS update method is inspectable.
            var onUpdate = UpdateMethodResolver.Resolve(type);
            var patchOwners = onUpdate != null
                ? _patchInspector.GetPatchOwners(onUpdate)
                : Array.Empty<PatchOwnerInfo>();

            var profilerMarkerName = TryGetProfilerMarkerName(type, out var systemIsLive);
            return new SystemDescriptor(
                type.FullName ?? type.Name,
                assemblyName,
                sourceKind,
                modName,
                MetricConfidence.Unavailable,
                patchOwners,
                profilerMarkerName: profilerMarkerName,
                // A world-bound catalog is runtime evidence. A live system whose marker-name API is
                // unavailable may use strict full-type-name compatibility; inactive catalog types may not.
                allowLegacyProfilerMarkerMatching: _world == null
                    || RuntimeMarkerIdentityPolicy.AllowStrictFullTypeFallback(systemIsLive, profilerMarkerName),
                // ComponentSystemGroup.Update is an inclusive container around child system updates.
                // Keep its timing visible, but downstream additive totals must not count it again.
                isAggregateContainer: typeof(ComponentSystemGroup).IsAssignableFrom(type));
        }

        private string TryGetProfilerMarkerName(Type type, out bool systemIsLive)
        {
            systemIsLive = false;
            if (_world == null || !_world.IsCreated || type == null)
                return null;

            try
            {
                var system = _world.GetExistingSystemManaged(type);
                if (system == null)
                    return null;

                systemIsLive = true;
                try
                {
                    return EntityManager.EntityManagerDebug.GetSystemProfilerMarkerName(
                        _world,
                        system.SystemHandle);
                }
                catch
                {
                    // The system is known live even when a game/Entities build cannot expose its marker name.
                    return null;
                }
            }
            catch
            {
                // A catalogued type may not be instantiated in this World. Do not guess its marker name.
                return null;
            }
        }
    }
}
