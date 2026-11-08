// SPDX-License-Identifier: Apache-2.0
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Drawing = System.Drawing;

namespace XianPal.Linux;

internal sealed partial class PetWindow
{
    private readonly Queue<PixelPoint> route = new();
    private readonly Queue<PetPathPlan> aiRoutes = new();
    private readonly ArtifactTravelClock flight = new();
    private readonly NativeArtifactView artifact = new() { IsVisible = false };
    private NativeTrailWindow trail;
    private PixelRect trailArea;
    private double trailScale, motionX, motionY;
    private Drawing.Color[] trailColors = PetBrain.DefaultColors("orbit");
    private ClaudeCliRunner brain;
    private long nextPathRequest, nextCare, nextBreak, nextIdleGesture, nextIdleQuote, nextScreenCheck, allowHoverUntil;
    private bool screenActive = true;
    private PixelRect routeArea;
    private double routeSpeed = 130;
    private Action arrived;
    private bool urgentRoute;
    private readonly System.Diagnostics.Stopwatch screenSession = System.Diagnostics.Stopwatch.StartNew();

    internal bool BrainWorking => brain != null;
    internal int AiRouteCount => aiRoutes.Count;
    internal bool RidingArtifact => flight.Active;

    private void CancelMotion()
    {
        Action complete = arrived; arrived = null; urgentRoute = false;
        route.Clear(); roamTarget = null; flight.Reset(); gait.Reset();
        artifact.IsVisible = false; sprite.IsVisible = true;
        trail?.Clear();
        if (!IsClosing) complete?.Invoke();
    }

    private void ResetCharacterBehavior()
    {
        CancelMotion(); aiRoutes.Clear();
        ClaudeCliRunner previous = brain; brain = null; previous?.Stop(); previous?.Dispose();
        trailColors = PetBrain.DefaultColors(Character.Id);
        nextPathRequest = 0;
        nextCare = clock.ElapsedMilliseconds + Random.Shared.Next(12, 29) * 60000L;
        nextBreak = clock.ElapsedMilliseconds + preferences.BreakMinutes * 60000L;
        screenSession.Restart();
        nextIdleGesture = clock.ElapsedMilliseconds + Random.Shared.Next(10000, 24000);
        nextIdleQuote = clock.ElapsedMilliseconds + Random.Shared.Next(50000, 110000);
    }

    private void BeginRoute(IEnumerable<PixelPoint> points, bool allowArtifact = true, double speed = 130)
    {
        CancelMotion();
        routeSpeed = speed;
        foreach (var point in points)
            if (point != Position && (route.Count == 0 || route.Last() != point)) route.Enqueue(point);
        if (route.Count == 0 || preferences.ReduceMotion) return;
        motionX = Position.X; motionY = Position.Y; roamStart = clock.ElapsedMilliseconds;
        var area = Screens.ScreenFromWindow(this)?.WorkingArea ?? new PixelRect(0, 0, 1024, 768);
        routeArea = area;
        var rectangle = new Drawing.Rectangle(area.X, area.Y, area.Width, area.Height);
        if (allowArtifact && ArtifactTravelClock.RouteLength(new Drawing.Point(Position.X, Position.Y), route.Select(point => new Drawing.Point(point.X, point.Y))) >= ArtifactTravelClock.Threshold(rectangle)) flight.Start();
        roamTarget = route.Dequeue();
    }

    internal void StartTour()
    {
        if (preferences.ReduceMotion || IsClosing) return;
        Opacity = 1; Position = Fit(Position);
        preferences.DockedEdge = "";
        allowHoverUntil = clock.ElapsedMilliseconds + 1000;
        var area = Screens.ScreenFromWindow(this)?.WorkingArea ?? new PixelRect(0, 0, 1024, 768);
        var rectangle = new Drawing.Rectangle(area.X, area.Y, area.Width, area.Height);
        var size = new Drawing.Size((int)Math.Ceiling(Width * RenderScaling), (int)Math.Ceiling(Height * RenderScaling));
        var origin = new Drawing.Point(Position.X, Position.Y);
        List<Drawing.Point> points = null;
        if (preferences.PathMode == "ai" && aiRoutes.Count > 0)
        {
            var plan = aiRoutes.Dequeue();
            points = CompanionRoutes.GenerateAiPath(rectangle, size, origin, plan.Curve, plan.ControlPoints);
        }
        if (points == null || points.Count < 4) points = CompanionRoutes.GeneratePlayfulPath(rectangle, size, origin, Random.Shared.Next());
        BeginRoute(points.Select(point => new PixelPoint(point.X, point.Y)));
        if (preferences.PathMode == "ai" && aiRoutes.Count == 0 && clock.ElapsedMilliseconds >= nextPathRequest && brain == null)
            RequestBrain("wake", true);
    }

    private bool TickRoam(long now, double elapsed)
    {
        if ((roamTarget != null || flight.Active) && Screens.ScreenFromWindow(this)?.WorkingArea != routeArea)
        { CancelMotion(); Position = Fit(Position); return false; }
        flight.Advance(elapsed);
        if (roamTarget is PixelPoint target && !flight.Landing)
        {
            double dx = target.X - motionX, dy = target.Y - motionY, distance = Math.Sqrt(dx * dx + dy * dy);
            double step = flight.Active ? flight.MovementStep(elapsed) : routeSpeed * Math.Min(34, elapsed) / 1000;
            if (distance > .001)
            {
                if (Math.Abs(dx) > .05) left = dx < 0;
                double moved = Math.Min(step, distance);
                motionX += dx * moved / distance; motionY += dy * moved / distance;
                var before = Position;
                Position = new PixelPoint((int)Math.Round(motionX), (int)Math.Round(motionY));
                var difference = Position - before;
                gait.RecordMotion(Math.Sqrt((double)difference.X * difference.X + (double)difference.Y * difference.Y), Math.Max(1, elapsed));
                RecordTrail();
            }
            if (distance <= step + .001)
            {
                Position = target;
                roamTarget = route.Count > 0 ? route.Dequeue() : null;
                if (roamTarget == null) { flight.BeginLanding(); nextRoam = now + Random.Shared.Next(16000, 35000); }
            }
            if (!flight.Active && roamTarget != null)
            {
                RenderFrame(left ? "running-left" : "running-right", gait.Advance(elapsed, 0, 24));
                return true;
            }
        }
        if (flight.Active)
        {
            RenderFrame("idle", 0);
            if (sprite.Source is Bitmap rider)
            {
                artifact.Update(rider, Character, flight.Envelope, flight.Seconds, left);
                sprite.IsVisible = false; artifact.IsVisible = true;
            }
            return true;
        }
        sprite.IsVisible = true; artifact.IsVisible = false;
        if (roamTarget == null && route.Count == 0 && arrived != null)
        {
            Action complete = arrived; arrived = null; urgentRoute = false;
            Gesture("waving", 900); complete();
        }
        return false;
    }

    internal void RunToCenter(Action complete)
    {
        var area = Screens.ScreenFromWindow(this)?.WorkingArea ?? new PixelRect(0, 0, 1024, 768);
        conversation?.Hide(); reply?.Hide(); preferences.DockedEdge = ""; Opacity = 1;
        var target = new PixelPoint(area.X + (area.Width - (int)(Width * RenderScaling)) / 2, area.Y + (area.Height - (int)(Height * RenderScaling)) / 2);
        BeginRoute(new[] { target }, false, 700);
        if (roamTarget == null) { complete(); return; }
        urgentRoute = true; arrived = complete;
    }

    internal void ApplyDock(string edge)
    {
        var area = Screens.ScreenFromWindow(this)?.WorkingArea;
        if (area == null) return;
        CancelMotion(); preferences.DockedEdge = edge;
        int width = (int)(Width * RenderScaling), height = (int)(Height * RenderScaling);
        int x = Math.Clamp(Position.X, area.Value.X, Math.Max(area.Value.X, area.Value.Right - width));
        int y = Math.Clamp(Position.Y, area.Value.Y, Math.Max(area.Value.Y, area.Value.Bottom - height));
        if (edge == "left") x = area.Value.X - (int)Math.Round(width * .42);
        else if (edge == "right") x = area.Value.Right - (int)Math.Round(width * .58);
        else if (edge == "bottom") y = area.Value.Bottom - (int)Math.Round(height * .56);
        Position = new PixelPoint(x, y); Opacity = preferences.DockedOpacity;
    }

    private void RecordTrail()
    {
        if (preferences.ReduceMotion || preferences.TrailOpacity <= 0 || !IsVisible) return;
        var area = Screens.ScreenFromWindow(this)?.WorkingArea;
        if (area == null) return;
        if (trail == null || area != trailArea || Math.Abs(trailScale - RenderScaling) > .01)
        {
            trail?.Close(); trailArea = area.Value; trailScale = RenderScaling;
            trail = new NativeTrailWindow(this, trailArea);
        }
        var point = new Point((Position.X - trailArea.X) / RenderScaling + Width / 2, (Position.Y - trailArea.Y) / RenderScaling + Height * .58);
        trail.AddPoint(point, Width, preferences.TrailOpacity, trailColors);
    }

    internal void Wake()
    {
        StartTour();
        if (brain == null) RequestBrain("wake", preferences.PathMode == "ai" && aiRoutes.Count == 0 && clock.ElapsedMilliseconds >= nextPathRequest);
    }

    internal void RequestBrain(string reason, bool refill = false)
    {
        if (brain != null || ChatWorking || IsClosing) return;
        PetCharacter character = Character;
        string prompt = reason == "history" ? PetBrain.BuildHistoryTodayPrompt(character, DateTime.Now) :
            reason == "wake" ? PetBrain.BuildProactivePrompt(character, DateTime.Now, refill) :
            PetBrain.BuildCarePrompt(character, DateTime.Now, reason, (int)Math.Max(1, screenSession.Elapsed.TotalMinutes));
        if (UiLanguage.IsEnglish) prompt += "\nWrite the SAY speech in English. Preserve the TRAIL and PATH protocol lines and the companion's personality.";
        if (refill) nextPathRequest = clock.ElapsedMilliseconds + 15 * 60000L;
        var output = new StringBuilder();
        var active = new ClaudeCliRunner(); brain = active;
        active.OutputReceived += text => Dispatcher.UIThread.Post(() => { if (brain == active && output.Length < 65536) output.Append(text); });
        active.Completed += code => Dispatcher.UIThread.Post(() =>
        {
            if (brain != active || IsClosing) return;
            brain = null; active.Dispose();
            if (code != 0 || Character.Id != character.Id) return;
            var response = PetBrain.Parse(output.ToString(), character);
            if (response.PathPlans != null && refill)
                foreach (var plan in response.PathPlans.Take(24)) aiRoutes.Enqueue(plan);
            trailColors = response.TrailColors;
            ShowLatestReply(response.Speech);
            Gesture("waving", 900);
        });
        try { active.Start(prompt, DirectoryPath, null, "desktop-brain", character.Id); }
        catch (Exception error)
        {
            brain = null; active.Dispose();
            if (reason is "wake" or "history") ShowLatestReply(error.Message);
        }
    }

    private void TickCare(long now)
    {
        if (IsVisible && !dragging && !hovering && !ChatWorking && brain == null && conversation?.IsVisible != true && roamTarget == null && !flight.Active)
        {
            if (now >= nextIdleGesture) { Gesture(new[] { "review", "waving", "jumping" }[Random.Shared.Next(3)], 1100); nextIdleGesture = now + Random.Shared.Next(10000, 24000); }
            if (now >= nextIdleQuote) { ShowLatestReply(Character.Quotes[Random.Shared.Next(Character.Quotes.Length)]); nextIdleQuote = now + Random.Shared.Next(50000, 110000); }
        }
        if (!preferences.ProactiveCare || preferences.PathMode != "ai" || !IsVisible || ChatWorking || brain != null || dragging || hovering || conversation?.IsVisible == true) return;
        if (now >= nextScreenCheck)
        {
            nextScreenCheck = now + 5000;
            bool active = X11Overlay.ScreenActive() ?? true;
            if (active != screenActive)
            {
                screenActive = active;
                if (active) { screenSession.Restart(); nextBreak = now + preferences.BreakMinutes * 60000L; }
                else screenSession.Reset();
            }
        }
        if (!screenActive) return;
        if (now >= nextBreak)
        {
            nextBreak = now + Math.Clamp(preferences.BreakMinutes, 5, 240) * 60000L;
            nextCare = now + Random.Shared.Next(12, 29) * 60000L;
            RequestBrain("break");
        }
        else if (now >= nextCare)
        {
            nextCare = now + Random.Shared.Next(12, 29) * 60000L;
            RequestBrain("idle-talk");
        }
    }
}
