using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed class FileEatOverlayForm : Form
    {
        private readonly Timer timer = new Timer();
        private DateTime startedAt;
        private Point start;
        private Point target;
        private string label = "文件";
        private bool folder;
        private int count;
        private bool completed;
        private bool layeredRenderSucceeded;
        internal const int EatingDurationMs = 1280;
        internal event Action Completed;

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

        internal FileEatOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            timer.Interval = 16;
            timer.Tick += TickAnimation;
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

        internal void Begin(Point from, Point mouth, string itemLabel, bool isFolder, int itemCount)
        {
            start = from;
            target = mouth;
            label = String.IsNullOrWhiteSpace(itemLabel) ? (isFolder ? "文件夹" : "文件") : itemLabel;
            folder = isFolder;
            count = Math.Max(1, itemCount);
            completed = false;
            startedAt = DateTime.UtcNow;
            if (!Visible) Show();
            timer.Start();
            RenderFrame(0f);
        }

        private void TickAnimation(object sender, EventArgs e)
        {
            float progress = (float)Math.Min(1.0, (DateTime.UtcNow - startedAt).TotalMilliseconds / EatingDurationMs);
            RenderFrame(progress);
            if (progress < 1f || completed) return;
            completed = true;
            timer.Stop();
            Hide();
            Action handler = Completed;
            if (handler != null) handler();
        }

        private void RenderFrame(float progress)
        {
            float eased = Ease(progress);
            Point center = new Point(
                (int)Math.Round(start.X + ((target.X - start.X) * eased)),
                (int)Math.Round(start.Y + ((target.Y - start.Y) * eased) - (Math.Sin(progress * Math.PI) * 34.0)));
            float scale = ScaleAt(progress);
            int width = Math.Max(8, (int)Math.Round(226 * scale));
            int height = Math.Max(5, (int)Math.Round(74 * scale));
            Rectangle bounds = new Rectangle(center.X - width / 2 - 14, center.Y - height / 2 - 14, width + 28, height + 28);
            Location = bounds.Location;
            Size = bounds.Size;
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                int alpha = (int)Math.Round(255 * (progress < 0.68f ? 1f : Math.Max(0f, (1f - progress) / 0.32f)));
                RectangleF card = new RectangleF(14, 14, width, height);
                using (GraphicsPath cardPath = RoundedRectangle(card, Math.Max(3f, 15f * scale)))
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(alpha / 5, 0, 0, 0)))
                using (SolidBrush background = new SolidBrush(Color.FromArgb(alpha, 250, 252, 249)))
                using (Pen border = new Pen(Color.FromArgb(alpha, 86, 174, 126), Math.Max(1f, 1.5f * scale)))
                {
                    graphics.TranslateTransform(2f, 3f);
                    graphics.FillPath(shadow, cardPath);
                    graphics.ResetTransform();
                    graphics.FillPath(background, cardPath);
                    graphics.DrawPath(border, cardPath);
                }
                if (scale > 0.22f)
                {
                    DrawItemIcon(graphics, new RectangleF(26 * scale, 25 * scale, 38 * scale, 36 * scale), alpha, folder);
                    using (Font font = UiText.Font(Math.Max(5.5f, 9.2f * scale), FontStyle.Bold))
                    using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(alpha, 38, 54, 44)))
                    {
                        string text = count > 1 ? label + "  等 " + count.ToString() + " 项" : label;
                        Rectangle textRect = new Rectangle((int)(70 * scale), (int)(23 * scale), Math.Max(1, width - (int)(78 * scale)), Math.Max(1, height - (int)(25 * scale)));
                        TextRenderer.DrawText(graphics, Compact(text, 24), font, textRect, textBrush.Color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                    }
                }
                Push(bitmap, bounds.Location);
            }
        }

        private static void DrawItemIcon(Graphics graphics, RectangleF bounds, int alpha, bool isFolder)
        {
            Color fill = isFolder ? Color.FromArgb(alpha, 255, 192, 67) : Color.FromArgb(alpha, 255, 255, 255);
            Color line = isFolder ? Color.FromArgb(alpha, 210, 139, 28) : Color.FromArgb(alpha, 62, 151, 103);
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(line, Math.Max(1f, bounds.Width / 16f)))
            {
                if (isFolder)
                {
                    RectangleF tab = new RectangleF(bounds.Left + 2, bounds.Top + 2, bounds.Width * 0.48f, bounds.Height * 0.28f);
                    graphics.FillRectangle(brush, tab);
                    graphics.FillRectangle(brush, new RectangleF(bounds.Left, bounds.Top + bounds.Height * 0.18f, bounds.Width, bounds.Height * 0.76f));
                    graphics.DrawRectangle(pen, bounds.Left, bounds.Top + bounds.Height * 0.18f, bounds.Width, bounds.Height * 0.76f);
                }
                else
                {
                    graphics.FillRectangle(brush, bounds);
                    graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    graphics.DrawLine(pen, bounds.Left + bounds.Width * 0.22f, bounds.Top + bounds.Height * 0.42f, bounds.Right - bounds.Width * 0.18f, bounds.Top + bounds.Height * 0.42f);
                    graphics.DrawLine(pen, bounds.Left + bounds.Width * 0.22f, bounds.Top + bounds.Height * 0.64f, bounds.Right - bounds.Width * 0.28f, bounds.Top + bounds.Height * 0.64f);
                }
            }
        }

        private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = Math.Min(Math.Min(bounds.Width, bounds.Height), radius * 2f);
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static string Compact(string value, int maximum)
        {
            string clean = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return clean.Length <= maximum ? clean : clean.Substring(0, maximum - 1) + "…";
        }

        private static float Ease(float value)
        {
            value = Math.Max(0f, Math.Min(1f, value));
            return 1f - ((1f - value) * (1f - value) * (1f - value));
        }

        private static float ScaleAt(float progress)
        {
            progress = Math.Max(0f, Math.Min(1f, progress));
            // The card is actually swallowed: there is no residual seven-percent
            // thumbnail left hovering over the mouth at completion.
            return 1f - Ease(progress);
        }

        internal static bool ValidateProgression()
        {
            return EatingDurationMs >= 1200 && ScaleAt(0f) > ScaleAt(0.45f) && ScaleAt(0.45f) > ScaleAt(0.9f) && ScaleAt(1f) == 0f;
        }

        internal bool LayeredRenderSucceeded { get { return layeredRenderSucceeded; } }

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
            if (disposing) { timer.Stop(); timer.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
