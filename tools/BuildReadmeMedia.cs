// SPDX-License-Identifier: Apache-2.0
// Documentation compositions use existing character art and real, isolated UI
// controls. They never submit prompts, start task timers, or call a model.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using XianPal;

internal static class BuildReadmeMedia
{
    private static readonly Color Ink = Color.FromArgb(32, 66, 67);
    private static readonly Color Muted = Color.FromArgb(103, 123, 115);
    private static readonly Color Paper = Color.FromArgb(249, 246, 235);
    private static readonly MethodInfo AppendFrame = typeof(BuildReminderPreviews).GetMethod("AppendGifFrame", BindingFlags.NonPublic | BindingFlags.Static);
    [StructLayout(LayoutKind.Sequential)] private struct PrintRect { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct CharacterRange { internal int First, Last; }
    [StructLayout(LayoutKind.Sequential)] private struct FormatRange
    {
        internal IntPtr Dc, TargetDc;
        internal PrintRect Area, Page;
        internal CharacterRange Characters;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wparam, ref FormatRange format);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);

    [STAThread] private static int Main(string[] args)
    {
        try
        {
            if (Environment.GetEnvironmentVariable("FUFU_TEST_MODE") != "1" || String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FUFU_STATE_DIR")))
                throw new InvalidOperationException("README media requires isolated demonstration state.");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string assets = args[0], output = args[1], previews = args[2];
            Environment.SetEnvironmentVariable("XIANPAL_TEST_ASSETS", Path.GetFullPath(assets));
            UiLanguage.Set("en");
            UiLanguageBindings.Initialize();
            Directory.CreateDirectory(output);
            Image[] portraits = new Image[4];
            try
            {
                for (int i = 0; i < 4; i++) portraits[i] = Image.FromFile(Path.Combine(CharacterRoot(assets, i), "idle", "00.png"));
                Hero(output, portraits);
                Workbench(output);
                Settings(output);
                Hall(output);
                History(output);
                Walking(assets, output);
                Sword(assets, output, 640, 360);
                if (new FileInfo(Path.Combine(output, "sword-flight.gif")).Length > 5 * 1024 * 1024) Sword(assets, output, 560, 315);
                CharacterGallery(output, portraits);
                foreach (string file in Directory.GetFiles(output))
                    if (new FileInfo(file).Length > 5 * 1024 * 1024) throw new InvalidDataException("README media exceeds repository budget: " + Path.GetFileName(file));
                Console.WriteLine("PASS README media: cover, social preview, real-control demo, 24-frame gait and sword-flight loop; no CLI/model calls.");
                return 0;
            }
            finally { foreach (Image image in portraits) if (image != null) image.Dispose(); }
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static string CharacterRoot(string root, int index)
    {
        return index == 0 ? root : Path.Combine(root, "characters", PetCharacter.All[index].Id);
    }

    private static void Quality(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    }

    private static void Text(Graphics g, string text, float size, Color color, float x, float y, bool bold)
    {
        using (Font font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
        using (SolidBrush brush = new SolidBrush(color)) g.DrawString(text, font, brush, x, y);
    }

    private static void Panel(Graphics g, RectangleF r, Color color, int radius)
    {
        using (GraphicsPath path = PromptForm.RoundedRectangle(Rectangle.Round(r), radius))
        using (SolidBrush fill = new SolidBrush(color)) g.FillPath(fill, path);
    }

    private static void Portrait(Graphics g, Image source, Rectangle target)
    {
        Rectangle crop = Rectangle.Empty;
        using (Bitmap pixels = new Bitmap(source))
            for (int y = 0; y < pixels.Height; y++)
            for (int x = 0; x < pixels.Width; x++)
                if (pixels.GetPixel(x, y).A > 12)
                    crop = crop.IsEmpty ? new Rectangle(x, y, 1, 1) : Rectangle.Union(crop, new Rectangle(x, y, 1, 1));
        if (crop.IsEmpty) return;
        float scale = Math.Min(target.Width / (float)crop.Width, target.Height / (float)crop.Height);
        RectangleF destination = new RectangleF(target.X + (target.Width - crop.Width * scale) / 2,
            target.Bottom - crop.Height * scale, crop.Width * scale, crop.Height * scale);
        g.DrawImage(source, destination, crop, GraphicsUnit.Pixel);
    }

    private static void Hero(string output, Image[] portraits)
    {
        using (Bitmap image = new Bitmap(1600, 840, PixelFormat.Format24bppRgb))
        using (Graphics g = Graphics.FromImage(image))
        {
            Quality(g);
            using (LinearGradientBrush background = new LinearGradientBrush(new Point(0, 0), new Point(1600, 840), Paper, Color.FromArgb(226, 239, 232)))
                g.FillRectangle(background, 0, 0, image.Width, image.Height);
            using (Pen ring = new Pen(Color.FromArgb(42, 99, 131, 111), 1.5f))
            {
                g.DrawEllipse(ring, 765, 40, 800, 800); g.DrawEllipse(ring, 805, 80, 720, 720);
                g.DrawArc(ring, 900, 175, 530, 530, 12, 255);
            }
            Panel(g, new RectangleF(68, 58, 251, 40), Color.FromArgb(224, 234, 220), 18);
            Text(g, "AI DESKTOP COMPANION", 17, Ink, 88, 66, true);
            Text(g, "XianPal", 94, Ink, 62, 142, true);
            Text(g, "AI at your side.", 39, Ink, 68, 278, true);
            Text(g, "Companions for real work.", 26, Muted, 71, 349, false);
            string[] chips = { "4 companions", "Claude Code", "Chat · files · tasks" };
            int[] widths = { 170, 190, 260 }; int cursor = 74;
            for (int i = 0; i < chips.Length; i++)
            {
                Panel(g, new RectangleF(cursor, 420, widths[i], 46), i == 1 ? Ink : Color.FromArgb(238, 229, 199), 16);
                Text(g, chips[i], 20, i == 1 ? Paper : Ink, cursor + 13, 430, i == 1);
                cursor += widths[i] + 10;
            }
            Panel(g, new RectangleF(74, 535, 650, 201), Color.FromArgb(253, 252, 245), 24);
            Text(g, "Your Claude Code. Your desktop.", 23, Ink, 99, 560, true);
            Text(g, "“Summarize these notes, then plan a follow-up.”", 18, Muted, 99, 606, false);
            Panel(g, new RectangleF(99, 659, 578, 43), Color.FromArgb(231, 240, 230), 13);
            Text(g, "Chat in context. Schedule work. Keep the record.", 17, Ink, 112, 668, false);
            // Actual, unchanged runtime artwork; no external character images.
            Portrait(g, portraits[0], new Rectangle(820, 100, 300, 300));
            Portrait(g, portraits[1], new Rectangle(1150, 110, 300, 300));
            Portrait(g, portraits[2], new Rectangle(820, 455, 300, 285));
            Portrait(g, portraits[3], new Rectangle(1150, 550, 300, 210));
            Text(g, "Qingheng", 23, Ink, 909, 415, true);
            Text(g, "Feijian", 23, Ink, 1252, 428, true);
            Text(g, "Cangzhi", 23, Ink, 905, 758, true);
            Text(g, "Xiaomo", 23, Ink, 1253, 785, true);
            Text(g, "Windows · Original companions · Apache-2.0 code", 18, Muted, 75, 782, false);
            image.Save(Path.Combine(output, "hero.png"), ImageFormat.Png);
            using (Bitmap social = new Bitmap(1280, 640, PixelFormat.Format24bppRgb))
            using (Graphics s = Graphics.FromImage(social))
            {
                Quality(s); s.Clear(Paper); s.DrawImage(image, new Rectangle(30, 0, 1219, 640));
                social.Save(Path.Combine(output, "social-preview.png"), ImageFormat.Png);
            }
        }
    }

    private static void CharacterGallery(string output, Image[] portraits)
    {
        string[] roles = { "Wandering cultivator", "Talisman artisan", "Herbalist", "Cloud-shell turtle" };
        using (var image = new Bitmap(1360, 500, PixelFormat.Format24bppRgb))
        using (var graphics = Graphics.FromImage(image))
        {
            Quality(graphics); graphics.Clear(Paper);
            for (int i = 0; i < portraits.Length; i++)
            {
                Portrait(graphics, portraits[i], new Rectangle(i * 340 + 30, 25, 280, 310));
                Text(graphics, UiLanguage.Text(PetCharacter.All[i].Name), 25, Ink, i * 340 + 100, 365, true);
                Text(graphics, roles[i], 19, Muted, i * 340 + 65, 414, false);
            }
            image.Save(Path.Combine(output, "characters.png"), ImageFormat.Png);
        }
    }

    private static object Field(object instance, string name)
    {
        return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    }

    private static Bitmap Capture(Form form)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(SystemInformation.VirtualScreen.Right + 80, 0);
        form.Show();
        Application.DoEvents();
        form.CreateControl();
        CreateHandles(form);
        UiLanguageBindings.Apply(form);
        form.PerformLayout();
        Bitmap capture = new Bitmap(form.Width, form.Height, PixelFormat.Format32bppArgb);
        form.DrawToBitmap(capture, new Rectangle(Point.Empty, capture.Size));
        using (Graphics g = Graphics.FromImage(capture)) PrintNativeContents(g, form, Point.Empty);
        return capture;
    }

    private static void PrintNativeContents(Graphics g, Control parent, Point offset)
    {
        foreach (Control child in parent.Controls)
        {
            // Control.Visible includes the hidden parent's visibility; the
            // local flag is needed to omit inactive interval/date widgets.
            MethodInfo state = typeof(Control).GetMethod("GetState", BindingFlags.Instance | BindingFlags.NonPublic);
            if (state != null && !(bool)state.Invoke(child, new object[] { 2 })) continue;
            Form window = parent.FindForm();
            Point screen = child.PointToScreen(Point.Empty);
            Point origin = new Point(screen.X - window.Left, screen.Y - window.Top);
            ComboBox combo = child as ComboBox;
            RichTextBox rich = child as RichTextBox;
            // DrawToBitmap omits native RichEdit content and hidden ComboBox
            // text. Use RichEdit's print API and the actual combo's text/font,
            // rather than replacing either widget with a mock layout.
            if (combo != null && !String.IsNullOrEmpty(combo.Text))
            {
                Rectangle interior = new Rectangle(origin.X + 2, origin.Y + 2, Math.Max(1, combo.Width - 21), Math.Max(1, combo.Height - 4));
                using (SolidBrush fill = new SolidBrush(combo.BackColor)) g.FillRectangle(fill, interior);
                using (SolidBrush ink = new SolidBrush(combo.ForeColor))
                using (StringFormat format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(combo.Text, combo.Font, ink, new RectangleF(interior.X + 2, interior.Y, interior.Width - 2, interior.Height), format);
            }
            if (rich != null && !String.IsNullOrEmpty(rich.Text))
            using (Bitmap content = new Bitmap(rich.Width, rich.Height, PixelFormat.Format24bppRgb))
            using (Graphics printer = Graphics.FromImage(content))
            {
                printer.Clear(rich.BackColor);
                IntPtr dc = printer.GetHdc();
                try
                {
                    PrintRect page = new PrintRect { Right = rich.Width * 15, Bottom = rich.Height * 15 };
                    FormatRange range = new FormatRange { Dc = dc, TargetDc = dc, Area = page, Page = page,
                        Characters = new CharacterRange { First = 0, Last = -1 } };
                    SendMessage(rich.Handle, 0x0439, new IntPtr(1), ref range);
                }
                finally { printer.ReleaseHdc(dc); SendMessage(rich.Handle, 0x0439, IntPtr.Zero, IntPtr.Zero); }
                g.DrawImageUnscaled(content, origin);
            }
            PrintNativeContents(g, child, origin);
        }
    }

    private static void CreateHandles(Control control)
    {
        IntPtr handle = control.Handle;
        foreach (Control child in control.Controls) CreateHandles(child);
    }

    private static void Workbench(string output)
    {
        using (ClaudeTaskService service = new ClaudeTaskService())
        using (ActivityTrayForm activity = new ActivityTrayForm())
        {
            // Populate only in-memory mock records; no scheduler is started.
            var tasks = (List<ScheduledClaudeTask>)Field(service, "tasks"); tasks.Clear();
            tasks.Add(new ScheduledClaudeTask { CharacterId = "orbit", Prompt = "Summarize Markdown notes and next steps.", WorkingDirectory = @"C:\Projects\notes", Recurrence = "weekly", Weekday = 5,
                MinutesAfterMidnight = 18 * 60, RunMode = "interactive-powershell", OverlapMode = "block", DesktopNumber = 2 });
            tasks.Add(new ScheduledClaudeTask { CharacterId = "spark", Prompt = "Review the plan and list open questions.", WorkingDirectory = @"C:\Projects\weekly-review", Recurrence = "daily",
                MinutesAfterMidnight = 9 * 60, RunMode = "background", OverlapMode = "block" });
            using (ClaudeTaskForm scheduled = new ClaudeTaskForm(service))
            {
                ((TextBox)Field(scheduled, "prompt")).Text = "Summarize the Markdown notes in this folder and suggest next steps.";
                scheduled.GetType().GetField("workingDirectory", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scheduled, @"C:\Projects\notes");
                scheduled.GetType().GetMethod("UpdateDirectoryText", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scheduled, null);
                ((ComboBox)Field(scheduled, "recurrence")).SelectedIndex = 1;
                ((ComboBox)Field(scheduled, "weekday")).SelectedIndex = 5;
                ((DateTimePicker)Field(scheduled, "time")).Value = new DateTime(2026, 10, 23, 18, 0, 0);
                var records = new List<ActivityRecord> { new ActivityRecord { Title = "Weekly notes", SessionId = "documentation-demo", CharacterId = "orbit", State = "Completed", Updated = new DateTime(2026, 10, 21, 14, 30, 0),
                    WorkingDirectory = @"C:\Projects\notes", Output = "You: Summarize this week's notes and suggest next steps.\r\n\r\nQingheng: Here is a short review:\r\n\r\n1. Completed: organize reference material and tasks.\r\n2. Open questions: confirm two deadlines.\r\n3. Next step: start with the smallest item.\r\n\r\nIllustrative data. No live model was called." } };
                records[0].Output = "";
                records[0].StartConversation("Summarize this week's notes and suggest next steps.");
                records[0].AppendOutput("Here is a short review:\n\n1. Completed: organize notes and references.\n2. Open questions: confirm two deadlines.\n3. Next step: start with the smallest item.");
                records[0].AppendUserPrompt("Which item should I start with?");
                records[0].AppendOutput("Confirm the first deadline, then draft a short checklist.");
                activity.SetActivities(records);
                ((ListBox)Field(activity, "list")).SelectedIndex = 0;
                ((ChineseInputTextBox)Field(activity, "reply")).Text = "What are the three priorities for tomorrow?";
                using (Bitmap left = Capture(activity))
                using (Bitmap right = Capture(scheduled))
                using (Bitmap image = new Bitmap(1600, 1030, PixelFormat.Format24bppRgb))
                using (Graphics g = Graphics.FromImage(image))
                {
                    left.Save(Path.Combine(output, "chat-bubbles.png"), ImageFormat.Png);
                    Quality(g); g.Clear(Paper);
                    Text(g, "A companion for conversations and work.", 44, Ink, 60, 40, true);
                    Text(g, "Continue a conversation. Schedule a task. Review what happened.", 24, Muted, 63, 104, false);
                    Text(g, "01  Keep the conversation going", 25, Ink, 68, 180, true);
                    Text(g, "02  Schedule work with Claude Code", 25, Ink, 828, 180, true);
                    Panel(g, new RectangleF(58, 230, 706, 610), Color.FromArgb(226, 234, 224), 20);
                    g.DrawImage(left, new Rectangle(68, 240, 685, 578));
                    Panel(g, new RectangleF(818, 230, 714, 751), Color.FromArgb(226, 234, 224), 20);
                    g.DrawImage(right, new Rectangle(828, 240, 694, 721));
                    Text(g, "Double-click a bubble to open the conversation.", 21, Muted, 69, 871, false);
                    Text(g, "Actual UI controls · Illustrative data · No live CLI or model calls", 19, Muted, 67, 977, false);
                    image.Save(Path.Combine(output, "workbench.png"), ImageFormat.Png);
                }
            }
        }
    }

    private static void Settings(string output)
    {
        using (var pet = new PetForm())
        {
            typeof(PetForm).GetMethod("ShowDefaultSettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pet, null);
            using (var window = (Form)Field(pet, "defaultSettingsWindow"))
            {
                ((ComboBox)window.Controls.Find("CompanionScope", true)[0]).SelectedIndex = 1;
                ((TextBox)window.Controls.Find("ClaudeExecutable", true)[0]).Text = @"C:\Applications\Claude\claude.exe";
                ((TextBox)window.Controls.Find("ClaudeSettings", true)[0]).Text = @"C:\Projects\profiles\qingheng.json";
                foreach (Control control in window.Controls) if (control is TabControl) ((TabControl)control).SelectedIndex = 1;
                using (var capture = Capture(window)) capture.Save(Path.Combine(output, "claude-settings.png"), ImageFormat.Png);
            }
        }
    }

    private static void Hall(string output)
    {
        var sect = CompanionRolePresets.Create("Cloudline Workshop", Program.StateDirectory, false);
        var run = new SectRun { Sect = sect.Name, Status = "running" };
        string[] statuses = { "success", "running", "pending", "pending" };
        for (int i = 0; i < sect.Members.Count; i++)
        {
            sect.Members[i].Name = UiLanguage.Text(PetCharacter.All[i].Name);
            run.Steps.Add(new SectStepRun { Id = "demo-" + i, Member = sect.Members[i].Name, CharacterId = sect.Members[i].CharacterId, Status = statuses[i] });
        }
        run.Transfers.Add(new SectTransfer { From = "demo-0", To = "demo-1", Text = "Agreed scope, file ownership and acceptance criteria." });
        using (var hall = new SectHallControl(delegate { return sect; }, delegate { return run; }, delegate { }))
        using (var image = new Bitmap(1280, 820))
        {
            hall.Font = UiText.Font(10.5f);
            hall.Size = image.Size;
            hall.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            image.Save(Path.Combine(output, "sect-hall.png"), ImageFormat.Png);
        }
    }

    private static void History(string output)
    {
        var task = new ScheduledClaudeTask { CharacterId = "orbit", Prompt = "Summarize this week's notes and suggest next steps.", WorkingDirectory = @"C:\Projects\notes" };
        var record = new ClaudeTaskRunRecord { CharacterId = "orbit", StartedAt = "2026-10-23T18:00:00", CompletedAt = "2026-10-23T18:00:12", Status = "success", Transcript = "The notes are organized into three themes.\n\nCompleted: organize reference material.\nOpen: confirm two deadlines.\nNext step: draft a short checklist.", Summary = "Notes reviewed; three next steps identified." };
        task.History.Add(record);
        using (var history = new ClaudeTaskHistoryForm(task))
        using (var transcript = new ClaudeTaskRunTranscriptForm(task, record))
        using (var left = Capture(history))
        using (var right = Capture(transcript))
        using (var image = new Bitmap(1600, 710, PixelFormat.Format24bppRgb))
        using (var graphics = Graphics.FromImage(image))
        {
            Quality(graphics); graphics.Clear(Paper);
            Text(graphics, "A record for every run.", 40, Ink, 45, 24, true);
            Text(graphics, "Review status and open the saved output. Task records stay separate from ordinary chat.", 21, Muted, 48, 83, false);
            graphics.DrawImage(left, new Rectangle(35, 140, 710, 510));
            graphics.DrawImage(right, new Rectangle(775, 140, 790, 540));
            Text(graphics, "Actual UI controls · Illustrative data · No live model calls", 17, Muted, 48, 674, false);
            image.Save(Path.Combine(output, "scheduled-task-history.png"), ImageFormat.Png);
        }
    }

    private static BinaryWriter Gif(string file, int width, int height)
    {
        BinaryWriter output = new BinaryWriter(File.Create(file));
        output.Write(Encoding.ASCII.GetBytes("GIF89a")); output.Write((ushort)width); output.Write((ushort)height);
        output.Write(new byte[] { 0x70, 0, 0, 0x21, 0xff, 11 });
        output.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0")); output.Write(new byte[] { 3, 1, 0, 0, 0 }); return output;
    }

    private static void GifFrame(BinaryWriter output, Bitmap image)
    {
        using (MemoryStream source = new MemoryStream())
        {
            image.Save(source, ImageFormat.Gif);
            AppendFrame.Invoke(null, new object[] { output, source.ToArray() });
        }
    }

    private static void Walking(string assets, string output)
    {
        List<Image[]> frames = new List<Image[]>();
        try
        {
            for (int i = 0; i < 4; i++)
            {
                Image[] sequence = new Image[24]; frames.Add(sequence);
                for (int f = 0; f < 24; f++) sequence[f] = Image.FromFile(Path.Combine(CharacterRoot(assets, i), "running-right", f.ToString("00") + ".png"));
            }
            using (BinaryWriter gif = Gif(Path.Combine(output, "walking.gif"), 720, 280))
            {
                for (int frame = 0; frame < 24; frame++)
                using (Bitmap image = new Bitmap(720, 280, PixelFormat.Format24bppRgb))
                using (Graphics g = Graphics.FromImage(image))
                {
                    Quality(g); g.Clear(Color.White);
                    Text(g, "XianPal · 24-phase original animation", 16, Ink, 16, 10, true);
                    for (int i = 0; i < 4; i++)
                    {
                        g.DrawImage(frames[i][frame], new Rectangle(i * 180 + 2, 34, 176, 191));
                        Text(g, UiLanguage.Text(PetCharacter.All[i].Name), 17, Ink, i * 180 + 55, 240, true);
                    }
                    GifFrame(gif, image);
                }
                gif.Write((byte)0x3b);
            }
        }
        finally { foreach (Image[] group in frames) foreach (Image image in group) if (image != null) image.Dispose(); }
    }

    private static void Sword(string assets, string output, int width, int height)
    {
        Dictionary<string, List<Image>> frames = BuildReminderPreviews.Load(assets);
        try
        {
            using (BinaryWriter gif = Gif(Path.Combine(output, "sword-flight.gif"), width, height))
            {
                for (int frame = 0; frame < 118; frame++)
                using (Bitmap image = new Bitmap(width, height, PixelFormat.Format24bppRgb))
                using (Graphics g = Graphics.FromImage(image))
                {
                    // White is an exact GIF palette entry, avoiding full-canvas
                    // halftone noise and keeping the animation small and clean.
                    Quality(g); g.Clear(Color.White); g.ScaleTransform(width / 960f, height / 540f);
                    // Rotate the cyclic demo so GitHub's still thumbnail starts
                    // with a visible sword / wake, not the pre-summoning pose.
                    ReminderPerformance.Render(g, new Size(960, 540), "command", frames, 1.20 + ((frame + 55) % 118) * .04, false, Rectangle.Empty);
                    Text(g, "Qingheng · Sword flight", 22, Ink, 22, 20, true);
                    GifFrame(gif, image);
                }
                gif.Write((byte)0x3b);
            }
        }
        finally { BuildReminderPreviews.Release(frames); }
    }
}
