using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.AdapterTests
{
    [TestFixture]
    public sealed class SettingsApiContractTests
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
        public void Shared_settings_exposes_the_standard_Options_roots()
        {
            var sharedSettings = GameType("Game.Settings.SharedSettings");
            Assert.That(sharedSettings.GetProperty("instance", BindingFlags.Public | BindingFlags.Static), Is.Not.Null,
                "The public SharedSettings singleton is the standard catalog root.");

            var rootNames = new[]
            {
                "general", "audio", "gameplay", "radio", "graphics", "editor", "userInterface", "input",
                "userState", "keybinding", "benchmark", "modding"
            };

            foreach (var rootName in rootNames)
            {
                var property = sharedSettings.GetProperty(rootName, PublicInstance);
                Assert.That(property, Is.Not.Null, $"Expected public built-in Options root '{rootName}'.");
                Assert.That(property!.GetMethod?.IsPublic, Is.True, $"Options root '{rootName}' must be readable through the public API.");
                Assert.That(property.PropertyType.FullName, Does.StartWith("Game.Settings."),
                    $"Options root '{rootName}' should resolve to a built-in game settings type.");
            }
        }

        [Test]
        public void Setting_exposes_the_supported_apply_methods()
        {
            var setting = GameType("Game.Settings.Setting");
            foreach (var methodName in new[] { "Apply", "ApplyAndSave" })
            {
                var method = setting.GetMethod(methodName, PublicInstance);
                Assert.That(method, Is.Not.Null, $"Expected public Setting.{methodName} API.");
                Assert.That(method!.GetParameters(), Is.Empty, $"Setting.{methodName} should not require hidden caller arguments.");
            }
        }

        [Test]
        public void Options_ui_metadata_exposes_visibility_and_value_control_attributes()
        {
            var expectedTypes = new[]
            {
                "SettingsUIHiddenAttribute",
                "SettingsUIDeveloperAttribute",
                "SettingsUIPlatformAttribute",
                "SettingsUIHideByConditionAttribute",
                "SettingsUIDisableByConditionAttribute",
                "SettingsUIAdvancedAttribute",
                "SettingsUISliderAttribute",
                "SettingsUIDropdownAttribute",
                "SettingsUIConfirmationAttribute",
                "SettingsUISetterAttribute",
                "SettingsUIButtonAttribute"
            };

            foreach (var name in expectedTypes)
                Assert.That(GameType("Game.Settings." + name), Is.Not.Null, "Missing standard Options metadata type " + name);
        }

        [Test]
        public void Public_options_members_distinguish_value_controls_from_action_buttons()
        {
            var members = GetStandardOptionsRootTypes()
                .SelectMany(type => type.GetProperties(PublicInstance))
                .ToArray();
            var actionButtons = members.Where(property => HasAttribute(property, "Game.Settings.SettingsUIButtonAttribute")).ToArray();
            var valueControls = members.Where(property =>
                property.GetMethod?.IsPublic == true
                && property.SetMethod?.IsPublic == true
                && !HasAttribute(property, "Game.Settings.SettingsUIButtonAttribute")
                && (HasAttribute(property, "Game.Settings.SettingsUISliderAttribute")
                    || HasAttribute(property, "Game.Settings.SettingsUIDropdownAttribute")
                    || HasAttribute(property, "Game.Settings.SettingsUISetterAttribute")))
                .ToArray();

            Assert.That(actionButtons.Length, Is.GreaterThan(0), "Action buttons must be explicitly marked by standard Options metadata.");
            Assert.That(valueControls.Length, Is.GreaterThan(0), "Standard reversible value controls must have public read/write member metadata.");
            Assert.That(actionButtons.Any(property => property.SetMethod?.IsPublic == true), Is.True,
                "Button metadata, not the presence of a setter, must distinguish action controls from reversible values.");
        }

        [Test]
        public void Graphics_quality_section_uses_public_standard_options_metadata()
        {
            var shared = GameType("Game.Settings.SharedSettings");
            var graphics = shared.GetProperty("graphics", PublicInstance)!.PropertyType;
            var children = graphics.GetProperty("qualitySettings", PublicInstance);
            Assert.That(children?.GetMethod?.IsPublic, Is.True,
                "Quality settings are reachable through the standard graphics Options root.");
            var shadows = GameType("Game.Settings.ShadowsQualitySettings");
            Assert.That(HasAttribute(shadows, "Game.Settings.SettingsUISectionAttribute"), Is.True);
            Assert.That(HasAttribute(shadows, "Game.Settings.SettingsUIDisableByConditionAttribute"), Is.True,
                "Section-level enablement must be checked before writes.");
            var depth = GameType("Game.Settings.DepthOfFieldQualitySettings");
            Assert.That(HasAttribute(depth.GetProperty("nearSampleCount", PublicInstance)!,
                "Game.Settings.SettingsUISliderAttribute"), Is.True);
        }

        private Type GameType(string fullName)
        {
            Assert.That(_game, Is.Not.Null, "Game.dll must be loaded before checking its contract.");
            var type = _game!.GetType(fullName, throwOnError: false);
            Assert.That(type, Is.Not.Null, "Missing CS2 API type " + fullName);
            return type!;
        }

        private IEnumerable<Type> GetStandardOptionsRootTypes()
        {
            var sharedSettings = GameType("Game.Settings.SharedSettings");
            foreach (var name in new[]
            {
                "general", "audio", "gameplay", "radio", "graphics", "editor", "userInterface", "input",
                "userState", "keybinding", "benchmark", "modding"
            })
            {
                var property = sharedSettings.GetProperty(name, PublicInstance);
                Assert.That(property, Is.Not.Null, "Missing public Options root " + name);
                yield return property!.PropertyType;
            }
        }

        private static bool HasAttribute(MemberInfo member, string fullName)
        {
            return member.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName == fullName);
        }

    }
}
