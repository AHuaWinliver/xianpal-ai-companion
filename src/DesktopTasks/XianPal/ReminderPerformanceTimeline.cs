// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;

namespace XianPal
{
    internal static partial class ReminderPerformance
    {
        internal const double EntranceEnd = 1.12;
        internal const double CardAt = 5.60;
        internal const double EndAt = 6.24;
        internal const double GestureSeconds = .96;
        internal const double FlightStart = 1.82;
        internal const double FlightEnd = 5.18;

        internal sealed class Pose
        {
            internal RectangleF Bounds;
            internal float Rotation;
            internal string Animation;
            internal float Cycle;
            internal bool FlipX;
        }

        internal static bool Supports(string style)
        {
            return style == "command" || style == "beam" || style == "warning" || style == "stone";
        }

        internal static string Title(string style)
        {
            switch (style)
            {
                case "command": return "御剑飞仙";
                case "beam": return "灵符报时";
                case "warning": return "草木知时";
                case "stone": return "墨涟唤醒";
                default: return "仙伴提醒";
            }
        }

        internal static string Line(string style)
        {
            switch (style)
            {
                case "command": return "道友，约好的时辰到了。";
                case "beam": return "叮，给你送来一张准时符！";
                case "warning": return "风过草木，该歇一歇啦。";
                case "stone": return "小墨踏着水纹，准时到啦。";
                default: return "约好的时间到了。";
            }
        }

        internal static Color Accent(string style)
        {
            return style == "command" ? Color.FromArgb(80, 190, 211) :
                   style == "beam" ? Color.FromArgb(227, 121, 133) :
                   style == "warning" ? Color.FromArgb(115, 181, 139) : Color.FromArgb(207, 173, 102);
        }

        internal static float Smooth(double value)
        {
            float p = (float)Math.Max(0, Math.Min(1, value));
            return p * p * (3 - 2 * p);
        }

        private static float Window(double t, double start, double end, double fade)
        {
            return Smooth((t - start) / fade) * (1 - Smooth((t - end + fade) / fade));
        }

        internal static int FrameIndex(float cycle, int count)
        {
            if (count <= 1) return 0;
            return Math.Min(count - 1, (int)((cycle - Math.Floor(cycle)) * count));
        }

        internal static Rectangle CardBounds(Size size)
        {
            int width = Math.Max(1, Math.Min(520, size.Width - 24));
            int x = Math.Max(0, (size.Width - width) / 2);
            int y = Math.Max(12, Math.Min(size.Height / 2 + 105, size.Height - 188 - 18));
            return new Rectangle(x, y, width, 188);
        }

        internal static Pose Sample(Size size, string style, double seconds, bool reducedMotion)
        {
            double t = Math.Max(0, seconds);
            float height = Math.Min(312, size.Height * .40f);
            float width = height * 384 / 416;
            float cardTop = CardBounds(size).Top;
            float bottom = Math.Min(size.Height / 2f + 78, cardTop - 16);
            float x = (size.Width - width) / 2, y = bottom - height;
            Pose result = new Pose { Bounds = new RectangleF(x, y, width, height), Animation = "idle", Cycle = 0 };
            if (reducedMotion || t >= CardAt) return result;
            if (t < EntranceEnd)
            {
                float progress = Smooth(t / EntranceEnd);
                float travel = size.Width + 32 - x;
                result.Bounds = new RectangleF(size.Width + 32 - travel * progress, y, width, height);
                result.Animation = "running-left";
                // Travel, not timer ticks, determines footsteps and decelerates them at arrival.
                // A screen-wide dash must not turn into a dozen tiny, strobing
                // walk cycles on large monitors. Stretch its visual stride and
                // cap the entrance at three cycles; still ease it by distance.
                float stride = Math.Max(height * .75f, travel / 3);
                result.Cycle = .25f + travel * progress / stride;
                return result;
            }
            double local = t - EntranceEnd;
            float envelope = Window(t, EntranceEnd, 5.30, .48);
            result.Animation = local < GestureSeconds ? "review" : (local < GestureSeconds * 3 ? "waving" : "review");
            result.Cycle = (float)(local / GestureSeconds);
            if (style == "command")
            {
                float p = Smooth((t - FlightStart) / (FlightEnd - FlightStart));
                double angle = p * Math.PI * 2;
                float reach = Math.Min(size.Width * .27f, Math.Max(0, (size.Width - height * 1.9f) / 2 - 32));
                float lift = Math.Max(0, Math.Min(size.Height * .18f, (y - 28) * .85f));
                result.Bounds.X += reach * (float)Math.Sin(angle);
                result.Bounds.Y -= lift * (float)Math.Pow(Math.Sin(Math.PI * p), 2);
                result.Rotation = 7 * (float)Math.Sin(angle) * (float)Math.Sin(Math.PI * p);
                // Calm planted feet while flying, not a walking sprite pedalling in the air.
                result.Animation = t < FlightStart ? "waving" : "review";
                result.Cycle = t < FlightStart ? (float)(local / (FlightStart - EntranceEnd)) : .12f + .035f * (float)Math.Sin(local * 2);
                result.FlipX = t >= FlightStart && t < FlightEnd && Math.Cos(angle) < 0;
            }
            else if (style == "beam")
            {
                result.Animation = local < GestureSeconds ? "waving" :
                    local < GestureSeconds * 2 ? "jumping" : local < GestureSeconds * 3 ? "waving" : "review";
                result.Bounds.Y -= (float)(6 * (1 - Math.Cos(local * Math.PI * 2 / GestureSeconds))) * envelope;
                result.Rotation = (float)Math.Sin(local * Math.PI * 2 / GestureSeconds) * 2.5f * envelope;
            }
            else if (style == "warning")
            {
                result.Animation = local >= GestureSeconds && local < GestureSeconds * 2 ? "waving" : "review";
                result.Bounds.Y -= 2 * (float)Math.Sin(local * 2) * envelope;
                result.Rotation = (float)Math.Sin(local * 1.5) * .8f * envelope;
            }
            else if (style == "stone")
            {
                result.Animation = local < GestureSeconds ? "jumping" : local < GestureSeconds * 2 ? "waving" : "review";
                // A single small shell-bob precedes the expanding ink ripples; no borrowed fist.
                float hop = (float)Math.Sin(Math.PI * Math.Min(1, Math.Max(0, (local - .16) / .72)));
                result.Bounds.Y -= 24 * hop;
                result.Rotation = 5 * (float)Math.Sin(local * 2.2) * envelope;
            }
            return result;
        }

    }
}
