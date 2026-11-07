// SPDX-License-Identifier: Apache-2.0
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

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
        return AppBuilder.Configure<DesktopValidation>().UsePlatformDetect().StartWithClassicDesktopLifetime(Array.Empty<string>());
    }

    public override void Initialize() => Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime;
        var pet = new PetWindow();
        desktop.MainWindow = pet;
        pet.Opened += async (_, _) =>
        {
            try
            {
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
                    throw new Exception("Native conversation did not display the complete Unicode stream.");
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
                TaskWindow.Show(pet, tasks);
                ReminderWindows.ShowSettings(pet, pet.Services.Reminders);
                pet.Services.Reminders.TriggerForPreview(new DailyReminder { Text = "ARM64 提醒测试", Effect = "popup" });
                await Task.Delay(200);
                Window popup = desktop.Windows.Last(window => window.Title.Contains("提醒") && !window.Title.Contains("倒计时"));
                Capture(popup, "reminder-popup.png");
                popup.Close();
                pet.Services.Reminders.TriggerForPreview(new DailyReminder { Text = "ARM64 演绎测试", Effect = "impact" });
                await Task.Delay(1200);
                Window performance = desktop.Windows.Single(window => window.Title == "仙伴 · 全屏提醒");
                if (performance.WindowState != WindowState.FullScreen) throw new Exception("Reminder performance did not enter full screen.");
                Capture(performance, "reminder-performance.png");
                performance.Close();
                var sect = CompanionRolePresets.Create("ARM64 验证门派", Program.StateDirectory, true);
                sect.Goal = "读取隔离样例并记录结果，供本次桌面测试验证。";
                pet.Services.Workshop.Sects.Add(sect);
                pet.Services.SaveWorkshop();
                WorkshopWindow.Show(pet, pet.Services);
                await Task.Delay(500);
                foreach (Window window in desktop.Windows.Where(window => window != pet && window != pet.Conversation).ToArray())
                    Capture(window, window.Title.Contains("门派") ? "workshop.png" : window.Title.Contains("倒计时") ? "reminders.png" : "tasks.png");
                pet.Services.StartSect(sect);
                await WaitUntil(() => !pet.Services.Execution.IsRunning);
                if (pet.Services.Execution.Record.Status != "success" || pet.Services.Execution.Record.Transfers.Count == 0)
                    throw new Exception("Native four-agent execution or handoffs failed.");
                File.WriteAllText(Path.Combine(evidence, "result.txt"),
                    "PASS native Linux desktop: four characters, Unicode conversation, isolated task history, reminders UI and four-agent handoffs.");
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

    private static void Capture(Window window, string filename)
    {
        var size = new PixelSize(Math.Max(1, (int)window.ClientSize.Width), Math.Max(1, (int)window.ClientSize.Height));
        using var bitmap = new RenderTargetBitmap(size);
        bitmap.Render(window);
        bitmap.Save(Path.Combine(evidence, filename));
    }
}
