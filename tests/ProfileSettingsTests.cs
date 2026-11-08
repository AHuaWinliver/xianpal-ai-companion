// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using XianPal;

internal static class ProfileSettingsTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static object Field(object value, string name)
    { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }

    [STAThread] private static int Main(string[] args)
    {
        try
        {
            if (Environment.GetEnvironmentVariable("FUFU_TEST_MODE") != "1") throw new Exception("Isolated test state required.");
            Directory.CreateDirectory(XianPal.Program.StateDirectory);
            Application.EnableVisualStyles();
            UiLanguageBindings.Initialize();
            TestLanguage();
            TestProfiles();
            TestTasks(args[0]);
            Console.WriteLine("PASS product settings, locale persistence and companion task ownership; no model calls.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void TestLanguage()
    {
        UiLanguage.Set("en");
        using (var window = new Form())
        using (var input = new TextBox { Text = "取消 🌱 用户原文" })
        using (var label = new Label { Text = "执行道友" })
        using (var choice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList })
        {
            choice.Items.AddRange(new object[] { "每天", "每周" }); choice.SelectedIndex = 1;
            window.Controls.AddRange(new Control[] { input, label, choice });
            UiLanguageBindings.Apply(window);
            Check(label.Text == "Run as" && (string)choice.Items[1] == "Weekly" && choice.SelectedIndex == 1, "English captions preserve selection");
            UiLanguage.Set("zh-CN"); UiLanguageBindings.Apply(window);
            Check(label.Text == "执行道友" && (string)choice.Items[1] == "每周" && input.Text == "取消 🌱 用户原文", "switching back preserves original captions and user input");
            UiLanguage.Set("en");
            typeof(UiLanguage).GetField("selected", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            Check(UiLanguage.Current == "en", "language selection survives a fresh settings read");
        }
    }

    private static void TestProfiles()
    {
        string root = XianPal.Program.StateDirectory;
        var preferences = new IntegrationPreferences();
        string global = Path.Combine(root, "global claude.cmd"); File.WriteAllText(global, "@exit /b 0");
        preferences.SetExecutablePath(null, global);
        foreach (PetCharacter role in PetCharacter.All)
        {
            string executable = Path.Combine(root, role.Id + " 中文 ' cli.cmd"); File.WriteAllText(executable, "@exit /b 0");
            string settings = Path.Combine(root, role.Id + " settings.json"); File.WriteAllText(settings, "{\"env\":{\"PROFILE\":\"" + role.Id + "\"}}", new UTF8Encoding(false));
            preferences.SetExecutablePath(role.Id, executable);
            preferences.SetSettingsPath(role.Id, settings);
        }
        preferences.Save();
        foreach (PetCharacter role in PetCharacter.All)
        {
            string executable = preferences.ExecutablePathFor(role.Id);
            Check(ClaudeCliRunner.FindClaudeCommand(role.Id.ToUpperInvariant()) == executable && ClaudeSettingsFile.CurrentPath(role.Id) == preferences.SettingsPathFor(role.Id), role.Id + " resolves its own executable and JSON profile");
            string script = PetForm.BuildTerminalStartInfo(root, role.Id).Arguments;
            script = Encoding.Unicode.GetString(Convert.FromBase64String(script.Substring(script.LastIndexOf(' ') + 1)));
            Check(script.Contains(executable.Replace("'", "''")) && script.Contains(preferences.SettingsPathFor(role.Id)), role.Id + " manual terminal uses the same profile with quoted paths");
        }
        string removed = preferences.ExecutablePathFor("moss"); File.Delete(removed);
        bool rejected = false; try { ClaudeCliRunner.FindClaudeCommand("moss"); } catch (FileNotFoundException) { rejected = true; }
        Check(rejected, "missing configured executable fails instead of using a different companion's CLI");
        File.WriteAllText(removed, "@exit /b 0");
        preferences.SetExecutablePath("moss", ""); preferences.Save();
        Check(ClaudeCliRunner.FindClaudeCommand("moss") == global, "clearing an override restores the global executable");
    }

    private static void TestTasks(string assets)
    {
        Environment.SetEnvironmentVariable("XIANPAL_TEST_ASSETS", Path.GetFullPath(assets));
        using (var service = new ClaudeTaskService())
        {
            foreach (PetCharacter role in PetCharacter.All)
            {
                var task = service.Add("Keep original 中文🌱 " + role.Id, XianPal.Program.StateDirectory, "daily", TimeSpan.FromHours(9), DayOfWeek.Monday, 60, DateTime.Now, "background", "block", role.Id);
                var run = service.BeginRun(task, DateTime.Now);
                service.CompleteRun(task, run.Id, true, 0, "原文 🌱", DateTime.Now);
                service.RecordSkipped(task, DateTime.Now, "Still active");
                string replacement = role.Id == "orbit" ? "spark" : "orbit";
                service.Update(task, task.Prompt, task.WorkingDirectory, "weekly", TimeSpan.FromHours(10), DayOfWeek.Friday, 60, DateTime.Now, "background", "block", replacement);
                Check(run.CharacterId == role.Id && task.History[0].CharacterId == role.Id && task.CharacterId == replacement, "editing owner preserves historical and skipped run identities for " + role.Id);
            }
            using (var window = new ClaudeTaskForm(service))
            {
                var list = (ListBox)Field(window, "list");
                Check(list.Items.Count == 4, "one task window includes all companions");
                foreach (PetCharacter role in PetCharacter.All)
                    Check(window.GetType().GetMethod("Avatar", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { role.Id }) is Image, role.Id + " task avatar loads the original character art");
                UiLanguageBindings.Apply(window);
                Check(window.Text == "Claude Code scheduled tasks", "task window title is localized");
                UiLanguage.Set("zh-CN"); UiLanguageBindings.Apply(window);
                Check(((ScheduledClaudeTask)list.Items[0]).Prompt.Contains("中文🌱"), "language switching leaves task prompts untouched");
            }
            var legacy = service.Tasks[0]; legacy.CharacterId = ""; legacy.History[0].CharacterId = "";
            service.AssignLegacyTasks("pippin");
            Check(legacy.CharacterId == "pippin" && legacy.History[0].CharacterId == "pippin", "legacy tasks and records receive an explicit migrated owner");
        }
        using (var reloaded = new ClaudeTaskService())
            Check(reloaded.Tasks.Count == 4 && reloaded.Tasks[0].History.Count > 0 && !String.IsNullOrEmpty(reloaded.Tasks[0].CharacterId), "owner assignments and run history persist across reload");
    }
}
