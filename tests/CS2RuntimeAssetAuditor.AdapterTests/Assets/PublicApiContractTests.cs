using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.AdapterTests.Assets
{
    [TestFixture]
    public sealed class PublicApiContractTests
    {
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
        private MetadataLoadContext? _metadata;
        private string[] _assemblyPaths = Array.Empty<string>();

        [SetUp]
        public void SetUp()
        {
            var managedPath = Environment.GetEnvironmentVariable("CSII_MANAGEDPATH");
            if (string.IsNullOrWhiteSpace(managedPath))
                managedPath = Environment.GetEnvironmentVariable("CSII_TOOLPATH");
            if (string.IsNullOrWhiteSpace(managedPath) || !Directory.Exists(managedPath))
                Assert.Fail("Set CSII_MANAGEDPATH or CSII_TOOLPATH to the local CS2 managed reference directory.");

            _assemblyPaths = Directory.GetFiles(managedPath!, "*.dll");
            var frameworkPath = Environment.GetEnvironmentVariable("CS2APA_NET48_REFERENCE_PATH");
            if (!string.IsNullOrWhiteSpace(frameworkPath) && Directory.Exists(frameworkPath))
                _assemblyPaths = _assemblyPaths.Concat(Directory.GetFiles(frameworkPath, "*.dll")).ToArray();
            else
            {
                var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location);
                if (!string.IsNullOrWhiteSpace(runtimePath) && Directory.Exists(runtimePath))
                    _assemblyPaths = _assemblyPaths.Concat(Directory.GetFiles(runtimePath, "*.dll")).ToArray();
            }
            var platformPaths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
                .Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)
                .Where(path => !Path.GetFileName(path).Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase));
            _metadata = new MetadataLoadContext(new PathAssemblyResolver(_assemblyPaths.Concat(platformPaths)), "mscorlib");
        }

        [TearDown]
        public void TearDown()
        {
            _metadata?.Dispose();
            _metadata = null;
        }

        [Test]
        public void Prefab_system_resolves_prefabs_from_supported_public_inputs()
        {
            var prefabSystem = GameType("Game.Prefabs.PrefabSystem");
            var expectedInputs = new[]
            {
                "Game.Prefabs.PrefabData",
                "Unity.Entities.Entity",
                "Game.Prefabs.PrefabRef"
            };

            foreach (var input in expectedInputs)
            {
                Assert.That(HasGenericMethod(prefabSystem, "GetPrefab", input, 1), Is.True, $"GetPrefab<T>({input})");
                Assert.That(HasTryGetPrefab(prefabSystem, input), Is.True, $"TryGetPrefab<T>({input}, out T)");
            }
        }

        [Test]
        public void Prefab_data_and_reference_expose_public_identity_fields()
        {
            var prefabData = GameType("Game.Prefabs.PrefabData");
            var prefabRef = GameType("Game.Prefabs.PrefabRef");

            Assert.That(prefabData.GetField("m_Index", PublicInstance)?.FieldType.FullName, Is.EqualTo("System.Int32"));
            Assert.That(prefabRef.GetField("m_Prefab", PublicInstance)?.FieldType.FullName, Is.EqualTo("Unity.Entities.Entity"));
        }

        [Test]
        public void Render_prefab_and_lod_expose_public_geometry_and_surface_relationships()
        {
            var renderPrefab = GameType("Game.Prefabs.RenderPrefab");
            var geometryProperty = renderPrefab.GetProperty("geometryAsset", PublicInstance);
            var surfacesProperty = renderPrefab.GetProperty("surfaceAssets", PublicInstance);
            var lodProperties = GameType("Game.Prefabs.LodProperties");

            Assert.That(renderPrefab.GetProperty("hasGeometryAsset", PublicInstance)?.PropertyType.FullName, Is.EqualTo("System.Boolean"));
            Assert.That(geometryProperty?.PropertyType.FullName, Is.EqualTo("Colossal.IO.AssetDatabase.GeometryAsset"));
            Assert.That(EnumerableItemType(surfacesProperty?.PropertyType)?.FullName, Is.EqualTo("Colossal.IO.AssetDatabase.SurfaceAsset"));
            Assert.That(lodProperties.GetField("m_LodMeshes", PublicInstance)?.FieldType.GetElementType()?.FullName, Is.EqualTo("Game.Prefabs.RenderPrefab"));
        }

        [Test]
        public void Asset_database_exposes_geometry_surface_and_texture_metadata()
        {
            var geometry = GameType("Colossal.IO.AssetDatabase.GeometryAsset");
            var surface = GameType("Colossal.IO.AssetDatabase.SurfaceAsset");
            var texture = GameType("Colossal.IO.AssetDatabase.TextureAsset");

            AssertPublicProperties(geometry, "meshCount", "compressedDataSize", "shapeCount", "shapeDataSize", "shapeCompressedDataSize");
            Assert.That(EnumerableItemType(surface.GetProperty("textures", PublicInstance)?.PropertyType)?.FullName,
                Is.EqualTo("Colossal.IO.AssetDatabase.TextureAsset"));
            AssertPublicProperties(texture, "format", "dimension", "mipsCount", "mipBias", "width", "height", "depth", "rawData", "state", "isDataLoaded", "isObjectLoaded");
        }

        [Test]
        public void Geometry_totals_are_resident_on_the_render_prefab_and_asset_detail_exposes_its_load_state()
        {
            var renderPrefab = GameType("Game.Prefabs.RenderPrefab");
            foreach (var name in new[] { "meshCount", "vertexCount", "indexCount" })
                Assert.That(renderPrefab.GetProperty(name, PublicInstance)?.PropertyType.FullName, Is.EqualTo("System.Int32"), $"RenderPrefab.{name}");

            var geometry = GameType("Colossal.IO.AssetDatabase.GeometryAsset");
            var dataType = geometry.GetProperty("data", PublicInstance)?.PropertyType;
            var loadingType = geometry.GetProperty("loading", PublicInstance)?.PropertyType;
            Assert.That(dataType?.IsByRef, Is.True, "GeometryAsset.data must be a ref property");
            Assert.That(loadingType?.IsByRef, Is.True, "GeometryAsset.loading must be a ref property");
            Assert.That(dataType!.GetElementType()!.GetProperty("IsValid", PublicInstance)?.PropertyType.FullName, Is.EqualTo("System.Boolean"));
            foreach (var field in new[] { "meshInfos", "meshOffsets", "subMeshInfos" })
                Assert.That(dataType.GetElementType()!.GetField(field, PublicInstance), Is.Not.Null, $"GeometryAsset.Data.{field}");
            foreach (var field in new[] { "m_AsyncLoadingScheduled", "m_AsyncLoadingStarted" })
                Assert.That(loadingType!.GetElementType()!.GetField(field, PublicInstance)?.FieldType.FullName, Is.EqualTo("System.Boolean"), $"GeometryAsset.Loading.{field}");
            Assert.That(geometry.GetMethod("GetSubMeshCount", PublicInstance), Is.Not.Null);
        }

        [Test]
        public void Mod_settings_persistence_uses_file_location_load_and_save_apis()
        {
            var fileLocation = GameType("Colossal.IO.AssetDatabase.FileLocationAttribute");
            Assert.That(fileLocation.GetConstructors().Any(ctor => ctor.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "System.String" })), Is.True);

            var database = GameType("Colossal.IO.AssetDatabase.AssetDatabase");
            Assert.That(database.GetProperty("global", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(database.GetMethods(PublicInstance).Any(method => method.Name == "LoadSettings"
                && method.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "System.String", "System.Object", "System.Object", "System.Boolean" })), Is.True);

            var modSetting = GameType("Game.Modding.ModSetting");
            Assert.That(modSetting.GetMethod("ApplyAndSave", PublicInstance, null, Type.EmptyTypes, null), Is.Not.Null);
        }

        [Test]
        public void Texture_footprint_formats_are_graphics_format_names_reported_by_texture_assets()
        {
            var texture = GameType("Colossal.IO.AssetDatabase.TextureAsset");
            var formatType = texture.GetProperty("format", PublicInstance)!.PropertyType;
            Assert.That(formatType.FullName, Is.EqualTo("UnityEngine.Experimental.Rendering.GraphicsFormat"));
            Assert.That(texture.GetProperty("depth", PublicInstance)!.PropertyType.FullName, Is.EqualTo("System.Int32"));
            Assert.That(texture.GetProperty("dimension", PublicInstance)!.PropertyType.FullName, Is.EqualTo("UnityEngine.Rendering.TextureDimension"));

            var graphicsFormatNames = formatType.GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => field.Name).ToArray();
            foreach (var name in CS2RuntimeAssetAuditor.Assets.Core.Rendering.TextureFootprintEstimator.SupportedFormatNames)
                Assert.That(graphicsFormatNames, Does.Contain(name), $"{name} is not a GraphicsFormat member name.");
        }

        [Test]
        public void Ui_system_uses_the_available_public_binding_base()
        {
            var uiSystem = GameType("Game.UI.UISystemBase");
            Assert.That(uiSystem.IsPublic, Is.True);
            Assert.That(uiSystem.BaseType?.FullName, Is.EqualTo("Game.GameSystemBase"));
        }

        [Test]
        public void Ui_system_supports_the_value_and_trigger_bindings_used_by_phase_one()
        {
            var uiSystem = GameType("Game.UI.UISystemBase");
            var bindingMethods = BindingFlags.Instance | BindingFlags.NonPublic;
            var addBinding = uiSystem.GetMethod("AddBinding", bindingMethods);
            var addUpdateBinding = uiSystem.GetMethod("AddUpdateBinding", bindingMethods);
            var valueBinding = GameType("Colossal.UI.Binding.ValueBinding`1");
            var triggerBinding = GameType("Colossal.UI.Binding.TriggerBinding`1");
            var stringReader = GameType("Colossal.UI.Binding.StringReader");

            Assert.That(addBinding?.GetParameters().Single().ParameterType.FullName, Is.EqualTo("Colossal.UI.Binding.IBinding"));
            Assert.That(addUpdateBinding?.GetParameters().Single().ParameterType.FullName, Is.EqualTo("Colossal.UI.Binding.IUpdateBinding"));
            Assert.That(valueBinding.GetMethod("Update", PublicInstance)?.GetParameters().Single().ParameterType.Name, Is.EqualTo("T"));
            Assert.That(triggerBinding.GetConstructors(PublicInstance)
                .Any(constructor => constructor.GetParameters().Length == 4
                    && constructor.GetParameters()[1].ParameterType.FullName == "System.String"
                    && constructor.GetParameters()[3].ParameterType.GetGenericTypeDefinition().FullName == "Colossal.UI.Binding.IReader`1"), Is.True);
            Assert.That(stringReader.IsPublic, Is.True);
        }

        [Test]
        public void Prefab_classification_and_source_markers_are_available_as_public_api()
        {
            var prefabBase = GameType("Game.Prefabs.PrefabBase");
            AssertPublicProperties(prefabBase, "isBuiltin", "isSubscribedMod", "isPackaged", "asset");
            Assert.That(prefabBase.GetField("components", PublicInstance), Is.Not.Null);

            foreach (var markerType in new[]
            {
                "Game.Prefabs.BuildingPrefab",
                "Game.Prefabs.StaticObjectPrefab",
                "Game.Prefabs.CityServiceBuilding",
                "Game.Prefabs.TreeObject",
                "Game.Prefabs.VehiclePrefab",
                "Game.Prefabs.NetPrefab",
                "Game.Prefabs.RenderPrefab"
            })
                Assert.That(GameType(markerType).IsPublic, Is.True, markerType);

            var assetData = GameType("Colossal.IO.AssetDatabase.AssetData");
            AssertPublicProperties(assetData, "identifier", "uniqueName", "name");
        }

        [Test]
        public void Census_query_components_are_available_as_public_api()
        {
            foreach (var componentType in new[]
            {
                "Game.Objects.Object",
                "Game.Net.Edge",
                "Game.Prefabs.PrefabRef",
                "Game.Common.Owner",
                "Game.Vehicles.Controller",
                "Game.Tools.Temp",
                "Game.Common.Deleted",
                "Game.Common.Overridden"
            })
                Assert.That(GameType(componentType).IsPublic, Is.True, componentType);

            Assert.That(GameType("Game.Net.Edge").GetField("m_Start", PublicInstance), Is.Not.Null);
            Assert.That(GameType("Game.Net.Edge").GetField("m_End", PublicInstance), Is.Not.Null);
            Assert.That(GameType("Game.Common.Owner").GetField("m_Owner", PublicInstance), Is.Not.Null);
            Assert.That(GameType("Game.Vehicles.Controller").GetField("m_Controller", PublicInstance), Is.Not.Null);
        }

        [Test]
        public void Census_capture_uses_the_supported_async_component_list_api()
        {
            var entityQuery = GameType("Unity.Entities.EntityQuery");
            var method = entityQuery.GetMethods(PublicInstance)
                .FirstOrDefault(candidate => candidate.Name == "ToComponentDataListAsync"
                    && candidate.IsGenericMethodDefinition
                    && candidate.GetGenericArguments().Length == 1
                    && candidate.GetParameters().Length == 2
                    && candidate.GetParameters()[1].ParameterType.IsByRef);

            Assert.That(method, Is.Not.Null);
            Assert.That(method!.ReturnType.GetGenericTypeDefinition().FullName, Is.EqualTo("Unity.Collections.NativeList`1"));
            var parameters = method.GetParameters();
            Assert.That(parameters.Length, Is.EqualTo(2));
            Assert.That(parameters[1].ParameterType.IsByRef, Is.True);
            Assert.That(parameters[1].ParameterType.GetElementType()?.FullName, Is.EqualTo("Unity.Jobs.JobHandle"));
        }

        private static bool HasGenericMethod(Type type, string name, string parameterType, int genericArity)
        {
            return type.GetMethods(PublicInstance)
                .Where(method => method.Name == name && method.IsGenericMethodDefinition && method.GetGenericArguments().Length == genericArity)
                .Any(method => method.GetParameters().Select(parameter => parameter.ParameterType.FullName).SequenceEqual(new[] { parameterType }));
        }

        private static bool HasTryGetPrefab(Type type, string inputType)
        {
            return type.GetMethods(PublicInstance)
                .Where(method => method.Name == "TryGetPrefab" && method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1)
                .Any(method =>
                {
                    var parameters = method.GetParameters();
                    return parameters.Length == 2 && parameters[0].ParameterType.FullName == inputType &&
                        parameters[1].ParameterType.IsByRef && parameters[1].ParameterType.GetElementType()!.Name == "T";
                });
        }

        private static void AssertPublicProperties(Type type, params string[] names)
        {
            foreach (var name in names)
                Assert.That(type.GetProperty(name, PublicInstance), Is.Not.Null, $"{type.FullName}.{name}");
        }

        private static Type? EnumerableItemType(Type? type)
        {
            if (type == null)
                return null;
            if (type.IsGenericType && type.GetGenericTypeDefinition().FullName == "System.Collections.Generic.IEnumerable`1")
                return type.GetGenericArguments()[0];
            if (type.IsGenericType && type.GetGenericTypeDefinition().FullName == "System.Collections.Generic.IReadOnlyDictionary`2")
                return type.GetGenericArguments()[1];
            return null;
        }

        private Type GameType(string fullName)
        {
            return FindType(fullName) ?? throw new InvalidOperationException($"Game API type {fullName} was not found in the supplied managed references.");
        }

        private Type? FindType(string fullName)
        {
            foreach (var path in _assemblyPaths)
            {
                try
                {
                    var assembly = _metadata!.LoadFromAssemblyPath(path);
                    var type = assembly.GetType(fullName, throwOnError: false);
                    if (type != null)
                        return type;
                }
                catch (BadImageFormatException)
                {
                    // Some local game folders can include native DLLs next to managed references.
                }
                catch (InvalidOperationException)
                {
                    // MetadataLoadContext rejects native images as non-assemblies.
                }
                catch (FileNotFoundException)
                {
                    // A different assembly can reference optional game dependencies not present in the local toolchain.
                }
                catch (TypeLoadException)
                {
                    // Continue to the assembly that owns the requested public type.
                }
            }
            return null;
        }
    }
}
