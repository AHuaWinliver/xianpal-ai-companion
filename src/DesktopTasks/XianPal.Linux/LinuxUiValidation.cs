// SPDX-License-Identifier: Apache-2.0
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;

namespace XianPal.Linux;

internal static class LinuxUiValidation
{
    internal static async Task Run(PetWindow pet, IClassicDesktopStyleApplicationLifetime desktop, string evidence)
    {
        var caption = DesktopUi.Label("执行道友");
        var input = new TextBox { Text = "取消 🌱 user content" };
        var choices = new ComboBox { ItemsSource = new[] { "每天", "每周" }, SelectedIndex = 1 };
        var probe = DesktopUi.Dialog("默认设置", DesktopUi.Stack(caption, input, choices), 440, 260);
        probe.Show(pet);
        await Task.Delay(80);
        UiLanguage.Set("en");
        if (caption.Text != "Run as" || probe.Title != "Default settings" || choices.SelectedIndex != 1 || input.Text != "取消 🌱 user content")
            throw new Exception("English UI switching changed user data, selection or failed to update captions.");
        UiLanguage.Set("zh-CN");
        if (caption.Text != "执行道友" || (string)choices.ItemsSource!.Cast<object>().ElementAt(1) != "每周")
            throw new Exception("Chinese UI switching did not restore captions.");
        probe.Close();
        UiLanguage.Set("en");
        pet.ShowSettings();
        await Task.Delay(150);
        var settings = desktop.Windows.Single(window => window.Name == "DefaultSettingsWindow");
        var controls = settings.GetVisualDescendants().OfType<Control>().ToArray();
        var scope = controls.OfType<ComboBox>().Single(control => control.Name == "CompanionScope");
        var executable = controls.OfType<TextBox>().Single(control => control.Name == "ClaudeExecutable");
        var preferences = IntegrationPreferences.Load();
        scope.SelectedIndex = 1;
        if (executable.Text != preferences.ExecutableOverrideFor("orbit")) throw new Exception("Companion executable editor loaded the wrong profile.");
        executable.Text = "unsaved fixture value";
        scope.SelectedIndex = 2; scope.SelectedIndex = 1;
        if (executable.Text != "unsaved fixture value") throw new Exception("Changing profile discarded an unsaved executable field.");
        executable.Text = preferences.ExecutableOverrideFor("orbit");
        DesktopValidation.Capture(settings, "settings-en.png");
        settings.Close(); // Cancel: draft changes must not persist.
        if (IntegrationPreferences.Load().ExecutableOverrideFor("orbit") != preferences.ExecutableOverrideFor("orbit"))
            throw new Exception("Cancelling settings changed the saved executable.");
        TaskWindow.Show(pet, pet.Services.Tasks);
        await Task.Delay(150);
        var tasks = desktop.Windows.Single(window => window.Title == "Claude Code scheduled tasks");
        int avatarCount = tasks.GetVisualDescendants().OfType<Image>().Count(image => image.Source != null);
        if (avatarCount == 0) throw new Exception("Task overview did not display companion avatars.");
        foreach (PetCharacter character in PetCharacter.All)
        {
            using var avatar = new Avalonia.Media.Imaging.Bitmap(character.AvatarPath);
            if (avatar.PixelSize.Width == 0 || avatar.PixelSize.Height == 0) throw new Exception("Companion avatar is empty: " + character.Id);
        }
        DesktopValidation.Capture(tasks, "tasks-en.png");
        tasks.Close();
        UiLanguage.Set("zh-CN");
        File.WriteAllText(Path.Combine(evidence, "ui-results.txt"), "PASS live English/Chinese captions; preserved input and selection; separate settings window; profile draft retention and cancellation; all-companion task avatars.\n");
    }
}
