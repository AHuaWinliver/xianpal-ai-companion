// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace XianPal
{
    internal sealed partial class PetForm
    {
        private Form defaultSettingsWindow;

        private void ShowDefaultSettings()
        {
            if (defaultSettingsWindow != null && !defaultSettingsWindow.IsDisposed)
            { defaultSettingsWindow.Show(); defaultSettingsWindow.Activate(); return; }
            var window = new Form { Name = "DefaultSettingsWindow", Text = UiLanguage.Text("Default settings"), ClientSize = new Size(720, 540),
                MinimumSize = new Size(650, 510), StartPosition = FormStartPosition.CenterScreen,
                Icon = AppIcon.WindowIcon, Font = UiText.Font(9.5f), BackColor = Color.FromArgb(250, 250, 248) };
            defaultSettingsWindow = window;
            var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 8) };
            var general = SettingsPage(tabs, "General");
            var profilesPage = SettingsPage(tabs, "Companions");
            var appearance = SettingsPage(tabs, "Appearance");
            var behavior = SettingsPage(tabs, "Behavior");
            var language = new ComboBox { Name = "InterfaceLanguage", DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
            language.Items.AddRange(new object[] { "English", "简体中文" });
            language.SelectedIndex = UiLanguage.IsEnglish ? 0 : 1;
            SettingsRow(general, "Language", language);
            var startup = SettingsCheck(general, "Start at login", IsStartupEnabled());
            var onTop = SettingsCheck(general, "Keep on top", TopMost);
            var monitor = SettingsCheck(general, "Monitor external Claude sessions (off by default)", integrationPreferences.MonitorExternalSessions);
            var work = new TextBox { Name = "DefaultWorkingDirectory", Text = promptForm.WorkingDirectory, Width = 350 };
            SettingsRow(general, "Default Claude working directory", SettingsPicker(work, true, false));
            var visibility = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
            visibility.Items.AddRange(new object[] { UiLanguage.Text("All virtual desktops"), UiLanguage.Text("Desktop 1 only") });
            visibility.SelectedIndex = desktopScope == "first" ? 1 : 0;
            SettingsRow(general, "Virtual desktop visibility", visibility);

            var draft = new JavaScriptSerializer().Deserialize<IntegrationPreferences>(new JavaScriptSerializer().Serialize(integrationPreferences));
            var profileIds = new List<string> { "" };
            var names = new List<string> { UiLanguage.Text("Global defaults") };
            foreach (PetCharacter profile in PetCharacter.Available) { profileIds.Add(profile.Id); names.Add(UiLanguage.Text(profile.Name)); }
            var scope = new ComboBox { Name = "CompanionScope", DropDownStyle = ComboBoxStyle.DropDownList, Width = 350 };
            scope.Items.AddRange(names.ToArray());
            var executable = new TextBox { Name = "ClaudeExecutable", Width = 350 };
            var configuration = new TextBox { Name = "ClaudeSettings", Width = 350 };
            SettingsRow(profilesPage, "Companions", scope);
            SettingsRow(profilesPage, "Claude executable", SettingsPicker(executable, false, false));
            SettingsRow(profilesPage, "Claude settings file", SettingsPicker(configuration, false, true));
            profilesPage.Controls.Add(new Label { Text = UiLanguage.Text("Leave a field empty to inherit the global value. Empty global fields use Claude's defaults."),
                AutoSize = true, MaximumSize = new Size(620, 0), Margin = new Padding(12) });
            string currentScope = "";
            var dirty = new HashSet<string>();
            Action storeFields = delegate
            {
                string oldExe = RawExecutable(draft, currentScope);
                string oldSettings = draft.SettingsPathFor(currentScope);
                if (oldExe != executable.Text || oldSettings != configuration.Text) dirty.Add(currentScope);
                if (currentScope.Length == 0) { draft.ClaudeExecutablePath = executable.Text; draft.ClaudeSettingsPath = configuration.Text; }
                else
                {
                    if (draft.CharacterClaudeExecutables == null) draft.CharacterClaudeExecutables = new Dictionary<string, string>();
                    if (draft.CharacterClaudeSettings == null) draft.CharacterClaudeSettings = new Dictionary<string, string>();
                    draft.CharacterClaudeExecutables[currentScope] = executable.Text;
                    draft.CharacterClaudeSettings[currentScope] = configuration.Text;
                }
            };
            executable.Text = RawExecutable(draft, ""); configuration.Text = draft.SettingsPathFor("");
            scope.SelectedIndexChanged += delegate
            {
                storeFields(); currentScope = profileIds[Math.Max(0, scope.SelectedIndex)];
                executable.Text = RawExecutable(draft, currentScope); configuration.Text = draft.SettingsPathFor(currentScope);
            };
            scope.SelectedIndex = Math.Max(0, profileIds.IndexOf(currentCharacterId));

            var scale = new NumericUpDown { Minimum = 50, Maximum = 125, Increment = 5, Value = (decimal)(petScale * 100), Width = 150 };
            var dock = new NumericUpDown { Name = "DockedOpacity", Minimum = 20, Maximum = 100, Value = dockOpacityPercent, Width = 150 };
            var trail = new NumericUpDown { Minimum = 0, Maximum = 100, Value = (decimal)(trailOpacity * 100), Width = 150 };
            SettingsRow(appearance, "Companion size", scale);
            SettingsRow(appearance, "Docked opacity", dock);
            SettingsRow(appearance, "Trail opacity", trail);
            var reduced = SettingsCheck(appearance, "Reduce motion", reduceMotion);
            var reset = new Button { Text = UiLanguage.Text("Reset size and position"), AutoSize = true, Margin = new Padding(12) };
            reset.Click += delegate { SetPetScale(0.5f, false); ResetPosition(); SavePreferences(); scale.Value = 50; };
            appearance.Controls.Add(reset);
            var care = SettingsCheck(behavior, "Allow proactive messages and break reminders", aiCareEnabled);
            var minutes = new NumericUpDown { Minimum = 5, Maximum = 240, Value = breakReminderMinutes, Width = 150 };
            SettingsRow(behavior, "Break reminder interval (minutes)", minutes);
            var paths = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 350 };
            paths.Items.AddRange(new object[] { UiLanguage.Text("Built-in random curves"), UiLanguage.Text("AI route pool (24 routes per request)") });
            paths.SelectedIndex = movementPathMode == "ai" ? 1 : 0;
            SettingsRow(behavior, "Roaming routes", paths);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12) };
            var apply = new Button { Name = "ApplySettings", Text = UiLanguage.Text("Apply"), AutoSize = true, Height = 32 };
            var cancel = new Button { Text = UiLanguage.Text("Cancel"), AutoSize = true, Height = 32 };
            cancel.Click += delegate { window.Close(); };
            apply.Click += delegate
            {
                try
                {
                    storeFields();
                    if (!Directory.Exists(work.Text)) throw new DirectoryNotFoundException(UiLanguage.Text("Working directory does not exist."));
                    foreach (string id in dirty)
                    {
                        draft.SetExecutablePath(id, RawExecutable(draft, id));
                        draft.SetSettingsPath(id, ClaudeSettingsFile.Validate(draft.SettingsPathFor(id)));
                    }
                    draft.MonitorExternalSessions = monitor.Checked;
                    draft.Save();
                    integrationPreferences.ClaudeExecutablePath = draft.ClaudeExecutablePath;
                    integrationPreferences.ClaudeSettingsPath = draft.ClaudeSettingsPath;
                    integrationPreferences.CharacterClaudeExecutables = draft.CharacterClaudeExecutables;
                    integrationPreferences.CharacterClaudeSettings = draft.CharacterClaudeSettings;
                    integrationPreferences.MonitorExternalSessions = draft.MonitorExternalSessions;
                    if (startup.Checked != IsStartupEnabled()) SetStartup(startup.Checked);
                    TopMost = onTop.Checked; dockOpacityPercent = (int)dock.Value; trailOpacity = (float)trail.Value / 100;
                    reduceMotion = reduced.Checked;
                    SetPetScale((float)scale.Value / 100, true);
                    bool careChanged = aiCareEnabled != care.Checked || breakReminderMinutes != (int)minutes.Value;
                    aiCareEnabled = care.Checked; breakReminderMinutes = (int)minutes.Value;
                    string pathMode = paths.SelectedIndex == 1 ? "ai" : "builtin";
                    if (pathMode != movementPathMode) SetMovementPathMode(pathMode);
                    string selectedScope = visibility.SelectedIndex == 1 ? "first" : "all";
                    if (selectedScope != desktopScope) SelectDesktopScope(selectedScope);
                    promptForm.SetWorkingDirectory(Path.GetFullPath(work.Text));
                    if (careChanged) ResetScreenSession(screenSessionActive);
                    if (reduceMotion) CancelWander();
                    UiLanguage.Set(language.SelectedIndex == 1 ? "zh-CN" : "en");
                    SavePreferences(); RefreshPetVisual();
                    window.Close();
                }
                catch (Exception error) { MessageBox.Show(window, UiLanguage.Text(error.Message), UiLanguage.Text("Settings were not saved"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            buttons.Controls.Add(apply); buttons.Controls.Add(cancel);
            window.Controls.Add(tabs); window.Controls.Add(buttons);
            window.FormClosed += delegate { if (defaultSettingsWindow == window) defaultSettingsWindow = null; };
            window.Show(this);
        }

        private static string RawExecutable(IntegrationPreferences preferences, string id)
        {
            if (String.IsNullOrEmpty(id)) return preferences.ClaudeExecutablePath ?? "";
            string value;
            return preferences.CharacterClaudeExecutables != null && preferences.CharacterClaudeExecutables.TryGetValue(id, out value) ? value ?? "" : "";
        }

        private static FlowLayoutPanel SettingsPage(TabControl tabs, string title)
        {
            var page = new TabPage(UiLanguage.Text(title));
            var body = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12) };
            page.Controls.Add(body); tabs.TabPages.Add(page); return body;
        }

        private static void SettingsRow(FlowLayoutPanel panel, string title, Control control)
        {
            var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(10, 7, 10, 7) };
            row.Controls.Add(new Label { Text = UiLanguage.Text(title), AutoSize = true, Margin = new Padding(0, 0, 0, 5) });
            row.Controls.Add(control); panel.Controls.Add(row);
        }

        private static CheckBox SettingsCheck(FlowLayoutPanel panel, string text, bool value)
        {
            var check = new CheckBox { Text = UiLanguage.Text(text), Checked = value, AutoSize = true, Margin = new Padding(12, 8, 12, 8) };
            panel.Controls.Add(check); return check;
        }

        private static Control SettingsPicker(TextBox text, bool folder, bool json)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            var browse = new Button { Text = UiLanguage.Text("Browse…"), AutoSize = true };
            browse.Click += delegate
            {
                if (folder)
                {
                    using (var picker = new FolderBrowserDialog())
                    {
                        if (Directory.Exists(text.Text)) picker.SelectedPath = text.Text;
                        if (picker.ShowDialog() == DialogResult.OK) text.Text = picker.SelectedPath;
                    }
                }
                else using (var picker = new OpenFileDialog { CheckFileExists = true, RestoreDirectory = true,
                    Filter = json ? "JSON (*.json)|*.json" : "Claude executable (*.exe;*.cmd;*.bat)|*.exe;*.cmd;*.bat" })
                    if (picker.ShowDialog() == DialogResult.OK) text.Text = picker.FileName;
            };
            row.Controls.Add(text); row.Controls.Add(browse); return row;
        }
    }
}
