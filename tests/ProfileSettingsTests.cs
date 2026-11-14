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
            TestReminderWeekday();
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
            var task = new ScheduledClaudeTask { CharacterId = "orbit", WorkingDirectory = root };
            var record = new ClaudeTaskRunRecord { CharacterId = role.Id, BackgroundJobId = "job-profile-test", WorkingDirectory = root };
            script = PetForm.BuildBackgroundAttachStartInfo(task, record).Arguments;
            script = Encoding.Unicode.GetString(Convert.FromBase64String(script.Substring(script.LastIndexOf(' ') + 1)));
            Check(script.Contains(executable.Replace("'", "''")) && script.Contains("attach 'job-profile-test'") && script.Contains(preferences.SettingsPathFor(role.Id)), role.Id + " background attach uses the recorded owner, not the edited task owner");
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
                string editedDirectory = Path.Combine(XianPal.Program.StateDirectory, "edited-folder"); Directory.CreateDirectory(editedDirectory);
                service.Update(task, "Updated task 中文🌱", editedDirectory, "weekly", TimeSpan.FromHours(10), DayOfWeek.Friday, 60, DateTime.Now, "background", "block", replacement);
                Check(run.CharacterId == role.Id && task.History[0].CharacterId == role.Id && task.CharacterId == replacement, "editing owner preserves historical and skipped run identities for " + role.Id);
                Check(run.WorkingDirectory == XianPal.Program.StateDirectory && run.Prompt.Contains("Keep original") && ClaudeTaskRunTranscriptForm.BuildTranscript(task, run).Contains("Keep original"), "historical directory and original prompt survive task edits for " + role.Id);
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

                var selected = (ScheduledClaudeTask)list.SelectedItem;
                window.GetType().GetMethod("EditSelectedTask", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                var repeat = (ComboBox)Field(window, "recurrence");
                var weekday = (ComboBox)Field(window, "weekday");
                Check(repeat.SelectedIndex == 1 && weekday.SelectedIndex == (int)DayOfWeek.Friday, "task editor restores the saved weekly day");
                weekday.SelectedIndex = (int)DayOfWeek.Sunday;
                window.GetType().GetMethod("SaveTask", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                Check(selected.Weekday == (int)DayOfWeek.Sunday && TaskPresentation.Schedule(selected) == "每周日 10:00" &&
                      ClaudeTaskService.CalculateNextRun(selected, new DateTime(2026, 11, 14, 12, 0, 0)) == new DateTime(2026, 11, 15, 10, 0, 0),
                      "editing a weekly task updates its schedule and next execution day");
            }
            var legacy = service.Tasks[0]; legacy.CharacterId = ""; legacy.History[0].CharacterId = "";
            service.AssignLegacyTasks("pippin");
            Check(legacy.CharacterId == "pippin" && legacy.History[0].CharacterId == "pippin", "legacy tasks and records receive an explicit migrated owner");
        }
        using (var reloaded = new ClaudeTaskService())
        {
            var weekly = new System.Collections.Generic.List<ScheduledClaudeTask>(reloaded.Tasks).Find(task => task.Weekday == (int)DayOfWeek.Sunday);
            Check(reloaded.Tasks.Count == 4 && reloaded.Tasks[0].History.Count > 0 && !String.IsNullOrEmpty(reloaded.Tasks[0].CharacterId) && weekly != null,
                  "weekly day, owner assignments and run history persist across reload");
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var dateStyle = System.Globalization.DateTimeStyles.RoundtripKind;
            DateTime due = DateTime.Parse(weekly.NextRunAt, culture, dateStyle);
            int runs = 0;
            reloaded.TaskDue += delegate(ScheduledClaudeTask task) { if (task == weekly) runs++; };
            reloaded.CheckNow(due.AddSeconds(-1));
            Check(runs == 0 && due.DayOfWeek == DayOfWeek.Sunday && due.TimeOfDay == TimeSpan.FromHours(10), "the saved weekly task waits for Sunday at 10:00");
            reloaded.CheckNow(due);
            reloaded.CheckNow(due.AddSeconds(1));
            Check(runs == 1 && DateTime.Parse(weekly.NextRunAt, culture, dateStyle) == due.AddDays(7),
                  "the weekly task dispatches once and schedules the same time next week");
        }
    }

    private static void TestReminderWeekday()
    {
        string path = Path.Combine(XianPal.Program.StateDirectory, "reminders.json");
        File.WriteAllText(path, "{\"Reminders\":[{\"Text\":\"Legacy daily\",\"MinutesAfterMidnight\":600}]}");
        UiLanguage.Set("en");
        using (var service = new ReminderService())
        using (var window = new ReminderForm(service))
        {
            var repeat = (ComboBox)Field(window, "recurrence");
            var weekday = (ComboBox)Field(window, "weekday");
            UiLanguageBindings.Apply(window);
            Check(repeat.SelectedIndex == 0 && (string)repeat.Items[1] == "Weekly" && weekday.Items.Count == 7 &&
                  (string)weekday.Items[(int)DayOfWeek.Sunday] == "Sunday", "reminder editor offers daily and every weekday in English");
            repeat.SelectedIndex = 1;
            weekday.SelectedIndex = (int)DayOfWeek.Sunday;
            ((DateTimePicker)Field(window, "time")).Value = DateTime.Today.AddHours(10);
            ((TextBox)Field(window, "task")).Text = "Weekly reminder";
            window.GetType().GetMethod("AddReminder", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
        }
        using (var service = new ReminderService())
        {
            var reminders = new System.Collections.Generic.List<DailyReminder>(service.Reminders);
            var legacy = reminders.Find(item => item.Text == "Legacy daily");
            var weekly = reminders.Find(item => item.Text == "Weekly reminder");
            Check(legacy != null && legacy.Weekday == -1 && weekly != null && weekly.Weekday == (int)DayOfWeek.Sunday,
                  "weekly reminder survives reload and a legacy reminder stays daily");
            int dailyCount = 0, weeklyCount = 0;
            service.ReminderDue += delegate(DailyReminder reminder)
            {
                if (reminder == legacy) dailyCount++;
                if (reminder == weekly) weeklyCount++;
            };
            var saturday = new DateTime(2026, 11, 14, 9, 59, 0);
            service.CheckNow(saturday);
            service.CheckNow(saturday.AddMinutes(1));
            Check(dailyCount == 1 && weeklyCount == 0, "Saturday fires the daily reminder only");
            service.CheckNow(saturday.AddDays(1));
            service.CheckNow(saturday.AddDays(1).AddMinutes(1));
            service.CheckNow(saturday.AddDays(1).AddMinutes(2));
            Check(dailyCount == 2 && weeklyCount == 1, "Sunday fires the weekly reminder once at 10:00");
            service.CheckNow(saturday.AddDays(8));
            service.CheckNow(saturday.AddDays(8).AddMinutes(1));
            Check(weeklyCount == 2, "the same weekly reminder fires again the following Sunday");
        }
    }
}
