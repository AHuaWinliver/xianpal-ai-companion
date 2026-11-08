// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Drawing = System.Drawing;

namespace XianPal.Linux;

internal sealed class NativeHallView : Control, IDisposable
{
    private readonly Dictionary<string, Bitmap> cache = new();
    private readonly Queue<string> order = new();
    private readonly Dictionary<string, Rect> seats = new();
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly DispatcherTimer timer;
    private readonly Bitmap background;
    private CompanionSect sect;
    private SectRun run;
    private Size viewport = new(760, 390);
    private bool disposed;
    internal event Action<string> MemberSelected;
    internal bool HasBackground => background != null;
    internal int SeatCount => seats.Count;

    internal NativeHallView()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "scenes", "sect-hall-v2.png");
        if (File.Exists(path)) background = new Bitmap(path);
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(32), DispatcherPriority.Background, (_, _) => { if (IsEffectivelyVisible) InvalidateVisual(); });
        timer.Start();
        PointerPressed += (_, e) =>
        {
            var point = e.GetPosition(this);
            foreach (var seat in seats) if (seat.Value.Contains(point)) { MemberSelected?.Invoke(seat.Key); break; }
        };
    }

    internal void SetScene(CompanionSect selected, SectRun record)
    {
        int before = Labels().Count;
        sect = selected; run = record;
        if (before != Labels().Count) InvalidateMeasure();
        InvalidateVisual();
    }

    internal void SetViewport(Size size)
    {
        if (size.Width < 1 || size.Height < 1 || size == viewport) return;
        viewport = size; InvalidateMeasure();
    }

    private List<string> Labels() => run != null ? run.Steps.Select(step => step.Member).Distinct().ToList() : sect?.Members.Select(member => member.Name).ToList() ?? new();

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = SectHallLayout.CanvasSize(new Drawing.Size(Math.Max(1, (int)viewport.Width), Math.Max(1, (int)viewport.Height)), Labels().Count, 1);
        return new Size(size.Width, size.Height);
    }

    private Bitmap Frame(string id, string state, int index)
    {
        var character = PetCharacter.Find(id);
        string root = character.ResourceRoot ?? (character.Id == "orbit" ? PetWindow.Assets : Path.Combine(PetWindow.Assets, "characters", character.Id));
        string file = Path.Combine(root, state, (index % CharacterPackCatalog.FrameCount(state)).ToString("00") + ".png");
        string key = character.ResourceRevision + "|" + file;
        if (cache.TryGetValue(key, out var frame)) return frame;
        if (!File.Exists(file)) return null;
        frame = new Bitmap(file);
        while (cache.Count >= 96) { string oldest = order.Dequeue(); cache[oldest].Dispose(); cache.Remove(oldest); }
        cache.Add(key, frame); order.Enqueue(key); return frame;
    }

    private static IBrush Brush(string color, double alpha = 1) => new SolidColorBrush(Color.Parse(color), alpha);
    private static void Fill(DrawingContext context, string color, double x, double y, double width, double height) => context.DrawRectangle(Brush(color), null, new Rect(x, y, width, height));
    private static void Text(DrawingContext context, string text, Rect box, IBrush brush, double size = 13)
    {
        var formatted = new FormattedText(text ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(DesktopFonts.TextFamily), size, brush) { MaxTextWidth = Math.Max(1, box.Width), MaxTextHeight = Math.Max(1, box.Height), Trimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center };
        context.DrawText(formatted, new Point(box.X, box.Y + Math.Max(0, (box.Height - formatted.Height) / 2)));
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (disposed || Bounds.Width < 1 || Bounds.Height < 1) return;
        var canvas = new Rect(Bounds.Size);
        context.DrawRectangle(Brush("#183133"), null, canvas);
        if (background != null)
        {
            double scale = Math.Max(Bounds.Width / background.Size.Width, Bounds.Height / background.Size.Height);
            double width = Bounds.Width / scale, height = Bounds.Height / scale;
            context.DrawImage(background, new Rect((background.Size.Width - width) / 2, background.Size.Height - height, width, height), canvas);
        }
        var labels = Labels(); seats.Clear();
        var boxes = SectHallLayout.Seats(new Drawing.Size((int)Bounds.Width, (int)Bounds.Height), labels.Count, 1);
        for (int i = 0; i < labels.Count; i++) seats[labels[i]] = new Rect(boxes[i].X, boxes[i].Y, boxes[i].Width, boxes[i].Height);
        var banner = new Rect(Bounds.Width * .2, 12, Bounds.Width * .6, 36);
        context.DrawRectangle(Brush("#1C1E1C", .85), new Pen(Brush("#A48142"), 1), banner);
        Text(context, run?.Sect ?? sect?.Name ?? UiLanguage.Text("门派大堂"), banner, Brush("#F6D78C"), 18);
        foreach (var transfer in run?.Transfers.TakeLast(24) ?? Enumerable.Empty<SectTransfer>())
        {
            var from = run.Steps.Find(step => step.Id == transfer.From); var to = run.Steps.Find(step => step.Id == transfer.To);
            if (from == null || to == null || from.Member == to.Member || !seats.TryGetValue(from.Member, out var a) || !seats.TryGetValue(to.Member, out var b)) continue;
            context.DrawLine(new Pen(Brush("#64BAC5", .7), 1.5), a.Center, b.Center);
            double progress = run.Status == "running" && to.Status == "running" ? clock.Elapsed.TotalSeconds * .45 % 1 : .75;
            context.DrawEllipse(Brush("#FAD573"), null, a.Center + (b.Center - a.Center) * progress, 3, 3);
        }
        foreach (var seat in seats)
        {
            var own = run?.Steps.Where(step => step.Member == seat.Key).ToArray() ?? Array.Empty<SectStepRun>();
            string status = own.Any(step => step.Status == "running") ? "running" : own.Any(step => step.Status is "failed" or "timeout") ? "failed" : own.Any(step => step.Status == "pending") ? "pending" : own.Length > 0 && own.All(step => step.Status == "success") ? "success" : run?.Status ?? "idle";
            string pose = status == "running" ? "review" : status == "success" ? "waving" : status == "failed" ? "failed" : "idle";
            string id = own.FirstOrDefault()?.CharacterId ?? sect?.Members.Find(member => member.Name == seat.Key)?.CharacterId ?? "orbit";
            double scale = Math.Min(1.5, Math.Min(seat.Value.Width / SectHallLayout.WorkstationWidth, seat.Value.Height / SectHallLayout.WorkstationHeight));
            var origin = new Point(seat.Value.X + (seat.Value.Width - 220 * scale) / 2, seat.Value.Y + (seat.Value.Height - 270 * scale) / 2);
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(origin.X, origin.Y)))
            {
                context.DrawEllipse(Brush("#07181A", .4), null, new Rect(46, 224, 128, 18));
                var image = Frame(id, pose, (int)(clock.Elapsed.TotalSeconds * (status == "running" ? 24 : 12)));
                if (image != null) context.DrawImage(image, new Rect(22, 44, 192 * 384.0 / 416, 192));
                DrawDesk(context, status == "running");
            }
            var name = new Rect(origin.X + 25 * scale, origin.Y + 18 * scale, 170 * scale, 22);
            var state = new Rect(origin.X + 35 * scale, origin.Y + 239 * scale, 150 * scale, 22);
            context.DrawRectangle(Brush("#102628", .85), null, name); context.DrawRectangle(Brush("#102628", .85), null, state);
            Text(context, seat.Key, name, Brush("#FBE0A6"));
            Text(context, UiLanguage.Text(status == "idle" ? "待命" : status == "running" ? "正在运行" : status == "success" ? "已完成" : status == "pending" ? "等待依赖" : "失败"), state, Brush(status == "running" ? "#66D6C2" : status == "failed" ? "#EB8578" : "#ADC3AE"));
        }
    }

    private void DrawDesk(DrawingContext c, bool working)
    {
        const int left = 23, top = 179, width = 174, center = 110;
        Fill(c,"#2F1A18",left+9,top+15,10,44); Fill(c,"#2F1A18",left+width-19,top+15,10,44);
        Fill(c,"#8B5930",left+10,top+18,3,36); Fill(c,"#8B5930",left+width-17,top+18,3,36);
        Fill(c,"#48261F",left+5,top+12,width-10,26); Fill(c,"#734028",left+9,top+17,width-18,16);
        Fill(c,"#311D1A",center-2,top+17,4,16); Fill(c,"#D6A653",center-width/4-5,top+22,10,3); Fill(c,"#D6A653",center+width/4-5,top+22,10,3);
        Fill(c,"#3C211C",left-4,top+6,width+8,10); Fill(c,"#A36535",left-4,top,width+8,9); Fill(c,"#DDA659",left-4,top,width+8,2);
        Fill(c,"#633423",left,top+5,width,2); Fill(c,"#7F5B34",center-38,top-4,72,6); Fill(c,"#F2DBA6",center-33,top-6,62,8);
        for(int x=center-23;x<center+18;x+=9)Fill(c,"#977F5B",x,top-3,5,1);
        Fill(c,"#326D64",center+18,top-7,14,3); Fill(c,"#DEE5CE",left+12,top-11,11,9); Fill(c,"#547373",left+14,top-10,7,2); Fill(c,"#CDD8C4",left+23,top-8,3,4);
        Fill(c,"#1E2B2D",left+width-26,top-9,12,8); Fill(c,"#B9843F",left+width-21,top-20,3,16);
        if(working)Fill(c,"#DEC072",center-8+(int)(clock.Elapsed.TotalSeconds*4)%3*5,top-10,2,5);
    }

    public void Dispose() { disposed = true; timer.Stop(); foreach (var image in cache.Values) image.Dispose(); cache.Clear(); background?.Dispose(); InvalidateVisual(); }
}
