using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CS2RuntimeAssetAuditor.Core
{
    public static class RuntimeTypeDiscovery
    {
        public static IReadOnlyList<Type> Enumerate(
            IEnumerable<Assembly> assemblies,
            Func<Assembly, Type[]> typeProvider = null)
        {
            return EnumerateWithStatus(assemblies, out _, out _, typeProvider);
        }

        public static IReadOnlyList<Type> EnumerateWithStatus(
            IEnumerable<Assembly> assemblies,
            out bool isComplete,
            out string error,
            Func<Assembly, Type[]> typeProvider = null)
        {
            if (assemblies == null)
                throw new ArgumentNullException(nameof(assemblies));

            typeProvider ??= assembly => assembly.GetTypes();
            var result = new List<Type>();
            var errors = new List<string>();

            foreach (var assembly in assemblies)
            {
                if (assembly == null)
                    continue;

                try
                {
                    var types = typeProvider(assembly);
                    if (types != null)
                        result.AddRange(types.Where(type => type != null));
                }
                catch (ReflectionTypeLoadException ex)
                {
                    result.AddRange(ex.Types.Where(type => type != null));
                    errors.Add(FormatFailure(assembly, GetLoaderExceptionMessage(ex)));
                }
                catch (Exception ex)
                {
                    errors.Add(FormatFailure(assembly, ex.GetBaseException().Message));
                }
            }

            isComplete = errors.Count == 0;
            error = isComplete ? null : string.Join("; ", errors);
            return result;
        }

        private static string GetLoaderExceptionMessage(ReflectionTypeLoadException exception)
        {
            var messages = (exception.LoaderExceptions ?? Array.Empty<Exception>())
                .Where(error => error != null && !string.IsNullOrWhiteSpace(error.GetBaseException().Message))
                .Select(error => error.GetBaseException().Message)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return messages.Length == 0 ? exception.GetBaseException().Message : string.Join("; ", messages);
        }

        private static string FormatFailure(Assembly assembly, string message)
        {
            string name;
            try
            {
                name = assembly.GetName().Name ?? assembly.FullName ?? "unknown assembly";
            }
            catch
            {
                name = assembly.FullName ?? "unknown assembly";
            }

            return "Failed to enumerate types from '" + name + "': " + (message ?? "unknown error");
        }
    }
}
