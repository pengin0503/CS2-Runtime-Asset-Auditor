using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeProfiler.Advisor.Settings;
using CS2RuntimeProfiler.Core.Advisor;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests
{
    [TestFixture]
    public sealed class StandardSettingCoveragePolicyTests
    {
        [Test]
        public void Nested_quality_controls_under_an_official_Options_section_are_catalogued_once()
        {
            var root = new Game.Settings.FakeGraphicsRoot();
            var builder = new GameSettingCatalogBuilder(() => new[]
            { new SettingCategoryRoot("graphics", root), new SettingCategoryRoot("graphics", root) },
                new SettingUiMetadataReader());

            var catalog = builder.GetCatalog();
            var sliders = catalog.Where(x => x.SettingId == "Game.Settings.FakeQualitySettings::shadowResolution").ToArray();
            Assert.That(sliders, Has.Length.EqualTo(1));
            Assert.That(sliders[0].IsUserFacing, Is.True);
            Assert.That(sliders[0].IsWritable, Is.False, "Unknown class-level disable condition is fail-closed.");
            Assert.That(sliders[0].ApplyBehavior, Is.EqualTo(SettingApplyBehavior.ReadOnlyForAdvisor));
            Assert.That(sliders[0].Minimum, Is.EqualTo(512));
            Assert.That(sliders[0].Maximum, Is.EqualTo(4096));
            Assert.That(catalog.Any(x => x.SettingId.EndsWith("::resetButton")), Is.False);
            Assert.That(catalog.Any(x => x.SettingId.EndsWith("::hiddenValue") && x.IsWritable), Is.False);
        }

        [Test]
        public void Class_level_section_makes_an_unannotated_standard_value_visible_but_conditionally_read_only()
        {
            var catalog = new GameSettingCatalogBuilder(() => new[]
            { new SettingCategoryRoot("graphics", new Game.Settings.FakeGraphicsRoot()) },
                new SettingUiMetadataReader()).GetCatalog();
            var option = catalog.Single(x => x.SettingId == "Game.Settings.FakeQualitySettings::terrainCastShadows");
            Assert.That(option.IsUserFacing, Is.True);
            Assert.That(option.IsWritable, Is.False);
        }

        [Test]
        public void Generic_gateway_does_not_treat_unverified_nested_list_or_keybinding_as_a_reversible_value()
        {
            var catalog = new GameSettingCatalogBuilder(() => new[]
            { new SettingCategoryRoot("keybinding", new Game.Settings.FakeKeybindings()) },
                new SettingUiMetadataReader()).GetCatalog();
            Assert.That(catalog.Any(x => x.IsWritable), Is.False);
        }

        [Test]
        public void Nested_section_without_a_verified_apply_owner_stays_read_only_even_with_a_public_setter()
        {
            var catalog = new GameSettingCatalogBuilder(() => new[]
            { new SettingCategoryRoot("graphics", new Game.Settings.FakeUnconditionedGraphicsRoot()) },
                new SettingUiMetadataReader()).GetCatalog();

            var setting = catalog.Single(x => x.SettingId == "Game.Settings.FakeUnconditionedQualitySettings::enabled");
            Assert.That(setting.IsUserFacing, Is.True);
            Assert.That(setting.IsWritable, Is.False);
            Assert.That(setting.HasSafeReversibleWritePath, Is.False);
        }

        [Test]
        public void Only_the_verified_public_depth_of_field_modes_receive_a_performance_tag()
        {
            var catalog = new GameSettingCatalogBuilder(() => new[]
            { new SettingCategoryRoot("graphics", new Game.Settings.GraphicsSettings()) },
                new SettingUiMetadataReader()).GetCatalog();

            Assert.That(catalog.Single(x => x.SettingId == "Game.Settings.GraphicsSettings::depthOfFieldMode")
                .SemanticTags, Is.EqualTo(new[] { "rendering.depth-of-field-mode" }));
            Assert.That(catalog.Single(x => x.SettingId == "Game.Settings.GraphicsSettings::vSync")
                .SemanticTags, Is.Empty);
        }
    }
}

namespace Game.Settings
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class SettingsUIDisableByConditionAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class SettingsUIHiddenAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class SettingsUIButtonAttribute : Attribute { }

    [SettingsUISection, SettingsUIDisableByCondition]
    internal sealed class FakeQualitySettings
    {
        [SettingsUISlider(min = 512, max = 4096)]
        public int shadowResolution { get; set; } = 1024;
        public bool terrainCastShadows { get; set; } = true;
        [SettingsUIHidden, SettingsUISlider(min = 1, max = 4)]
        public int hiddenValue { get; set; } = 1;
        [SettingsUIButton]
        public bool resetButton { get; set; }
    }

    internal sealed class FakeGraphicsRoot
    {
        public List<FakeQualitySettings> qualitySettings { get; set; } = new List<FakeQualitySettings>
        { new FakeQualitySettings() };
    }

    [SettingsUISection]
    internal sealed class FakeUnconditionedQualitySettings
    {
        public bool enabled { get; set; } = true;
    }

    internal sealed class FakeUnconditionedGraphicsRoot
    {
        public List<FakeUnconditionedQualitySettings> qualitySettings { get; set; } = new List<FakeUnconditionedQualitySettings>
        { new FakeUnconditionedQualitySettings() };
    }

    internal sealed class FakeKeybindings
    {
        [SettingsUIHidden]
        public List<int> bindings { get; set; } = new List<int> { 1 };
    }

    internal sealed class GraphicsSettings
    {
        public enum DepthOfFieldMode { Disabled, Physical, TiltShift }
        [SettingsUISection]
        public DepthOfFieldMode depthOfFieldMode { get; set; } = DepthOfFieldMode.Physical;
        [SettingsUISection]
        public bool vSync { get; set; } = true;
    }
}
