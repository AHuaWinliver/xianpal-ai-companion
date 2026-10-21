using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed class ReminderForm : Form
    {
        private readonly ReminderService service;
        private readonly DateTimePicker time = new DateTimePicker();
        private readonly TextBox task = new TextBox();
        private readonly ComboBox effectMode = new ComboBox();
        private readonly Label placeholder = new Label();
        private readonly ListBox list = new ListBox();
        private readonly Button toggle = new Button();
        private readonly Button delete = new Button();
        private readonly ReminderActionButton preview = new ReminderActionButton();
        private readonly NumericUpDown customMinutes = new NumericUpDown();
        private readonly Label countdownStatus = new Label();
        private readonly Button cancelCountdown = new Button();
        private readonly Timer countdownClock = new Timer();

        internal ReminderForm(ReminderService reminderService)
        {
            service = reminderService;
            Text = "提醒与倒计时";
            Icon = AppIcon.WindowIcon;
            ClientSize = new Size(520, 552);
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(253, 253, 252);
            DoubleBuffered = true;
            Font = new Font("Microsoft YaHei UI", 9f);

            Label title = new Label { Text = "提醒与倒计时", Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold), ForeColor = Color.FromArgb(34, 34, 32), AutoSize = true, Location = new Point(18, 16) };
            Label subtitle = new Label { Text = "到点后，当前仙伴会来到屏幕中间", Font = new Font("Microsoft YaHei UI", 8f), ForeColor = Color.FromArgb(112, 112, 106), AutoSize = true, Location = new Point(126, 18) };
            Button close = TextButton("×", new Point(476, 8), new Size(30, 30));
            close.Font = new Font("Segoe UI", 13f);
            close.Click += delegate { Hide(); };

            RoundedPanel countdownCard = new RoundedPanel { Radius = 16, BorderColor = Color.FromArgb(226, 226, 223), BackColor = Color.FromArgb(247, 247, 246), Location = new Point(14, 52), Size = new Size(492, 122) };
            int presetX = 12;
            foreach (KeyValuePair<string, int> preset in new[]
            {
                new KeyValuePair<string, int>("5 分钟", 5), new KeyValuePair<string, int>("10 分钟", 10),
                new KeyValuePair<string, int>("15 分钟", 15), new KeyValuePair<string, int>("30 分钟", 30),
                new KeyValuePair<string, int>("1 小时", 60), new KeyValuePair<string, int>("2 小时", 120)
            })
            {
                int selectedMinutes = preset.Value;
                Button quick = new Button { Text = preset.Key, Location = new Point(presetX, 12), Size = new Size(70, 31) };
                StyleButton(quick, false);
                quick.Click += delegate { StartCountdown(selectedMinutes); };
                countdownCard.Controls.Add(quick);
                presetX += 78;
            }
            Label customLabel = new Label { Text = "自定义", AutoSize = true, ForeColor = Color.FromArgb(80, 80, 76), Location = new Point(14, 78), BackColor = countdownCard.BackColor };
            customMinutes.Minimum = 1;
            customMinutes.Maximum = 10080;
            customMinutes.Value = 25;
            customMinutes.Location = new Point(70, 72);
            customMinutes.Size = new Size(72, 28);
            Label minuteUnit = new Label { Text = "分钟", AutoSize = true, ForeColor = Color.FromArgb(80, 80, 76), Location = new Point(146, 78), BackColor = countdownCard.BackColor };
            Button startCustom = new Button { Text = "开始", Location = new Point(188, 68), Size = new Size(64, 33) };
            StyleButton(startCustom, true);
            startCustom.Click += delegate { StartCountdown((int)customMinutes.Value); };
            cancelCountdown.Text = "取消";
            cancelCountdown.Location = new Point(258, 68);
            cancelCountdown.Size = new Size(58, 33);
            StyleButton(cancelCountdown, false);
            cancelCountdown.Click += delegate { service.CancelCountdown(); UpdateCountdownStatus(); };
            countdownStatus.Location = new Point(326, 61);
            countdownStatus.Size = new Size(154, 52);
            countdownStatus.TextAlign = ContentAlignment.MiddleLeft;
            countdownStatus.ForeColor = Color.FromArgb(40, 132, 84);
            countdownStatus.BackColor = countdownCard.BackColor;
            countdownCard.Controls.Add(customLabel);
            countdownCard.Controls.Add(customMinutes);
            countdownCard.Controls.Add(minuteUnit);
            countdownCard.Controls.Add(startCustom);
            countdownCard.Controls.Add(cancelCountdown);
            countdownCard.Controls.Add(countdownStatus);

            Label dailyTitle = new Label { Text = "每日提醒", Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(52, 52, 49), AutoSize = true, Location = new Point(18, 184) };
            RoundedPanel editor = new RoundedPanel { Radius = 16, BorderColor = Color.FromArgb(226, 226, 223), BackColor = Color.FromArgb(247, 247, 246), Location = new Point(14, 210), Size = new Size(492, 76) };
            time.Format = DateTimePickerFormat.Custom;
            time.CustomFormat = "HH:mm";
            time.ShowUpDown = true;
            time.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            time.Location = new Point(14, 22);
            time.Size = new Size(78, 29);

            task.BorderStyle = BorderStyle.None;
            task.BackColor = editor.BackColor;
            task.Font = new Font("Microsoft YaHei UI", 9.5f);
            task.Location = new Point(108, 27);
            task.Size = new Size(204, 24);
            task.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddReminder(); } };
            task.TextChanged += delegate { UpdatePlaceholder(); };
            task.GotFocus += delegate { UpdatePlaceholder(); };
            task.LostFocus += delegate { UpdatePlaceholder(); };

            placeholder.Text = "提醒我做什么…";
            placeholder.ForeColor = Color.FromArgb(145, 145, 140);
            placeholder.BackColor = editor.BackColor;
            placeholder.Font = task.Font;
            placeholder.AutoSize = true;
            placeholder.Location = new Point(108, 27);
            placeholder.Click += delegate { task.Focus(); };

            effectMode.DropDownStyle = ComboBoxStyle.DropDownList;
            effectMode.FlatStyle = FlatStyle.Flat;
            effectMode.Font = new Font("Microsoft YaHei UI", 8.5f);
            effectMode.BackColor = Color.White;
            effectMode.Location = new Point(318, 21);
            effectMode.Size = new Size(108, 28);
            effectMode.Items.AddRange(new object[] { "角色全屏演出", "仅提示框" });
            effectMode.SelectedIndex = 0;

            RoundIconButton add = new RoundIconButton { Text = "+", Font = new Font("Segoe UI", 18f), ForeColor = Color.White, BackColor = Color.FromArgb(32, 33, 31), FlatStyle = FlatStyle.Flat, Location = new Point(434, 17), Size = new Size(42, 42), Cursor = Cursors.Hand };
            add.FlatAppearance.BorderSize = 0;
            add.Click += delegate { AddReminder(); };
            editor.Controls.Add(time);
            editor.Controls.Add(task);
            editor.Controls.Add(placeholder);
            editor.Controls.Add(effectMode);
            editor.Controls.Add(add);

            RoundedPanel listCard = new RoundedPanel { Radius = 16, BorderColor = Color.FromArgb(226, 226, 223), BackColor = Color.FromArgb(247, 247, 246), Location = new Point(14, 298), Size = new Size(492, 205) };
            list.BorderStyle = BorderStyle.None;
            list.BackColor = listCard.BackColor;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = 48;
            list.IntegralHeight = false;
            list.Location = new Point(8, 8);
            list.Size = new Size(476, 189);
            list.DrawItem += DrawReminder;
            list.SelectedIndexChanged += delegate { UpdateButtons(); };
            listCard.Controls.Add(list);

            toggle.Text = "停用";
            toggle.Location = new Point(14, 512);
            toggle.Size = new Size(74, 31);
            StyleButton(toggle, false);
            toggle.Click += delegate { DailyReminder item = list.SelectedItem as DailyReminder; service.Toggle(item); RefreshList(item); };

            delete.Text = "删除";
            delete.Location = new Point(94, 512);
            delete.Size = new Size(70, 31);
            StyleButton(delete, false);
            delete.ForeColor = Color.FromArgb(174, 65, 58);
            delete.Click += delegate { DailyReminder item = list.SelectedItem as DailyReminder; service.Delete(item); RefreshList(null); };

            preview.Text = "立即测试效果";
            preview.Name = "reminderPreviewButton";
            preview.AccessibleName = "立即测试提醒效果";
            preview.Location = new Point(370, 512);
            preview.Size = new Size(136, 31);
            preview.Click += delegate { PreviewCurrentReminder(); };

            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(close);
            Controls.Add(countdownCard);
            Controls.Add(dailyTitle);
            Controls.Add(editor);
            Controls.Add(listCard);
            Controls.Add(toggle);
            Controls.Add(delete);
            Controls.Add(preview);
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            Resize += delegate { ApplyRoundedRegion(); };
            service.Changed += delegate { if (!IsDisposed) { RefreshList(list.SelectedItem as DailyReminder); UpdateCountdownStatus(); } };
            countdownClock.Interval = 1000;
            countdownClock.Tick += delegate { if (Visible) UpdateCountdownStatus(); };
            countdownClock.Start();
            ApplyRoundedRegion();
            UpdatePlaceholder();
            RefreshList(null);
            UpdateCountdownStatus();
        }

        internal void ShowNear(Rectangle petBounds)
        {
            Rectangle area = Screen.FromRectangle(petBounds).WorkingArea;
            int x = petBounds.Left + ((petBounds.Width - Width) / 2);
            x = Math.Max(area.Left + 8, Math.Min(x, area.Right - Width - 8));
            int y = petBounds.Bottom + 8;
            if (y + Height > area.Bottom - 8) y = petBounds.Top - Height - 8;
            Location = new Point(x, Math.Max(area.Top + 8, y));
            RefreshList(list.SelectedItem as DailyReminder);
            UpdateCountdownStatus();
            if (!Visible) Show();
            Activate();
            task.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = PromptForm.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 18))
            using (Pen pen = new Pen(Color.FromArgb(221, 221, 217))) e.Graphics.DrawPath(pen, path);
        }

        private void AddReminder()
        {
            string text = task.Text.Trim();
            if (text.Length == 0) { task.Focus(); return; }
            string effect = effectMode.SelectedIndex == 1 ? "popup" : "impact";
            DailyReminder added = service.Add(time.Value.TimeOfDay, text, effect);
            task.Clear();
            RefreshList(added);
            task.Focus();
        }

        private void StartCountdown(int minutes)
        {
            DailyReminder countdown = service.StartCountdown(minutes);
            UpdateCountdownStatus();
            countdownStatus.Text = countdown.CountdownMinutes + " 分钟倒计时已开始\n" + countdown.TimeText + " 到点";
        }

        private void UpdateCountdownStatus()
        {
            DailyReminder countdown = service.Countdown;
            cancelCountdown.Enabled = countdown != null;
            if (countdown == null)
            {
                countdownStatus.Text = "当前没有倒计时";
                countdownStatus.ForeColor = Color.FromArgb(120, 120, 115);
                return;
            }
            DateTime due;
            if (!DateTime.TryParse(countdown.DueAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out due))
            {
                countdownStatus.Text = "倒计时状态异常";
                return;
            }
            TimeSpan remaining = due.ToLocalTime() - DateTime.Now;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            countdownStatus.ForeColor = Color.FromArgb(40, 132, 84);
            countdownStatus.Text = FormatRemaining(remaining) + "\n" + due.ToLocalTime().ToString("HH:mm") + " 到点";
        }

        internal static string FormatRemaining(TimeSpan remaining)
        {
            int totalSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;
            return hours > 0
                ? String.Format("剩余 {0}:{1:00}:{2:00}", hours, minutes, seconds)
                : String.Format("剩余 {0:00}:{1:00}", minutes, seconds);
        }

        private void RefreshList(DailyReminder selected)
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (DailyReminder reminder in service.Reminders) list.Items.Add(reminder);
            list.EndUpdate();
            if (selected != null && list.Items.Contains(selected)) list.SelectedItem = selected;
            else if (list.Items.Count > 0) list.SelectedIndex = 0;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            DailyReminder item = list.SelectedItem as DailyReminder;
            toggle.Enabled = delete.Enabled = item != null;
            preview.Enabled = true;
            toggle.Text = item != null && item.Enabled ? "停用" : "启用";
        }

        private void PreviewCurrentReminder()
        {
            DailyReminder item = list.SelectedItem as DailyReminder;
            if (item == null)
            {
                string previewText = task.Text.Trim();
                item = new DailyReminder
                {
                    MinutesAfterMidnight = Math.Max(0, Math.Min(1439, (int)time.Value.TimeOfDay.TotalMinutes)),
                    Text = previewText.Length == 0 ? "这是提醒效果测试" : previewText,
                    Enabled = true,
                    Effect = effectMode.SelectedIndex == 1 ? "popup" : "impact"
                };
            }
            service.TriggerForPreview(item);
        }

        internal bool PreviewButtonAvailable
        {
            get { return preview.Enabled; }
        }

        internal void TriggerPreviewForTest()
        {
            PreviewCurrentReminder();
        }

        private void DrawReminder(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            DailyReminder item = list.Items[e.Index] as DailyReminder;
            if (item == null) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color background = selected ? Color.FromArgb(231, 241, 234) : list.BackColor;
            using (SolidBrush brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
            using (SolidBrush dot = new SolidBrush(item.Enabled ? Color.FromArgb(40, 132, 84) : Color.FromArgb(170, 170, 165))) e.Graphics.FillEllipse(dot, e.Bounds.Left + 11, e.Bounds.Top + 20, 8, 8);
            Rectangle timeBounds = new Rectangle(e.Bounds.Left + 30, e.Bounds.Top + 11, 58, 27);
            Rectangle textBounds = new Rectangle(e.Bounds.Left + 92, e.Bounds.Top + 11, e.Bounds.Width - 188, 27);
            Rectangle stateBounds = new Rectangle(e.Bounds.Right - 90, e.Bounds.Top + 11, 78, 27);
            using (Font bold = new Font(list.Font, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, item.TimeText, bold, timeBounds, Color.FromArgb(38, 38, 36), TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, item.Text, list.Font, textBounds, item.Enabled ? Color.FromArgb(38, 38, 36) : Color.FromArgb(135, 135, 130), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            string stateText = item.Enabled ? (ReminderService.NormalizeEffect(item.Effect) == "popup" ? "提示 · 每天" : "演出 · 每天") : "已停用";
            TextRenderer.DrawText(e.Graphics, stateText, list.Font, stateBounds, item.Enabled ? Color.FromArgb(40, 132, 84) : Color.FromArgb(145, 145, 140), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        private void UpdatePlaceholder()
        {
            placeholder.Visible = task.TextLength == 0 && !task.Focused;
        }

        private static Button TextButton(string text, Point location, Size size)
        {
            Button button = new Button { Text = text, Location = location, Size = size, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 240, 237);
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

        protected override void Dispose(bool disposing)
        {
            if (disposing) countdownClock.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class ReminderActionButton : Button
    {
        private bool hovering;

        internal ReminderActionButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color background = !Enabled ? Color.FromArgb(166, 178, 169) : (hovering ? Color.FromArgb(31, 116, 73) : Color.FromArgb(40, 132, 84));
            e.Graphics.Clear(Parent == null ? Color.FromArgb(253, 253, 252) : Parent.BackColor);
            using (GraphicsPath path = PromptForm.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 8))
            using (SolidBrush brush = new SolidBrush(background))
            using (Pen border = new Pen(Color.FromArgb(27, 99, 62)))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -4), Color.White, background);
        }
    }
}
