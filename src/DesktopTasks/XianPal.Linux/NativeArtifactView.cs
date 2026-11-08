// SPDX-License-Identifier: Apache-2.0
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace XianPal.Linux;

// Uses the same 384 x 416 coordinates and actual opaque sole anchor as Windows.
internal sealed class NativeArtifactView : Control
{
    private Bitmap rider;
    private PetCharacter character;
    private double foot, envelope, seconds;
    private bool left;

    internal static int FootRow(Bitmap image)
    {
        int width = image.PixelSize.Width, height = image.PixelSize.Height, stride = width * 4;
        IntPtr buffer = Marshal.AllocHGlobal(stride * height);
        try
        {
            image.CopyPixels(new PixelRect(0, 0, width, height), buffer, stride * height, stride);
            for (int y = height - 1; y >= 0; y--)
                for (int x = 0; x < width; x += 3)
                    if (Marshal.ReadByte(buffer, y * stride + x * 4 + 3) > 200) return y;
            throw new InvalidDataException("Character artwork has no opaque sole anchor.");
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    internal void Update(Bitmap image, PetCharacter profile, double amount, double time, bool facingLeft)
    {
        if (!ReferenceEquals(rider, image)) { foot = FootRow(image) * 416.0 / image.PixelSize.Height; rider = image; }
        character = profile; envelope = Math.Clamp(amount, 0, 1); seconds = time; left = facingLeft;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (rider == null || character == null) return;
        double fit = Math.Min(Bounds.Width / 384, Bounds.Height / 416);
        using var canvas = context.PushTransform(Matrix.CreateScale(fit, fit) * Matrix.CreateTranslation((Bounds.Width - 384 * fit) / 2, (Bounds.Height - 416 * fit) / 2));
        bool mirror = character.Id == "pippin" ? !left : left;
        using var facing = context.PushTransform(mirror ? Matrix.CreateScale(-1, 1) * Matrix.CreateTranslation(384, 0) : Matrix.Identity);
        double scale = 1 - .16 * envelope, y = (-3 + Math.Sin(seconds * 2.5) * 1.5) * envelope;
        double deck = y + foot * scale;
        if (envelope > .001)
        {
            using var platform = context.PushTransform(Matrix.CreateScale(1, Math.Max(.01, Math.Min(1, (416 - deck - 3) / 48))) * Matrix.CreateTranslation(0, deck));
            DrawVessel(context, character.ReminderStyle, envelope, seconds);
        }
        context.DrawImage(rider, new Rect(0, 0, rider.Size.Width, rider.Size.Height), new Rect((384 - 384 * scale) / 2, y, 384 * scale, 416 * scale));
    }

    private static IBrush Brush(string color, double opacity) => new SolidColorBrush(Color.Parse(color), opacity);
    private static void Line(DrawingContext context, IBrush color, double width, double x1, double y1, double x2, double y2) => context.DrawLine(new Pen(color, width, lineCap: PenLineCap.Round), new Point(x1, y1), new Point(x2, y2));
    private static StreamGeometry Geometry(params Point[] points)
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open(); path.BeginFigure(points[0], true);
        foreach (var point in points.Skip(1)) path.LineTo(point); path.EndFigure(true);
        return geometry;
    }

    internal static void DrawVessel(DrawingContext context, string style, double opacity, double seconds)
    {
        string accent = style == "beam" ? "#F3A76D" : style == "warning" ? "#7AD0A8" : style == "stone" ? "#9AADC6" : "#72D4E4";
        var gold = Brush("#EAC272", opacity); var rim = new Pen(Brush(accent, opacity * .9), 1.8);
        context.DrawEllipse(Brush(accent, opacity * .12), null, new Rect(28, -6, 328, 38));
        if (style == "command")
        {
            context.DrawGeometry(Brush("#CAEDEB", opacity), rim, Geometry(new Point(83, 0), new Point(323, 0), new Point(362, 7), new Point(323, 15), new Point(83, 15)));
            Line(context, gold, 1.7, 84, 7, 342, 7); Line(context, Brush("#395363", opacity), 11, 35, 7, 79, 7); Line(context, gold, 1.7, 79, -9, 79, 23);
            for (int i = 0; i < 4; i++) Line(context, gold, 1.7, 40 + i * 8, 3, 43 + i * 8, 11);
        }
        else if (style == "beam")
        {
            context.DrawRectangle(Brush("#FFE0A5", opacity), new Pen(gold, 1.7), new Rect(57, 0, 270, 24));
            context.DrawEllipse(Brush("#AD5B40", opacity), null, new Rect(47, -4, 18, 32)); context.DrawEllipse(Brush("#AD5B40", opacity), null, new Rect(319, -4, 18, 32));
            for (int i = 0; i < 4; i++) { double x = 103 + i * 49; Line(context, Brush("#B84240", opacity), 2, x, 6, x + 10, 18); Line(context, Brush("#B84240", opacity), 2, x + 10, 7, x, 18); }
        }
        else if (style == "warning")
        {
            var leaf = new StreamGeometry();
            using (var path = leaf.Open())
            {
                path.BeginFigure(new Point(35, 12), true); path.CubicBezierTo(new Point(112, 48), new Point(267, 46), new Point(349, -3));
                path.CubicBezierTo(new Point(263, 7), new Point(142, -7), new Point(35, 12)); path.EndFigure(true);
            }
            context.DrawGeometry(Brush("#68AE83", opacity), rim, leaf); Line(context, gold, 1.7, 50, 13, 336, 2);
            for (int i = 0; i < 5; i++) Line(context, gold, 1.5, 100 + i * 38, 17, 113 + i * 38, 27);
        }
        else
        {
            context.DrawEllipse(Brush("#344E63", opacity), new Pen(gold, 1.7), new Rect(43, -2, 298, 31));
            context.DrawEllipse(null, rim, new Rect(62, 6, 260, 20));
            for (int i = 0; i < 6; i++) context.DrawEllipse(null, new Pen(gold, 1.2), new Rect(69 + i * 36, 8, 27, 12));
        }
        for (int i = 0; i < 2; i++)
            Line(context, Brush(accent, opacity * (.34 - i * .1)), 2 - i * .5, 38, 26 + i * 8, 161, 26 + i * 8 + Math.Sin(seconds * 3 + i) * 3);
    }
}
