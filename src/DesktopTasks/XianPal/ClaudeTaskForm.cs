using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed class ClaudeTaskForm : Form
    {
        private readonly ClaudeTaskService service;
        private readonly TextBox prompt = new TextBox();
        private readonly Button directory = new Button();
        private readonly ComboBox recurrence = new ComboBox();
        private readonly ComboBox weekday = new ComboBox();
        private readonly DateTimePicker time = new DateTimePicker();
        private readonly NumericUpDown intervalValue = new NumericUpDown();
        private readonly ComboBox intervalUnit = new ComboBox();
        private readonly ComboBox runMode = new ComboBox();
        private readonly ComboBox overlapMode = new ComboBox();
        private readonly ComboBox desktop = new ComboBox();
        private readonly Button defaultDesktop = new Button();
        private readonly ListBox list = new ListBox();
        private readonly Button toggle = new Button();
        private readonly Button delete = new Button();
        private readonly Button runNow = new Button();
        private readonly Button edit = new Button();
        private readonly Button history = new Button();
        private readonly Button save = new Button();
        private readonly Button cancelEdit = new Button();
        private string workingDirectory;
        private ScheduledClaudeTask editingTask;

        internal ClaudeTaskForm(ClaudeTaskService taskService)
        {
            service = taskService;
            Text = "Claude Code 定时任务";
            Icon = AppIcon.WindowIcon;
            ClientSize = new Size(610, 634);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(253, 253, 252);
            DoubleBuffered = true;
            Font = UiText.Font(9f);

            Label title = new Label { Text = "Claude Code 定时任务", Font = UiText.Font(11f, FontStyle.Bold), ForeColor = Color.FromArgb(34, 34, 32), AutoSize = true, Location = new Point(20, 17) };
            Label subtitle = new Label { Text = "到点后在指定目录自动执行", ForeColor = Color.FromArgb(112, 112, 106), AutoSize = true, Location = new Point(188, 20) };
            Button close = TextButton("×", new Point(564, 8), new Size(30, 30));
            close.Font = new Font("Segoe UI", 13f);
            close.Click += delegate { Hide(); };

            RoundedPanel editor = new RoundedPanel { Radius = 16, BorderColor = Color.FromArgb(226, 226, 223), BackColor = Color.FromArgb(247, 247, 246), Location = new Point(16, 52), Size = new Size(578, 272) };
            Label promptLabel = NewLabel("执行任务", new Point(16, 14), editor.BackColor);
            prompt.Multiline = true;
            prompt.ScrollBars = ScrollBars.Vertical;
            prompt.BorderStyle = BorderStyle.FixedSingle;
            prompt.Font = UiText.Font(9.5f);
            prompt.Location = new Point(88, 12);
            prompt.Size = new Size(472, 82);
            prompt.BackColor = Color.White;

            directory.TextAlign = ContentAlignment.MiddleLeft;
            directory.Location = new Point(88, 102);
            directory.Size = new Size(200, 29);
            StyleButton(directory, false);
            directory.Click += ChooseDirectory;

            runMode.DropDownStyle = ComboBoxStyle.DropDownList;
            runMode.Items.AddRange(new object[] { "后台运行（旧）", "PowerShell 窗口", "终端窗口", "CLI 交互·PS", "CLI 交互·终端", "Claude BG·关窗" });
            runMode.DropDownWidth = 240;
            runMode.SelectedIndex = 3;
            runMode.Location = new Point(296, 102);
            runMode.Size = new Size(126, 28);

            overlapMode.DropDownStyle = ComboBoxStyle.DropDownList;
            overlapMode.Items.AddRange(new object[] { "阻塞模式", "并行开窗" });
            overlapMode.SelectedIndex = 0;
            overlapMode.Location = new Point(430, 102);
            overlapMode.Size = new Size(130, 28);

            desktop.DropDownStyle = ComboBoxStyle.DropDownList;
            desktop.Items.Add("当前桌面");
            for (int i = 1; i <= 99; i++) desktop.Items.Add("桌面 " + i);
            desktop.SelectedIndex = service.DefaultDesktopNumber;
            desktop.Location = new Point(88, 184);
            desktop.Size = new Size(126, 28);
            defaultDesktop.Text = "设为新任务默认";
            defaultDesktop.Location = new Point(222, 180);
            defaultDesktop.Size = new Size(130, 30);
            StyleButton(defaultDesktop, false);
            defaultDesktop.Click += delegate
            {
                service.SetDefaultDesktop(desktop.SelectedIndex);
                MessageBox.Show("新任务默认窗口位置：" + desktop.Text + "。已有任务保持原设置。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            desktop.Enabled = defaultDesktop.Enabled = runMode.SelectedIndex > 0;
            runMode.SelectedIndexChanged += delegate { desktop.Enabled = defaultDesktop.Enabled = runMode.SelectedIndex > 0; };
            editor.Controls.Add(NewLabel("运行桌面", new Point(16, 188), editor.BackColor));
            editor.Controls.Add(desktop);
            editor.Controls.Add(defaultDesktop);
            editor.Controls.Add(new Label { Text = "虚拟桌面（Win+Tab），需提前创建", AutoSize = true, Location = new Point(88, 218), ForeColor = Color.FromArgb(120, 120, 115), Font = UiText.Font(7.5f), BackColor = editor.BackColor });

            recurrence.DropDownStyle = ComboBoxStyle.DropDownList;
            recurrence.Items.AddRange(new object[] { "每天", "每周", "每隔一段时间" });
            recurrence.SelectedIndex = 0;
            recurrence.Location = new Point(88, 141);
            recurrence.Size = new Size(126, 28);
            recurrence.SelectedIndexChanged += delegate { UpdateScheduleControls(); };

            weekday.DropDownStyle = ComboBoxStyle.DropDownList;
            weekday.Items.AddRange(new object[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" });
            weekday.SelectedIndex = (int)DateTime.Now.DayOfWeek;
            weekday.Location = new Point(222, 141);
            weekday.Size = new Size(84, 28);

            time.Format = DateTimePickerFormat.Custom;
            time.CustomFormat = "HH:mm";
            time.ShowUpDown = true;
            time.Value = DateTime.Today.AddHours(9);
            time.Location = new Point(314, 141);
            time.Size = new Size(82, 28);

            intervalValue.Minimum = 1;
            intervalValue.Maximum = 10080;
            intervalValue.Value = 60;
            intervalValue.Location = new Point(222, 141);
            intervalValue.Size = new Size(82, 28);
            intervalUnit.DropDownStyle = ComboBoxStyle.DropDownList;
            intervalUnit.Items.AddRange(new object[] { "分钟", "小时", "天" });
            intervalUnit.SelectedIndex = 0;
            intervalUnit.Location = new Point(314, 141);
            intervalUnit.Size = new Size(82, 28);

            cancelEdit.Text = "取消";
            cancelEdit.Location = new Point(402, 138);
            cancelEdit.Size = new Size(56, 34);
            cancelEdit.Visible = false;
            StyleButton(cancelEdit, false);
            cancelEdit.Click += delegate { CancelEditing(); };
            save.Text = "添加任务";
            save.Location = new Point(464, 138);
            save.Size = new Size(96, 34);
            StyleButton(save, true);
            save.Click += delegate { SaveTask(); };
            Label note = new Label { Text = "任务由本机 claude CLI 执行；电脑休眠期间不会唤醒，恢复后补执行到期任务。", ForeColor = Color.FromArgb(120, 120, 115), AutoSize = true, Location = new Point(17, 242), BackColor = editor.BackColor, Font = UiText.Font(7.5f) };
            runMode.SelectedIndexChanged += delegate
            {
                note.Text = runMode.SelectedIndex == 5 ? "Claude 原生 Background：创建成功后关窗；阻塞等待后台完成，需已支持 --bg 的 CLI。" : runMode.SelectedIndex >= 3 ? "原生 CLI 可继续追问及确认权限；阻塞模式等待此 CLI 会话退出，不自动关窗。" : "任务由本机 claude CLI 执行；电脑休眠期间不会唤醒，恢复后补执行到期任务。";
            };
            note.Text = "原生 CLI 可继续追问及确认权限；阻塞模式等待此 CLI 会话退出，不自动关窗。";

            editor.Controls.Add(promptLabel);
            editor.Controls.Add(prompt);
            editor.Controls.Add(directory);
            editor.Controls.Add(runMode);
            editor.Controls.Add(overlapMode);
            editor.Controls.Add(recurrence);
            editor.Controls.Add(weekday);
            editor.Controls.Add(time);
            editor.Controls.Add(intervalValue);
            editor.Controls.Add(intervalUnit);
            editor.Controls.Add(cancelEdit);
            editor.Controls.Add(save);
            editor.Controls.Add(note);

            Label taskTitle = new Label { Text = "已保存任务", Font = UiText.Font(9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(52, 52, 49), AutoSize = true, Location = new Point(20, 290) };
            RoundedPanel listCard = new RoundedPanel { Radius = 16, BorderColor = Color.FromArgb(226, 226, 223), BackColor = Color.FromArgb(247, 247, 246), Location = new Point(16, 316), Size = new Size(578, 205) };
            list.BorderStyle = BorderStyle.None;
            list.BackColor = listCard.BackColor;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = 62;
            list.IntegralHeight = false;
            list.Location = new Point(8, 8);
            list.Size = new Size(562, 189);
            list.DrawItem += DrawTask;
            list.SelectedIndexChanged += delegate { UpdateButtons(); };
            list.DoubleClick += delegate { EditSelectedTask(); };
            listCard.Controls.Add(list);

            toggle.Text = "停用";
            toggle.Location = new Point(16, 535);
            toggle.Size = new Size(72, 34);
            StyleButton(toggle, false);
            toggle.Click += delegate { ScheduledClaudeTask item = list.SelectedItem as ScheduledClaudeTask; service.Toggle(item, DateTime.Now); RefreshList(item); };
            delete.Text = "删除";
            delete.Location = new Point(96, 535);
            delete.Size = new Size(72, 34);
            StyleButton(delete, false);
            delete.ForeColor = Color.FromArgb(174, 65, 58);
            delete.Click += delegate { service.Delete(list.SelectedItem as ScheduledClaudeTask); RefreshList(null); };
            edit.Text = "编辑";
            edit.Location = new Point(176, 535);
            edit.Size = new Size(72, 34);
            StyleButton(edit, false);
            edit.Click += delegate { EditSelectedTask(); };
            history.Text = "运行情况";
            history.Location = new Point(256, 535);
            history.Size = new Size(96, 34);
            StyleButton(history, false);
            history.Click += delegate { ShowHistory(); };
            runNow.Text = "立即执行";
            runNow.Location = new Point(476, 535);
            runNow.Size = new Size(118, 34);
            StyleButton(runNow, true);
            runNow.Click += delegate { service.RunNow(list.SelectedItem as ScheduledClaudeTask); };

            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(close);
            Controls.Add(editor);
            Controls.Add(taskTitle);
            Controls.Add(listCard);
            Controls.Add(toggle);
            Controls.Add(delete);
            Controls.Add(edit);
            Controls.Add(history);
            Controls.Add(runNow);
            taskTitle.Top += 48;
            listCard.Top += 48;
            foreach (Button button in new Button[] { toggle, delete, edit, history, runNow }) button.Top += 48;
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            Resize += delegate { ApplyRoundedRegion(); };
            service.Changed += delegate { if (!IsDisposed) RefreshList(list.SelectedItem as ScheduledClaudeTask); };
            workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            UpdateDirectoryText();
            UpdateScheduleControls();
            RefreshList(null);
            ApplyRoundedRegion();
        }

        internal void ShowNear(Rectangle petBounds, string defaultDirectory)
        {
            if (!String.IsNullOrWhiteSpace(defaultDirectory) && Directory.Exists(defaultDirectory))
            {
                workingDirectory = Path.GetFullPath(defaultDirectory);
                UpdateDirectoryText();
            }
            Rectangle area = Screen.FromRectangle(petBounds).WorkingArea;
            int x = Math.Max(area.Left + 8, Math.Min(petBounds.Left + (petBounds.Width - Width) / 2, area.Right - Width - 8));
            int y = petBounds.Bottom + 8;
            if (y + Height > area.Bottom - 8) y = petBounds.Top - Height - 8;
            Location = new Point(x, Math.Max(area.Top + 8, y));
            if (!Visible) Show();
            Location = new Point(x, Math.Max(area.Top + 8, y));
            Activate();
            prompt.Focus();
        }

        private void ChooseDirectory(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择该定时任务的 Claude Code 工作目录";
                dialog.SelectedPath = workingDirectory;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                workingDirectory = dialog.SelectedPath;
                UpdateDirectoryText();
            }
        }

        private string SelectedRunMode()
        {
            string[] modes = { "background", "powershell", "terminal", "interactive-powershell", "interactive-terminal", "claude-background" };
            return modes[Math.Max(0, Math.Min(modes.Length - 1, runMode.SelectedIndex))];
        }

        private void SaveTask()
        {
            string taskText = prompt.Text.Trim();
            if (taskText.Length == 0) { prompt.Focus(); return; }
            string mode = recurrence.SelectedIndex == 1 ? "weekly" : (recurrence.SelectedIndex == 2 ? "interval" : "daily");
            int multiplier = intervalUnit.SelectedIndex == 2 ? 1440 : (intervalUnit.SelectedIndex == 1 ? 60 : 1);
            try
            {
                ScheduledClaudeTask added = editingTask;
                if (editingTask == null)
                    added = service.Add(taskText, workingDirectory, mode, time.Value.TimeOfDay, (DayOfWeek)Math.Max(0, weekday.SelectedIndex), (int)intervalValue.Value * multiplier, DateTime.Now, SelectedRunMode(), overlapMode.SelectedIndex == 1 ? "parallel" : "block");
                else
                    service.Update(editingTask, taskText, workingDirectory, mode, time.Value.TimeOfDay, (DayOfWeek)Math.Max(0, weekday.SelectedIndex), (int)intervalValue.Value * multiplier, DateTime.Now, SelectedRunMode(), overlapMode.SelectedIndex == 1 ? "parallel" : "block");
                service.SetDesktop(added, desktop.SelectedIndex);
                prompt.Clear();
                editingTask = null;
                save.Text = "添加任务";
                cancelEdit.Visible = false;
                desktop.SelectedIndex = service.DefaultDesktopNumber;
                RefreshList(added);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void EditSelectedTask()
        {
            ScheduledClaudeTask task = list.SelectedItem as ScheduledClaudeTask;
            if (task == null) return;
            editingTask = task;
            prompt.Text = task.Prompt;
            workingDirectory = task.WorkingDirectory;
            UpdateDirectoryText();
            recurrence.SelectedIndex = task.Recurrence == "weekly" ? 1 : (task.Recurrence == "interval" ? 2 : 0);
            runMode.SelectedIndex = Array.IndexOf(new[] { "background", "powershell", "terminal", "interactive-powershell", "interactive-terminal", "claude-background" }, ClaudeTaskService.NormalizeRunMode(task.RunMode));
            overlapMode.SelectedIndex = task.OverlapMode == "parallel" ? 1 : 0;
            desktop.SelectedIndex = ClaudeTaskService.NormalizeDesktopNumber(task.DesktopNumber);
            weekday.SelectedIndex = Math.Max(0, Math.Min(6, task.Weekday));
            time.Value = DateTime.Today.AddMinutes(Math.Max(0, Math.Min(1439, task.MinutesAfterMidnight)));
            int interval = Math.Max(1, task.IntervalMinutes);
            if (interval % 1440 == 0) { intervalUnit.SelectedIndex = 2; interval /= 1440; }
            else if (interval % 60 == 0) { intervalUnit.SelectedIndex = 1; interval /= 60; }
            else intervalUnit.SelectedIndex = 0;
            intervalValue.Value = Math.Max(intervalValue.Minimum, Math.Min(intervalValue.Maximum, interval));
            save.Text = "保存修改";
            cancelEdit.Visible = true;
            UpdateScheduleControls();
            prompt.Focus();
            prompt.SelectionStart = prompt.TextLength;
        }

        private void CancelEditing()
        {
            editingTask = null;
            prompt.Clear();
            save.Text = "添加任务";
            cancelEdit.Visible = false;
            desktop.SelectedIndex = service.DefaultDesktopNumber;
        }

        private void ShowHistory()
        {
            ScheduledClaudeTask task = list.SelectedItem as ScheduledClaudeTask;
            if (task == null) return;
            using (ClaudeTaskHistoryForm form = new ClaudeTaskHistoryForm(task)) form.ShowDialog(this);
        }

        private void UpdateScheduleControls()
        {
            bool isInterval = recurrence.SelectedIndex == 2;
            bool isWeekly = recurrence.SelectedIndex == 1;
            weekday.Visible = isWeekly;
            time.Visible = !isInterval;
            intervalValue.Visible = intervalUnit.Visible = isInterval;
        }

        private void UpdateDirectoryText()
        {
            string clean = (workingDirectory ?? "").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(clean);
            directory.Text = "目录  ·  " + (String.IsNullOrEmpty(name) ? clean : name);
        }

        private void RefreshList(ScheduledClaudeTask selected)
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (ScheduledClaudeTask task in service.Tasks) list.Items.Add(task);
            list.EndUpdate();
            if (selected != null && list.Items.Contains(selected)) list.SelectedItem = selected;
            else if (list.Items.Count > 0) list.SelectedIndex = 0;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            ScheduledClaudeTask task = list.SelectedItem as ScheduledClaudeTask;
            toggle.Enabled = delete.Enabled = runNow.Enabled = edit.Enabled = history.Enabled = task != null;
            toggle.Text = task != null && task.Enabled ? "停用" : "启用";
        }

        private void DrawTask(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            ScheduledClaudeTask task = list.Items[e.Index] as ScheduledClaudeTask;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color background = selected ? Color.FromArgb(231, 241, 234) : list.BackColor;
            using (SolidBrush brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
            using (SolidBrush dot = new SolidBrush(task.Enabled ? Color.FromArgb(40, 132, 84) : Color.FromArgb(170, 170, 165))) e.Graphics.FillEllipse(dot, e.Bounds.Left + 10, e.Bounds.Top + 16, 8, 8);
            Rectangle scheduleBounds = new Rectangle(e.Bounds.Left + 28, e.Bounds.Top + 8, 126, 24);
            Rectangle promptBounds = new Rectangle(e.Bounds.Left + 158, e.Bounds.Top + 8, e.Bounds.Width - 168, 24);
            Rectangle pathBounds = new Rectangle(e.Bounds.Left + 28, e.Bounds.Top + 34, e.Bounds.Width - 38, 21);
            string scheduleText = (ClaudeTaskService.RunModeLabel(task.RunMode) + "/") + (task.OverlapMode == "parallel" ? "并行 · " : "阻塞 · ") + task.ScheduleText;
            using (Font bold = new Font(list.Font, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, scheduleText, bold, scheduleBounds, Color.FromArgb(38, 38, 36), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, task.Prompt, list.Font, promptBounds, task.Enabled ? Color.FromArgb(38, 38, 36) : Color.FromArgb(130, 130, 126), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (Font pathFont = UiText.Font(7.5f))
                TextRenderer.DrawText(e.Graphics, (task.RunMode != "background" ? (task.DesktopNumber == 0 ? "当前桌面" : "桌面 " + task.DesktopNumber) + " · " : "") + task.WorkingDirectory, pathFont, pathBounds, Color.FromArgb(118, 118, 112), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static Label NewLabel(string text, Point location, Color backColor)
        {
            return new Label { Text = text, AutoSize = true, Location = location, ForeColor = Color.FromArgb(80, 80, 76), BackColor = backColor };
        }

        private static Button TextButton(string text, Point location, Size size)
        {
            Button button = new Button { Text = text, Location = location, Size = size, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private static void StyleButton(Button button, bool primary)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.Cursor = Cursors.Hand;
            button.BackColor = primary ? Color.FromArgb(32, 33, 31) : Color.FromArgb(247, 247, 245);
            button.ForeColor = primary ? Color.White : Color.FromArgb(60, 60, 57);
            button.FlatAppearance.BorderColor = primary ? Color.FromArgb(32, 33, 31) : Color.FromArgb(220, 220, 216);
        }

        private void ApplyRoundedRegion()
        {
            Region old = Region;
            Region = new Region(PromptForm.RoundedRectangle(ClientRectangle, 18));
            if (old != null) old.Dispose();
        }
    }

    internal sealed class ClaudeTaskHistoryForm : Form
    {
        private readonly ScheduledClaudeTask task;
        private readonly ListView runs = new ListView();
        private readonly RichTextBox details = new UnicodeRichTextBox();
        private readonly Timer liveRefresh = new Timer { Interval = 400 };

        internal ClaudeTaskHistoryForm(ScheduledClaudeTask scheduledTask)
        {
            task = scheduledTask;
            Text = "任务运行情况";
            Icon = AppIcon.WindowIcon;
            ClientSize = new Size(680, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(253, 253, 252);
            Font = UiText.Font(9f);

            Label title = new Label { Text = "运行情况  ·  双击记录查看历史对话", Font = UiText.Font(12f, FontStyle.Bold), AutoSize = true, Location = new Point(18, 16), ForeColor = Color.FromArgb(34, 34, 32) };
            Label taskText = new Label { Text = task == null ? "" : task.Prompt, AutoEllipsis = true, Location = new Point(18, 48), Size = new Size(644, 25), ForeColor = Color.FromArgb(70, 70, 66) };
            Label path = new Label { Text = task == null ? "" : "目录：" + task.WorkingDirectory, AutoEllipsis = true, Location = new Point(18, 74), Size = new Size(644, 23), ForeColor = Color.FromArgb(112, 112, 106), Font = UiText.Font(8f) };

            runs.View = View.Details;
            runs.FullRowSelect = true;
            runs.HideSelection = false;
            runs.GridLines = false;
            runs.Location = new Point(18, 108);
            runs.Size = new Size(644, 205);
            runs.Columns.Add("状态", 82);
            runs.Columns.Add("方式", 82);
            runs.Columns.Add("开始时间", 150);
            runs.Columns.Add("完成时间", 140);
            runs.Columns.Add("耗时", 74);
            runs.Columns.Add("退出码", 62);
            runs.SelectedIndexChanged += delegate { ShowSelectedDetails(); };
            runs.DoubleClick += delegate { OpenSelectedTranscript(); };

            details.Location = new Point(18, 326);
            details.Size = new Size(644, 112);
            details.ReadOnly = true;
            details.BorderStyle = BorderStyle.FixedSingle;
            details.BackColor = Color.FromArgb(247, 247, 245);
            details.Font = UiText.ContentFont(9f);
            details.Text = "选择一条运行记录查看 Claude 回复摘要。";

            Button close = new Button { Text = "关闭", DialogResult = DialogResult.OK, Location = new Point(574, 446), Size = new Size(88, 28) };
            AcceptButton = close;
            CancelButton = close;
            Controls.Add(title);
            Controls.Add(taskText);
            Controls.Add(path);
            Controls.Add(runs);
            Controls.Add(details);
            Controls.Add(close);
            LoadHistory();
            liveRefresh.Tick += delegate
            {
                if (task != null && task.History != null && task.History.Count != runs.Items.Count) LoadHistory();
                foreach (ListViewItem item in runs.Items)
                {
                    ClaudeTaskRunRecord record = item.Tag as ClaudeTaskRunRecord;
                    if (record == null) continue;
                    item.Text = record.StatusText;
                    DateTime completed, started;
                    bool done = DateTime.TryParse(record.CompletedAt, out completed);
                    item.SubItems[3].Text = done ? completed.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "—";
                    item.SubItems[4].Text = DateTime.TryParse(record.StartedAt, out started) ? FormatDuration((done ? completed : DateTime.Now) - started) : "—";
                    item.SubItems[5].Text = record.Status == "running" || record.RunMode == "claude-background" ? "—" : record.ExitCode.ToString();
                }
                ShowSelectedDetails();
            };
            liveRefresh.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) liveRefresh.Dispose();
            base.Dispose(disposing);
        }

        private void LoadHistory()
        {
            ClaudeTaskRunRecord selected = runs.SelectedItems.Count == 0 ? null : runs.SelectedItems[0].Tag as ClaudeTaskRunRecord;
            runs.Items.Clear();
            if (task == null || task.History == null || task.History.Count == 0)
            {
                details.Text = "该任务还没有运行记录。";
                return;
            }
            foreach (ClaudeTaskRunRecord record in task.History)
            {
                DateTime started, completed;
                bool hasStart = DateTime.TryParse(record.StartedAt, out started);
                bool hasCompleted = DateTime.TryParse(record.CompletedAt, out completed);
                string duration = hasStart && hasCompleted ? FormatDuration(completed - started) : "—";
                ListViewItem item = new ListViewItem(record.StatusText);
                item.ForeColor = record.Status == "success" ? Color.FromArgb(40, 132, 84) : (record.Status == "failed" ? Color.FromArgb(178, 64, 56) : Color.FromArgb(177, 116, 38));
                item.SubItems.Add(ClaudeTaskService.RunModeLabel(record.RunMode));
                item.SubItems.Add(hasStart ? started.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "—");
                item.SubItems.Add(hasCompleted ? completed.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "—");
                item.SubItems.Add(duration);
                item.SubItems.Add(record.Status == "running" || record.RunMode == "claude-background" ? "—" : record.ExitCode.ToString());
                item.Tag = record;
                runs.Items.Add(item);
            }
            if (runs.Items.Count > 0)
            {
                ListViewItem selection = runs.Items[0];
                foreach (ListViewItem item in runs.Items) if (selected != null && ((ClaudeTaskRunRecord)item.Tag).Id == selected.Id) selection = item;
                selection.Selected = true;
            }
        }

        private void ShowSelectedDetails()
        {
            if (runs.SelectedItems.Count == 0) return;
            ClaudeTaskRunRecord record = runs.SelectedItems[0].Tag as ClaudeTaskRunRecord;
            string text = record == null ? "本次运行没有可显示的回复摘要。" :
                (record.RunMode != "background" ? (record.RunMode == "claude-background" ? "创建窗口桌面：" : "运行桌面：") + (record.DesktopNumber == 0 ? "当前桌面" : "桌面 " + record.DesktopNumber) + "\r\n\r\n" : "") +
                (String.IsNullOrWhiteSpace(record.Summary) ? "本次运行没有可显示的回复摘要。" : record.Summary);
            if (text != details.Text) { details.Text = text; UiText.ApplyMixedGlyphFonts(details); }
        }

        private void OpenSelectedTranscript()
        {
            if (runs.SelectedItems.Count == 0) return;
            ClaudeTaskRunRecord record = runs.SelectedItems[0].Tag as ClaudeTaskRunRecord;
            if (record == null) return;
            using (ClaudeTaskRunTranscriptForm form = new ClaudeTaskRunTranscriptForm(task, record)) form.ShowDialog(this);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero) return "—";
            if (duration.TotalHours >= 1) return String.Format("{0}:{1:00}:{2:00}", (int)duration.TotalHours, duration.Minutes, duration.Seconds);
            return String.Format("{0}:{1:00}", (int)duration.TotalMinutes, duration.Seconds);
        }
    }

    internal sealed class ClaudeTaskRunTranscriptForm : Form
    {
        private readonly Timer liveRefresh = new Timer { Interval = 400 };
        internal ClaudeTaskRunTranscriptForm(ScheduledClaudeTask task, ClaudeTaskRunRecord record)
        {
            Text = "Claude Code 历史对话";
            Icon = AppIcon.WindowIcon;
            ClientSize = new Size(760, 560);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(640, 460);
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(253, 253, 252);
            Font = UiText.Font(9f);

            string status = record == null ? "未知" : record.StatusText;
            Label title = new Label { Text = "Claude Code 历史对话 · " + status, Font = UiText.Font(11.5f, FontStyle.Bold), AutoSize = true, Location = new Point(18, 15), ForeColor = Color.FromArgb(34, 34, 32) };
            Label meta = new Label
            {
                Text = BuildMeta(task, record),
                Location = new Point(18, 45),
                Size = new Size(724, 46),
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(100, 100, 95),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            RichTextBox transcript = new UnicodeRichTextBox
            {
                ReadOnly = true,
                DetectUrls = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Font = UiText.ContentFont(9.5f),
                Location = new Point(18, 98),
                Size = new Size(724, 410),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Text = BuildTranscript(task, record)
            };
            UiText.ApplyMixedGlyphFonts(transcript);
            Button close = new Button { Text = "关闭", DialogResult = DialogResult.OK, Location = new Point(654, 520), Size = new Size(88, 29), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            Button attach = new Button { Text = "打开原生后台会话", Location = new Point(18, 520), Size = new Size(170, 29), Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Visible = record != null && record.RunMode == "claude-background" };
            attach.Click += delegate
            {
                try { PetForm.OpenBackgroundSession(task, record); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            Controls.Add(title);
            Controls.Add(meta);
            Controls.Add(transcript);
            Controls.Add(close);
            Controls.Add(attach);
            AcceptButton = close;
            CancelButton = close;
            liveRefresh.Tick += delegate
            {
                string text = BuildTranscript(task, record);
                if (text != transcript.Text)
                {
                    int start = transcript.SelectionStart, length = transcript.SelectionLength;
                    bool atEnd = start + length >= transcript.TextLength;
                    transcript.Text = text;
                    UiText.ApplyMixedGlyphFonts(transcript);
                    transcript.Select(Math.Min(start, transcript.TextLength), Math.Min(length, Math.Max(0, transcript.TextLength - start)));
                    if (atEnd) { transcript.Select(transcript.TextLength, 0); transcript.ScrollToCaret(); }
                }
                meta.Text = BuildMeta(task, record);
                attach.Enabled = record != null && BackgroundTaskSupervisor.ValidJobId(record.BackgroundJobId);
            };
            liveRefresh.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) liveRefresh.Dispose();
            base.Dispose(disposing);
        }

        internal static string BuildTranscript(ScheduledClaudeTask task, ClaudeTaskRunRecord record)
        {
            string conversation = record == null ? "" : record.Transcript;
            if (String.IsNullOrWhiteSpace(conversation) && record != null) conversation = record.Summary;
            if (String.IsNullOrWhiteSpace(conversation)) conversation = "此记录没有保存到可显示的回复内容。";
            return "任务：\r\n" + (task == null ? "" : task.Prompt) + "\r\n\r\nClaude 回复：\r\n" + conversation;
        }

        private static string BuildMeta(ScheduledClaudeTask task, ClaudeTaskRunRecord record)
        {
            DateTime started;
            string time = record != null && DateTime.TryParse(record.StartedAt, out started) ? started.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "未知时间";
            string mode = ClaudeTaskService.RunModeLabel(record == null ? null : record.RunMode);
            if (record != null && record.RunMode != "background") mode += " / " + (record.DesktopNumber == 0 ? "当前桌面" : "桌面 " + record.DesktopNumber);
            string result = record != null && record.RunMode == "claude-background" ? record.StatusText + "（依据后台状态；非创建命令退出码）" : record != null && record.Status == "running" ? "运行中（实时更新）" : "退出码 " + (record == null ? "—" : record.ExitCode.ToString());
            return time + "  ·  " + mode + "  ·  " + result + "\r\n目录：" + (task == null ? "" : task.WorkingDirectory);
        }
    }
}
