// SPDX-License-Identifier: Apache-2.0
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace XianPal.Linux;

internal sealed class DesktopValidation : Application
{
    private static string evidence;

    internal static int Start(string path)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Native desktop validation requires Linux.");
        evidence = Path.GetFullPath(path);
        Directory.CreateDirectory(evidence);
        string state = Path.Combine(evidence, "state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(state);
        Environment.SetEnvironmentVariable("FUFU_TEST_MODE", "1");
        Environment.SetEnvironmentVariable("FUFU_STATE_DIR", state);
        UiLanguage.Set("zh-CN");
        Environment.SetEnvironmentVariable("PYTHONIOENCODING", "utf-8");
        string fixture = Path.Combine(evidence, "claude-fixture.py");
        File.WriteAllText(fixture, """
#!/usr/bin/env python3
import json, sys, time
request = sys.stdin.read()
print(json.dumps({"type":"system","subtype":"init","session_id":"d0325306-7425-4231-a8ed-c3f2de69e87b"}), flush=True)
for text in ["道友，", "中文🌱", "回复已完成。"]:
    print(json.dumps({"type":"stream_event","event":{"delta":{"type":"text_delta","text":text}}}, ensure_ascii=False), flush=True)
    time.sleep(.03)
print(json.dumps({"type":"result","result":"道友，中文🌱回复已完成。","is_error":False}), flush=True)
""");
        File.SetUnixFileMode(fixture, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Environment.SetEnvironmentVariable("FUFU_TEST_CLAUDE_COMMAND", fixture);
        return AppBuilder.Configure<DesktopValidation>().UsePlatformDetect().WithDesktopFonts().WithLinuxRendering().StartWithClassicDesktopLifetime(Array.Empty<string>());
    }

    public override void Initialize() => Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime;
        var pet = new PetWindow();
        desktop.MainWindow = pet;
        bool verificationStarted = false;
        pet.Opened += async (_, _) =>
        {
            if (verificationStarted) return;
            verificationStarted = true;
            try
            {
                VerifyGlyphs(DesktopFonts.TextFamily, new uint[] { '道', '友', '中', '文', '回', '复' });
                VerifyGlyphs(DesktopFonts.EmojiFamily, new uint[] { 0x1f331, 0x1f642, 0x1f680 });
                await Task.Delay(400);
                foreach (PetCharacter character in PetCharacter.All)
                {
                    pet.SelectCharacter(character);
                    await Task.Delay(80);
                    Capture(pet, character.Id + ".png");
                }
                pet.SelectCharacter(PetCharacter.All[0]);
                pet.OpenConversation();
                await Task.Delay(300);
                pet.Conversation.SendMessage("请输出中文和表情。");
                await WaitUntil(() => !pet.Conversation.IsRunning);
                if (pet.Conversation.LastReply != "道友，中文🌱回复已完成。")
                    throw new Exception("Native conversation did not display the complete Unicode stream. Received: " + pet.Conversation.LastReply);
                Capture(pet.Conversation, "conversation.png");
                string before = pet.Conversation.LastReply;
                ClaudeTaskService tasks = pet.Services.Tasks;
                ScheduledClaudeTask scheduled = tasks.Add("隔离任务", Program.StateDirectory, "interval",
                    TimeSpan.Zero, DayOfWeek.Monday, 5, DateTime.Now);
                tasks.RunNow(scheduled);
                await WaitUntil(() => scheduled.History.Count > 0 && scheduled.History[0].Status != "running");
                if (scheduled.History[0].Status != "success" || !scheduled.History[0].Transcript.Contains("中文🌱"))
                    throw new Exception("Native scheduled task output failed.");
                if (pet.Conversation.LastReply != before) throw new Exception("A scheduled task leaked into regular chat.");
                await LinuxNativeValidation.Run(pet, evidence);
                await LinuxUiValidation.Run(pet, desktop, evidence);
                await pet.ValidateMotion(evidence);
                await LinuxInstallerValidation.Run(pet, evidence);
                TaskWindow.Show(pet, tasks);
                ReminderWindows.ShowSettings(pet, pet.Services.Reminders);
                pet.Services.Reminders.TriggerForPreview(new DailyReminder { Text = "仙伴提醒测试", Effect = "popup" });
                await Task.Delay(200);
                Window popup = desktop.Windows.Last(window => window.Title.Contains("提醒") && !window.Title.Contains("倒计时"));
                Capture(popup, "reminder-popup.png");
                popup.Close();
                pet.Services.Reminders.TriggerForPreview(new DailyReminder { Text = "仙伴演绎测试", Effect = "impact" });
                await Task.Delay(1200);
                Window performance = desktop.Windows.Single(window => window.Title == "仙伴 · 全屏提醒");
                if (performance.WindowState != WindowState.FullScreen) throw new Exception("Reminder performance did not enter full screen.");
                Capture(performance, "reminder-performance.png");
                performance.Close();
                UiLanguage.Set("en");
                var sect = CompanionRolePresets.Create("Cloudline Workshop", Program.StateDirectory, true);
                sect.Goal = "Inspect the isolated example and record the result. Demonstration data; no live model calls.";
                foreach (var member in sect.Members) member.Name = UiLanguage.Text(PetCharacter.Find(member.CharacterId).Name);
                pet.Services.Workshop.Sects.Add(sect);
                pet.Services.SaveWorkshop();
                WorkshopWindow.Show(pet, pet.Services);
                await Task.Delay(500);
                foreach (Window window in desktop.Windows.Where(window => window != pet && window != pet.Conversation).ToArray())
                    Capture(window, window.Name == "WorkshopWindow" ? "workshop.png" : window.Title == UiLanguage.Text("提醒与倒计时") ? "reminders.png" : "tasks.png");
                Window workshop = desktop.Windows.Single(window => window.Name == "WorkshopWindow");
                NativeHallView hall = workshop.GetVisualDescendants().OfType<NativeHallView>().Single();
                foreach (var size in new[] { new Size(620, 560), new Size(1100, 820), new Size(800, 690) })
                {
                    workshop.Width = size.Width; workshop.Height = size.Height;
                    await Task.Delay(100);
                    if (!hall.HasBackground || hall.SeatCount != 4 || hall.Bounds.Width > workshop.ClientSize.Width || hall.Bounds.Height < 200)
                        throw new Exception("Native hall lost its background or proportional workstations after resize.");
                }
                pet.Services.StartSect(sect);
                await WaitUntil(() => !pet.Services.Execution.IsRunning);
                if (pet.Services.Execution.Record.Status != "success" || pet.Services.Execution.Record.Transfers.Count == 0)
                    throw new Exception("Native four-agent execution or handoffs failed.");
                await Task.Delay(200);
                Capture(workshop, "workshop-completed.png");
                File.WriteAllText(Path.Combine(evidence, "result.txt"),
                    "PASS native Linux desktop: four characters, Unicode conversation, isolated task history, reminders UI and four-agent handoffs.\n" +
                    "Runtime: .NET " + Environment.Version + "\nArchitecture: " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture +
                    "\nRenderer preference: " + (Environment.GetEnvironmentVariable("XIANPAL_RENDERER") ?? "auto") + "\n");
                desktop.Shutdown(0);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(evidence, "failure.txt"), error.ToString());
                Console.Error.WriteLine(error);
                desktop.Shutdown(1);
            }
        };
        base.OnFrameworkInitializationCompleted();
    }

    private static async Task WaitUntil(Func<bool> predicate)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(45);
        while (!predicate())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Native desktop validation timed out.");
            await Task.Delay(60);
        }
    }

    private static void VerifyGlyphs(string family, uint[] scalars)
    {
        if (!FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var font))
            throw new Exception("Required bundled font or glyph is unavailable: " + family);
        foreach (uint scalar in scalars)
        {
#if XIANPAL_LINUX_COMPAT
            bool found = font.TryGetGlyph(scalar, out ushort glyph) && glyph != 0;
#else
            bool found = font.CharacterToGlyphMap.TryGetGlyph((int)scalar, out ushort glyph) && glyph != 0;
#endif
            if (!found) throw new Exception("Required bundled glyph is unavailable: " + family + " U+" + scalar.ToString("X"));
        }
    }

    internal static void Capture(Window window, string filename)
    {
        var size = new PixelSize(Math.Max(1, (int)window.ClientSize.Width), Math.Max(1, (int)window.ClientSize.Height));
        using var bitmap = new RenderTargetBitmap(size);
        bitmap.Render(window);
        bitmap.Save(Path.Combine(evidence, filename));
    }
}
