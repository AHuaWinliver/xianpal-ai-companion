// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace XianPal.Linux;

internal sealed class NativeTrailWindow : Window
{
    private readonly TrailCanvas canvas = new();
    private readonly DispatcherTimer timer;
    private bool available, attempted;
    internal bool ClickThrough { get; private set; }

    internal NativeTrailWindow(PetWindow pet, PixelRect area)
    {
        Title = "XianPal · Motion trail";
        Position = area.Position; Width = area.Width / pet.RenderScaling; Height = area.Height / pet.RenderScaling;
        Topmost = true; ShowActivated = false; ShowInTaskbar = false; CanResize = false; IsHitTestVisible = false;
        DesktopUi.RemoveDecorations(this);
        Background = Brushes.Transparent; TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        TransparencyBackgroundFallback = Brushes.Transparent;
        Content = canvas;
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(32), DispatcherPriority.Background, (_, _) =>
        {
            canvas.Expire(); canvas.InvalidateVisual();
            if (canvas.Empty) { timer.Stop(); Hide(); }
        });
        Opened += (_, _) =>
        {
            ClickThrough = X11Overlay.MakeClickThrough(this, pet);
            attempted = true;
            available = ClickThrough && ActualTransparencyLevel != WindowTransparencyLevel.None && X11Overlay.HasCompositor();
            if (!available) Hide();
        };
        Closed += (_, _) => timer.Stop();
    }

    internal void AddPoint(Point point, double size, double opacity, System.Drawing.Color[] colors)
    {
        if (attempted && !available) return;
        canvas.Add(point, size, opacity, colors);
        if (!IsVisible) Show();
        if (available) timer.Start();
        else canvas.Clear();
    }

    internal void Clear() { canvas.Clear(); timer.Stop(); Hide(); }

    internal sealed class TrailCanvas : Control
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly List<(Point Position, long Time)> points = new();
        private Color[] colors = { Color.Parse("#72D4E4"), Color.Parse("#EAC272"), Color.Parse("#DE94C6"), Color.Parse("#7DD4AA") };
        private double width = 16, opacity = .45;
        internal bool Empty => points.Count < 2;
        internal void Clear() { points.Clear(); InvalidateVisual(); }
        internal void Add(Point point, double size, double alpha, System.Drawing.Color[] palette)
        {
            Expire(); width = Math.Clamp(size * .12, 5, 34); opacity = Math.Clamp(alpha, 0, 1);
            if (palette != null && palette.Length >= 3) colors = palette.Select(color => Color.FromRgb(color.R, color.G, color.B)).ToArray();
            if (points.Count > 0 && ((Vector)(points[^1].Position - point)).Length < 1) return;
            points.Add((point, clock.ElapsedMilliseconds));
            while (points.Count > 150) points.RemoveAt(0);
        }
        internal void Expire() { long cutoff = clock.ElapsedMilliseconds - 1350; points.RemoveAll(point => point.Time < cutoff); }
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (points.Count < 2) return;
            long now = clock.ElapsedMilliseconds;
            for (int index = 1; index < points.Count; index++)
            {
                double age = Math.Clamp(1 - (now - points[index].Time) / 1350.0, 0, 1);
                double taper = Math.Pow(index / (double)(points.Count - 1), .8);
                Point a = points[index - 1].Position, b = points[index].Position;
                Vector direction = b - a;
                if (direction.Length < .1) continue;
                Vector normal = new(-direction.Y / direction.Length, direction.X / direction.Length);
                for (int lane = 0; lane < colors.Length; lane++)
                {
                    double laneOffset = (lane - (colors.Length - 1) / 2.0) * width * .22 * taper;
                    Point p = a + normal * laneOffset, q = b + normal * laneOffset;
                    double thickness = Math.Max(.35, width * .23 * taper), alpha = opacity * age * age * taper;
                    context.DrawLine(new Pen(new SolidColorBrush(colors[lane], alpha * .08), thickness * 6, lineCap: PenLineCap.Round), p, q);
                    context.DrawLine(new Pen(new SolidColorBrush(colors[lane], alpha * .24), thickness * 2.5, lineCap: PenLineCap.Round), p, q);
                    context.DrawLine(new Pen(new SolidColorBrush(colors[lane], alpha * .8), thickness, lineCap: PenLineCap.Round), p, q);
                }
            }
        }
    }
}
