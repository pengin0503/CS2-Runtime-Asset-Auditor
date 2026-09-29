using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.AdapterTests
{
    // Game API the review fixes (#14-#21) rely on. Each test names the behavior of the game build it depends on.
    [TestFixture]
    public sealed class GameIntegrationApiContractTests
    {
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

        // #14: materials are described from the SurfaceAsset's properties and its shared template material.
        [Test]
        public void Surface_asset_exposes_properties_and_template_material_without_instantiation()
        {
            var surface = ApiType("Colossal.IO.AssetDatabase", "Colossal.IO.AssetDatabase.SurfaceAsset");
            Assert.Multiple(() =>
            {
                Assert.That(surface.GetMethod("GetTemplateMaterial", PublicInstance, null, Type.EmptyTypes, null)?.ReturnType.FullName,
                    Is.EqualTo("UnityEngine.Material"));
                Assert.That(Parameters(surface.GetMethod("LoadProperties", PublicInstance)), Is.EqualTo(new[] { "System.Boolean" }));
                Assert.That(Parameters(surface.GetMethod("UnloadProperties", PublicInstance)), Is.EqualTo(new[] { "System.Boolean" }));
                foreach (var property in new[] { "isDataLoaded", "keywords", "isCurrentlyUsingVT", "materialTemplateHash", "textures" })
                    Assert.That(surface.GetProperty(property, PublicInstance)?.GetMethod?.IsPublic, Is.True, property);
            });
        }

        // #15: the synchronous copy only completes jobs writing the component and reads on the main thread.
        [Test]
        public void Entity_query_copies_component_data_synchronously()
        {
            var query = ApiType("Unity.Entities", "Unity.Entities.EntityQuery");
            var copy = query.GetMethods(PublicInstance).SingleOrDefault(method => method.Name == "ToComponentDataArray"
                && method.IsGenericMethodDefinition
                && Parameters(method).SequenceEqual(new[] { "Unity.Collections.AllocatorManager+AllocatorHandle" }));
            Assert.That(copy, Is.Not.Null);
            Assert.That(copy!.ReturnType.Name, Is.EqualTo("NativeArray`1"));
        }

        // #16: m_Items is a List; items before m_NextIndex are in flight, the rest wait for a worker.
        [Test]
        public void Pathfind_action_list_is_a_list_with_a_dispatch_index()
        {
            var queue = ApiType("Game", "Game.Pathfind.PathfindQueueSystem");
            var actionList = queue.GetNestedType("ActionList`1", BindingFlags.Public);
            Assert.That(actionList, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(actionList!.GetField("m_Items", PublicInstance)?.FieldType.Name, Is.EqualTo("List`1"));
                Assert.That(actionList.GetField("m_NextIndex", PublicInstance)?.FieldType.FullName, Is.EqualTo("System.Int32"));
                Assert.That(queue.GetField("m_PathfindActions", AnyInstance)?.FieldType.Name, Is.EqualTo("ActionList`1"));
            });
        }

        // #17: extensions and activity props derive from StaticObjectPrefab next to BuildingPrefab.
        [Test]
        public void Static_object_prefab_has_the_subclasses_the_classifier_handles()
        {
            var game = Load("Game");
            var staticObject = game.GetType("Game.Prefabs.StaticObjectPrefab", throwOnError: true)!;
            var direct = game.GetTypes()
                .Where(type => BaseTypeName(type) == staticObject.FullName)
                .Select(type => type.FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.That(direct, Is.EqualTo(new[]
            {
                "Game.Prefabs.ActivityPropPrefab",
                "Game.Prefabs.BuildingExtensionPrefab",
                "Game.Prefabs.BuildingPrefab"
            }), "A new StaticObjectPrefab subclass needs a classification decision.");
            Assert.That(game.GetType("Game.Prefabs.PlantObject"), Is.Not.Null);
            Assert.That(game.GetType("Game.Prefabs.TreeObject"), Is.Not.Null);
        }

        // #20: the global quality preset is reached through QualitySetting's level methods.
        [Test]
        public void Graphics_settings_expose_quality_levels_through_methods()
        {
            var graphics = ApiType("Game", "Game.Settings.GraphicsSettings");
            var quality = ApiType("Game", "Game.Settings.QualitySetting");
            var level = quality.GetNestedType("Level", BindingFlags.Public);
            Assert.That(level, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(quality.IsAssignableFrom(graphics), Is.True);
                Assert.That(level!.GetField("Custom"), Is.Not.Null);
                Assert.That(graphics.GetMethod("GetLevel", PublicInstance, null, Type.EmptyTypes, null), Is.Not.Null);
                Assert.That(Parameters(graphics.GetMethod("SetLevel", PublicInstance)), Is.EqualTo(new[] { level.FullName, "System.Boolean" }));
                Assert.That(graphics.GetMethod("EnumerateAvailableLevels", PublicInstance), Is.Not.Null);
                Assert.That(Parameters(graphics.GetMethod("GetMockName", PublicInstance)), Is.EqualTo(new[] { level.FullName }));
                Assert.That(ApiType("Game", "Game.Settings.SharedSettings").GetProperty("graphics", PublicInstance)?.PropertyType.FullName,
                    Is.EqualTo("Game.Settings.GraphicsSettings"));
            });
        }

        // #21: pausing sets selectedSpeed to zero; SimulationSystem has no separate pause member.
        [Test]
        public void Simulation_pause_is_the_selected_speed()
        {
            var simulation = ApiType("Game", "Game.Simulation.SimulationSystem");
            var gameManager = ApiType("Game", "Game.SceneFlow.GameManager");
            Assert.Multiple(() =>
            {
                Assert.That(simulation.GetProperty("selectedSpeed", PublicInstance)?.PropertyType.FullName, Is.EqualTo("System.Single"));
                Assert.That(simulation.GetMethod("IsPaused", AnyInstance), Is.Null);
                Assert.That(simulation.GetField("m_Paused", AnyInstance), Is.Null);
                Assert.That(gameManager.GetProperty("isGameLoading", PublicInstance)?.PropertyType.FullName, Is.EqualTo("System.Boolean"));
            });
        }

        // The diagnostic log reads these members every frame to tell apart why the simulation ran slow.
        [Test]
        public void Diagnostic_log_reads_simulation_pathfinding_and_frame_timing_members()
        {
            var simulation = ApiType("Game", "Game.Simulation.SimulationSystem");
            var pathfindResults = ApiType("Game", "Game.Pathfind.PathfindResultSystem");
            var frameTimingManager = ApiType("UnityEngine.CoreModule", "UnityEngine.FrameTimingManager");
            var frameTiming = ApiType("UnityEngine.CoreModule", "UnityEngine.FrameTiming");
            Assert.Multiple(() =>
            {
                Assert.That(simulation.GetProperty("smoothSpeed", PublicInstance)?.PropertyType.FullName, Is.EqualTo("System.Single"));
                Assert.That(simulation.GetProperty("frameIndex", PublicInstance)?.PropertyType.FullName, Is.EqualTo("System.UInt32"));
                Assert.That(simulation.GetProperty("frameDuration", PublicInstance)?.PropertyType.FullName, Is.EqualTo("System.Single"));
                Assert.That(simulation.GetProperty("performancePreference", PublicInstance)?.PropertyType.IsEnum, Is.True);
                Assert.That(pathfindResults.GetProperty("pendingSimulationFrame", PublicInstance)?.PropertyType.FullName, Is.EqualTo("System.UInt32"));
                Assert.That(pathfindResults.GetProperty("pendingRequestCount", PublicInstance)?.PropertyType.FullName, Is.EqualTo("System.Int32"));
                Assert.That(frameTimingManager.GetMethod("CaptureFrameTimings", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
                Assert.That(frameTimingManager.GetMethod("GetLatestTimings", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
                Assert.That(frameTimingManager.GetMethod("IsFeatureEnabled", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
                foreach (var field in new[] { "cpuMainThreadFrameTime", "cpuRenderThreadFrameTime", "gpuFrameTime", "cpuMainThreadPresentWaitTime" })
                    Assert.That(frameTiming.GetField(field)?.FieldType.FullName, Is.EqualTo("System.Double"), field);
            });
        }

        // The reference set holds only the assemblies the mod compiles against; a base type from another assembly
        // (for example HDRP) cannot be resolved and cannot be a Game prefab either.
        private static string? BaseTypeName(Type type)
        {
            try { return type.BaseType?.FullName; }
            catch (FileNotFoundException) { return null; }
        }

        private Assembly Load(string name)
        {
            Assert.That(_metadata, Is.Not.Null);
            return _metadata!.LoadFromAssemblyPath(Path.Combine(_managedPath, name + ".dll"));
        }

        private Type ApiType(string assembly, string fullName)
        {
            var type = Load(assembly).GetType(fullName, throwOnError: false);
            Assert.That(type, Is.Not.Null, "Missing API type " + fullName);
            return type!;
        }

        private static IEnumerable<string?> Parameters(MethodInfo? method)
        {
            Assert.That(method, Is.Not.Null);
            return method!.GetParameters().Select(parameter => parameter.ParameterType.FullName);
        }
    }
}
