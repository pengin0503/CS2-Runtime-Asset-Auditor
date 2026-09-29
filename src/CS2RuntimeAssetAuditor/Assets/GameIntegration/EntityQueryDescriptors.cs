using System;
using Unity.Entities;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration
{
    internal static class EntityQueryDescriptors
    {
        /// <summary>
        /// Builds a query description whose component lists are never null. Unity.Entities 1.x (game 1.6.2f1)
        /// reads every list's Length in EntityQueryManager.ConvertToEntityQueryBuilder without a null check, and
        /// EntityQueryDesc defaults each list to an empty array, so assigning null makes CreateEntityQuery throw
        /// a NullReferenceException.
        /// </summary>
        public static EntityQueryDesc Create(ComponentType[]? all, ComponentType[]? any, ComponentType[]? none)
        {
            return new EntityQueryDesc
            {
                All = all ?? Array.Empty<ComponentType>(),
                Any = any ?? Array.Empty<ComponentType>(),
                None = none ?? Array.Empty<ComponentType>()
            };
        }
    }
}
