using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using CS2RuntimeProfiler.Advisor.Settings;
using CS2RuntimeProfiler.Core.Advisor;

namespace CS2RuntimeProfiler.Tests
{
    [TestFixture]
    public sealed class GameSettingCatalogPolicyTests
    {
        [TestCase(typeof(bool), SettingValueKind.Boolean)]
        [TestCase(typeof(int), SettingValueKind.Integer)]
        [TestCase(typeof(float), SettingValueKind.Float)]
        [TestCase(typeof(string), SettingValueKind.String)]
        [TestCase(typeof(TestQuality), SettingValueKind.Enumeration)]
        public void Standard_value_types_are_catalogable(Type valueType, SettingValueKind expectedKind)
        {
            var metadata = StandardMember();
            metadata.ValueType = valueType;
            metadata.CurrentValue = valueType == typeof(TestQuality) ? "High" : "1";
            if (valueType == typeof(TestQuality))
                metadata.AllowedValues = new[] { "Low", "Medium", "High" };

            var descriptor = new SettingUiMetadataReader().Read(metadata);

            Assert.That(descriptor, Is.Not.Null);
            Assert.That(descriptor!.ValueKind, Is.EqualTo(expectedKind));
            Assert.That(descriptor.SettingId, Is.EqualTo("Game.Settings.GraphicsSettings::shadowQuality"));
            Assert.That(descriptor.IsUserFacing, Is.True);
            Assert.That(descriptor.IsReadable, Is.True);
            Assert.That(descriptor.IsWritable, Is.True);
        }

        [Test]
        public void Keybinding_value_is_catalogable_without_treating_it_as_an_untyped_object()
        {
            var metadata = StandardMember();
            metadata.ValueType = typeof(object);
            metadata.IsKeybinding = true;
            metadata.CurrentValue = "K";

            var descriptor = new SettingUiMetadataReader().Read(metadata);

            Assert.That(descriptor, Is.Not.Null);
            Assert.That(descriptor!.ValueKind, Is.EqualTo(SettingValueKind.Keybinding));
            Assert.That(descriptor.IsWritable, Is.True);
        }

        [Test]
        public void Action_buttons_are_not_catalogued_as_reversible_settings()
        {
            var metadata = StandardMember();
            metadata.IsActionButton = true;

            var descriptor = new SettingUiMetadataReader().Read(metadata);

            Assert.That(descriptor, Is.Null);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void Hidden_or_developer_only_members_are_never_user_facing_or_writable(bool hidden, bool developerOnly)
        {
            var metadata = StandardMember();
            metadata.IsHidden = hidden;
            metadata.IsDeveloperOnly = developerOnly;

            var descriptor = new SettingUiMetadataReader().Read(metadata);

            Assert.That(descriptor, Is.Not.Null);
            Assert.That(descriptor!.IsUserFacing, Is.False);
            Assert.That(descriptor.IsWritable, Is.False);
            Assert.That(descriptor.CapabilityState, Is.EqualTo(SettingCapabilityState.ReadOnlyForAdvisor));
        }

        [Test]
        public void Dynamic_visibility_and_enablement_are_preserved_and_fail_closed()
        {
            var hidden = StandardMember();
            hidden.HasHideByCondition = true;
            hidden.IsCurrentlyVisible = false;
            var disabled = StandardMember();
            disabled.HasDisableByCondition = true;
            disabled.IsCurrentlyEnabled = false;
            var unknown = StandardMember();
            unknown.HasHideByCondition = true;
            unknown.IsCurrentlyVisible = null;

            var reader = new SettingUiMetadataReader();
            var hiddenDescriptor = reader.Read(hidden)!;
            var disabledDescriptor = reader.Read(disabled)!;
            var unknownDescriptor = reader.Read(unknown)!;

            Assert.That(hiddenDescriptor.IsCurrentlyVisible, Is.False);
            Assert.That(hiddenDescriptor.IsWritable, Is.False);
            Assert.That(disabledDescriptor.IsCurrentlyEnabled, Is.False);
            Assert.That(disabledDescriptor.IsWritable, Is.False);
            Assert.That(unknownDescriptor.IsCurrentlyVisible, Is.Null);
            Assert.That(unknownDescriptor.IsWritable, Is.False);
        }

        [Test]
        public void Confirmation_and_restart_requirements_are_preserved()
        {
            var confirmation = StandardMember();
            confirmation.RequiresConfirmation = true;
            var restart = StandardMember();
            restart.RequiresRestart = true;

            var reader = new SettingUiMetadataReader();
            var confirmationDescriptor = reader.Read(confirmation)!;
            var restartDescriptor = reader.Read(restart)!;

            Assert.That(confirmationDescriptor.ApplyBehavior, Is.EqualTo(SettingApplyBehavior.ConfirmationRequired));
            Assert.That(confirmationDescriptor.IsWritable, Is.True);
            Assert.That(restartDescriptor.RequiresRestart, Is.True);
            Assert.That(restartDescriptor.ApplyBehavior, Is.EqualTo(SettingApplyBehavior.RestartRequired));
        }

        [Test]
        public void Missing_proven_reversible_writer_is_read_only_instead_of_guessed()
        {
            var metadata = StandardMember();
            metadata.IsPublicSetter = false;

            var descriptor = new SettingUiMetadataReader().Read(metadata)!;

            Assert.That(descriptor.HasSafeReversibleWritePath, Is.False);
            Assert.That(descriptor.IsWritable, Is.False);
            Assert.That(descriptor.ApplyBehavior, Is.EqualTo(SettingApplyBehavior.ReadOnlyForAdvisor));
        }

        [Test]
        public void Custom_setter_metadata_without_a_proven_public_path_remains_read_only()
        {
            var metadata = StandardMember();
            metadata.RequiresCustomSetter = true;

            var descriptor = new SettingUiMetadataReader().Read(metadata)!;
            Assert.That(descriptor.IsWritable, Is.False);
            Assert.That(descriptor.HasSafeReversibleWritePath, Is.False);
        }

        [Test]
        public void Slider_with_unverified_display_scale_remains_read_only()
        {
            var metadata = StandardMember();
            metadata.HasUnverifiedValueScale = true;
            Assert.That(new SettingUiMetadataReader().Read(metadata)!.IsWritable, Is.False);
        }

        [Test]
        public void Catalog_builder_uses_only_the_supplied_standard_options_roots()
        {
            var settings = new FakeGraphicsSettings { shadowQuality = 2 };
            var roots = new[] { new SettingCategoryRoot("graphics", settings) };
            var builder = new GameSettingCatalogBuilder(() => roots, new SettingUiMetadataReader());

            var catalog = builder.GetCatalog();

            Assert.That(catalog.Select(item => item.SettingId), Is.EqualTo(new[]
            {
                typeof(FakeGraphicsSettings).FullName + "::shadowQuality"
            }));
            Assert.That(catalog[0].CurrentValue, Is.EqualTo("2"));
        }

        [Test]
        public void Section_only_value_control_remains_catalogued()
        {
            var catalog = new GameSettingCatalogBuilder(
                () => new[] { new SettingCategoryRoot("graphics", new Game.Settings.FakeSectionSettings()) },
                new SettingUiMetadataReader()).GetCatalog();

            Assert.That(catalog.Select(x => x.SettingId), Does.Contain("Game.Settings.FakeSectionSettings::vSync"));
        }

        [Test]
        public void Slider_limits_are_preserved_for_write_validation()
        {
            var catalog = new GameSettingCatalogBuilder(
                () => new[] { new SettingCategoryRoot("graphics", new Game.Settings.FakeNumericSettings()) },
                new SettingUiMetadataReader()).GetCatalog();
            var slider = catalog.Single(x => x.SettingId.EndsWith("::maxFrameLatency"));
            Assert.That(slider.Minimum, Is.EqualTo(1));
            Assert.That(slider.Maximum, Is.EqualTo(3));
        }

        private static SettingUiMemberMetadata StandardMember()
        {
            return new SettingUiMemberMetadata
            {
                SettingsTypeFullName = "Game.Settings.GraphicsSettings",
                MemberName = "shadowQuality",
                Category = "graphics",
                DisplayName = "Shadow Quality",
                ValueType = typeof(int),
                CurrentValue = "1",
                AllowedValues = Array.Empty<string>(),
                IsStandardSettingsRoot = true,
                IsPublicMember = true,
                IsPublicGetter = true,
                IsPublicSetter = true,
                IsReadable = true,
                IsCurrentlyVisible = true,
                IsCurrentlyEnabled = true,
                IsPlatformSupported = true,
                HasStandardValueControl = true
            };
        }

        private enum TestQuality
        {
            Low,
            Medium,
            High
        }

        private sealed class FakeGraphicsSettings
        {
            public int shadowQuality { get; set; }
        }

    }
}

namespace Game.Settings
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
    internal sealed class SettingsUISectionAttribute : Attribute { }

    internal sealed class FakeSectionSettings
    {
        [SettingsUISection]
        public bool vSync { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class SettingsUISliderAttribute : Attribute
    {
        public int min;
        public int max;
    }

    internal sealed class FakeNumericSettings
    {
        [SettingsUISection, SettingsUISlider(min = 1, max = 3)]
        public int maxFrameLatency { get; set; }
    }
}
