// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using XianPal;

internal static class ReminderPerformanceTests
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process, uint flags);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr handle, int index);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static object Field(object instance, string name)
    {
        return instance.GetType().GetField(name, PrivateInstance).GetValue(instance);
    }

    private static long Fingerprint(Bitmap image)
    {
        long hash = 17;
        for (int y = 0; y < image.Height; y += 5)
        for (int x = 0; x < image.Width; x += 5)
        {
            Color c = image.GetPixel(x, y);
            // At alpha 1..8, unpremultiplication can quantize a very faint warm
            // halo to (255,0,255); this is not a visible colour-key fringe.
            Check(!(c.A > 8 && c.R > 235 && c.G < 20 && c.B > 235), "Magenta edge/background in native render");
            hash = unchecked(hash * 31 + c.ToArgb());
        }
        return hash;
    }

    private static Bitmap Render(Dictionary<string, List<Image>> frames, string style, double seconds, bool reduced, Rectangle exclusion)
    {
        Bitmap bitmap = new Bitmap(960, 540, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            ReminderPerformance.Render(g, bitmap.Size, style, frames, seconds, reduced, exclusion);
        }
        return bitmap;
    }

    [STAThread] private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            HashSet<string> titles = new HashSet<string>(), renderSignatures = new HashSet<string>();
            foreach (PetCharacter character in PetCharacter.All)
            {
                string style = character.ReminderStyle;
                Check(ReminderPerformance.Supports(style), "Missing character choreography");
                Check(titles.Add(ReminderPerformance.Title(style)), "Repeated performance title");
                Check(!ReminderOverlayForm.UsesPunchFist(style) && !ReminderOverlayForm.UsesScreenBreak(style), "Borrowed punch or screen-break effect");
                if (character.Id == "orbit") TestSwordFlight();
                HashSet<int> phases = new HashSet<int>();
                for (int f = 0; f < 24; f++) phases.Add(ReminderPerformance.FrameIndex((f + .5f) / 24f, 24));
                Check(phases.Count == 24, "Not all 24 approved gesture poses are used");
                foreach (Size viewport in new[] { new Size(800, 480), new Size(960, 540), new Size(1920, 1080), new Size(3840, 2160) })
                {
                    Rectangle card = ReminderPerformance.CardBounds(viewport);
                    for (int step = 0; step <= 130; step++)
                    {
                        double t = step * .05;
                        ReminderPerformance.Pose pose = ReminderPerformance.Sample(viewport, style, t, false);
                        Check(pose.Bounds.Width > 0 && pose.Bounds.Height > 0 && !Single.IsNaN(pose.Rotation), "Invalid pose");
                        if (t >= ReminderPerformance.EntranceEnd)
                        {
                            Check(pose.Bounds.Left >= 0 && pose.Bounds.Right <= viewport.Width && pose.Bounds.Top >= 0, "Clipped character after entry");
                            Check(pose.Bounds.Bottom < card.Top, "Reminder card covers character");
                        }
                    }
                    ReminderPerformance.Pose a = ReminderPerformance.Sample(viewport, style, ReminderPerformance.EntranceEnd - .001, false);
                    ReminderPerformance.Pose b = ReminderPerformance.Sample(viewport, style, ReminderPerformance.EntranceEnd + .001, false);
                    Check(Math.Abs(a.Bounds.X - b.Bounds.X) < .1, "Entry position snaps");
                    Check(a.Cycle <= 3.251f, "Screen-wide entrance produced strobing micro-steps");
                    ReminderPerformance.Pose still = ReminderPerformance.Sample(viewport, style, 0, true);
                    Check(still.Animation == "idle" && still.Bounds == ReminderPerformance.Sample(viewport, style, 4, true).Bounds, "Reduced-motion mode animates");
                }
                string root = character.Id == "orbit" ? args[0] : Path.Combine(args[0], "characters", character.Id);
                Dictionary<string, List<Image>> frames = BuildReminderPreviews.Load(root);
                try
                {
                    HashSet<long> changed = new HashSet<long>();
                    for (int frame = 0; frame < 32; frame++)
                    using (Bitmap image = Render(frames, style, 1.2 + frame * .14, false, Rectangle.Empty))
                    {
                        changed.Add(Fingerprint(image));
                        bool translucent = false;
                        for (int y = 0; y < image.Height; y += 7)
                        for (int x = 0; x < image.Width; x += 7)
                        {
                            int alpha = image.GetPixel(x, y).A;
                            if (alpha > 0 && alpha < 250) translucent = true;
                        }
                        Check(translucent, "Effects lost per-pixel transparency");
                    }
                    Check(changed.Count >= 30, "Repeated/frozen scene samples");
                    using (Bitmap a = Render(frames, style, 0, true, Rectangle.Empty))
                    using (Bitmap b = Render(frames, style, 4, true, Rectangle.Empty))
                        Check(Fingerprint(a) == Fingerprint(b), "Reduced-motion render is not static");
                    Rectangle hole = ReminderPerformance.CardBounds(new Size(960, 540));
                    using (Bitmap image = Render(frames, style, 5.7, false, hole))
                    {
                        renderSignatures.Add(Fingerprint(image).ToString());
                        for (int y = hole.Top; y < hole.Bottom; y += 4)
                        for (int x = hole.Left; x < hole.Right; x += 4)
                            Check(image.GetPixel(x, y).A == 0, "Scene intercepts/obscures reminder card");
                    }
                    TestReminderRouting(frames, style, character.Name);
                    if (character.Id == "orbit") TestOverlayClock(frames, style, character.Name);
                }
                finally { BuildReminderPreviews.Release(frames); }
                Console.WriteLine("PASS " + character.Name + ": 24 poses, timeline, 4 viewports, alpha, card avoidance, routing and dismissal");
            }
            Check(renderSignatures.Count == 4, "Character performance art not distinct");
            Rectangle secondary = new Rectangle(-1920, 0, 1920, 1080);
            Check(ReminderOverlayForm.CalculateScreenCenter(secondary) == new Point(-960, 540), "Secondary monitor target changed");
            TestNativeSurface();
            Console.WriteLine("PASS full-screen performances: no real reminders/settings/Claude sessions were created");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void TestSwordFlight()
    {
        Check(ReminderPerformance.Title("command") == "御剑飞仙", "Flight title is stale");
        foreach (Size viewport in new[] { new Size(800, 480), new Size(960, 540), new Size(1920, 1080), new Size(3840, 2160) })
        {
            ReminderPerformance.Pose rest = ReminderPerformance.Sample(viewport, "command", 0, true), previous = null;
            float minX = Single.MaxValue, maxX = Single.MinValue, minY = Single.MaxValue;
            bool sawLeft = false, sawRight = false;
            for (double t = ReminderPerformance.FlightStart; t < ReminderPerformance.FlightEnd; t += .005)
            {
                ReminderPerformance.Pose pose = ReminderPerformance.Sample(viewport, "command", t, false);
                Check(pose.Animation == "review", "Airborne rider must not walk or jump on the sword");
                minX = Math.Min(minX, pose.Bounds.X); maxX = Math.Max(maxX, pose.Bounds.X); minY = Math.Min(minY, pose.Bounds.Y);
                sawLeft |= pose.FlipX; sawRight |= !pose.FlipX;
                PointF sole = ReminderPerformance.SwordContact(pose);
                Check(sole.Y < ReminderPerformance.CardBounds(viewport).Top, "Sword overlaps the reminder card");
                double rotation = pose.Rotation * Math.PI / 180;
                // All blade / guard / grip endpoints follow the same pivot and fit this monitor.
                foreach (PointF local in new[] {
                    new PointF(-pose.Bounds.Height * .80f, -pose.Bounds.Height * 21 / 416f),
                    new PointF(pose.Bounds.Height * .92f, -pose.Bounds.Height * 21 / 416f + pose.Bounds.Height * .044f) })
                {
                    float lx = pose.FlipX ? -local.X : local.X;
                    float x = pose.Bounds.X + pose.Bounds.Width / 2 + lx * (float)Math.Cos(rotation) - local.Y * (float)Math.Sin(rotation);
                    float y = pose.Bounds.Bottom + lx * (float)Math.Sin(rotation) + local.Y * (float)Math.Cos(rotation);
                    Check(x >= 4 && x <= viewport.Width - 4 && y > 0 && y < viewport.Height, "Flight sword leaves target screen");
                }
                if (previous != null)
                {
                    Check(Math.Abs(pose.Bounds.X - previous.Bounds.X) < viewport.Width * .005f &&
                          Math.Abs(pose.Bounds.Y - previous.Bounds.Y) < viewport.Height * .005f, "Flight path jumps");
                }
                previous = pose;
            }
            Check(maxX - minX > viewport.Width * .40 && minY < rest.Bounds.Y - 30, "Sword performance only hovers in place");
            Check(sawLeft && sawRight, "Sword and rider do not turn together");
            ReminderPerformance.Pose landed = ReminderPerformance.Sample(viewport, "command", ReminderPerformance.FlightEnd, false);
            Check(Math.Abs(landed.Bounds.X - rest.Bounds.X) < .1 && Math.Abs(landed.Bounds.Y - rest.Bounds.Y) < .1 &&
                  Math.Abs(landed.Rotation) < .01 && !landed.FlipX, "Rider did not land back at the reminder position");
        }
        Console.WriteLine("PASS sword flight: full-width arc, planted feet, shared blade / rider pivot, turns, smooth return and screen bounds");
    }

    private static void TestOverlayClock(Dictionary<string, List<Image>> frames, string style, string name)
    {
        Rectangle offscreen = new Rectangle(SystemInformation.VirtualScreen.Right + 64, 0, 960, 540);
        using (ReminderOverlayForm overlay = new ReminderOverlayForm(new DailyReminder { Text = "计时器离屏验证" }, frames, false, style, name, offscreen))
        {
            // Disabled, offscreen test windows cannot take the user's keyboard focus.
            overlay.Enabled = false;
            overlay.Show();
            Application.DoEvents();
            Control card = (Control)Field(overlay, "card");
            Check(!card.Visible, "Performance card appeared before entry");
            ReminderPerformanceSurface surface = (ReminderPerformanceSurface)Field(overlay, "performanceSurface");
            Check(surface != null && surface.Owner == overlay && surface.LastPushSucceeded, "Owned native scene did not start: " +
                (surface == null ? "null surface, fallback=" + Field(overlay, "performanceFallback") : "owner=" + (surface.Owner == overlay) + ", pushed=" + surface.LastPushSucceeded));
            Stopwatch clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < ReminderPerformance.EndAt + .20)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(3);
            }
            Application.DoEvents();
            Check(card.Visible && (int)Field(overlay, "phase") == 4, "Real WinForms timer did not reveal the card");
            Check(!((Timer)Field(overlay, "timer")).Enabled, "Timer did not stop after effect cleanup");
            Check(surface.LastPushSucceeded, "Native scene presentation failed during animation");
            typeof(ReminderOverlayForm).GetMethod("Dismiss", PrivateInstance).Invoke(overlay, new object[0]);
            Check(!overlay.Visible && !surface.Visible, "Dismissal left the full-screen scene visible");
        }
        Console.WriteLine("PASS actual offscreen WinForms timeline: entry, owned alpha scene, card at 5.60s, cleanup, dismissal");
    }

    private static void TestReminderRouting(Dictionary<string, List<Image>> frames, string style, string name)
    {
        Rectangle offscreen = new Rectangle(SystemInformation.VirtualScreen.Right + 64, 0, 960, 540);
        using (ReminderOverlayForm full = new ReminderOverlayForm(new DailyReminder { Text = "离线演出测试" }, frames, false, style, name, offscreen))
        {
            Check((bool)Field(full, "characterPerformance"), "Full reminder did not select new renderer");
            Check(full.Bounds == offscreen, "Overlay changed target monitor bounds");
            Check(((Timer)Field(full, "timer")).Interval == 16, "Overlay did not request smooth presentation");
            int dismissed = 0;
            full.Dismissed += delegate { dismissed++; };
            Message message = new Message();
            object[] args = { message, Keys.Escape };
            Check((bool)typeof(ReminderOverlayForm).GetMethod("ProcessCmdKey", PrivateInstance).Invoke(full, args), "Escape not handled");
            Check(dismissed == 1, "Escape did not dismiss");
        }
        using (ReminderOverlayForm snooze = new ReminderOverlayForm(new DailyReminder { Text = "稍后测试" }, frames, false, style, name, offscreen))
        {
            int requested = 0;
            snooze.SnoozeRequested += delegate(DailyReminder item, int minutes) { requested = minutes; };
            typeof(ReminderOverlayForm).GetMethod("Snooze", PrivateInstance).Invoke(snooze, new object[0]);
            Check(requested == 5, "Snooze no longer requests five minutes");
        }
        using (ReminderOverlayForm popup = new ReminderOverlayForm(new DailyReminder { Text = "提示测试", Effect = "popup" }, frames, false, style, name, offscreen))
        using (ReminderOverlayForm countdown = new ReminderOverlayForm(new DailyReminder { Text = "倒计时测试", IsCountdown = true }, frames, false, style, name, offscreen))
        using (ReminderOverlayForm reduced = new ReminderOverlayForm(new DailyReminder { Text = "减少动态测试" }, frames, true, style, name, offscreen))
        {
            Check(!(bool)Field(popup, "characterPerformance"), "Popup-only mode acquired a full-screen show");
            Check(!(bool)Field(countdown, "characterPerformance"), "Countdown mode acquired a full-screen show");
            Check(!(bool)Field(reduced, "performanceFallback") && !((Timer)Field(reduced, "timer")).Enabled, "Reduced-motion timer must remain stopped");
            Check((int)Field(reduced, "phase") == 4, "Reduced motion delayed the card");
        }
    }

    private static void TestNativeSurface()
    {
        Rectangle offscreen = new Rectangle(SystemInformation.VirtualScreen.Right + 64, 0, 160, 90);
        // Warm up GDI+/WinForms before measuring native handles.
        using (ReminderPerformanceSurface surface = new ReminderPerformanceSurface(offscreen))
        {
            surface.Show();
            Check(surface.Present(delegate(Graphics g) { g.Clear(Color.Transparent); }), "Layered surface could not present");
        }
        uint before = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
        for (int pass = 0; pass < 5; pass++)
        using (ReminderPerformanceSurface surface = new ReminderPerformanceSurface(offscreen))
        {
            surface.Show();
            for (int frame = 0; frame < 12; frame++)
                Check(surface.Present(delegate(Graphics g)
                {
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(90, 80, 180, 160))) g.FillEllipse(brush, 20, 15, 60, 60);
                }), "UpdateLayeredWindow failed");
            int flags = GetWindowLong(surface.Handle, -20);
            Check((flags & 0x08080020) == 0x08080020, "Surface must stay layered, non-activating and click-through");
            object bitmap = Field(surface, "pixels");
            Check(((Bitmap)bitmap).GetPixel(40, 40).A == 90, "Native DIB lost premultiplied alpha");
            Check(surface.Present(delegate(Graphics g) { }), "Persistent DIB update failed");
            Check(Object.ReferenceEquals(bitmap, Field(surface, "pixels")), "Allocated a new full-screen bitmap each frame");
            surface.Size = new Size(200, 110);
            Check(surface.Present(delegate(Graphics g) { }), "Resize failed to rebuild native DIB");
            surface.Hide();
        }
        uint after = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
        Check(after <= before + 2, "Native DIB/DC handles leaked after repeated shows");
        Console.WriteLine("PASS offscreen native layered presentation, alpha, click-through, resize and GDI handle cleanup");
    }
}
