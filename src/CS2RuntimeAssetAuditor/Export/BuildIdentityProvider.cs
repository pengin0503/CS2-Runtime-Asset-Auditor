using System;
using System.Reflection;

namespace CS2RuntimeAssetAuditor.Export
{
    public static class BuildIdentityProvider
    {
        public const string Unknown = "unknown";

        public static string Current => Resolve(typeof(BuildIdentityProvider).Assembly);

        public static string Resolve(Assembly assembly)
        {
            if (assembly == null)
                return Unknown;

            try
            {
                var module = assembly.ManifestModule;
                var id = module?.ModuleVersionId ?? Guid.Empty;
                return id == Guid.Empty ? Unknown : id.ToString("N");
            }
            catch
            {
                return Unknown;
            }
        }
    }
}
