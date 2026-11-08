// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace XianPal.Installer
{
    internal sealed class InstallerForm : Form
    {
        private readonly Button install = new Button { AutoSize = true, Name = "Install" };
        private readonly Button cancel = new Button { AutoSize = true };
        private readonly TextBox destination = new TextBox { Dock = DockStyle.Fill, Name = "InstallDirectory" };
        private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(430, 0) };
        private readonly ComboBox language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        private bool installing;

        internal InstallerForm()
        {
            Name = "XianPalInstaller"; Text = "XianPal Setup";
            Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            ClientSize = new Size(900, 510); MinimumSize = new Size(820, 500);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f); BackColor = Color.FromArgb(250, 248, 244);
            var preview = new InstallerPreview { Dock = DockStyle.Fill };
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 2 };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58)); content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            var left = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            language.Items.AddRange(new object[] { "English", "简体中文" }); language.SelectedIndex = UiLanguage.IsEnglish ? 0 : 1;
            var title = new Label { Text = "XianPal", AutoSize = true, Font = new Font("Segoe UI", 25, FontStyle.Bold), ForeColor = Color.FromArgb(32, 75, 66), Margin = new Padding(0, 16, 0, 12) };
            var description = new Label { AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(0, 0, 0, 16) };
            var location = new Label { AutoSize = true, Margin = new Padding(0, 12, 0, 6) };
            var folder = new TableLayoutPanel { ColumnCount = 2, AutoSize = false, Width = 420, Height = 36 };
            folder.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); folder.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var browse = new Button { AutoSize = true, Name = "BrowseInstallDirectory" };
            destination.Text = Program.InstallDirectory;
            browse.Click += delegate
            {
                using (var picker = new FolderBrowserDialog())
                {
                    if (Directory.Exists(destination.Text)) picker.SelectedPath = destination.Text;
                    if (picker.ShowDialog(this) == DialogResult.OK) destination.Text = picker.SelectedPath;
                }
            };
            folder.Controls.Add(destination, 0, 0); folder.Controls.Add(browse, 1, 0);
            var information = new Label { AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(0, 12, 0, 12) };
            var actions = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 16, 0, 0) };
            install.Padding = new Padding(24, 8, 24, 8); install.FlatStyle = FlatStyle.Flat;
            install.BackColor = Color.FromArgb(32, 75, 66); install.ForeColor = Color.White;
            cancel.Padding = new Padding(12, 8, 12, 8); actions.Controls.Add(install); actions.Controls.Add(cancel);
            left.Controls.AddRange(new Control[] { language, title, description, location, folder, information, status, actions });
            content.Controls.Add(left, 0, 0); content.Controls.Add(preview, 1, 0); Controls.Add(content);
            Action captions = delegate
            {
                Text = UiLanguage.Text("XianPal Setup");
                description.Text = UiLanguage.Text("An animated desktop companion for your local Claude Code workflow.");
                location.Text = UiLanguage.Text("Installation folder");
                information.Text = UiLanguage.Text("Installs for the current user. Uses your own Claude Code configuration; no model or credentials are bundled.");
                browse.Text = UiLanguage.Text("Browse…"); install.Text = UiLanguage.Text("Install"); cancel.Text = UiLanguage.Text("Cancel");
                status.Text = UiLanguage.Text(Program.HasClaudeCli() ? "Claude Code CLI detected." : "Claude Code CLI was not detected. You can configure it after installation.");
                preview.Invalidate();
            };
            captions();
            language.SelectedIndexChanged += delegate
            {
                try { UiLanguage.Set(language.SelectedIndex == 1 ? "zh-CN" : "en"); captions(); }
                catch (Exception error) { status.Text = error.Message; }
            };
            cancel.Click += delegate { Close(); };
            install.Click += delegate
            {
                try { Program.SelectInstallDirectory(destination.Text); }
                catch (Exception error) { status.Text = UiLanguage.Text(error.Message); return; }
                installing = true; install.Enabled = cancel.Enabled = destination.Enabled = browse.Enabled = language.Enabled = false;
                status.Text = UiLanguage.Text("Installing XianPal…");
                var worker = new Thread(delegate()
                {
                    Exception failure = null;
                    try { Program.Install(); } catch (Exception error) { failure = error; }
                    BeginInvoke((Action)delegate
                    {
                        installing = false;
                        if (failure == null)
                        {
                            MessageBox.Show(this, UiLanguage.Text("XianPal is installed. Open the companion menu to configure Claude Code."), "XianPal", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            Close();
                        }
                        else
                        {
                            status.Text = UiLanguage.Text("Installation failed.") + " " + failure.GetBaseException().Message;
                            install.Enabled = cancel.Enabled = destination.Enabled = browse.Enabled = language.Enabled = true;
                        }
                    });
                });
                worker.SetApartmentState(ApartmentState.STA); worker.Start();
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (installing) e.Cancel = true; };
        }
    }

    internal sealed class InstallerPreview : Panel
    {
        private readonly List<Image[]> clips = new List<Image[]>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 16 };
        private readonly string[] headings = { "Desktop companionship", "Coordinated movement", "File interactions", "Character performances" };
        private readonly string[] captions = { "Four original companions with expressive animations.", "Smooth steps, turns, and character-specific motion.", "Drop files, then confirm your request in chat.", "Distinct animations for reminders and interactions." };

        internal InstallerPreview()
        {
            DoubleBuffered = true; BackColor = Color.FromArgb(238, 242, 232);
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("XianPalPayload.zip"))
                if (resource != null)
                    using (var archive = new ZipArchive(resource, ZipArchiveMode.Read))
                    {
                        string[] roots = { "assets", "assets/characters/spark", "assets/characters/moss", "assets/characters/pippin" };
                        string[] states = { "waving", "running-right", "eating", "jumping" };
                        for (int role = 0; role < roots.Length; role++)
                        {
                            var frames = new List<Image>();
                            for (int index = 0; index < 24; index++)
                            {
                                var entry = archive.GetEntry(roots[role] + "/" + states[role] + "/" + index.ToString("00") + ".png");
                                if (entry == null) break;
                                using (var input = entry.Open()) using (var image = Image.FromStream(input)) frames.Add(new Bitmap(image));
                            }
                            clips.Add(frames.ToArray());
                        }
                    }
            timer.Tick += delegate { Invalidate(); }; timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            int slide = (int)(clock.ElapsedMilliseconds / 4500 % 4);
            using (var heading = new Font("Segoe UI", 14, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, UiLanguage.Text(headings[slide]), heading, new Rectangle(18, 22, Width - 36, 65), Color.FromArgb(32, 75, 66), TextFormatFlags.WordBreak);
            var target = new Rectangle(25, 92, Math.Max(1, Width - 50), Math.Max(1, Height - 212));
            if (clips.Count > slide && clips[slide].Length > 0)
            {
                var frames = clips[slide]; double phase = clock.ElapsedMilliseconds / 42.0; int index = (int)phase % frames.Length;
                double scale = Math.Min((double)target.Width / frames[index].Width, (double)target.Height / frames[index].Height);
                int width = (int)(frames[index].Width * scale), height = (int)(frames[index].Height * scale);
                var rectangle = new Rectangle(target.X + (target.Width - width) / 2, target.Y + (target.Height - height) / 2, width, height);
                Draw(e.Graphics, frames[index], rectangle, 1);
                Draw(e.Graphics, frames[(index + 1) % frames.Length], rectangle, (float)(phase % 1));
            }
            using (var font = new Font("Segoe UI", 10))
                TextRenderer.DrawText(e.Graphics, UiLanguage.Text(captions[slide]), font, new Rectangle(20, Height - 98, Width - 40, 72), Color.FromArgb(73, 91, 78), TextFormatFlags.WordBreak);
        }

        private static void Draw(Graphics graphics, Image image, Rectangle target, float opacity)
        {
            using (var attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
                graphics.DrawImage(image, target, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); foreach (var clip in clips) foreach (Image frame in clip) frame.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
