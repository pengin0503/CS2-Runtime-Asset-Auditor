using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.AdapterTests
{
    // The mod starts and closes diagnostic city sessions from these hooks; the game reuses one World across loads.
    [TestFixture]
    public sealed class LifecycleApiContractTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private MetadataLoadContext? _metadata;
        private Assembly? _game;

        [SetUp]
        public void SetUp()
        {
            var managedPath = Environment.GetEnvironmentVariable("CSII_MANAGEDPATH");
            if (string.IsNullOrWhiteSpace(managedPath))
                managedPath = Environment.GetEnvironmentVariable("CSII_TOOLPATH");
            if (string.IsNullOrWhiteSpace(managedPath) || !Directory.Exists(managedPath))
                Assert.Fail("Set CSII_MANAGEDPATH or CSII_TOOLPATH to the local CS2 managed reference directory.");

            var assemblyPaths = Directory.GetFiles(managedPath!, "*.dll").ToList();
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
            var distinctPaths = assemblyPaths.Concat(platformPaths)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _metadata = new MetadataLoadContext(new PathAssemblyResolver(distinctPaths), "mscorlib");
            var gamePath = Path.Combine(managedPath!, "Game.dll");
            Assert.That(File.Exists(gamePath), Is.True, "Game.dll must exist in the selected local managed reference directory.");
            _game = _metadata.LoadFromAssemblyPath(gamePath);
        }

        [TearDown]
        public void TearDown()
        {
            _metadata?.Dispose();
            _metadata = null;
            _game = null;
        }

        [TestCase("OnGamePreload")]
        [TestCase("OnGameLoadingComplete")]
        public void GameSystemBase_exposes_overridable_load_hooks_with_purpose_and_game_mode(string name)
        {
            var systemBase = _game!.GetType("Game.GameSystemBase", throwOnError: true)!;
            var hook = systemBase.GetMethods(AnyInstance).SingleOrDefault(method => method.Name == name);
            Assert.That(hook, Is.Not.Null, name);
            Assert.That(hook!.IsVirtual && (hook.IsFamily || hook.IsFamilyOrAssembly), Is.True, name + " must be protected virtual");
            var parameters = hook.GetParameters();
            Assert.That(parameters.Select(parameter => parameter.ParameterType.FullName),
                Is.EqualTo(new[] { "Colossal.Serialization.Entities.Purpose", "Game.GameMode" }));
            Assert.That(parameters[0].ParameterType.Assembly.GetName().Name, Is.EqualTo("Colossal.Core"),
                "Purpose must come from an assembly the mod project references.");
        }

        [Test]
        public void GameMode_defines_Game()
        {
            var mode = _game!.GetType("Game.GameMode", throwOnError: true)!;
            Assert.That(mode.GetField("Game"), Is.Not.Null);
        }
    }
}
