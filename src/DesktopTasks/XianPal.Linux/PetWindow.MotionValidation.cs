// SPDX-License-Identifier: Apache-2.0
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace XianPal.Linux;

internal sealed partial class PetWindow
{
    // Invoked only by --desktop-test, against a private state directory and a
    // fake CLI. Exercise the real renderer and state transitions on the UI thread.
    internal async Task ValidateMotion(string evidence)
    {
        if (Environment.GetEnvironmentVariable("FUFU_TEST_MODE") != "1") throw new InvalidOperationException("Isolated validation required.");
        var original = Character;
        var saved = new CompatibleJson().Serialize(preferences);
        timer.Stop();
        try
        {
            preferences.PathMode = "builtin"; preferences.ProactiveCare = false; preferences.ReduceMotion = false;
            preferences.DockedEdge = ""; preferences.TrailOpacity = 0; preferences.DockedOpacity = 1;
            conversation?.Hide(); reply?.Hide(); hovering = false; ChatWorking = false;
            PixelRect area = Screens.ScreenFromWindow(this)!.WorkingArea;
            var home = new PixelPoint(area.X + area.Width / 2 - 70, area.Y + area.Height / 2 - 70);
            foreach (PetCharacter profile in PetCharacter.All)
            {
                SelectCharacter(profile); Position = home;
                foreach (int direction in new[] { -1, 1 })
                {
                    var target = Fit(new PixelPoint(home.X + direction * 120, home.Y));
                    BeginRoute(new[] { target }, false);
                    var observed = new HashSet<object>();
                    for (int i = 0; i < 45; i++)
                    {
                        TickRoam(clock.ElapsedMilliseconds, 16);
                        observed.Add(sprite.Source!);
                    }
                    if (observed.Count < 8 || left != (direction < 0) || flight.Active)
                        throw new Exception("Walking did not advance a directional 24-phase gait: " + profile.Id);
                    await Task.Delay(30);
                    DesktopValidation.Capture(this, "walking-" + profile.Id + (direction < 0 ? "-left.png" : "-right.png"));
                    CancelMotion(); Position = home;
                }
                StartTour();
                if (!RidingArtifact) throw new Exception("Long route did not mount a spirit artifact: " + profile.Id);
                for (int i = 0; i < 45; i++) TickRoam(clock.ElapsedMilliseconds, 16);
                if (!artifact.IsVisible || sprite.IsVisible || flight.Envelope < .99)
                    throw new Exception("Artifact takeoff or sprite handover failed: " + profile.Id);
                await Task.Delay(30);
                DesktopValidation.Capture(this, "artifact-" + profile.Id + ".png");
                CancelMotion();
                if (artifact.IsVisible || !sprite.IsVisible || RidingArtifact || gait.Speed != 0)
                    throw new Exception("Cancelling travel left a stale artifact or moving gait.");
            }
            Position = home;
            foreach (string edge in new[] { "left", "right", "bottom" })
            {
                ApplyDock(edge);
                PixelPoint docked = Position;
                nextRoam = 0;
                Animate(); Animate();
                if (Position != docked || roamTarget != null || preferences.DockedEdge != edge)
                    throw new Exception("An opaque docked companion resumed roaming at " + edge);
                HideCompanion(); ShowCompanion();
                if (Position != docked) throw new Exception("Restoring a hidden companion lost its dock.");
            }
            preferences.DockedEdge = ""; Position = Fit(new PixelPoint(area.X + 20, area.Y + 20));
            int calls = 0;
            RunToCenter(() => calls++);
            ChatWorking = true; hovering = true;
            for (int i = 0; i < 600 && calls == 0; i++) TickRoam(clock.ElapsedMilliseconds, 16);
            int expectedX = area.X + (area.Width - (int)(Width * RenderScaling)) / 2;
            int expectedY = area.Y + (area.Height - (int)(Height * RenderScaling)) / 2;
            if (calls != 1 || Position != new PixelPoint(expectedX, expectedY) || RidingArtifact)
                throw new Exception("Countdown did not arrive once at this monitor's center without an artifact.");
            CancelMotion();
            if (calls != 1) throw new Exception("Reminder arrival was delivered twice.");
            var overlay = new NativeTrailWindow(this, area);
            try
            {
                overlay.AddPoint(new Point(30, 40), Width, .5, trailColors);
                await Task.Delay(80);
                if (!overlay.ClickThrough) throw new Exception("X11 trail overlay has a nonempty native input region.");
                if (overlay.IsVisible && overlay.ActualTransparencyLevel == WindowTransparencyLevel.None)
                    throw new Exception("Noncomposited desktop retained an opaque trail window.");
            }
            finally { overlay.Close(); }
            File.WriteAllText(Path.Combine(evidence, "motion-results.txt"), "PASS four original companions: left/right walking phase progression; bounded long routes; spirit-artifact takeoff and cancellation; opaque edge docking and hide/show restoration; single-monitor countdown arrival; native X11 mouse-through trail and transparency fallback. No model calls.\n");
        }
        finally
        {
            ChatWorking = false; hovering = false; CancelMotion();
            var restore = new CompatibleJson().Deserialize<DesktopPreferences>(saved);
            preferences.PathMode = restore.PathMode; preferences.ProactiveCare = restore.ProactiveCare;
            preferences.ReduceMotion = restore.ReduceMotion; preferences.TrailOpacity = restore.TrailOpacity;
            preferences.DockedOpacity = restore.DockedOpacity; preferences.DockedEdge = restore.DockedEdge;
            SelectCharacter(original); Position = Fit(new PixelPoint(restore.X ?? Position.X, restore.Y ?? Position.Y));
            nextRoam = clock.ElapsedMilliseconds + 20000; lastTick = clock.ElapsedMilliseconds; timer.Start();
        }
    }
}
