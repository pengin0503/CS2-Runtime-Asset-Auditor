using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

// The panel can be toggled with a key the player assigns in the options. No key is assigned by default.
public class PanelKeyBindingTests
{
    [Test]
    public void Setting_declares_the_toggle_action_and_a_binding_without_a_default_key()
    {
        var setting = ReadSource("Setting.cs");

        Assert.Multiple(() =>
        {
            Assert.That(setting, Does.Contain("internal const string TogglePanelActionName = \"TogglePanel\";"));
            Assert.That(setting, Does.Contain("[SettingsUIKeyboardAction(TogglePanelActionName)]"));
            Assert.That(setting, Does.Match(@"\[SettingsUIKeyboardBinding\(TogglePanelActionName\)\]\s+public ProxyBinding TogglePanelBinding \{ get; set; \}"),
                "The binding attribute must name only the action so its default key stays BindingKeyboard.None.");
            Assert.That(setting, Does.Not.Contain("BindingKeyboard."), "No default key may be assigned.");
            Assert.That(setting, Does.Contain("KeyBindingGroup"));
        });
    }

    [Test]
    public void Mod_registers_key_bindings_before_loading_settings_and_creating_systems()
    {
        var mod = ReadSource("Mod.cs");
        var register = mod.IndexOf("Settings.RegisterKeyBindings()", StringComparison.Ordinal);
        var load = mod.IndexOf("AssetDatabase.global.LoadSettings(", StringComparison.Ordinal);
        var firstSystem = mod.IndexOf("updateSystem.UpdateAt<", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(register, Is.GreaterThan(0));
            Assert.That(register, Is.LessThan(load), "A saved binding is applied only to an already registered action.");
            Assert.That(register, Is.LessThan(firstSystem), "The UI system resolves the action when it is created.");
        });
    }

    [Test]
    public void Ui_system_polls_the_action_every_frame_before_the_refresh_throttle()
    {
        var ui = ReadSource(Path.Combine("UI", "ProfilerUISystem.cs"));
        var update = ui.Substring(ui.IndexOf("protected override void OnUpdate()", StringComparison.Ordinal));
        var poll = update.IndexOf("_togglePanelAction.WasPerformedThisFrame()", StringComparison.Ordinal);
        var throttle = update.IndexOf("if (now < _nextRefreshAt)", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(poll, Is.GreaterThan(0));
            Assert.That(poll, Is.LessThan(throttle), "A key press between UI refreshes must not be dropped.");
            Assert.That(ui, Does.Contain("settings.GetAction(Setting.TogglePanelActionName)"));
            Assert.That(ui, Does.Contain("action.shouldBeEnabled = true;"));
            Assert.That(ui, Does.Contain("_togglePanelAction.shouldBeEnabled = false;"), "The action is released with the system.");
        });
    }

    [TestCase("LocaleEN.cs")]
    [TestCase("LocaleJA.cs")]
    public void Both_locales_label_the_key_binding(string file)
    {
        var locale = ReadSource(Path.Combine("Localization", file));

        foreach (var id in new[]
        {
            "GetOptionGroupLocaleID(Setting.KeyBindingGroup)",
            "GetOptionLabelLocaleID(nameof(Setting.TogglePanelBinding))",
            "GetOptionDescLocaleID(nameof(Setting.TogglePanelBinding))",
            "GetBindingKeyLocaleID(Setting.TogglePanelActionName)",
            "GetBindingMapLocaleID()"
        })
            Assert.That(Regex.Matches(locale, Regex.Escape(id)).Count, Is.EqualTo(1), id);
    }

    private static string ReadSource(string relativePath)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CS2RuntimeAssetAuditor.sln")))
            directory = directory.Parent;
        Assert.That(directory, Is.Not.Null);
        return File.ReadAllText(Path.Combine(directory!.FullName, "src", "CS2RuntimeAssetAuditor", relativePath));
    }
}
