// SPDX-License-Identifier: Apache-2.0
using Avalonia;
using Avalonia.Controls;

namespace XianPal.Linux;

internal sealed partial class PetWindow
{
    internal async Task ValidateNativeInput(string evidence)
    {
        if (Environment.GetEnvironmentVariable("XIANPAL_CI_INPUT") != "1")
        {
            File.WriteAllText(Path.Combine(evidence, "input-results.txt"), "SKIP XTEST pointer input: explicit CI opt-in not set.\n");
            return;
        }
        var original = Character; var saved = new CompatibleJson().Serialize(preferences);
        using var pointer = new X11InputProbe();
        try
        {
            preferences.ReduceMotion = false; preferences.PathMode = "builtin"; preferences.TrailOpacity = 0;
            preferences.DockedEdge = ""; Opacity = 1; ChatWorking = false;
            conversation?.Hide(); reply?.Hide(); CancelMotion();
            var area = Screens.ScreenFromWindow(this)!.WorkingArea;
            foreach (var profile in PetCharacter.All)
            {
                pointer.Move(new PixelPoint(area.X + 2, area.Y + 2));
                SelectCharacter(profile); nextRoam = long.MaxValue; nextIdleGesture = nextIdleQuote = long.MaxValue;
                Position = Fit(new PixelPoint(area.X + area.Width / 2, area.Y + area.Height / 2));
                await Task.Delay(80);
                var start = this.PointToScreen(new Point(Width * .2, Height * .25));
                pointer.Move(start); await Task.Delay(35); pointer.Button(1, true); await Task.Delay(25);
                int direction = Array.IndexOf(PetCharacter.All, profile) % 2 == 0 ? -1 : 1;
                var poses = new HashSet<object>();
                for (int i = 1; i <= 10; i++)
                {
                    pointer.Move(new PixelPoint(start.X + direction * i * 3, start.Y));
                    await Task.Delay(25); poses.Add(sprite.Source!);
                }
                double slow = gait.Speed;
                if (!dragging || slow <= 1 || left != (direction < 0) || poses.Count < 2)
                    throw new Exception("Native pointer drag did not animate " + profile.Id + ": speed=" + slow + ", poses=" + poses.Count);
                for (int i = 1; i <= 10; i++)
                {
                    pointer.Move(new PixelPoint(start.X + direction * (30 + i * 9), start.Y));
                    await Task.Delay(12);
                }
                if (gait.Speed < slow * 1.25) throw new Exception("Drag cadence did not respond to native pointer speed: " + profile.Id);
                await Task.Delay(220);
                if (!dragging || gait.Speed != 0 || !ReferenceEquals(sprite.Source, frames["idle/00.png"]))
                    throw new Exception("A held but stationary drag kept walking or playing a hover gesture: " + profile.Id);
                pointer.Button(1, false); await Task.Delay(50);
                if (dragging || gait.Speed != 0) throw new Exception("Native release did not clear drag state.");
                ShowLatestReply("a🌱" + new string('x', 129));
                await Task.Delay(60);
                if (reply.WindowStartupLocation != WindowStartupLocation.Manual || !replyLabel.Text.Contains("🌱"))
                    throw new Exception("Initial reply positioning or Unicode truncation was invalid.");
                int expectedY = Math.Clamp(Position.Y + (int)(Height * RenderScaling) + 5, area.Y, Math.Max(area.Y, area.Bottom - (int)(reply.Height * RenderScaling)));
                if (Math.Abs(reply.Position.Y - expectedY) > 2) throw new Exception("The first reply bubble was not anchored to its companion.");
                pointer.Move(this.PointToScreen(new Point(Width * .85, Height * .6)));
                pointer.Button(3, true); pointer.Button(3, false); await Task.Delay(80);
                if (ContextMenu?.IsOpen != true || reply.IsVisible) throw new Exception("Native right-click did not open the menu and hide the reply.");
                ContextMenu.Close(); pointer.Move(new PixelPoint(area.X + 2, area.Y + 2));
                DesktopValidation.Capture(this, "input-" + profile.Id + ".png");
            }
            File.WriteAllText(Path.Combine(evidence, "input-results.txt"), "PASS native XTEST events on isolated Xvfb: expanded drag/right-click regions; four companions; opposite directions; speed-driven gait; held-drag pause to standing; release cleanup; first reply anchor and surrogate-safe text; right-click hides reply.\n");
        }
        finally
        {
            ContextMenu?.Close(); reply?.Hide(); dragging = false; dragPointer = null; CancelMotion();
            var restore = new CompatibleJson().Deserialize<DesktopPreferences>(saved);
            preferences.ReduceMotion = restore.ReduceMotion; preferences.PathMode = restore.PathMode; preferences.TrailOpacity = restore.TrailOpacity;
            preferences.DockedEdge = restore.DockedEdge;
            SelectCharacter(original); nextRoam = clock.ElapsedMilliseconds + 20000;
        }
    }
}
