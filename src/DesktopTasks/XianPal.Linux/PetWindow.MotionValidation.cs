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
                SelectCharacter(profile); Position = home; await Task.Delay(40);
                foreach (int direction in new[] { -1, 1 })
                {
                    var target = Fit(new PixelPoint(home.X + direction * 120, home.Y));
                    BeginRoute(new[] { target }, false);
                    var observed = new HashSet<object>();
                    for (int i = 0; i < 45; i++)
                    {
                        TickRoam(clock.ElapsedMilliseconds, 16);
                        observed.Add(sprite.Source!);
                        await Task.Delay(8); // Let the native X11 position notification arrive.
                    }
                    if (observed.Count < 8 || left != (direction < 0) || flight.Active)
                        throw new Exception("Walking did not advance a directional 24-phase gait: " + profile.Id + ", frames=" + observed.Count + ", left=" + left + ", direction=" + direction + ", flight=" + flight.Active + ", position=" + Position);
                    await Task.Delay(30);
                    DesktopValidation.Capture(this, "walking-" + profile.Id + (direction < 0 ? "-left.png" : "-right.png"));
                    CancelMotion(); Position = home; await Task.Delay(40);
                }
                StartTour();
                if (!RidingArtifact) throw new Exception("Long route did not mount a spirit artifact: " + profile.Id);
                for (int i = 0; i < 45; i++) { TickRoam(clock.ElapsedMilliseconds, 16); await Task.Delay(8); }
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
                await Task.Delay(40);
                PixelPoint docked = Position;
                nextRoam = 0;
                Animate(); Animate();
                if (Position != docked || roamTarget != null || preferences.DockedEdge != edge)
                    throw new Exception("An opaque docked companion resumed roaming at " + edge);
                HideCompanion(); ShowCompanion();
                await Task.Delay(40);
                if (Position != docked) throw new Exception("Restoring a hidden companion lost its dock.");
            }
            preferences.DockedEdge = ""; Position = Fit(new PixelPoint(area.X + 20, area.Y + 20));
            await Task.Delay(40);
            int calls = 0;
            RunToCenter(() => calls++);
            ChatWorking = true; hovering = true;
            for (int i = 0; i < 600 && calls == 0; i++) { TickRoam(clock.ElapsedMilliseconds, 16); await Task.Delay(4); }
            await Task.Delay(40);
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
            await ValidateBrainAndPack(evidence);
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

    private async Task ValidateBrainAndPack(string evidence)
    {
        var json = new CompatibleJson();
        string savedIntegration = json.Serialize(IntegrationPreferences.Load());
        string fixture = Path.Combine(Program.StateDirectory, "brain-fixture.py");
        string callsFile = Path.Combine(Program.StateDirectory, "brain-calls.txt");
        File.WriteAllText(fixture, """
#!/usr/bin/env python3
import json, os, sys
sys.stdin.read()
with open(os.path.join(os.environ['FUFU_STATE_DIR'], 'brain-calls.txt'), 'a') as log:
    log.write('request\n')
line = 'PATH|curve=wave|points=0.05,0.05;0.95,0.95;0.05,0.95;0.95,0.05'
text = 'TRAIL|style=aurora|colors=#72D4E4,#EAC272,#7DD4AA\n' + '\n'.join([line]*24) + '\nSAY|A quiet moment, companion. \U0001f331'
print(json.dumps({'type':'result','result':text,'is_error':False}), flush=True)
""");
        File.SetUnixFileMode(fixture, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            var integration = IntegrationPreferences.Load();
            integration.SetExecutablePath(Character.Id, fixture); integration.SetSettingsPath(Character.Id, ""); integration.ClaudeSettingsPath = ""; integration.Save();
            ChatWorking = false; preferences.PathMode = "ai";
            aiRoutes.Clear(); nextPathRequest = 0;
            RequestBrain("wake", true); RequestBrain("wake", true);
            DateTime deadline = DateTime.UtcNow.AddSeconds(20);
            while (BrainWorking && DateTime.UtcNow < deadline) await Task.Delay(30);
            if (BrainWorking || AiRouteCount != 24 || !currentReply.Contains("quiet moment") || trailColors[0].R != 0x72)
                throw new Exception("AI route batch, speech or light palette was not applied.");
            for (int i = 0; i < 12; i++) { StartTour(); CancelMotion(); }
            if (AiRouteCount != 12 || File.ReadAllLines(callsFile).Length != 1)
                throw new Exception("Cached AI routes triggered extra model requests.");
        }
        finally
        {
            ClaudeCliRunner running = brain; brain = null; running?.Stop(); running?.Dispose();
            json.Deserialize<IntegrationPreferences>(savedIntegration).Save();
            preferences.PathMode = "builtin"; aiRoutes.Clear();
        }
        string packRoot = Path.Combine(Program.StateDirectory, "pack-fixture", "test-familiar");
        var manifest = new CharacterPackManifest
        {
            schemaVersion = 1, id = "test-familiar", version = "0.1.0", name = "Test familiar", persona = "An isolated original-art validation fixture.",
            quotes = new[] { "One", "Two", "Three", "Four", "Five" }, reminderStyle = "stone", canvasWidth = 384, canvasHeight = 416,
            mouthX = .5f, mouthY = .3f, files = new Dictionary<string, string>()
        };
        foreach (string state in CharacterPackCatalog.States)
        {
            Directory.CreateDirectory(Path.Combine(packRoot, state));
            for (int i = 0; i < CharacterPackCatalog.FrameCount(state); i++)
            {
                string relative = state + "/" + i.ToString("00") + ".png";
                byte[] bytes = File.ReadAllBytes(Path.Combine(Assets, relative));
                File.WriteAllBytes(Path.Combine(packRoot, relative), bytes);
                manifest.files.Add(relative, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
            }
        }
        File.WriteAllText(Path.Combine(packRoot, "character.json"), json.Serialize(manifest));
        PetCharacter external = CharacterPackCatalog.ReadPack(packRoot, false, out var checkedFrames); checkedFrames.Dispose();
        foreach (string style in new[] { "command", "beam", "warning", "stone" })
        {
            external.ReminderStyle = style;
            using var performance = new NativePackPerformance(external) { Seconds = 3.1 };
            var preview = new Window { Width = 800, Height = 500, Content = performance, ShowActivated = false };
            try { preview.Show(this); await Task.Delay(40); DesktopValidation.Capture(preview, "external-performance-" + style + ".png"); }
            finally { preview.Close(); }
        }
        File.WriteAllText(Path.Combine(evidence, "brain-pack-results.txt"), "PASS fake CLI: a single request supplies 24 route plans, speech and RGB colors; duplicate in-flight requests suppressed; twelve routes reused without further CLI calls. External data-only pack validated and rendered with its own frames in all four performance timelines.\n");
    }
}
