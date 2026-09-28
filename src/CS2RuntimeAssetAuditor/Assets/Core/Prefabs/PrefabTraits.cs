using System;

namespace CS2RuntimeAssetAuditor.Assets.Core.Prefabs
{
    [Flags]
    public enum PrefabTraits
    {
        None = 0,
        Building = 1 << 0,
        ServiceBuilding = 1 << 1,
        Prop = 1 << 2,
        Tree = 1 << 3,
        Vehicle = 1 << 4,
        Network = 1 << 5,
        RenderOnly = 1 << 6
    }
}
