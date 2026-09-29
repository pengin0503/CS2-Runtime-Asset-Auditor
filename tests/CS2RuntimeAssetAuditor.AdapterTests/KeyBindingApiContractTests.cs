using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.AdapterTests
{
    // The panel key binding relies on the ModSetting key-binding API: an action declared on the setting class, a
    // ProxyBinding property without a default key, registration before the settings load, and a per-frame poll.
    [TestFixture]
    public sealed class KeyBindingApiContractTests
    {
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
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

        [Test]
        public void Keyboard_binding_attribute_accepts_an_action_name_without_a_default_key()
        {
            var attribute = GameType("Game.Settings.SettingsUIKeyboardBindingAttribute");
            var actionNameOnly = attribute.GetConstructors()
                .SingleOrDefault(constructor => constructor.GetParameters()
                    .Select(parameter => parameter.ParameterType.FullName)
                    .SequenceEqual(new[] { "System.String" }));
            Assert.That(actionNameOnly, Is.Not.Null, "SettingsUIKeyboardBinding(string actionName) must exist.");

            var defaultKey = attribute.GetField("defaultKey", PublicInstance);
            Assert.That(defaultKey?.FieldType.FullName, Is.EqualTo("Game.Input.BindingKeyboard"));
            var none = GameType("Game.Input.BindingKeyboard").GetField("None");
            Assert.That(none, Is.Not.Null);
            Assert.That(Convert.ToInt32(none!.GetRawConstantValue()), Is.EqualTo(0),
                "An attribute built from the action name alone leaves defaultKey at BindingKeyboard.None, i.e. unassigned.");
        }

        [Test]
        public void Keyboard_action_attribute_declares_a_button_action_on_the_setting_class()
        {
            var attribute = GameType("Game.Settings.SettingsUIKeyboardActionAttribute");
            var usage = attribute.GetCustomAttributesData()
                .Single(data => data.AttributeType.FullName == "System.AttributeUsageAttribute");
            Assert.That(Convert.ToInt32(usage.ConstructorArguments[0].Value), Is.EqualTo((int)AttributeTargets.Class));

            var primary = attribute.GetConstructors().SingleOrDefault(constructor =>
            {
                var parameters = constructor.GetParameters();
                return parameters.Length > 1
                    && parameters[0].ParameterType.FullName == "System.String"
                    && parameters.Skip(1).All(parameter => parameter.IsOptional);
            });
            Assert.That(primary, Is.Not.Null, "SettingsUIKeyboardAction(string name) must bind to the constructor whose other parameters are optional.");
            Assert.That(primary!.GetParameters()[1].ParameterType.FullName, Is.EqualTo("Game.Input.ActionType"));
            Assert.That(Convert.ToInt32(primary.GetParameters()[1].RawDefaultValue),
                Is.EqualTo(Convert.ToInt32(GameType("Game.Input.ActionType").GetField("Button")!.GetRawConstantValue())));
        }

        [Test]
        public void Mod_setting_registers_bindings_and_resolves_actions()
        {
            var modSetting = GameType("Game.Modding.ModSetting");
            var register = modSetting.GetMethod("RegisterKeyBindings", PublicInstance, null, Type.EmptyTypes, null);
            Assert.That(register, Is.Not.Null);

            var getAction = modSetting.GetMethod("GetAction", PublicInstance);
            Assert.That(getAction?.GetParameters().Select(parameter => parameter.ParameterType.FullName),
                Is.EqualTo(new[] { "System.String" }));
            Assert.That(getAction!.ReturnType.FullName, Is.EqualTo("Game.Input.ProxyAction"));

            var registered = modSetting.GetProperty("keyBindingRegistered", PublicInstance);
            Assert.That(registered?.PropertyType.FullName, Is.EqualTo("System.Boolean"));
            Assert.That(registered!.GetMethod?.IsPublic, Is.True);

            foreach (var localeHelper in new[] { "GetBindingMapLocaleID", "GetBindingKeyLocaleID" })
                Assert.That(modSetting.GetMethods(PublicInstance).Any(method => method.Name == localeHelper), Is.True, localeHelper);
            Assert.That(GameType("Game.Input.ProxyBinding").IsValueType, Is.True,
                "ModSetting discovers bindings as read/write properties of type ProxyBinding.");
        }

        [Test]
        public void Proxy_action_can_be_enabled_by_a_mod_and_polled_each_frame()
        {
            var action = GameType("Game.Input.ProxyAction");
            var shouldBeEnabled = action.GetProperty("shouldBeEnabled", PublicInstance);
            Assert.That(shouldBeEnabled?.SetMethod?.IsPublic, Is.True);

            var performed = action.GetMethod("WasPerformedThisFrame", PublicInstance, null, Type.EmptyTypes, null);
            Assert.That(performed?.ReturnType.FullName, Is.EqualTo("System.Boolean"));
        }

        private Type GameType(string fullName)
        {
            Assert.That(_game, Is.Not.Null, "Game.dll must be loaded before checking its contract.");
            var type = _game!.GetType(fullName, throwOnError: false);
            Assert.That(type, Is.Not.Null, "Missing CS2 API type " + fullName);
            return type!;
        }
    }
}
