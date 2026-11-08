// SPDX-License-Identifier: Apache-2.0
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace XianPal.Linux;

internal sealed class NativeExpressionView : Control, IDisposable
{
    private readonly Dictionary<int, Bitmap> patches = new();
    private readonly System.Diagnostics.Stopwatch time = System.Diagnostics.Stopwatch.StartNew();
    private WalkingExpressionClock clock;
    private string characterId;
    private bool left;
    private int frame, blink, smile;

    internal NativeExpressionView() { IsHitTestVisible = false; IsVisible = false; }

    internal void Update(PetCharacter character, string state, int index)
    {
        if (character.Id != characterId)
        {
            Dispose(); characterId = character.Id;
            if (!character.IsExternal)
            {
                string root = character.Id == "orbit" ? PetWindow.Assets : Path.Combine(PetWindow.Assets, "characters", character.Id);
                for (int i = 1; i <= 6; i++)
                {
                    string path = Path.Combine(root, "profile-expressions", i.ToString("00") + ".png");
                    if (File.Exists(path)) patches[i] = new Bitmap(path);
                }
                if (patches.Count == 6) clock = new WalkingExpressionClock(unchecked(Environment.TickCount + character.Id.Length * 127), character.Id);
            }
            time.Restart();
        }
        IsVisible = clock != null && state.StartsWith("running", StringComparison.Ordinal);
        if (!IsVisible) return;
        frame = index % 24; left = characterId == "pippin" ? state == "running-right" : state == "running-left";
        clock.Sample(time.Elapsed.TotalSeconds, out blink, out smile);
        InvalidateVisual();
    }

    // The authored patches use the same head attachment transform as the baked
    // walking frames and PetForm.ApplyWalkingFaceTransform. Never move a patch
    // independently of the neck socket or change the approved head silhouette.
    internal static Matrix FaceTransform(string id, int frame, bool mirror, Size size)
    {
        double phase = frame / 24.0, t = phase * Math.PI * 2;
        Point root = id == "pippin" ? new Point(151, 296 + 20 * (1 - Math.Exp(-1))) : id == "spark" ? new Point(199, 187) : id == "moss" ? new Point(190, 200) : new Point(185, 194);
        Point target = id == "pippin" ? root + new Vector(0, Math.Cos(t * 2) * .6) : root;
        double amount = id == "pippin" ? .70 : id == "moss" ? .82 : id == "spark" ? 1.08 : 1;
        double angle = amount * (2.1 * Math.Sin(t - .38) + .35 * Math.Sin(2 * t - .55)) * Math.PI / 180;
        double stretch = id == "pippin" ? 1 + Math.Sin(t - .45) * 2 / 143 : 1;
        double a = Math.Cos(angle) * stretch, b = Math.Sin(angle) * stretch, c = -Math.Sin(angle), d = Math.Cos(angle);
        var neck = new Matrix(a, b, c, d, target.X - a * root.X - c * root.Y, target.Y - b * root.X - d * root.Y);
        var body = id == "pippin" ? Matrix.Identity : Matrix.CreateTranslation(-192, -260) * Matrix.CreateRotation(Math.Sin(t) * .45 * Math.PI / 180) * Matrix.CreateTranslation(192 + Math.Sin(t) * .9, 260 + Math.Cos(t * 2) * .75);
        var facing = mirror ? Matrix.CreateScale(-1, 1) * Matrix.CreateTranslation(384, 0) : Matrix.Identity;
        double fit = Math.Min(size.Width / 384, size.Height / 416);
        return neck * body * facing * Matrix.CreateScale(fit, fit) * Matrix.CreateTranslation((size.Width - 384 * fit) / 2, (size.Height - 416 * fit) / 2);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (!IsVisible || blink == 0 && smile == 0) return;
        using var transform = context.PushTransform(FaceTransform(characterId, frame, left, Bounds.Size));
        if (blink > 0 && patches.TryGetValue(blink, out var eyes)) context.DrawImage(eyes, new Rect(0, 0, 384, 416));
        if (smile > 0 && patches.TryGetValue(smile + 3, out var mouth)) context.DrawImage(mouth, new Rect(0, 0, 384, 416));
    }

    public void Dispose() { clock = null; foreach (var image in patches.Values) image.Dispose(); patches.Clear(); }
}
