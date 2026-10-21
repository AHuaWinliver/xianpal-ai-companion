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
            Directory.CreateDirectory(output);
            Image[] portraits = new Image[4];
            try
            {
                for (int i = 0; i < 4; i++) portraits[i] = Image.FromFile(Path.Combine(CharacterRoot(assets, i), "idle", "00.png"));
                Hero(output, portraits);
                Workbench(output);
                Walking(assets, output);
                Sword(assets, output, 640, 360);
                if (new FileInfo(Path.Combine(output, "sword-flight.gif")).Length > 5 * 1024 * 1024) Sword(assets, output, 560, 315);
                File.Copy(Path.Combine(previews, "characters.png"), Path.Combine(output, "characters.png"), true);
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
            Text(g, "AI COMPANION · " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3), 18, Ink, 88, 66, true);
            Text(g, "仙伴", 94, Ink, 62, 142, true);
            Text(g, "XianPal", 62, Ink, 270, 171, true);
            Text(g, "让 AI，陪在你的桌面上。", 39, Ink, 68, 278, true);
            Text(g, "有性格的伙伴，也有能落地的工作流。", 26, Muted, 71, 349, false);
            string[] chips = { "四位国风伙伴", "连接 Claude Code", "对话 · 文件 · 定时任务" };
            int[] widths = { 166, 227, 259 }; int cursor = 74;
            for (int i = 0; i < chips.Length; i++)
            {
                Panel(g, new RectangleF(cursor, 420, widths[i], 46), i == 1 ? Ink : Color.FromArgb(238, 229, 199), 16);
                Text(g, chips[i], 20, i == 1 ? Paper : Ink, cursor + 13, 430, i == 1);
                cursor += widths[i] + 10;
            }
            Panel(g, new RectangleF(74, 535, 650, 201), Color.FromArgb(253, 252, 245), 24);
            Text(g, "本机 Claude Code  ×  你的桌面伙伴", 23, Ink, 99, 560, true);
            Text(g, "“帮我整理这份文件，明天下午提醒我跟进。”", 23, Muted, 99, 606, false);
            Panel(g, new RectangleF(99, 659, 578, 43), Color.FromArgb(231, 240, 230), 13);
            Text(g, "文件处理有上下文，定时任务有记录，陪伴有回应。", 20, Ink, 112, 668, false);
            // Actual, unchanged runtime artwork; no external character images.
            Portrait(g, portraits[0], new Rectangle(820, 100, 300, 300));
            Portrait(g, portraits[1], new Rectangle(1150, 110, 300, 300));
            Portrait(g, portraits[2], new Rectangle(820, 455, 300, 285));
            Portrait(g, portraits[3], new Rectangle(1150, 550, 300, 210));
            Text(g, "青衡", 23, Ink, 929, 415, true);
            Text(g, "绯笺", 23, Ink, 1252, 428, true);
            Text(g, "苍芷", 23, Ink, 925, 758, true);
            Text(g, "小墨", 23, Ink, 1273, 785, true);
            Text(g, "Windows 10 / 11   ·   C# / WinForms   ·   Apache-2.0", 18, Muted, 75, 782, false);
            image.Save(Path.Combine(output, "hero.png"), ImageFormat.Png);
            using (Bitmap social = new Bitmap(1280, 640, PixelFormat.Format24bppRgb))
            using (Graphics s = Graphics.FromImage(social))
            {
                Quality(s); s.Clear(Paper); s.DrawImage(image, new Rectangle(30, 0, 1219, 640));
                social.Save(Path.Combine(output, "social-preview.png"), ImageFormat.Png);
            }
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
        form.CreateControl();
        CreateHandles(form);
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
            Point origin = new Point(offset.X + child.Left, offset.Y + child.Top);
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
            tasks.Add(new ScheduledClaudeTask { Prompt = "整理 Markdown 笔记，生成本周待办摘要。", WorkingDirectory = @"C:\Projects\notes", Recurrence = "weekly", Weekday = 5,
                MinutesAfterMidnight = 18 * 60, RunMode = "interactive-powershell", OverlapMode = "block", DesktopNumber = 2 });
            tasks.Add(new ScheduledClaudeTask { Prompt = "检查本周计划，列出仍需确认的事项。", WorkingDirectory = @"C:\Projects\weekly-review", Recurrence = "daily",
                MinutesAfterMidnight = 9 * 60, RunMode = "background", OverlapMode = "block" });
            using (ClaudeTaskForm scheduled = new ClaudeTaskForm(service))
            {
                ((TextBox)Field(scheduled, "prompt")).Text = "整理当前目录的 Markdown 笔记，生成本周待办摘要。";
                scheduled.GetType().GetField("workingDirectory", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scheduled, @"C:\Projects\notes");
                scheduled.GetType().GetMethod("UpdateDirectoryText", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scheduled, null);
                ((ComboBox)Field(scheduled, "recurrence")).SelectedIndex = 1;
                ((ComboBox)Field(scheduled, "weekday")).SelectedIndex = 5;
                ((DateTimePicker)Field(scheduled, "time")).Value = new DateTime(2026, 10, 23, 18, 0, 0);
                var records = new List<ActivityRecord> { new ActivityRecord { Title = "本周笔记整理", SessionId = "documentation-demo", CharacterId = "orbit", State = "已完成", Updated = new DateTime(2026, 10, 21, 14, 30, 0),
                    WorkingDirectory = @"C:\Projects\notes", Output = "你：帮我整理这周的笔记，给出下一步建议。\r\n\r\n青衡：道友，已按三个方向理清：\r\n\r\n1. 本周完成：整理资料与待办。\r\n2. 还需确认：两项计划的截止时间。\r\n3. 下一步：先处理最小的一件事。\r\n\r\n以上为预设演示内容，未调用真实模型。" } };
                activity.SetActivities(records);
                ((ListBox)Field(activity, "list")).SelectedIndex = 0;
                ((ChineseInputTextBox)Field(activity, "reply")).Text = "先帮我列出明天最重要的三件事。";
                using (Bitmap left = Capture(activity))
                using (Bitmap right = Capture(scheduled))
                using (Bitmap image = new Bitmap(1600, 1030, PixelFormat.Format24bppRgb))
                using (Graphics g = Graphics.FromImage(image))
                {
                    Quality(g); g.Clear(Paper);
                    Text(g, "聊得来，也做得到。", 44, Ink, 60, 40, true);
                    Text(g, "会话动态可继续回复；定时任务有目录、有周期、有运行记录。", 24, Muted, 63, 104, false);
                    Text(g, "01  跟进一段对话", 25, Ink, 68, 180, true);
                    Text(g, "02  把重复工作交给 CLI", 25, Ink, 828, 180, true);
                    Panel(g, new RectangleF(58, 230, 706, 610), Color.FromArgb(226, 234, 224), 20);
                    g.DrawImage(left, new Rectangle(68, 240, 685, 578));
                    Panel(g, new RectangleF(818, 230, 714, 751), Color.FromArgb(226, 234, 224), 20);
                    g.DrawImage(right, new Rectangle(828, 240, 694, 721));
                    Text(g, "双击气泡展开动态，查看历史与继续回复。", 21, Muted, 69, 871, false);
                    Text(g, "真实控件 / 预设演示数据 / 无真实 CLI 或模型调用", 19, Muted, 67, 977, false);
                    image.Save(Path.Combine(output, "workbench.png"), ImageFormat.Png);
                }
            }
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
                    Text(g, "XianPal  ·  24 相位原画动画", 16, Ink, 16, 10, true);
                    for (int i = 0; i < 4; i++)
                    {
                        g.DrawImage(frames[i][frame], new Rectangle(i * 180 + 2, 34, 176, 191));
                        Text(g, PetCharacter.All[i].Name, 17, Ink, i * 180 + 69, 240, true);
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
                    Text(g, "青衡 · 御剑飞仙", 22, Ink, 22, 20, true);
                    GifFrame(gif, image);
                }
                gif.Write((byte)0x3b);
            }
        }
        finally { BuildReminderPreviews.Release(frames); }
    }
}
