#nullable enable
using System;
using System.Reflection;

namespace CS2RuntimeProfiler.Core
{
    internal static class UpdateMethodResolver
    {
        private const BindingFlags UpdateMethodFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static MethodInfo? Resolve(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return type.GetMethod(
                "OnUpdate",
                UpdateMethodFlags,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
        }
    }
}
