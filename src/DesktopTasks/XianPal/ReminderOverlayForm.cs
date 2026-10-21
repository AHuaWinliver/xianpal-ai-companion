using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed class ReminderOverlayForm : Form
    {
        private readonly DailyReminder reminder;
        private readonly IDictionary<string, List<Image>> animations;
        private readonly Timer timer = new Timer();
        private readonly List<PointF[]> cracks;
        private readonly Image punchFist;
        private readonly Image impactCenter;
        private readonly RoundedPanel card = new RoundedPanel();
        private readonly bool reduceMotion;
        private readonly bool countdownMode;
        private readonly bool popupOnly;
        private readonly string impactStyle;
        private readonly string characterName;
        private readonly bool characterPerformance;
        private readonly Stopwatch performanceClock = new Stopwatch();
        private ReminderPerformanceSurface performanceSurface;
        private bool performanceFallback;
        private int phase;
        private int tick;
        private bool finished;
        internal event Action Dismissed;
        internal event Action<DailyReminder, int> SnoozeRequested;

        internal ReminderOverlayForm(DailyReminder item, IDictionary<string, List<Image>> frames, bool reducedMotion)
            : this(item, frames, reducedMotion, "command", "青衡", Screen.PrimaryScreen.Bounds)
        {
        }

        internal ReminderOverlayForm(DailyReminder item, IDictionary<string, List<Image>> frames, bool reducedMotion, string reminderStyle, string petName)
            : this(item, frames, reducedMotion, reminderStyle, petName, Screen.PrimaryScreen.Bounds)
        {
        }

        internal ReminderOverlayForm(DailyReminder item, IDictionary<string, List<Image>> frames, bool reducedMotion, string reminderStyle, string petName, Rectangle targetScreenBounds)
        {
            reminder = item;
            animations = frames;
            reduceMotion = reducedMotion;
            popupOnly = item != null && !item.IsCountdown && ReminderService.NormalizeEffect(item.Effect) == "popup";
            countdownMode = item != null && item.IsCountdown;
            impactStyle = String.IsNullOrWhiteSpace(reminderStyle) ? "warning" : reminderStyle.ToLowerInvariant();
            characterName = String.IsNullOrWhiteSpace(petName) ? "桌面宠物" : petName;
            characterPerformance = !popupOnly && !countdownMode && ReminderPerformance.Supports(impactStyle);
            Rectangle targetScreen = NormalizeTargetScreen(targetScreenBounds);
            StartPosition = FormStartPosition.Manual;
            Bounds = targetScreen;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            cracks = CreateCracks(ClientSize, 7319);
            string fistPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "reminder", "punch-fist.png");
            if (UsesPunchFist(impactStyle) && !countdownMode && !popupOnly && File.Exists(fistPath))
            {
                using (Image source = Image.FromFile(fistPath)) punchFist = new Bitmap(source);
            }
            string impactPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "reminder", "impact-center.png");
            if (UsesScreenBreak(impactStyle) && !countdownMode && !popupOnly && File.Exists(impactPath))
            {
                using (Image source = Image.FromFile(impactPath)) impactCenter = new Bitmap(source);
            }
            BuildCard();
            Controls.Add(card);
            PositionCard();

            timer.Interval = characterPerformance ? 16 : 40;
            timer.Tick += Animate;
            if (popupOnly || reduceMotion)
            {
                phase = 4;
                tick = 0;
                card.Visible = true;
            }
            else
            {
                phase = 0;
                card.Visible = false;
                timer.Start();
            }
            Resize += delegate { PositionCard(); };
            FormClosing += OnOverlayClosing;
        }

        internal static Rectangle NormalizeTargetScreen(Rectangle requested)
        {
            return requested.Width > 0 && requested.Height > 0 ? requested : Screen.PrimaryScreen.Bounds;
        }

        internal static Point CalculateScreenCenter(Rectangle screenBounds)
        {
            Rectangle valid = NormalizeTargetScreen(screenBounds);
            return new Point(valid.Left + (valid.Width / 2), valid.Top + (valid.Height / 2));
        }

        protected override bool ShowWithoutActivation
        {
            get { return false; }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (characterPerformance)
            {
                performanceClock.Restart();
                performanceSurface = new ReminderPerformanceSurface(Bounds);
                RenderPerformance();
                if (performanceSurface != null && !performanceFallback) performanceSurface.Show(this);
            }
            Activate();
            BringToFront();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Dismiss(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            if (popupOnly) return;
            if (characterPerformance)
            {
                if (performanceFallback) DrawRobot(e.Graphics);
                return;
            }
            if (countdownMode)
            {
                DrawRobot(e.Graphics);
                return;
            }
            bool screenBreak = UsesScreenBreak(impactStyle);
            float crackProgress = !screenBreak || phase < 3 ? 0f : (phase == 3 ? Math.Min(1f, tick / 23f) : 1f);
            DrawRobot(e.Graphics);
            if (crackProgress > 0f)
            {
                DrawCracks(e.Graphics, crackProgress);
                DrawImpactCenter(e.Graphics, crackProgress);
            }
            if (UsesPunchFist(impactStyle) && ((phase == 1 && tick >= 5) || phase == 2 || (phase == 3 && tick < 4))) DrawPunchFist(e.Graphics);
            if (screenBreak && phase == 3 && tick < 11) DrawImpactFlash(e.Graphics, tick);
        }

        private void BuildCard()
        {
            card.Radius = 20;
            card.BorderColor = Color.FromArgb(214, 214, 210);
            card.BackColor = Color.FromArgb(253, 253, 252);
            card.Size = new Size(520, characterPerformance ? 188 : 162);

            Label brand = new Label { Text = characterName + (characterPerformance ? " · " + ReminderPerformance.Title(impactStyle) : "提醒你"), Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 112, 72), AutoSize = true, Location = new Point(22, 17), BackColor = card.BackColor };
            Label time = new Label { Text = reminder.TimeText, Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(126, 126, 120), AutoSize = true, Location = new Point(420, 19), BackColor = card.BackColor };
            Label task = new Label { Text = reminder.Text, Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold), ForeColor = Color.FromArgb(34, 34, 32), Location = new Point(22, 49), Size = new Size(476, 52), AutoEllipsis = true, BackColor = card.BackColor };

            Button snooze = new Button { Text = "稍后 5 分钟", Location = new Point(290, 112), Size = new Size(98, 34), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(247, 247, 245), ForeColor = Color.FromArgb(62, 62, 58), Cursor = Cursors.Hand };
            snooze.FlatAppearance.BorderColor = Color.FromArgb(219, 219, 215);
            snooze.Click += delegate { Snooze(); };
            Button done = new Button { Text = "我知道了", Location = new Point(398, 112), Size = new Size(100, 34), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(32, 33, 31), ForeColor = Color.White, Cursor = Cursors.Hand };
            done.FlatAppearance.BorderColor = Color.FromArgb(32, 33, 31);
            done.Click += delegate { Dismiss(); };

            card.Controls.Add(brand);
            card.Controls.Add(time);
            card.Controls.Add(task);
            if (characterPerformance)
            {
                Label line = new Label { Text = ReminderPerformance.Line(impactStyle), AutoSize = true,
                    Font = new Font("Microsoft YaHei UI", 8.5f), ForeColor = Color.FromArgb(105, 115, 110),
                    Location = new Point(22, 105), BackColor = card.BackColor };
                card.Controls.Add(line);
                snooze.Top = done.Top = 138;
            }
            card.Controls.Add(snooze);
            card.Controls.Add(done);
        }

        private void PositionCard()
        {
            if (characterPerformance)
            {
                card.Bounds = ReminderPerformance.CardBounds(ClientSize);
                return;
            }
            int x = Math.Max(12, (ClientSize.Width - card.Width) / 2);
            int desiredY = (ClientSize.Height / 2) + 105;
            int y = Math.Max(12, Math.Min(desiredY, ClientSize.Height - card.Height - 18));
            card.Location = new Point(x, y);
        }

        private void Animate(object sender, EventArgs e)
        {
            if (characterPerformance)
            {
                if (!IsHandleCreated || !Visible) return;
                double elapsed = performanceClock.Elapsed.TotalSeconds;
                if (elapsed >= ReminderPerformance.CardAt && !card.Visible)
                {
                    phase = 4;
                    card.Visible = true;
                    card.BringToFront();
                }
                RenderPerformance();
                if (elapsed >= ReminderPerformance.EndAt) timer.Stop();
                return;
            }
            tick++;
            if (countdownMode)
            {
                if (phase == 0 && tick >= 17) { phase = 1; tick = 0; }
                else if (phase == 1 && tick >= 19)
                {
                    phase = 4;
                    tick = 0;
                    timer.Stop();
                    card.Visible = true;
                    card.BringToFront();
                }
                Invalidate();
                return;
            }
            if (phase == 0 && tick >= 30) { phase = 1; tick = 0; }
            else if (phase == 1 && tick >= 12) { phase = 2; tick = 0; }
            else if (phase == 2 && tick >= 41) { phase = 3; tick = 0; }
            else if (phase == 3 && tick >= 25)
            {
                phase = 4;
                tick = 0;
                timer.Stop();
                card.Visible = true;
                card.BringToFront();
            }
            Invalidate();
        }

        private void RenderPerformance()
        {
            if (performanceSurface == null || performanceSurface.IsDisposed || performanceFallback) { Invalidate(); return; }
            performanceSurface.Bounds = Bounds;
            Rectangle exclusion = card.Visible ? Rectangle.Inflate(card.Bounds, 2, 2) : Rectangle.Empty;
            try
            {
                if (performanceSurface.Present(delegate(Graphics g)
                {
                    ReminderPerformance.Render(g, ClientSize, impactStyle, animations, performanceClock.Elapsed.TotalSeconds, reduceMotion, exclusion);
                })) return;
            }
            catch (System.ComponentModel.Win32Exception) { }
            // Preserve the actionable reminder even if layered rendering is unavailable.
            performanceFallback = true;
            performanceSurface.Hide();
            performanceSurface.Dispose();
            performanceSurface = null;
            phase = 4;
            card.Visible = true;
            card.BringToFront();
            timer.Stop();
            Invalidate();
        }

        private void DrawRobot(Graphics graphics)
        {
            string animation;
            int index;
            int centerX = ClientSize.Width / 2;
            int robotY = Math.Max(20, (ClientSize.Height / 2) - 150);
            int robotX;
            if (countdownMode)
            {
                if (phase == 0)
                {
                    float progress = Math.Min(1f, tick / 11f);
                    float eased = 1f - ((1f - progress) * (1f - progress));
                    robotX = (int)Math.Round(ClientSize.Width + 40 + ((centerX - 96 - ClientSize.Width - 40) * eased));
                    animation = "running-left";
                    index = (tick * 2) % 8;
                }
                else
                {
                    robotX = centerX - 96;
                    if (phase == 1 && tick < 4) { animation = "waving"; index = tick; }
                    else if (phase == 1 && tick < 10) { animation = "jumping"; index = tick - 4; }
                    else if (phase == 1) { animation = "review"; index = tick - 10; }
                    else { animation = "idle"; index = 0; }
                }
                List<Image> countdownFrames;
                if (!animations.TryGetValue(animation, out countdownFrames) || countdownFrames.Count == 0) return;
                DrawColorKeySafeImage(graphics, countdownFrames[index % countdownFrames.Count], new Rectangle(robotX, robotY, 192, 208));
                return;
            }
            if (phase == 0)
            {
                float progress = Math.Min(1f, tick / 21f);
                float eased = 1f - ((1f - progress) * (1f - progress));
                robotX = (int)Math.Round(ClientSize.Width + 40 + ((centerX - 96 - ClientSize.Width - 40) * eased));
                animation = "running-left";
                index = tick % 8;
            }
            else
            {
                robotX = centerX - 96;
                if (UsesPunchFist(impactStyle) && phase == 1)
                {
                    animation = "punching";
                    index = Math.Min(1, tick / 5);
                }
                else if (UsesPunchFist(impactStyle) && phase == 2)
                {
                    animation = "punching";
                    index = Math.Min(3, 1 + (tick / 10));
                    float strikeProgress = Math.Min(1f, tick / 29f);
                    robotX += (int)Math.Round(strikeProgress * 8f);
                    robotY += (int)Math.Round(strikeProgress * 6f);
                }
                else if (UsesPunchFist(impactStyle) && phase == 3 && tick < 4)
                {
                    animation = "punching";
                    index = 3;
                    robotX += 8;
                    robotY += 6;
                }
                else
                {
                    animation = ReminderAnimationFor(impactStyle, phase, tick);
                    index = tick;
                    ApplyCharacterPerformanceMotion(impactStyle, phase, tick, ref robotX, ref robotY);
                }
            }
            List<Image> frames;
            if (!animations.TryGetValue(animation, out frames) || frames.Count == 0) return;
            Image image = frames[index % frames.Count];
            DrawColorKeySafeImage(graphics, image, new Rectangle(robotX, robotY, 192, 208));
        }

        internal static bool UsesPunchFist(string style)
        {
            return String.Equals(style, "punch", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool UsesScreenBreak(string style)
        {
            return UsesPunchFist(style);
        }

        internal static string ReminderAnimationFor(string style, int performancePhase, int performanceTick)
        {
            string normalized = String.IsNullOrWhiteSpace(style) ? "warning" : style.ToLowerInvariant();
            if (performancePhase >= 4) return "idle";
            if (normalized == "beam")
                return performancePhase == 1 ? "waving" : (performancePhase == 2 ? (performanceTick % 10 < 6 ? "jumping" : "review") : "waving");
            if (normalized == "pounce" || normalized == "charge" || normalized == "tackle")
                return performancePhase == 1 ? "running" : "jumping";
            if (normalized == "kick")
                return performancePhase == 1 ? "running" : (performancePhase == 2 ? "jumping" : "review");
            if (normalized == "panic")
                return performancePhase == 1 ? "failed" : (performancePhase == 2 ? "waving" : "jumping");
            if (normalized == "stone")
                return performancePhase == 1 ? "review" : (performancePhase == 2 ? "waiting" : "review");
            if (normalized == "drama")
                return performancePhase == 1 ? "waving" : (performancePhase == 2 ? "jumping" : "waving");
            if (normalized == "command")
                return performancePhase == 1 ? "review" : (performancePhase == 2 ? "waving" : "review");
            if (normalized == "warning")
                return performancePhase == 1 ? "waving" : "review";
            return performancePhase == 3 ? "jumping" : "punching";
        }

        private static void ApplyCharacterPerformanceMotion(string style, int performancePhase, int performanceTick, ref int x, ref int y)
        {
            string normalized = String.IsNullOrWhiteSpace(style) ? "warning" : style.ToLowerInvariant();
            if (normalized == "beam")
            {
                if (performancePhase == 2) y -= Math.Min(20, performanceTick);
                return;
            }
            if (normalized == "pounce" || normalized == "charge" || normalized == "tackle")
            {
                if (performancePhase == 1) x += (performanceTick % 4) - 2;
                else if (performancePhase == 2) { x += Math.Min(28, performanceTick); y -= Math.Min(24, performanceTick); }
                else if (performancePhase == 3) { x += 28; y += Math.Min(10, performanceTick); }
                return;
            }
            if (normalized == "kick" && performancePhase == 2)
            {
                x += Math.Min(22, performanceTick);
                y -= Math.Min(16, performanceTick);
            }
            else if (normalized == "panic") x += (performanceTick % 3) - 1;
            else if (normalized == "drama" && performancePhase == 2) y -= Math.Min(14, performanceTick);
            else if (normalized == "command" && performancePhase == 2) x += Math.Min(8, performanceTick / 3);
        }

        private void DrawPunchFist(Graphics graphics)
        {
            if (punchFist == null) return;
            int growthStep = phase == 1 ? Math.Max(0, tick - 5) : (phase == 2 ? 4 + tick : 33);
            float progress = Math.Min(1f, growthStep / 33f);
            float eased = progress * progress * (3f - (2f * progress));
            int maximum = (int)(Math.Min(ClientSize.Width, ClientSize.Height) * 0.88f);
            int size = CalculatePunchSize(progress, maximum);
            float startX = (ClientSize.Width / 2f) - 26f;
            float startY = (ClientSize.Height / 2f) - 73f;
            float centerX = startX + (((ClientSize.Width / 2f) - startX) * eased);
            float centerY = startY + ((((ClientSize.Height / 2f) - 12f) - startY) * eased);
            if (phase == 3) size = (int)(maximum * (1f - (tick * 0.035f)));
            DrawColorKeySafeImage(graphics, punchFist, new Rectangle((int)(centerX - (size / 2f)), (int)(centerY - (size / 2f)), size, size));
        }

        internal static int CalculatePunchSize(float progress, int maximum)
        {
            progress = Math.Max(0f, Math.Min(1f, progress));
            float eased = progress * progress * (3f - (2f * progress));
            return (int)Math.Round(38f + ((maximum - 38f) * eased));
        }

        internal static bool ValidatePunchProgression()
        {
            const int maximum = 900;
            int previous = CalculatePunchSize(0f, maximum);
            int largestStep = 0;
            for (int step = 1; step <= 33; step++)
            {
                int current = CalculatePunchSize(step / 33f, maximum);
                if (current < previous) return false;
                largestStep = Math.Max(largestStep, current - previous);
                previous = current;
            }
            return CalculatePunchSize(0f, maximum) == 38 && previous == maximum && largestStep < 42;
        }

        internal static bool UsesImpactEffect(DailyReminder item)
        {
            return item == null || (!item.IsCountdown && ReminderService.NormalizeEffect(item.Effect) != "popup");
        }

        internal static Bitmap CreateColorKeySafeBitmap(Image source, Size size)
        {
            Bitmap result = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(Point.Empty, result.Size));
            }

            Rectangle bounds = new Rectangle(Point.Empty, result.Size);
            BitmapData data = result.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int rowSize = Math.Abs(data.Stride);
                byte[] pixels = new byte[rowSize * result.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                for (int y = 0; y < result.Height; y++)
                    for (int x = 0; x < result.Width; x++)
                    {
                        int offset = (y * rowSize) + (x * 4);
                        if (pixels[offset + 3] <= 8)
                        {
                            pixels[offset] = 0; pixels[offset + 1] = 0; pixels[offset + 2] = 0; pixels[offset + 3] = 0;
                        }
                        else pixels[offset + 3] = 255;
                    }
                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally { result.UnlockBits(data); }
            return result;
        }

        private static void DrawColorKeySafeImage(Graphics graphics, Image source, Rectangle destination)
        {
            if (source == null || destination.Width <= 0 || destination.Height <= 0) return;
            using (Bitmap safe = CreateColorKeySafeBitmap(source, destination.Size))
            {
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.DrawImageUnscaled(safe, destination.Location);
            }
        }

        private void DrawCracks(Graphics graphics, float progress)
        {
            graphics.SmoothingMode = SmoothingMode.None;
            using (Pen shadow = new Pen(Color.FromArgb(35, 38, 40), 2f))
            using (Pen glass = new Pen(Color.FromArgb(246, 249, 251), 1f))
            {
                foreach (PointF[] path in cracks)
                {
                    int count = Math.Max(1, (int)Math.Ceiling((path.Length - 1) * progress));
                    for (int i = 0; i < count && i + 1 < path.Length; i++)
                    {
                        graphics.DrawLine(shadow, path[i], path[i + 1]);
                        PointF offsetStart = new PointF(path[i].X + 1, path[i].Y + 1);
                        PointF offsetEnd = new PointF(path[i + 1].X + 1, path[i + 1].Y + 1);
                        graphics.DrawLine(glass, offsetStart, offsetEnd);
                    }
                }
            }
        }

        private void DrawImpactCenter(Graphics graphics, float progress)
        {
            if (impactCenter == null) return;
            float eased = 1f - ((1f - progress) * (1f - progress));
            int finalSize = Math.Min(430, Math.Max(300, (int)(Math.Min(ClientSize.Width, ClientSize.Height) * 0.40f)));
            int size = (int)Math.Round(76 + ((finalSize - 76) * eased));
            int centerX = ClientSize.Width / 2;
            int centerY = (ClientSize.Height / 2) - 15;
            DrawColorKeySafeImage(graphics, impactCenter, new Rectangle(centerX - (size / 2), centerY - (size / 2), size, size));
        }

        private void DrawImpactFlash(Graphics graphics, int frame)
        {
            graphics.SmoothingMode = SmoothingMode.None;
            Point center = new Point(ClientSize.Width / 2, ClientSize.Height / 2 - 15);
            int radius = 24 + (frame * 19);
            using (Pen dark = new Pen(Color.FromArgb(30, 31, 30), 6f)) graphics.DrawEllipse(dark, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            using (Pen white = new Pen(Color.White, 3f)) graphics.DrawEllipse(white, center.X - radius + 4, center.Y - radius + 4, (radius - 4) * 2, (radius - 4) * 2);
        }

        internal static List<PointF[]> CreateCracks(Size size, int seed)
        {
            List<PointF[]> result = new List<PointF[]>();
            Random random = new Random(seed);
            PointF origin = new PointF(size.Width / 2f, size.Height / 2f - 15f);
            double maximum = Math.Sqrt((size.Width * size.Width) + (size.Height * size.Height));
            double[] baseAngles = { -3.08, -2.73, -2.34, -2.02, -1.67, -1.31, -0.91, -0.52, -0.08, 0.29, 0.70, 1.08, 1.45, 1.84, 2.24, 2.68 };
            List<PointF[]> primary = new List<PointF[]>();
            for (int ray = 0; ray < baseAngles.Length; ray++)
            {
                double angle = baseAngles[ray] + ((random.NextDouble() - 0.5) * 0.14);
                int segments = 15 + random.Next(5);
                double rayLength = maximum * (0.76 + (random.NextDouble() * 0.30));
                PointF[] path = new PointF[segments + 1];
                path[0] = origin;
                double drift = 0;
                for (int segment = 1; segment <= segments; segment++)
                {
                    double distance = 10 + ((rayLength - 10) * Math.Pow(segment / (double)segments, 1.24));
                    drift += (random.NextDouble() - 0.5) * 0.065;
                    if (segment % 5 == 0) drift += (random.NextDouble() - 0.5) * 0.09;
                    double bend = angle + drift;
                    path[segment] = new PointF(origin.X + (float)(Math.Cos(bend) * distance), origin.Y + (float)(Math.Sin(bend) * distance));
                    if (segment > 2 && segment < segments - 2 && ((segment + (ray * 3)) % 4 == 0 || random.NextDouble() < 0.10))
                    {
                        double branchAngle = bend + (random.Next(2) == 0 ? -0.62 : 0.62) + ((random.NextDouble() - 0.5) * 0.30);
                        float branchLength = 28 + random.Next(105);
                        PointF first = new PointF(path[segment].X + (float)(Math.Cos(branchAngle) * branchLength * 0.38), path[segment].Y + (float)(Math.Sin(branchAngle) * branchLength * 0.38));
                        branchAngle += (random.NextDouble() - 0.5) * 0.34;
                        PointF second = new PointF(first.X + (float)(Math.Cos(branchAngle) * branchLength * 0.36), first.Y + (float)(Math.Sin(branchAngle) * branchLength * 0.36));
                        branchAngle += (random.NextDouble() - 0.5) * 0.30;
                        PointF branchEnd = new PointF(second.X + (float)(Math.Cos(branchAngle) * branchLength * 0.26), second.Y + (float)(Math.Sin(branchAngle) * branchLength * 0.26));
                        result.Add(new[] { path[segment], first, second, branchEnd });
                    }
                }
                primary.Add(path);
                result.Add(path);
            }

            for (int ring = 0; ring < 3; ring++)
            {
                float radius = 92 + (ring * 88);
                for (int ray = 0; ray < baseAngles.Length; ray++)
                {
                    if ((ray + (ring * 2)) % 5 == 0) continue;
                    double startAngle = baseAngles[ray];
                    double endAngle = ray == baseAngles.Length - 1 ? baseAngles[0] + (Math.PI * 2) : baseAngles[ray + 1];
                    float startRadius = radius + random.Next(-18, 19);
                    float endRadius = radius + random.Next(-18, 19);
                    PointF a = new PointF(origin.X + (float)(Math.Cos(startAngle) * startRadius), origin.Y + (float)(Math.Sin(startAngle) * startRadius));
                    PointF b = new PointF(origin.X + (float)(Math.Cos(endAngle) * endRadius), origin.Y + (float)(Math.Sin(endAngle) * endRadius));
                    double middleAngle = (startAngle + endAngle) / 2.0;
                    float middleRadius = radius + random.Next(-24, 25);
                    PointF mid = new PointF(origin.X + (float)(Math.Cos(middleAngle) * middleRadius), origin.Y + (float)(Math.Sin(middleAngle) * middleRadius));
                    result.Add(new[] { a, mid, b });
                }
            }
            return result;
        }

        internal static bool ValidateCrackLayout(Size size)
        {
            List<PointF[]> paths = CreateCracks(size, 7319);
            if (paths.Count < 45) return false;
            float minX = Single.MaxValue, minY = Single.MaxValue, maxX = Single.MinValue, maxY = Single.MinValue;
            foreach (PointF[] path in paths)
                foreach (PointF point in path)
                {
                    minX = Math.Min(minX, point.X); minY = Math.Min(minY, point.Y);
                    maxX = Math.Max(maxX, point.X); maxY = Math.Max(maxY, point.Y);
                }
            return minX < 0 && minY < 0 && maxX > size.Width && maxY > size.Height;
        }

        private void Snooze()
        {
            if (finished) return;
            finished = true;
            timer.Stop();
            if (performanceSurface != null) performanceSurface.Hide();
            Hide();
            Action<DailyReminder, int> handler = SnoozeRequested;
            if (handler != null) handler(reminder, 5);
        }

        private void Dismiss()
        {
            if (finished) return;
            finished = true;
            timer.Stop();
            if (performanceSurface != null) performanceSurface.Hide();
            Hide();
            Action handler = Dismissed;
            if (handler != null) handler();
        }

        private void OnOverlayClosing(object sender, FormClosingEventArgs e)
        {
            if (!finished && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                BeginInvoke(new MethodInvoker(Dismiss));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                performanceClock.Stop();
                if (performanceSurface != null) performanceSurface.Dispose();
                if (punchFist != null) punchFist.Dispose();
                if (impactCenter != null) impactCenter.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
