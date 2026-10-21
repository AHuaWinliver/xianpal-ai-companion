using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace XianPal
{
    internal static class AppIcon
    {
        internal static readonly Color Background = Color.FromArgb(255, 250, 237, 191);
        private static Icon windowIcon;

        internal static Icon WindowIcon
        {
            get
            {
                if (windowIcon == null)
                {
                    string version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OrbitPet-" + version + ".ico");
                    if (File.Exists(path)) windowIcon = new Icon(path, new Size(32, 32));
                    else windowIcon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                }
                return windowIcon;
            }
        }

        // Composite onto an opaque badge, never resize the transparent sprite
        // directly. The desktop sprite itself remains completely unchanged.
        internal static Bitmap CreateBitmap(Image character, int size)
        {
            if (character == null) throw new ArgumentNullException("character");
            if (size < 16 || size > 256) throw new ArgumentOutOfRangeException("size");
            Rectangle crop = GetPortraitBounds(character);
            int largeSize = size * 4;
            using (Bitmap large = new Bitmap(largeSize, largeSize, PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(large))
                {
                    graphics.Clear(Background);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    float margin = largeSize * 0.07f;
                    float scale = Math.Min((largeSize - 2 * margin) / crop.Width, (largeSize - 2 * margin) / crop.Height);
                    RectangleF destination = new RectangleF((largeSize - crop.Width * scale) / 2f, (largeSize - crop.Height * scale) / 2f, crop.Width * scale, crop.Height * scale);
                    using (ImageAttributes attributes = new ImageAttributes())
                    {
                        attributes.SetWrapMode(WrapMode.TileFlipXY);
                        graphics.DrawImage(character, Rectangle.Round(destination), crop.X, crop.Y, crop.Width, crop.Height, GraphicsUnit.Pixel, attributes);
                    }
                    using (Pen border = new Pen(Color.FromArgb(28, 89, 63), largeSize * 0.035f))
                    {
                        float inset = border.Width / 2f;
                        graphics.DrawRectangle(border, inset, inset, largeSize - border.Width, largeSize - border.Width);
                    }
                    // Claude Code is represented by a warm-orange terminal
                    // badge. A small linked-ring connector binds it to the pet.
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    float badgeSize = largeSize * 0.34f;
                    float badgeLeft = largeSize - badgeSize - largeSize * 0.045f;
                    float badgeTop = largeSize - badgeSize - largeSize * 0.045f;
                    using (SolidBrush badge = new SolidBrush(Color.FromArgb(190, 82, 47)))
                        graphics.FillRectangle(badge, badgeLeft, badgeTop, badgeSize, badgeSize);
                    using (Pen terminal = new Pen(Color.FromArgb(255, 253, 244), largeSize * 0.035f))
                    {
                        terminal.StartCap = terminal.EndCap = LineCap.Round;
                        float x = badgeLeft + badgeSize * 0.19f, y = badgeTop + badgeSize * 0.28f;
                        graphics.DrawLines(terminal, new[] { new PointF(x, y), new PointF(x + badgeSize * 0.23f, y + badgeSize * 0.21f), new PointF(x, y + badgeSize * 0.42f) });
                        graphics.DrawLine(terminal, badgeLeft + badgeSize * 0.55f, badgeTop + badgeSize * 0.70f, badgeLeft + badgeSize * 0.81f, badgeTop + badgeSize * 0.70f);
                    }
                    if (size >= 24)
                    {
                        float ring = largeSize * 0.105f, linkX = badgeLeft - ring * 2.35f, linkY = badgeTop + badgeSize * 0.69f;
                        using (Pen outline = new Pen(Background, largeSize * 0.052f))
                        using (Pen link = new Pen(Color.FromArgb(28, 89, 63), largeSize * 0.025f))
                        {
                            graphics.DrawEllipse(outline, linkX, linkY, ring * 1.5f, ring);
                            graphics.DrawEllipse(outline, linkX + ring * 0.95f, linkY, ring * 1.5f, ring);
                            graphics.DrawEllipse(link, linkX, linkY, ring * 1.5f, ring);
                            graphics.DrawEllipse(link, linkX + ring * 0.95f, linkY, ring * 1.5f, ring);
                        }
                    }
                }
                Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(result))
                {
                    graphics.Clear(Background);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(large, new Rectangle(0, 0, size, size));
                }
                return result;
            }
        }

        private static Rectangle GetPortraitBounds(Image character)
        {
            using (Bitmap bitmap = new Bitmap(character))
            {
                int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
                for (int y = 0; y < bitmap.Height; y++)
                    for (int x = 0; x < bitmap.Width; x++)
                        if (bitmap.GetPixel(x, y).A > 32)
                        { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
                if (right < left) return new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                // Head and upper body are identifiable even at 16 px. Keep all
                // horizontal features and avoid stretching character proportions.
                return new Rectangle(left, top, right - left + 1, Math.Max(1, (int)((bottom - top + 1) * 0.65f)));
            }
        }
    }
}
