using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Prefabs
{
    public static class PrefabClassifier
    {
        public static PrefabTraits Classify(
            bool isBuilding = false,
            bool isServiceBuilding = false,
            bool isProp = false,
            bool isTree = false,
            bool isVehicle = false,
            bool isNetwork = false,
            bool isRenderOnly = false,
            bool isBuildingExtension = false,
            bool isPlant = false)
        {
            var traits = PrefabTraits.None;
            if (isBuilding || isServiceBuilding)
                traits |= PrefabTraits.Building;
            if (isServiceBuilding)
                traits |= PrefabTraits.ServiceBuilding;
            if (isProp)
                traits |= PrefabTraits.Prop;
            if (isTree)
                traits |= PrefabTraits.Tree;
            if (isVehicle)
                traits |= PrefabTraits.Vehicle;
            if (isNetwork)
                traits |= PrefabTraits.Network;
            if (isRenderOnly)
                traits |= PrefabTraits.RenderOnly;
            if (isBuildingExtension)
                traits |= PrefabTraits.BuildingExtension;
            if (isPlant)
                traits |= PrefabTraits.Plant;
            return traits;
        }

        public static string GetTypeId(PrefabTraits traits)
        {
            if ((traits & PrefabTraits.Network) != 0)
                return "Network";
            if ((traits & PrefabTraits.Vehicle) != 0)
                return "Vehicle";
            if ((traits & PrefabTraits.ServiceBuilding) != 0)
                return "ServiceBuilding";
            if ((traits & PrefabTraits.Building) != 0)
                return "Building";
            if ((traits & PrefabTraits.BuildingExtension) != 0)
                return "BuildingExtension";
            if ((traits & PrefabTraits.Tree) != 0)
                return "Tree";
            if ((traits & PrefabTraits.Plant) != 0)
                return "Plant";
            if ((traits & PrefabTraits.Prop) != 0)
                return "Prop";
            if ((traits & PrefabTraits.RenderOnly) != 0)
                return "RenderOnly";
            return "Prefab";
        }
    }
}
