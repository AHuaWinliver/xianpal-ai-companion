// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace XianPal
{
    // One wall-clock timeline is shared by the desktop overlay and offline previews.
    // Effects are always drawn BEFORE the approved, unmodified character bitmap.
    internal static partial class ReminderPerformance
    {
        internal static void Render(Graphics graphics, Size size, string style, IDictionary<string, List<Image>> frames,
                                    double seconds, bool reducedMotion, Rectangle cardExclusion)
        {
            GraphicsState original = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                if (!cardExclusion.IsEmpty) graphics.ExcludeClip(cardExclusion);
                Pose pose = Sample(size, style, seconds, reducedMotion);
                if (!reducedMotion && Supports(style) && seconds > EntranceEnd && seconds < EndAt)
                {
                    if (style == "command") DrawSwordFlight(graphics, size, pose, seconds);
                    else
                    {
                    GraphicsState effect = graphics.Save();
                    // All scenery is defined on a reference viewport, then uniformly fitted.
                    float scale = Math.Min(size.Width / 960f, size.Height / 540f);
                    float sole = pose.Bounds.Bottom - pose.Bounds.Height * .04f;
                    graphics.TranslateTransform(size.Width / 2f, sole - 52 * scale);
                    graphics.ScaleTransform(scale, scale);
                    float alpha = Window(seconds, EntranceEnd, EndAt, .64);
                    double local = seconds - EntranceEnd;
                    if (style == "beam") Talismans(graphics, local, alpha);
                    else if (style == "warning") HerbGarden(graphics, local, alpha);
                    else InkRipples(graphics, local, alpha);
                    graphics.Restore(effect);
                    }
                }
                List<Image> sequence;
                if (frames != null && (frames.TryGetValue(pose.Animation, out sequence) || frames.TryGetValue("idle", out sequence)) && sequence.Count > 0)
                {
                    GraphicsState character = graphics.Save();
                    graphics.TranslateTransform(pose.Bounds.X + pose.Bounds.Width / 2, pose.Bounds.Bottom);
                    graphics.RotateTransform(pose.Rotation);
                    if (pose.FlipX) graphics.ScaleTransform(-1, 1);
                    graphics.DrawImage(sequence[FrameIndex(pose.Cycle, sequence.Count)], -pose.Bounds.Width / 2, -pose.Bounds.Height, pose.Bounds.Width, pose.Bounds.Height);
                    graphics.Restore(character);
                }
            }
            finally { graphics.Restore(original); }
        }

        private static PointF Polar(float radius, double angle, float flatten)
        {
            return new PointF((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius * flatten);
        }

        private static Color Tint(Color color, float opacity)
        {
            return Color.FromArgb((int)(255 * Math.Max(0, Math.Min(1, opacity))), color);
        }

        private static void Light(Graphics g, PointF[] points, Color color, float opacity, float width)
        {
            if (points.Length < 2 || opacity < .003f) return;
            for (int layer = 2; layer >= 0; layer--)
            using (Pen pen = new Pen(Tint(color, opacity * (layer == 0 ? .75f : layer == 1 ? .16f : .05f)), width * (1 + layer * 2.5f)))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, points);
            }
        }

        private static void Star(Graphics g, PointF p, float radius, Color color, float alpha)
        {
            PointF[] diamond = { new PointF(p.X, p.Y - radius), new PointF(p.X + radius * .3f, p.Y), new PointF(p.X, p.Y + radius), new PointF(p.X - radius * .3f, p.Y) };
            using (SolidBrush brush = new SolidBrush(Tint(color, alpha))) g.FillPolygon(brush, diamond);
        }

        private static void Ring(Graphics g, float x, float y, float radius, float flatten, Color color, float alpha, float start)
        {
            PointF[] line = new PointF[121];
            for (int i = 0; i < line.Length; i++)
            {
                PointF p = Polar(radius, start + i * Math.PI * 2 / 120, flatten);
                line[i] = new PointF(x + p.X, y + p.Y);
            }
            Light(g, line, color, alpha, 1.1f);
        }

        internal static PointF SwordContact(Pose pose)
        {
            // Reviewed original review/waving soles end at y=395 on the 416px canvas.
            float deck = -pose.Bounds.Height * 21 / 416f;
            double radians = pose.Rotation * Math.PI / 180;
            return new PointF(pose.Bounds.X + pose.Bounds.Width / 2 - deck * (float)Math.Sin(radians),
                              pose.Bounds.Bottom + deck * (float)Math.Cos(radians));
        }

        private static void DrawSwordFlight(Graphics g, Size size, Pose pose, double seconds)
        {
            Color cyan = Accent("command"), gold = Color.FromArgb(228, 194, 120);
            float swordAlpha = Window(seconds, 1.28, 5.60, .30);
            float flightAlpha = Window(seconds, FlightStart, 5.48, .32);
            DrawSwordWake(g, size, seconds, flightAlpha, cyan, gold);

            // The summoning seal stays on the ground; it does not follow the airborne pet.
            float seal = Window(seconds, 1.18, 2.42, .36);
            if (seal > .001f)
            {
                PointF ground = SwordContact(Sample(size, "command", 0, true));
                float r = pose.Bounds.Height * .65f * Smooth((seconds - 1.18) / .35);
                Ring(g, ground.X, ground.Y + 5, r, .20f, cyan, seal * .6f, 0);
                Ring(g, ground.X, ground.Y + 5, r * .82f, .20f, gold, seal * .40f, 0);
            }

            // Thin drifting cloud ribbons provide a spacious flight scene, never an opaque sky.
            float unit = Math.Min(size.Width / 960f, size.Height / 540f);
            for (int cloud = 0; cloud < 4; cloud++)
            {
                PointF[] ribbon = new PointF[45];
                float x = size.Width * (cloud % 2 == 0 ? .18f : .82f);
                float y = size.Height * (cloud < 2 ? .32f : .53f);
                for (int i = 0; i < ribbon.Length; i++)
                {
                    float u = i / 44f;
                    ribbon[i] = new PointF(x + (u - .5f) * 170 * unit,
                        y + 9 * unit * (float)Math.Sin(u * Math.PI * 2 + cloud + seconds * .4));
                }
                Light(g, ribbon, cloud % 2 == 0 ? cyan : gold, flightAlpha * .22f, unit);
            }

            // Blade and rider share this EXACT foot-pivot transform, including turns.
            GraphicsState sword = g.Save();
            g.TranslateTransform(pose.Bounds.X + pose.Bounds.Width / 2, pose.Bounds.Bottom);
            g.RotateTransform(pose.Rotation);
            if (pose.FlipX) g.ScaleTransform(-1, 1);
            float h = pose.Bounds.Height, deck = -h * 21 / 416f;
            PointF[] blade = {
                new PointF(-h * .48f, deck), new PointF(h * .73f, deck),
                new PointF(h * .92f, deck + h * .018f), new PointF(h * .73f, deck + h * .044f),
                new PointF(-h * .48f, deck + h * .044f)
            };
            using (LinearGradientBrush metal = new LinearGradientBrush(new PointF(0, deck), new PointF(0, deck + h * .044f),
                Tint(Color.FromArgb(236, 251, 249), swordAlpha * .94f), Tint(cyan, swordAlpha * .78f)))
            using (Pen edge = new Pen(Tint(Color.FromArgb(59, 132, 148), swordAlpha * .85f), Math.Max(1, h * .005f)))
            {
                g.FillPolygon(metal, blade); g.DrawPolygon(edge, blade);
            }
            Light(g, new[] { new PointF(-h * .46f, deck + h * .016f), new PointF(h * .86f, deck + h * .018f) },
                cyan, swordAlpha * .8f, h * .009f);
            using (Pen grip = new Pen(Tint(Color.FromArgb(56, 93, 105), swordAlpha), h * .026f))
                g.DrawLine(grip, -h * .74f, deck + h * .02f, -h * .51f, deck + h * .02f);
            PointF[] guard = { new PointF(-h * .51f, deck - h * .05f), new PointF(-h * .47f, deck + h * .018f),
                new PointF(-h * .51f, deck + h * .09f), new PointF(-h * .55f, deck + h * .018f) };
            using (SolidBrush gilding = new SolidBrush(Tint(gold, swordAlpha))) g.FillPolygon(gilding, guard);
            Star(g, new PointF(-h * .76f, deck + h * .02f), h * .035f, gold, swordAlpha);
            g.Restore(sword);
        }

        private static void DrawSwordWake(Graphics g, Size size, double seconds, float alpha, Color cyan, Color gold)
        {
            if (alpha <= .001f || seconds < FlightStart) return;
            const int count = 56;
            PointF[] points = new PointF[count];
            double head = Math.Min(seconds, FlightEnd), tail = Math.Max(FlightStart, head - .90);
            for (int i = 0; i < count; i++)
                points[i] = SwordContact(Sample(size, "command", tail + (head - tail) * i / (count - 1), false));
            float dx = points[count - 1].X - points[0].X, dy = points[count - 1].Y - points[0].Y;
            if (dx * dx + dy * dy < 1) return;
            float unit = Sample(size, "command", seconds, true).Bounds.Height / 216f;
            for (int layer = 2; layer >= 0; layer--)
            {
                PointF[] ribbon = new PointF[count * 2];
                for (int i = 0; i < count; i++)
                {
                    PointF a = points[Math.Max(0, i - 1)], b = points[Math.Min(count - 1, i + 1)];
                    float tx = b.X - a.X, ty = b.Y - a.Y;
                    float length = Math.Max(.01f, (float)Math.Sqrt(tx * tx + ty * ty));
                    float width = (1 + 9 * Smooth(i / (double)(count - 1))) * unit * (1 + layer * 1.5f);
                    ribbon[i] = new PointF(points[i].X - ty / length * width, points[i].Y + tx / length * width);
                    ribbon[count * 2 - 1 - i] = new PointF(points[i].X + ty / length * width, points[i].Y - tx / length * width);
                }
                using (LinearGradientBrush brush = new LinearGradientBrush(points[0], points[count - 1],
                    Tint(cyan, 0), Tint(cyan, alpha * (layer == 0 ? .30f : layer == 1 ? .08f : .025f))))
                    g.FillPolygon(brush, ribbon);
            }
            for (int lane = 0; lane < 3; lane++)
            {
                PointF[] line = new PointF[count];
                for (int i = 0; i < count; i++) line[i] = new PointF(points[i].X, points[i].Y + (lane - 1) * 3 * unit);
                Light(g, line, lane == 1 ? gold : cyan, alpha * .42f, 1.1f * unit);
            }
            for (int i = 5; i < count; i += 9)
                Star(g, new PointF(points[i].X, points[i].Y + (float)Math.Sin(i + seconds * 2) * 12 * unit),
                    2.5f * unit, gold, alpha * .6f * i / count);
        }

        private static void Talismans(Graphics g, double t, float alpha)
        {
            Color pink = Accent("beam"), gold = Color.FromArgb(226, 184, 109);
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4 + t * .27;
                float spread = Smooth((t - i * .045) / .8);
                PointF position = Polar((185 + 20 * (float)Math.Sin(t + i)) * spread, angle, .70f);
                position.Y -= 70;
                GraphicsState paper = g.Save();
                g.TranslateTransform(position.X, position.Y);
                g.RotateTransform((float)Math.Sin(angle) * 16);
                using (SolidBrush fill = new SolidBrush(Tint(Color.FromArgb(255, 237, 201), alpha * .74f)))
                using (Pen border = new Pen(Tint(gold, alpha * .8f), 1))
                {
                    RectangleF page = new RectangleF(-14, -24, 28, 48);
                    g.FillRectangle(fill, page); g.DrawRectangle(border, page.X, page.Y, page.Width, page.Height);
                }
                float write = Smooth((t - .5 - i * .07) / .55);
                PointF[] rune = { new PointF(-6, -16), new PointF(6, -16), new PointF(0, -10), new PointF(0, 12 * write), new PointF(-7, 6), new PointF(5, 6), new PointF(0, 16) };
                Light(g, rune, pink, alpha * write, 1.4f);
                g.Restore(paper);
                Star(g, new PointF(position.X + 23, position.Y - 15), 4, gold, alpha * (.5f + .3f * (float)Math.Sin(t * 2 + i)));
            }
            PointF[] ribbon = new PointF[100];
            for (int i = 0; i < ribbon.Length; i++)
            {
                double u = i / 99.0;
                ribbon[i] = new PointF((float)(-420 + 840 * u), (float)(-35 + 52 * Math.Sin(u * 8 + t * 1.7)));
            }
            Light(g, ribbon, pink, alpha * .32f, 1.6f);
            Wisps(g, t, alpha, pink, gold, 16);
        }

        private static void Leaf(Graphics g, float x, float y, float angle, float size, Color color, float alpha)
        {
            GraphicsState saved = g.Save();
            g.TranslateTransform(x, y); g.RotateTransform(angle);
            using (GraphicsPath leaf = new GraphicsPath())
            using (SolidBrush fill = new SolidBrush(Tint(color, alpha * .60f)))
            using (Pen vein = new Pen(Tint(Color.FromArgb(228, 237, 181), alpha * .8f), .8f))
            {
                leaf.AddBezier(0, 0, -size * .4f, -size * .65f, size * .2f, -size, 0, -size * 1.4f);
                leaf.AddBezier(0, -size * 1.4f, size, -size * .8f, size * .6f, -size * .1f, 0, 0);
                g.FillPath(fill, leaf); g.DrawLine(vein, 0, 0, size * .12f, -size * 1.1f);
            }
            g.Restore(saved);
        }

        private static void HerbGarden(Graphics g, double t, float alpha)
        {
            Color green = Accent("warning"), gold = Color.FromArgb(218, 215, 145);
            for (int side = -1; side <= 1; side += 2)
            {
                PointF[] vine = new PointF[61];
                float growth = Smooth(t / 1.6);
                for (int i = 0; i < vine.Length; i++)
                {
                    float u = i / 60f * growth;
                    vine[i] = new PointF(side * (100 + 190 * u + 25 * (float)Math.Sin(u * 6)), 75 - 260 * u);
                }
                Light(g, vine, green, alpha * .65f, 1.8f);
                for (int i = 1; i <= 7; i++)
                {
                    float u = i / 8f;
                    float born = Smooth((growth - u) * 7);
                    float x = side * (100 + 190 * u + 25 * (float)Math.Sin(u * 6)), y = 75 - 260 * u;
                    Leaf(g, x, y, side * (32 + 8 * (float)Math.Sin(t * 1.4 + i)), 24 * born, green, alpha * born);
                    Leaf(g, x, y, -side * 52, 18 * born, gold, alpha * .6f * born);
                }
            }
            Ring(g, 0, 65, 240 * Smooth(t), .20f, green, alpha * .48f, 0);
            for (int i = 0; i < 14; i++)
            {
                float u = (float)((t * .10 + i / 14.0) % 1);
                Leaf(g, -400 + i * 62 + 12 * (float)Math.Sin(t + i), -230 + u * 330,
                     i * 37 + (float)t * 19, 7 + i % 4, green, alpha * (float)Math.Sin(Math.PI * u) * .65f);
            }
            Wisps(g, t * .6, alpha, green, gold, 16);
        }

        private static void InkRipples(Graphics g, double t, float alpha)
        {
            Color gold = Accent("stone"), ink = Color.FromArgb(78, 113, 130);
            for (int i = 0; i < 4; i++)
            {
                float age = (float)(t - .80 - i * .35);
                if (age <= 0) continue;
                float p = Math.Min(1, age / 3.3f);
                float fade = alpha * (1 - Smooth(p)) * Smooth(age / .3);
                Ring(g, 0, 67, 32 + 470 * Smooth(p), .31f, ink, fade * .70f, 0);
                Ring(g, 0, 67, 40 + 470 * Smooth(p), .31f, gold, fade * .65f, 0);
            }
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4 + t * .14;
                PointF center = Polar(185 + 30 * Smooth(t / 1.2), a, .55f);
                center.Y -= 25;
                PointF[] cloud = new PointF[25];
                for (int k = 0; k < cloud.Length; k++)
                {
                    double turn = k / 24.0 * Math.PI * 2.5;
                    float r = 2 + k * .65f;
                    cloud[k] = new PointF(center.X + (float)Math.Cos(turn) * r, center.Y + (float)Math.Sin(turn) * r * .65f);
                }
                Light(g, cloud, gold, alpha * .65f, 1.1f);
            }
            Wisps(g, t * .7, alpha, ink, gold, 12);
        }

        private static void Wisps(Graphics g, double t, float alpha, Color primary, Color secondary, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float u = (float)((t * .14 + i * .6180339) % 1);
                float x = -420 + (i * 137 % 840), y = 95 - u * 310;
                x += 20 * (float)Math.Sin(t + i);
                Star(g, new PointF(x, y), 2 + i % 3, i % 3 == 0 ? secondary : primary,
                     alpha * (float)Math.Sin(Math.PI * u) * .65f);
            }
        }
    }
}
