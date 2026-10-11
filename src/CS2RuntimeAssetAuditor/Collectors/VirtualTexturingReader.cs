using System;
using Colossal.IO.AssetDatabase.VirtualTexturing;
using CS2RuntimeAssetAuditor.Core;
using Unity.Entities;

namespace CS2RuntimeAssetAuditor.Collectors
{
    /// <summary>
    /// Reads the state TextureStreamingSystem already keeps (1.6.2f1): the working-set LOD bias that
    /// ManagedBatchSystem.CompleteVTRequests reads back from the GPU every frame, the VT mip bias and the request
    /// and material-load queues. Only property reads; no GPU readback or VT cache work is started here.
    /// </summary>
    internal sealed class VirtualTexturingReader
    {
        private readonly World _world;
        private TextureStreamingSystem? _system;

        public VirtualTexturingReader(World world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public VirtualTexturingReading Read()
        {
            try
            {
                // The game creates the system in its default world (GameManager reads it with GetExistingSystemManaged).
                _system ??= _world.IsCreated ? _world.GetExistingSystemManaged<TextureStreamingSystem>() : null;
                var system = _system;
                if (system == null)
                    return VirtualTexturingReading.Unavailable("The game's TextureStreamingSystem does not exist in this world.");
                return VirtualTexturingReading.FromSystem(
                    system.workingSetLodBias,
                    system.mipBias,
                    system.busyRequestsCount,
                    system.availableRequestsCount,
                    system.VTMaterialsLeftToLoadCount,
                    system.VTMaterialsDuplicatesToProcessCount);
            }
            catch (Exception ex)
            {
                _system = null;
                return VirtualTexturingReading.Unavailable("Reading the game's virtual-texturing state failed: " + ex.GetType().Name);
            }
        }
    }
}
