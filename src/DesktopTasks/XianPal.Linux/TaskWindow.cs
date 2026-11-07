// SPDX-License-Identifier: Apache-2.0
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace XianPal.Linux;

internal static class TaskWindow
{
    private static readonly string[] Periods = { "daily", "weekly", "interval" };
    private static readonly string[] Overlaps = { "block", "parallel" };

    internal static void Show(PetWindow pet, ClaudeTaskService tasks)
    {
        Window dialog = null;
        ScheduledClaudeTask active = null;
        var prompt = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 65, PlaceholderText = "执行任务" };
        var directory = new TextBox { Text = pet.DirectoryPath, PlaceholderText = "工作目录" };
        var period = new ComboBox { ItemsSource = new[] { "每天", "每周", "固定间隔" }, SelectedIndex = 0 };
        var time = new TimePicker { SelectedTime = TimeSpan.FromHours(9), ClockIdentifier = "24HourClock" };
        var weekday = new ComboBox { ItemsSource = new[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" }, SelectedIndex = 1 };
        var interval = new NumericUpDown { Minimum = 1, Maximum = 525600, Value = 60, Width = 95 };
        var overlap = new ComboBox { ItemsSource = new[] { "上次未完成则跳过", "允许并行" }, SelectedIndex = 0 };
        var list = new ListBox { Height = 155 };
        var history = DesktopUi.Action("运行情况", () => { if (active != null) ShowHistory(dialog, tasks, active); });
        ScheduledClaudeTask[] displayed = Array.Empty<ScheduledClaudeTask>();
        bool refreshing = false;

        void UpdateList()
        {
            refreshing = true;
            displayed = tasks.Tasks.ToArray();
            list.ItemsSource = displayed.Select(task => (task.Enabled ? "● " : "○ ") + task.ScheduleText + " · " + task.Prompt.Split('\n')[0]).ToArray();
            list.SelectedIndex = Array.IndexOf(displayed, active);
            refreshing = false;
        }

        void Edit(ScheduledClaudeTask task)
        {
            active = task;
            prompt.Text = task?.Prompt ?? "";
            directory.Text = task?.WorkingDirectory ?? pet.DirectoryPath;
            period.SelectedIndex = task == null ? 0 : Math.Max(0, Array.IndexOf(Periods, task.Recurrence));
            time.SelectedTime = TimeSpan.FromMinutes(task?.MinutesAfterMidnight ?? 540);
            weekday.SelectedIndex = task?.Weekday ?? 1;
            interval.Value = task?.IntervalMinutes ?? 60;
            overlap.SelectedIndex = task?.OverlapMode == "parallel" ? 1 : 0;
            history.IsEnabled = task != null;
        }

        list.SelectionChanged += (_, _) =>
        {
            if (!refreshing && list.SelectedIndex >= 0 && list.SelectedIndex < displayed.Length)
                Edit(displayed[list.SelectedIndex]);
        };
        period.SelectionChanged += (_, _) =>
        {
            time.IsEnabled = period.SelectedIndex != 2;
            weekday.IsEnabled = period.SelectedIndex == 1;
            interval.IsEnabled = period.SelectedIndex == 2;
        };

        void Save()
        {
            try
            {
                string recurrence = Periods[Math.Max(0, period.SelectedIndex)];
                string policy = Overlaps[Math.Max(0, overlap.SelectedIndex)];
                DayOfWeek day = (DayOfWeek)Math.Max(0, weekday.SelectedIndex);
                TimeSpan at = time.SelectedTime ?? TimeSpan.FromHours(9);
                int minutes = (int)(interval.Value ?? 60);
                if (active == null) active = tasks.Add(prompt.Text, directory.Text, recurrence, at, day, minutes, DateTime.Now, "background", policy);
                else tasks.Update(active, prompt.Text, directory.Text, recurrence, at, day, minutes, DateTime.Now, active.RunMode, policy);
                UpdateList();
                Edit(active);
            }
            catch (Exception error) { DesktopUi.Notice(dialog, "定时任务未保存", error.Message); }
        }

        var browse = DesktopUi.Action("选择目录…", async () => { string folder = await DesktopUi.Folder(dialog, "任务工作目录"); if (folder != null) directory.Text = folder; });
        var body = DesktopUi.Stack(prompt, directory, browse, DesktopUi.Row(period, time, weekday, interval),
            DesktopUi.Row(overlap, DesktopUi.Action("保存任务", Save), DesktopUi.Action("新任务", () => { Edit(null); UpdateList(); })),
            DesktopUi.Label("已保存任务"), list,
            DesktopUi.Row(DesktopUi.Action("立即执行", () => { if (active != null) tasks.RunNow(active); }), history,
                DesktopUi.Action("启用 / 停用", () => { if (active != null) tasks.Toggle(active, DateTime.Now); }),
                DesktopUi.Action("删除", () =>
                {
                    if (active == null) return;
                    var item = active;
                    var confirm = DesktopUi.Dialog("删除定时任务", DesktopUi.Label(""), 380, 160);
                    confirm.Content = DesktopUi.Stack(DesktopUi.Label("删除选中的任务和运行记录？"),
                        DesktopUi.Action("删除", () => { tasks.Delete(item); Edit(null); UpdateList(); confirm.Close(); }));
                    confirm.Show(dialog);
                })));
        dialog = DesktopUi.Dialog("Claude Code 定时任务", new ScrollViewer { Content = body }, 645, 525);
        Edit(null);
        UpdateList();
        tasks.Changed += UpdateList;
        dialog.Closed += (_, _) => tasks.Changed -= UpdateList;
        dialog.Show(pet);
    }

    private static void ShowHistory(Window owner, ClaudeTaskService tasks, ScheduledClaudeTask task)
    {
        var records = new ListBox { Height = 145 };
        var detail = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 160, MaxHeight = 260 };
        ClaudeTaskRunRecord selected = null;
        ClaudeTaskRunRecord[] displayed = Array.Empty<ClaudeTaskRunRecord>();
        bool refreshing = false;
        Window window = DesktopUi.Dialog("任务运行情况", DesktopUi.Stack(DesktopUi.Label(task.Prompt), DesktopUi.Label(task.WorkingDirectory), records, detail), 645, 500);
        void Refresh()
        {
            refreshing = true;
            displayed = task.History.ToArray();
            records.ItemsSource = displayed.Select(run => $"{run.StatusText} · {ClaudeTaskService.RunModeLabel(run.RunMode)} · {run.StartedAt}").ToArray();
            selected ??= displayed.FirstOrDefault();
            records.SelectedIndex = Array.IndexOf(displayed, selected);
            string output = selected?.Transcript ?? "";
            if (detail.Text != output) detail.Text = output;
            refreshing = false;
        }
        records.SelectionChanged += (_, _) =>
        {
            if (!refreshing && records.SelectedIndex >= 0)
            {
                selected = displayed[records.SelectedIndex];
                detail.Text = selected.Transcript;
            }
        };
        records.DoubleTapped += (_, _) =>
        {
            if (selected == null) return;
            ClaudeTaskRunRecord run = selected;
            var log = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
            void ShowOutput() { if (log.Text != run.Transcript) log.Text = run.Transcript; }
            ShowOutput();
            var transcript = DesktopUi.Dialog("定时任务运行详情", log, 640, 460);
            tasks.Changed += ShowOutput;
            transcript.Closed += (_, _) => tasks.Changed -= ShowOutput;
            transcript.Show(window);
        };
        Refresh();
        tasks.Changed += Refresh;
        window.Closed += (_, _) => tasks.Changed -= Refresh;
        window.Show(owner);
    }
}
