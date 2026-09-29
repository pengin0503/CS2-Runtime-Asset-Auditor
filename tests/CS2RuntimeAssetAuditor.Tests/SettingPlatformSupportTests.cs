using System;
using System.Linq;
using CS2RuntimeAssetAuditor.Advisor.Settings;
using CS2RuntimeAssetAuditor.Core.Advisor;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests
{
    public sealed class SettingPlatformSupportTests
    {
        private static Func<Attribute, bool?> RunningOn(string platform) =>
            attribute => ((Game.Settings.SettingsUIPlatformAttribute)attribute).Platforms.Contains(platform);

        private static GameSettingDescriptor Setting(Func<Attribute, bool?> isPlatformSet, string member) =>
            new GameSettingCatalogBuilder(
                    () => new[] { new SettingCategoryRoot("graphics", new Game.Settings.FakePlatformSettings()) },
                    new SettingUiMetadataReader(),
                    isPlatformSet)
                .GetCatalog()
                .Single(setting => setting.SettingId == "Game.Settings.FakePlatformSettings::" + member);

        [Test]
        public void A_setting_for_the_running_platform_is_user_facing_and_writable()
        {
            var vSync = Setting(RunningOn("PC"), "vSync");

            Assert.That(vSync.IsUserFacing, Is.True);
            Assert.That(vSync.IsWritable, Is.True);
        }

        [Test]
        public void A_setting_for_another_platform_is_not_user_facing()
        {
            var consoleOnly = Setting(RunningOn("PC"), "consoleOnly");

            Assert.That(consoleOnly.IsUserFacing, Is.False);
            Assert.That(consoleOnly.IsWritable, Is.False);
        }

        [Test]
        public void A_platform_setting_with_its_own_setter_stays_read_only()
        {
            var displayMode = Setting(RunningOn("PC"), "displayMode");

            Assert.That(displayMode.IsUserFacing, Is.True);
            Assert.That(displayMode.IsWritable, Is.False);
            Assert.That(displayMode.HasSafeReversibleWritePath, Is.False);
        }

        [Test]
        public void An_attribute_that_cannot_be_evaluated_keeps_the_setting_unsupported()
        {
            Assert.That(Setting(_ => null, "vSync").IsUserFacing, Is.False);
            Assert.That(Setting(_ => throw new InvalidOperationException(), "vSync").IsUserFacing, Is.False);
        }

        [Test]
        public void Settings_without_the_attribute_do_not_depend_on_the_platform()
        {
            Assert.That(Setting(_ => false, "anyPlatform").IsUserFacing, Is.True);
        }
    }
}

namespace Game.Settings
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
    internal sealed class SettingsUIPlatformAttribute : Attribute
    {
        public SettingsUIPlatformAttribute(params string[] platforms) { Platforms = platforms; }
        public string[] Platforms { get; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class SettingsUISetterAttribute : Attribute { }

    internal sealed class FakePlatformSettings
    {
        [SettingsUISection, SettingsUIPlatform("PC")]
        public bool vSync { get; set; }

        [SettingsUISection, SettingsUIPlatform("PlayStation", "Xbox")]
        public bool consoleOnly { get; set; }

        [SettingsUISection, SettingsUIPlatform("PC"), SettingsUISetter]
        public bool displayMode { get; set; }

        [SettingsUISection]
        public bool anyPlatform { get; set; }
    }
}
