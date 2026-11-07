// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace XianPal.Linux;

internal static class ReminderWindows
{
    internal static void ShowSettings(PetWindow pet, ReminderService service)
    {
        Window window = null;
        var input = new TextBox { PlaceholderText = "例如：起来活动一下" };
        var time = new TimePicker { ClockIdentifier = "24HourClock", SelectedTime = DateTime.Now.TimeOfDay };
        var effect = new ComboBox { ItemsSource = new[] { "角色演绎", "仅提示框" }, SelectedIndex = 0 };
        var list = new ListBox { MinHeight = 120, MaxHeight = 180 };
        var countdownText = DesktopUi.Label("");
        var countdown = new NumericUpDown { Minimum = 1, Maximum = 10080, Value = 25, Width = 90 };
        DailyReminder[] reminders = Array.Empty<DailyReminder>();
        DailyReminder Selected() => list.SelectedIndex < 0 ? null : reminders.ElementAtOrDefault(list.SelectedIndex);
        void Refresh()
        {
            string selectedId = Selected()?.Id;
            reminders = service.Reminders.ToArray();
            list.ItemsSource = reminders.Select(item => (item.Enabled ? "● " : "○ ") + item.TimeText + " · " + item.Text).ToArray();
            list.SelectedIndex = Array.FindIndex(reminders, reminder => reminder.Id == selectedId);
            countdownText.Text = service.Countdown == null ? "没有进行中的倒计时" : "倒计时结束于 " + service.Countdown.TimeText;
        }
        var presets = DesktopUi.Row();
        foreach (int minutes in new[] { 5, 10, 15, 30, 60, 120 })
            presets.Children.Add(DesktopUi.Action(minutes.ToString(), () => service.StartCountdown(minutes)));
        window = DesktopUi.Dialog("提醒与倒计时", DesktopUi.Stack(DesktopUi.Label("每日提醒"), input,
            DesktopUi.Row(time, effect, DesktopUi.Action("添加", () =>
            {
                try { service.Add(time.SelectedTime ?? TimeSpan.Zero, input.Text, effect.SelectedIndex == 1 ? "popup" : "impact"); input.Text = ""; }
                catch (ArgumentException error) { DesktopUi.Notice(window, "提醒未保存", error.Message); }
            })), list, DesktopUi.Row(DesktopUi.Action("立即测试效果", () =>
            {
                DailyReminder item = Selected() ?? new DailyReminder { Text = "仙伴提醒演示", Effect = effect.SelectedIndex == 1 ? "popup" : "impact" };
                service.TriggerForPreview(item);
            }), DesktopUi.Action("启用 / 停用", () => service.Toggle(Selected())), DesktopUi.Action("删除", () => service.Delete(Selected()))),
            DesktopUi.Label("倒计时预设（分钟）"), presets,
            DesktopUi.Row(countdown, DesktopUi.Action("开始自定义倒计时", () => service.StartCountdown((int)(countdown.Value ?? 25))),
                DesktopUi.Action("取消", service.CancelCountdown)), countdownText), 545, 480);
        Refresh();
        service.Changed += Refresh;
        window.Closed += (_, _) => service.Changed -= Refresh;
        window.Show(pet);
    }

    internal static void ShowAlert(PetWindow pet, ReminderService service, DailyReminder reminder, bool reduceMotion)
    {
        Window window = null;
        var buttons = DesktopUi.Row(DesktopUi.Action("知道了", () => window.Close()));
        if (!reminder.IsCountdown)
            buttons.Children.Add(DesktopUi.Action("稍后 5 分钟", () => { service.Snooze(reminder, 5); window.Close(); }));
        var title = DesktopUi.Label(pet.Character.Name + " · 提醒");
        title.FontSize = 20;
        var content = new Border
        {
            Padding = new Thickness(18), CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.Parse("#FAF6EA")),
            Child = DesktopUi.Stack(title, DesktopUi.Label(reminder.Text), buttons),
            MaxWidth = 520
        };
        if (reduceMotion || reminder.IsCountdown || reminder.Effect == "popup")
        {
            window = DesktopUi.Dialog(title.Text, content, 465, 200);
            window.Topmost = true;
            window.KeyDown += (_, e) => { if (e.Key == Key.Escape) window.Close(); };
            window.Show(pet);
            return;
        }
        window = CreatePerformance(pet, content);
        window.Show(pet);
    }

    private static Window CreatePerformance(PetWindow pet, Border card)
    {
        var stage = new Image { Stretch = Stretch.Uniform };
        card.IsVisible = false;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Bottom;
        card.Margin = new Thickness(18, 18, 18, 70);
        var grid = new Grid();
        grid.Children.Add(stage);
        grid.Children.Add(card);
        var screen = pet.Screens.ScreenFromWindow(pet) ?? pet.Screens.Primary;
        var overlay = new Window
        {
            Title = "仙伴 · 全屏提醒", WindowDecorations = WindowDecorations.None, Topmost = true,
            ShowInTaskbar = false, CanResize = false, Background = new SolidColorBrush(Color.Parse("#E6192628")), Content = grid,
            Position = screen?.Bounds.Position ?? default
        };
        var clock = new Stopwatch();
        Bitmap current = null;
        int frame = -1;
        string id = pet.Character.Id;
        string root = Path.Combine(AppContext.BaseDirectory, "performances", id);
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(32), DispatcherPriority.Render, (_, _) =>
        {
            int next = Math.Min(156, (int)(clock.ElapsedMilliseconds / 40));
            if (next != frame)
            {
                string path = Path.Combine(root, next.ToString("000") + ".png");
                if (File.Exists(path))
                {
                    Bitmap image = new(path);
                    stage.Source = image;
                    current?.Dispose();
                    current = image;
                }
                frame = next;
            }
            if (clock.ElapsedMilliseconds >= 5600) card.IsVisible = true;
        });
        overlay.Opened += (_, _) =>
        {
            overlay.WindowState = WindowState.FullScreen;
            clock.Start();
            timer.Start();
        };
        overlay.KeyDown += (_, e) => { if (e.Key == Key.Escape) overlay.Close(); };
        overlay.Closed += (_, _) => { timer.Stop(); stage.Source = null; current?.Dispose(); };
        return overlay;
    }
}
