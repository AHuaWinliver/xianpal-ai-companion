using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed class TrailOverlayForm : Form
    {
        private sealed class TrailPoint
        {
            internal Point Position;
            internal DateTime Created;
        }

        private readonly List<TrailPoint> points = new List<TrailPoint>();
        private readonly Timer fadeTimer = new Timer();
        private Color[] colors = PetBrain.DefaultColors("orbit");
        private string style = "rainbow";
        private float opacity = 0.42f;
        private Rectangle screenBounds;
        private bool layeredRenderSucceeded;
        private const double LifetimeSeconds = 3.2;
        private const float HeadCoreWidth = 16f;
        private const float TailCoreWidth = 1.4f;
        private const int MaximumPoints = 240;
        private const int GwlExStyle = -20;
        private const int WsExLayered = 0x00080000;
        private const int WsExTransparent = 0x00000020;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;
        private const int UlwAlpha = 0x00000002;
        private const byte AcSrcAlpha = 0x01;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction { internal byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { internal int X, Y; internal NativePoint(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize { internal int Width, Height; internal NativeSize(int width, int height) { Width = width; Height = height; } }
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr destinationDc, ref NativePoint destination, ref NativeSize size, IntPtr sourceDc, ref NativePoint source, int colorKey, ref BlendFunction blend, int flags);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);

        internal TrailOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            fadeTimer.Interval = 40;
            fadeTimer.Tick += delegate { RenderTrail(); };
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams value = base.CreateParams;
                value.ExStyle |= WsExLayered | WsExTransparent | WsExToolWindow | WsExNoActivate;
                return value;
            }
        }

        internal void Configure(Color[] palette, string trailStyle, float trailOpacity)
        {
            if (palette != null && palette.Length >= 3) colors = palette;
            style = String.IsNullOrWhiteSpace(trailStyle) ? "rainbow" : trailStyle;
            opacity = Math.Max(0.15f, Math.Min(0.80f, trailOpacity));
        }

        internal void Begin(Rectangle bounds)
        {
            screenBounds = bounds;
            points.Clear();
            if (!Visible) Show();
            fadeTimer.Start();
        }

        internal void AddPoint(Point screenPoint, Rectangle bounds)
        {
            if (screenBounds != bounds || !Visible) Begin(bounds);
            if (points.Count > 0)
            {
                Point previous = points[points.Count - 1].Position;
                int dx = screenPoint.X - previous.X, dy = screenPoint.Y - previous.Y;
                if ((dx * dx) + (dy * dy) < 9) return;
            }
            points.Add(new TrailPoint { Position = screenPoint, Created = DateTime.UtcNow });
            if (points.Count > MaximumPoints) points.RemoveRange(0, points.Count - MaximumPoints);
            RenderTrail();
        }

        internal static float ClampOpacity(float value) { return Math.Max(0.15f, Math.Min(0.80f, value)); }

        internal static float CoreWidthAt(float progress)
        {
            progress = Math.Max(0f, Math.Min(1f, progress));
            // Accelerate the growth near the pet so the ribbon visibly emerges
            // from the body and tapers into a hairline instead of looking like a
            // constant-width polyline.
            float eased = progress * progress * (3f - (2f * progress));
            return TailCoreWidth + ((HeadCoreWidth - TailCoreWidth) * eased);
        }

        internal static int AlphaAt(float progress, float ageFraction, float configuredOpacity)
        {
            progress = Math.Max(0f, Math.Min(1f, progress));
            ageFraction = Math.Max(0f, Math.Min(1f, ageFraction));
            float tailFade = 0.12f + (0.88f * (float)Math.Sqrt(progress));
            float lifetimeFade = 1f - (ageFraction * ageFraction);
            return (int)Math.Round(255f * ClampOpacity(configuredOpacity) * tailFade * lifetimeFade);
        }

        internal static bool ValidateTaperedLightProfile()
        {
            return CoreWidthAt(0f) < 2f && CoreWidthAt(0.5f) > CoreWidthAt(0f) * 3f &&
                   CoreWidthAt(1f) >= 15f && AlphaAt(0.05f, 0.8f, 0.4f) < AlphaAt(1f, 0f, 0.4f);
        }

        internal bool LayeredRenderSucceeded { get { return layeredRenderSucceeded; } }

        private void RenderTrail()
        {
            DateTime now = DateTime.UtcNow;
            points.RemoveAll(delegate(TrailPoint point) { return (now - point.Created).TotalSeconds > LifetimeSeconds; });
            if (points.Count < 2)
            {
                if (points.Count == 0) { fadeTimer.Stop(); Hide(); }
                return;
            }

            Rectangle dirty = Rectangle.Empty;
            foreach (TrailPoint point in points)
            {
                Rectangle around = new Rectangle(point.Position.X - 34, point.Position.Y - 34, 68, 68);
                dirty = dirty.IsEmpty ? around : Rectangle.Union(dirty, around);
            }
            dirty = Rectangle.Intersect(dirty, screenBounds);
            if (dirty.Width < 2 || dirty.Height < 2) return;
            Location = dirty.Location;
            Size = dirty.Size;

            using (Bitmap bitmap = new Bitmap(dirty.Width, dirty.Height, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                for (int i = 1; i < points.Count; i++)
                {
                    TrailPoint a = points[i - 1], b = points[i];
                    float age = (float)(now - b.Created).TotalSeconds;
                    float progress = i / (float)Math.Max(1, points.Count - 1);
                    int alpha = AlphaAt(progress, age / (float)LifetimeSeconds, opacity);
                    if (alpha < 2) continue;
                    Color baseColor = PaletteColor(progress, i, colors, StyleOffset(style));
                    Point p1 = new Point(a.Position.X - dirty.Left, a.Position.Y - dirty.Top);
                    Point p2 = new Point(b.Position.X - dirty.Left, b.Position.Y - dirty.Top);
                    float coreWidth = CoreWidthAt(progress) * (style == "candy" ? 1.12f : 1f);
                    float haloWidth = coreWidth * (style == "neon" ? 2.8f : 2.35f);
                    float bloomWidth = coreWidth * (style == "aurora" ? 1.75f : 1.55f);
                    using (Pen halo = new Pen(Color.FromArgb(Math.Max(1, alpha / 9), baseColor), haloWidth))
                    using (Pen bloom = new Pen(Color.FromArgb(Math.Max(1, alpha / 4), baseColor), bloomWidth))
                    using (Pen core = new Pen(Color.FromArgb(Math.Max(1, (int)(alpha * 0.82f)), Lighten(baseColor, 0.24f)), coreWidth))
                    {
                        halo.StartCap = halo.EndCap = LineCap.Round;
                        bloom.StartCap = bloom.EndCap = LineCap.Round;
                        core.StartCap = core.EndCap = LineCap.Round;
                        graphics.DrawLine(halo, p1, p2);
                        graphics.DrawLine(bloom, p1, p2);
                        graphics.DrawLine(core, p1, p2);
                    }
                }
                Push(bitmap, dirty.Location);
            }
        }

        private static int StyleOffset(string value)
        {
            if (value == "aurora") return 1;
            if (value == "neon") return 2;
            if (value == "candy") return 3;
            return 0;
        }

        private static Color PaletteColor(float progress, int segment, Color[] palette, int offset)
        {
            if (palette == null || palette.Length == 0) return Color.White;
            // Interpolate instead of switching abruptly between palette entries.
            // Two gentle color cycles across a trail resemble long-exposure light
            // rather than a row of disconnected colored dashes.
            float phase = (progress * Math.Max(1, palette.Length - 1) * 2f) + offset + ((segment % 3) * 0.08f);
            int first = ((int)Math.Floor(phase)) % palette.Length;
            if (first < 0) first += palette.Length;
            int second = (first + 1) % palette.Length;
            float mix = phase - (float)Math.Floor(phase);
            Color a = palette[first], b = palette[second];
            return Color.FromArgb(
                (int)Math.Round(a.R + ((b.R - a.R) * mix)),
                (int)Math.Round(a.G + ((b.G - a.G) * mix)),
                (int)Math.Round(a.B + ((b.B - a.B) * mix)));
        }

        private static Color Lighten(Color color, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            return Color.FromArgb(
                (int)Math.Round(color.R + ((255 - color.R) * amount)),
                (int)Math.Round(color.G + ((255 - color.G) * amount)),
                (int)Math.Round(color.B + ((255 - color.B) * amount)));
        }

        private void Push(Bitmap bitmap, Point screenLocation)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero), memoryDc = IntPtr.Zero, hBitmap = IntPtr.Zero, previous = IntPtr.Zero;
            try
            {
                memoryDc = CreateCompatibleDC(screenDc);
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                previous = SelectObject(memoryDc, hBitmap);
                NativePoint destination = new NativePoint(screenLocation.X, screenLocation.Y);
                NativeSize size = new NativeSize(bitmap.Width, bitmap.Height);
                NativePoint source = new NativePoint(0, 0);
                BlendFunction blend = new BlendFunction { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AcSrcAlpha };
                layeredRenderSucceeded = UpdateLayeredWindow(Handle, screenDc, ref destination, ref size, memoryDc, ref source, 0, ref blend, UlwAlpha);
            }
            finally
            {
                if (previous != IntPtr.Zero && memoryDc != IntPtr.Zero) SelectObject(memoryDc, previous);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (memoryDc != IntPtr.Zero) DeleteDC(memoryDc);
                if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { fadeTimer.Stop(); fadeTimer.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
