using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed class PromptForm : Form
    {
        private readonly ChineseInputTextBox prompt = new ChineseInputTextBox();
        private readonly Label placeholder = new Label();
        private readonly Button directoryButton = new Button();
        private readonly ToolTip tips = new ToolTip();
        private string workingDirectory;
        internal event Action<string, string> PromptSubmitted;
        internal event Action ActivityRequested;
        internal event Action ReminderRequested;

        internal string WorkingDirectory
        {
            get { return workingDirectory; }
        }

        internal string PromptTextForTest
        {
            get { return prompt.Text; }
        }

        internal PromptForm()
        {
            Text = "询问 Claude Code";
            Icon = AppIcon.WindowIcon;
            ClientSize = new Size(468, 184);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(253, 253, 252);
            DoubleBuffered = true;
            Font = UiText.Font(9f);
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            };

            Label brand = new Label { Text = "Claude Code", Font = UiText.Font(9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(38, 38, 36), AutoSize = true, Location = new Point(54, 13) };
            Label local = new Label { Text = "●  本机 CLI", Font = UiText.Font(8f), ForeColor = Color.FromArgb(40, 132, 84), AutoSize = true, Location = new Point(148, 15) };
            Button close = NewTextButton("×", new Point(12, 7), new Size(30, 28));
            close.Name = "temporaryCloseButton";
            close.Font = new Font("Segoe UI", 13f);
            close.Click += delegate { Hide(); };
            tips.SetToolTip(close, "暂时关闭对话窗口");

            RoundedPanel inputCard = new RoundedPanel { Radius = 18, BorderColor = Color.FromArgb(226, 226, 223), BackColor = Color.FromArgb(247, 247, 246), Location = new Point(14, 42), Size = new Size(440, 91) };

            prompt.Multiline = true;
            prompt.AcceptsReturn = true;
            prompt.BorderStyle = BorderStyle.None;
            prompt.BackColor = inputCard.BackColor;
            prompt.ForeColor = Color.FromArgb(34, 34, 32);
            prompt.Font = UiText.Font(10.2f);
            prompt.Location = new Point(14, 15);
            prompt.Size = new Size(356, 60);
            prompt.KeyDown += PromptKeyDown;
            prompt.TextChanged += delegate { UpdatePlaceholder(); };
            prompt.GotFocus += delegate { UpdatePlaceholder(); };
            prompt.LostFocus += delegate { UpdatePlaceholder(); };

            placeholder.Text = "询问 Claude Code…";
            placeholder.ForeColor = Color.FromArgb(145, 145, 140);
            placeholder.BackColor = inputCard.BackColor;
            placeholder.Font = prompt.Font;
            placeholder.AutoSize = true;
            placeholder.Location = new Point(14, 15);
            placeholder.Cursor = Cursors.IBeam;
            placeholder.Click += delegate { prompt.Focus(); };

            RoundIconButton send = new RoundIconButton { Text = "↑", Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.FromArgb(32, 33, 31), FlatStyle = FlatStyle.Flat, Location = new Point(383, 24), Size = new Size(43, 43), Cursor = Cursors.Hand };
            send.FlatAppearance.BorderSize = 0;
            send.Click += delegate { Submit(); };
            tips.SetToolTip(send, "发送（Enter）");
            inputCard.Controls.Add(prompt);
            inputCard.Controls.Add(placeholder);
            inputCard.Controls.Add(send);

            workingDirectory = LoadWorkingDirectory();
            directoryButton = NewTextButton("", new Point(14, 143), new Size(150, 29));
            directoryButton.TextAlign = ContentAlignment.MiddleLeft;
            directoryButton.ForeColor = Color.FromArgb(92, 92, 88);
            directoryButton.Click += ChooseDirectory;
            UpdateDirectoryButton();

            Button activity = NewTextButton("动态", new Point(172, 143), new Size(58, 29));
            activity.ForeColor = Color.FromArgb(92, 92, 88);
            activity.Click += delegate { Action handler = ActivityRequested; if (handler != null) handler(); };
            tips.SetToolTip(activity, "查看 Claude 会话动态");

            Button reminders = NewTextButton("提醒", new Point(234, 143), new Size(58, 29));
            reminders.ForeColor = Color.FromArgb(92, 92, 88);
            reminders.Click += delegate { Action handler = ReminderRequested; if (handler != null) handler(); };
            tips.SetToolTip(reminders, "设置倒计时和每日提醒");

            Label hint = new Label { Text = "Enter 发送 · Shift+Enter 换行", ForeColor = Color.FromArgb(145, 145, 140), AutoSize = true, Location = new Point(302, 150), Font = UiText.Font(6.6f) };

            Controls.Add(brand);
            Controls.Add(local);
            Controls.Add(close);
            Controls.Add(inputCard);
            Controls.Add(directoryButton);
            Controls.Add(activity);
            Controls.Add(reminders);
            Controls.Add(hint);
            Resize += delegate { ApplyRoundedRegion(); };
            ApplyRoundedRegion();
            UpdatePlaceholder();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int CsDropShadow = 0x00020000;
                CreateParams parameters = base.CreateParams;
                parameters.ClassStyle |= CsDropShadow;
                return parameters;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 18))
            using (Pen pen = new Pen(Color.FromArgb(221, 221, 217)))
                e.Graphics.DrawPath(pen, path);
        }

        internal void ShowNear(Rectangle petBounds)
        {
            Rectangle area = Screen.FromRectangle(petBounds).WorkingArea;
            int x = petBounds.Left + ((petBounds.Width - Width) / 2);
            x = Math.Max(area.Left + 8, Math.Min(x, area.Right - Width - 8));
            int y = petBounds.Bottom + 8;
            if (y + Height > area.Bottom - 8) y = petBounds.Top - Height - 8;
            Location = new Point(x, Math.Max(area.Top + 8, y));
            if (!Visible) Show();
            WindowState = FormWindowState.Normal;
            Activate();
            prompt.Focus();
        }

        internal void ShowForDroppedPaths(Rectangle petBounds, string directory, string preparedPrompt)
        {
            if (!String.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                workingDirectory = directory;
                UpdateDirectoryButton();
                SaveWorkingDirectory();
            }
            prompt.Text = preparedPrompt ?? "";
            prompt.SelectionStart = prompt.TextLength;
            prompt.SelectionLength = 0;
            ShowNear(petBounds);
        }

        private void PromptKeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Shift && e.KeyCode == Keys.Enter && !prompt.IsComposing) { e.SuppressKeyPress = true; Submit(); }
        }

        private void Submit()
        {
            string text = prompt.Text.Trim();
            if (text.Length == 0) return;
            Action<string, string> handler = PromptSubmitted;
            prompt.Clear();
            Hide();
            if (handler != null) handler(text, workingDirectory);
        }

        private void ChooseDirectory(object sender, EventArgs e)
        {
            ChooseWorkingDirectory(this);
        }

        internal bool ChooseWorkingDirectory(IWin32Window owner)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择 Claude Code 默认打开和执行任务的目录";
                dialog.SelectedPath = workingDirectory;
                if (dialog.ShowDialog(owner) == DialogResult.OK)
                {
                    SetWorkingDirectory(dialog.SelectedPath);
                    return true;
                }
            }
            return false;
        }

        internal bool SetWorkingDirectory(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return false;
            workingDirectory = Path.GetFullPath(directory);
            UpdateDirectoryButton();
            SaveWorkingDirectory();
            return true;
        }

        private void UpdateDirectoryButton()
        {
            string clean = (workingDirectory ?? "").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string folder = Path.GetFileName(clean);
            if (String.IsNullOrEmpty(folder)) folder = clean;
            directoryButton.Text = "⌁  " + (String.IsNullOrEmpty(folder) ? "选择工作目录" : folder);
            tips.SetToolTip(directoryButton, "工作目录：" + workingDirectory);
        }

        private void SaveWorkingDirectory()
        {
            try
            {
                Directory.CreateDirectory(Program.StateDirectory);
                File.WriteAllText(Path.Combine(Program.StateDirectory, "working-directory.txt"), workingDirectory);
            }
            catch { }
        }

        private void UpdatePlaceholder()
        {
            placeholder.Visible = prompt.TextLength == 0 && !prompt.Focused;
        }

        private static Button NewTextButton(string text, Point location, Size size)
        {
            Button button = new Button { Text = text, Location = location, Size = size, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(242, 242, 239);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(232, 232, 228);
            return button;
        }

        private void ApplyRoundedRegion()
        {
            Region old = Region;
            Region = new Region(RoundedRectangle(ClientRectangle, 18));
            if (old != null) old.Dispose();
        }

        internal static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static string LoadWorkingDirectory()
        {
            try
            {
                string file = Path.Combine(Program.StateDirectory, "working-directory.txt");
                if (File.Exists(file))
                {
                    string saved = File.ReadAllText(file).Trim();
                    if (Directory.Exists(saved)) return saved;
                }
            }
            catch { }
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
    }

    internal sealed class RoundedPanel : Panel
    {
        internal int Radius = 16;
        internal Color BorderColor = Color.LightGray;

        internal RoundedPanel()
        {
            DoubleBuffered = true;
            Resize += delegate { UpdateRegion(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = PromptForm.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (Pen pen = new Pen(BorderColor))
                e.Graphics.DrawPath(pen, path);
        }

        private void UpdateRegion()
        {
            Region old = Region;
            Region = new Region(PromptForm.RoundedRectangle(ClientRectangle, Radius));
            if (old != null) old.Dispose();
        }
    }

    internal sealed class RoundIconButton : Button
    {
        internal RoundIconButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Resize += delegate { UpdateRegion(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush background = new SolidBrush(Parent == null ? Color.FromArgb(247, 247, 246) : Parent.BackColor))
                e.Graphics.FillRectangle(background, ClientRectangle);
            using (SolidBrush circle = new SolidBrush(Enabled ? Color.FromArgb(32, 33, 31) : Color.FromArgb(170, 170, 166)))
                e.Graphics.FillEllipse(circle, new Rectangle(1, 1, Width - 2, Height - 2));
            if (Text == "↑")
            {
                using (Pen arrow = new Pen(Color.White, 2.2f))
                {
                    arrow.StartCap = LineCap.Round;
                    arrow.EndCap = LineCap.Round;
                    float center = Width / 2f;
                    e.Graphics.DrawLine(arrow, center, Height * 0.66f, center, Height * 0.34f);
                    e.Graphics.DrawLine(arrow, center, Height * 0.34f, Width * 0.36f, Height * 0.48f);
                    e.Graphics.DrawLine(arrow, center, Height * 0.34f, Width * 0.64f, Height * 0.48f);
                }
            }
            else
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private void UpdateRegion()
        {
            Region old = Region;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(ClientRectangle);
                Region = new Region(path);
            }
            if (old != null) old.Dispose();
        }
    }
}
