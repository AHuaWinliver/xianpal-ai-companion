// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;

namespace XianPal
{
    internal static class CompanionRoutes
    {
        internal static List<Point> GeneratePlayfulPath(Rectangle area, Size petSize, Point origin, int seed)
        {
            List<Point> result = new List<Point>();
            Random generator = new Random(seed);
            int margin = 14;
            int left = area.Left + margin, top = area.Top + margin;
            int right = Math.Max(left, area.Right - petSize.Width - margin);
            int bottom = Math.Max(top, area.Bottom - petSize.Height - margin);
            int width = Math.Max(1, right - left), height = Math.Max(1, bottom - top);
            int cx = left + width / 2, cy = top + height / 2;
            int pattern = Math.Abs(seed % 4);
            if (pattern == 0)
            {
                for (int i = 0; i < 24; i++)
                {
                    double angle = (Math.PI * 2.0 * i) / 23.0;
                    result.Add(new Point(cx + (int)(Math.Sin(angle) * width * 0.43), cy + (int)(Math.Sin(angle * 2.0) * height * 0.38)));
                }
            }
            else if (pattern == 1)
            {
                for (int i = 0; i < 26; i++)
                {
                    double ratio = i / 25.0, angle = ratio * Math.PI * 4.5;
                    result.Add(new Point(cx + (int)(Math.Cos(angle) * width * 0.44 * ratio), cy + (int)(Math.Sin(angle) * height * 0.42 * ratio)));
                }
            }
            else if (pattern == 2)
            {
                for (int i = 0; i < 14; i++)
                {
                    int x = (i % 2 == 0) ? left + generator.Next(0, Math.Max(1, width / 5)) : right - generator.Next(0, Math.Max(1, width / 5));
                    int y = top + (int)Math.Round(height * (i / 13.0));
                    result.Add(new Point(x, y));
                }
            }
            else
            {
                const int starPoints = 20;
                for (int i = 0; i < starPoints; i++)
                {
                    double angle = (-Math.PI / 2.0) + (i * Math.PI * 4.0 / starPoints);
                    double radius = i % 2 == 0 ? 0.46 : 0.20;
                    result.Add(new Point(cx + (int)(Math.Cos(angle) * width * radius), cy + (int)(Math.Sin(angle) * height * radius)));
                }
            }
            Point safeOrigin = new Point(Math.Max(left, Math.Min(origin.X, right)), Math.Max(top, Math.Min(origin.Y, bottom)));
            result.Add(safeOrigin);
            return result;
        }

        internal static List<Point> GenerateAiPath(Rectangle area, Size petSize, Point origin, string curve, PointF[] normalizedControls)
        {
            List<Point> result = new List<Point>();
            if (normalizedControls == null || normalizedControls.Length < 4) return result;
            int margin = 14;
            int left = area.Left + margin, top = area.Top + margin;
            int right = Math.Max(left, area.Right - petSize.Width - margin);
            int bottom = Math.Max(top, area.Bottom - petSize.Height - margin);
            int width = Math.Max(1, right - left), height = Math.Max(1, bottom - top);
            List<PointF> controls = new List<PointF>();
            foreach (PointF normalized in normalizedControls)
            {
                if (Single.IsNaN(normalized.X) || Single.IsInfinity(normalized.X) || Single.IsNaN(normalized.Y) || Single.IsInfinity(normalized.Y)) return result;
                float nx = Math.Max(0.04f, Math.Min(0.96f, normalized.X));
                float ny = Math.Max(0.04f, Math.Min(0.96f, normalized.Y));
                controls.Add(new PointF(left + (nx * width), top + (ny * height)));
            }
            if (String.Equals(curve, "orbit", StringComparison.OrdinalIgnoreCase)) controls.Add(controls[0]);

            for (int segment = 0; segment < controls.Count - 1; segment++)
            {
                PointF p0 = controls[Math.Max(0, segment - 1)];
                PointF p1 = controls[segment];
                PointF p2 = controls[segment + 1];
                PointF p3 = controls[Math.Min(controls.Count - 1, segment + 2)];
                for (int sample = 0; sample < 10; sample++)
                {
                    float t = sample / 10f;
                    float t2 = t * t, t3 = t2 * t;
                    float x = 0.5f * ((2f * p1.X) + ((-p0.X + p2.X) * t) + ((2f * p0.X - 5f * p1.X + 4f * p2.X - p3.X) * t2) + ((-p0.X + 3f * p1.X - 3f * p2.X + p3.X) * t3));
                    float y = 0.5f * ((2f * p1.Y) + ((-p0.Y + p2.Y) * t) + ((2f * p0.Y - 5f * p1.Y + 4f * p2.Y - p3.Y) * t2) + ((-p0.Y + 3f * p1.Y - 3f * p2.Y + p3.Y) * t3));
                    result.Add(new Point(Math.Max(left, Math.Min((int)Math.Round(x), right)), Math.Max(top, Math.Min((int)Math.Round(y), bottom))));
                }
            }
            result.Add(new Point((int)Math.Round(controls[controls.Count - 1].X), (int)Math.Round(controls[controls.Count - 1].Y)));

            int minX = Int32.MaxValue, maxX = Int32.MinValue, minY = Int32.MaxValue, maxY = Int32.MinValue;
            foreach (Point point in result) { minX = Math.Min(minX, point.X); maxX = Math.Max(maxX, point.X); minY = Math.Min(minY, point.Y); maxY = Math.Max(maxY, point.Y); }
            if (maxX - minX < width * 0.55 || maxY - minY < height * 0.40) { result.Clear(); return result; }
            result.Add(new Point(Math.Max(left, Math.Min(origin.X, right)), Math.Max(top, Math.Min(origin.Y, bottom))));
            return result;
        }

    }
}
