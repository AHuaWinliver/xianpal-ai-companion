// SPDX-License-Identifier: Apache-2.0
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace XianPal.Linux;

// External packs supply their own complete frames. Never substitute a bundled
// companion's rendered performance when a new pack is selected.
internal sealed class NativePackPerformance : Control, IDisposable
{
    private readonly CharacterAnimationSet frames;
    private readonly string style;
    internal double Seconds { get; set; }

    internal NativePackPerformance(PetCharacter character)
    {
        CharacterPackCatalog.ReadPack(character.ResourceRoot, true, out frames);
        style = ReminderPerformance.Supports(character.ReminderStyle) ? character.ReminderStyle : "stone";
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var pose = ReminderPerformance.Sample(new System.Drawing.Size((int)Bounds.Width, (int)Bounds.Height), style, Seconds, false);
        var box = new Rect(pose.Bounds.X, pose.Bounds.Y, pose.Bounds.Width, pose.Bounds.Height);
        if (Seconds > ReminderPerformance.EntranceEnd && Seconds < ReminderPerformance.CardAt)
        {
            double alpha = ReminderPerformance.Smooth((Seconds - ReminderPerformance.EntranceEnd) / .5) * (1 - ReminderPerformance.Smooth((Seconds - 5.1) / .5));
            Color accent = Color.Parse(style == "beam" ? "#E37985" : style == "warning" ? "#73B58B" : style == "stone" ? "#CFAD66" : "#50BED3");
            Point center = new(box.Center.X, box.Bottom - box.Height * .04);
            if (style == "command")
            {
                double scale = box.Width / 384;
                using var transform = context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(box.X, center.Y));
                NativeArtifactView.DrawVessel(context, style, alpha, Seconds);
            }
            else for (int i = 0; i < 8; i++)
            {
                double angle = Seconds * .6 + i * Math.PI / 4;
                double radius = box.Width * (.65 + .10 * Math.Sin(Seconds * 2 + i));
                Point point = center + new Vector(Math.Cos(angle) * radius, Math.Sin(angle) * radius * .35 - box.Height * .5);
                var pen = new Pen(new SolidColorBrush(accent, alpha * .8), 2);
                if (style == "beam")
                {
                    context.DrawRectangle(new SolidColorBrush(Color.Parse("#FFE0A5"), alpha), pen, new Rect(point.X - 9, point.Y - 20, 18, 40));
                    context.DrawLine(pen, point - new Vector(4, 11), point + new Vector(4, 10));
                    context.DrawLine(pen, point + new Vector(4, -9), point + new Vector(-4, 9));
                }
                else if (style == "warning")
                {
                    context.DrawLine(pen, center, point);
                    context.DrawEllipse(new SolidColorBrush(accent, alpha * .65), null, new Rect(point.X - 8, point.Y - 4, 16, 8));
                }
                else
                {
                    double size = box.Width * (.3 + ((Seconds * .25 + i / 8.0) % 1));
                    context.DrawEllipse(null, new Pen(new SolidColorBrush(accent, alpha * (1 - i / 10.0)), 1.5), new Rect(center.X - size, center.Y - size * .22, size * 2, size * .44));
                }
            }
        }
        var animation = frames.Frames[pose.Animation];
        var image = animation[ReminderPerformance.FrameIndex(pose.Cycle, animation.Count)];
        using var rotation = context.PushTransform(Matrix.CreateTranslation(-box.Center.X, -box.Center.Y) * Matrix.CreateRotation(pose.Rotation * Math.PI / 180) * Matrix.CreateTranslation(box.Center.X, box.Center.Y));
        using var facing = context.PushTransform(pose.FlipX ? Matrix.CreateTranslation(-box.Center.X, 0) * Matrix.CreateScale(-1, 1) * Matrix.CreateTranslation(box.Center.X, 0) : Matrix.Identity);
        context.DrawImage(image, box);
    }

    public void Dispose() => frames.Dispose();
}
