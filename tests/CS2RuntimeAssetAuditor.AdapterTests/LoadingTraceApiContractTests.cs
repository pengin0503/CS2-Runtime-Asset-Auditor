using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.AdapterTests
{
    // Game and Unity API the loading trace reads (LoadingMetricsBehaviour, DiagnosticSessionSystem). CI does not
    // compile game-dependent code, so these contracts are what catches a signature change in a game update.
    [TestFixture]
    public sealed class LoadingTraceApiContractTests
    {
        private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
        private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private MetadataLoadContext? _metadata;
        private string _managedPath = string.Empty;

        [SetUp]
        public void SetUp()
        {
            var managedPath = Environment.GetEnvironmentVariable("CSII_MANAGEDPATH");
            if (string.IsNullOrWhiteSpace(managedPath))
                managedPath = Environment.GetEnvironmentVariable("CSII_TOOLPATH");
            if (string.IsNullOrWhiteSpace(managedPath) || !Directory.Exists(managedPath))
                Assert.Fail("Set CSII_MANAGEDPATH or CSII_TOOLPATH to the local CS2 managed reference directory.");
            _managedPath = managedPath!;

            var assemblyPaths = Directory.GetFiles(_managedPath, "*.dll").ToList();
            var frameworkPath = Environment.GetEnvironmentVariable("CS2RUNTIME_NET48_REFERENCE_PATH");
            if (!string.IsNullOrWhiteSpace(frameworkPath) && Directory.Exists(frameworkPath))
                assemblyPaths.AddRange(Directory.GetFiles(frameworkPath, "*.dll"));
            else
            {
                var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location);
                if (!string.IsNullOrWhiteSpace(runtimePath) && Directory.Exists(runtimePath))
                    assemblyPaths.AddRange(Directory.GetFiles(runtimePath, "*.dll"));
            }

            var platformPaths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
                .Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)
                .Where(path => !Path.GetFileName(path).Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase));
            _metadata = new MetadataLoadContext(
                new PathAssemblyResolver(assemblyPaths.Concat(platformPaths).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()),
                "mscorlib");
        }

        [TearDown]
        public void TearDown()
        {
            _metadata?.Dispose();
            _metadata = null;
        }

        [Test]
        public void Unity_profiler_exposes_the_sampled_memory_counters_as_int64()
        {
            var profiler = ApiType("UnityEngine.CoreModule", "UnityEngine.Profiling.Profiler");
            foreach (var name in new[] { "GetTotalAllocatedMemoryLong", "GetAllocatedMemoryForGraphicsDriver" })
            {
                var method = profiler.GetMethod(name, PublicStatic, null, Type.EmptyTypes, null);
                Assert.That(method?.ReturnType.FullName, Is.EqualTo("System.Int64"), name);
            }
        }

        [Test]
        public void Global_asset_database_exposes_count_and_aggregate_cache_state()
        {
            var database = ApiType("Colossal.IO.AssetDatabase", "Colossal.IO.AssetDatabase.AssetDatabase");
            var global = database.GetProperty("global", PublicStatic);
            Assert.That(global?.GetMethod?.IsPublic, Is.True, "AssetDatabase.global");
            var type = global!.PropertyType;
            Assert.Multiple(() =>
            {
                Assert.That(FindProperty(type, "count")?.PropertyType.FullName, Is.EqualTo("System.Int32"), "count");
                Assert.That(FindProperty(type, "isCached")?.PropertyType.FullName, Is.EqualTo("System.Boolean"), "isCached");
            });
        }

        [Test]
        public void Game_loaded_hook_receives_a_context_with_the_load_purpose()
        {
            var systemBase = ApiType("Game", "Game.GameSystemBase");
            var hook = systemBase.GetMethods(AnyInstance).SingleOrDefault(method => method.Name == "OnGameLoaded");
            Assert.That(hook, Is.Not.Null);
            Assert.That(hook!.IsVirtual && (hook.IsFamily || hook.IsFamilyOrAssembly), Is.True, "OnGameLoaded must be protected virtual");
            var parameters = hook.GetParameters();
            Assert.That(parameters.Select(parameter => parameter.ParameterType.FullName),
                Is.EqualTo(new[] { "Colossal.Serialization.Entities.Context" }));
            var context = parameters[0].ParameterType;
            var purpose = (MemberInfo?)context.GetProperty("purpose", PublicInstance) ?? context.GetField("purpose", PublicInstance);
            Assert.That(purpose, Is.Not.Null, "Context.purpose");
            var purposeType = purpose is PropertyInfo property ? property.PropertyType : ((FieldInfo)purpose!).FieldType;
            Assert.That(purposeType.FullName, Is.EqualTo("Colossal.Serialization.Entities.Purpose"));
            Assert.That(purposeType.GetField("LoadGame"), Is.Not.Null, "Purpose.LoadGame distinguishes a restored save");
        }

        [Test]
        public void Game_mode_names_used_in_interruption_reasons_exist()
        {
            var mode = ApiType("Game", "Game.GameMode");
            foreach (var name in new[] { "Game", "MainMenu", "Editor" })
                Assert.That(mode.GetField(name), Is.Not.Null, name);
        }

        private static PropertyInfo? FindProperty(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var property = current.GetProperty(name, PublicInstance | BindingFlags.DeclaredOnly);
                if (property != null) return property;
            }
            return type.GetInterfaces().Select(item => item.GetProperty(name, PublicInstance)).FirstOrDefault(item => item != null);
        }

        private Type ApiType(string assemblyName, string typeName)
        {
            var path = Path.Combine(_managedPath, assemblyName + ".dll");
            Assert.That(File.Exists(path), Is.True, assemblyName + ".dll must exist in the managed reference directory.");
            return _metadata!.LoadFromAssemblyPath(path).GetType(typeName, throwOnError: true)!;
        }
    }
}
