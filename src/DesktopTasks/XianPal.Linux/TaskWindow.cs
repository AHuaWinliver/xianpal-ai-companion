// SPDX-License-Identifier: Apache-2.0
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Layout;

namespace XianPal.Linux;

internal static class TaskWindow
{
    private static readonly string[] Periods = { "daily", "weekly", "interval" };
    private static readonly string[] Overlaps = { "block", "parallel" };
    private static readonly string[] Modes = { "background", "interactive-terminal", "claude-background" };

    internal static void Show(PetWindow pet, ClaudeTaskService tasks)
    {
        Window dialog = null;
        ScheduledClaudeTask active = null;
        var profiles = PetCharacter.Available;
        var ownerIds = profiles.Select(profile => profile.Id).ToList();
        var ownerNames = profiles.Select(profile => profile.Name).ToList();
        var companion = new ComboBox { ItemsSource = ownerNames.ToArray(), SelectedIndex = Math.Max(0, ownerIds.IndexOf(pet.Character.Id)) };
        var avatars = new Dictionary<string, Bitmap>();
        Bitmap Avatar(string id)
        {
            var profile = PetCharacter.Available.FirstOrDefault(profile => profile.Id == id);
            if (profile == null) return null;
            string key = profile.AvatarPath + "|" + profile.ResourceRevision;
            if (avatars.TryGetValue(key, out var image)) return image;
            try { image = new Bitmap(profile.AvatarPath); avatars[key] = image; return image; }
            catch (Exception error) when (error is IOException or ArgumentException) { return null; }
        }
        var prompt = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 65, Watermark = "执行任务" };
        var directory = new TextBox { Text = pet.DirectoryPath, Watermark = "工作目录" };
        var period = new ComboBox { ItemsSource = new[] { "每天", "每周", "固定间隔" }, SelectedIndex = 0 };
        var time = new TimePicker { SelectedTime = TimeSpan.FromHours(9), ClockIdentifier = "24HourClock" };
        var weekday = new ComboBox { ItemsSource = new[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" }, SelectedIndex = 1 };
        var interval = new NumericUpDown { Minimum = 1, Maximum = 525600, Value = 60, Width = 95 };
        var overlap = new ComboBox { ItemsSource = new[] { "上次未完成则跳过", "允许并行" }, SelectedIndex = 0 };
        var list = new ListBox { Height = 185 };
        list.ItemTemplate = new FuncDataTemplate<ScheduledClaudeTask>((task, _) =>
        {
            var image = new Image { Source = Avatar(task.CharacterId), Width = 44, Height = 48, Stretch = Stretch.Uniform };
            var body = DesktopUi.Stack(DesktopUi.Label(PetCharacter.OwnerName(task.CharacterId) + (task.Enabled ? " · ●" : " · ○")),
                DesktopUi.Label(task.Prompt.Split('\n')[0]), DesktopUi.Label(task.ScheduleText + " · " + task.WorkingDirectory));
            body.Spacing = 3;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("54,*"), Margin = new Avalonia.Thickness(3, 5) };
            row.Children.Add(image); Grid.SetColumn(body, 1); row.Children.Add(body);
            return row;
        });
        var mode = new ComboBox { ItemsSource = new[] { "后台输出", "原生 CLI 终端", "Claude Background" }, SelectedIndex = 0 };
        var history = DesktopUi.Action("运行情况", () => { if (active != null) ShowHistory(pet, dialog, tasks, active); });
        ScheduledClaudeTask[] displayed = Array.Empty<ScheduledClaudeTask>();
        bool refreshing = false;

        void UpdateList()
        {
            refreshing = true;
            displayed = tasks.Tasks.ToArray();
            list.ItemsSource = displayed;
            list.SelectedIndex = Array.IndexOf(displayed, active);
            refreshing = false;
        }

        void Edit(ScheduledClaudeTask task)
        {
            active = task;
            string actor = task?.CharacterId ?? pet.Character.Id;
            if (!ownerIds.Contains(actor))
            {
                ownerIds.Add(actor); ownerNames.Add(PetCharacter.OwnerName(actor));
                companion.ItemsSource = ownerNames.ToArray();
            }
            companion.SelectedIndex = ownerIds.IndexOf(actor);
            prompt.Text = task?.Prompt ?? "";
            directory.Text = task?.WorkingDirectory ?? pet.DirectoryPath;
            period.SelectedIndex = task == null ? 0 : Math.Max(0, Array.IndexOf(Periods, task.Recurrence));
            time.SelectedTime = TimeSpan.FromMinutes(task?.MinutesAfterMidnight ?? 540);
            weekday.SelectedIndex = task?.Weekday ?? 1;
            interval.Value = task?.IntervalMinutes ?? 60;
            overlap.SelectedIndex = task?.OverlapMode == "parallel" ? 1 : 0;
            mode.SelectedIndex = task == null || task.RunMode == "background" ? 0 : task.RunMode == "claude-background" ? 2 : 1;
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
                string runMode = Modes[Math.Max(0, mode.SelectedIndex)];
                string actor = ownerIds[Math.Max(0, companion.SelectedIndex)];
                if (active == null) active = tasks.Add(prompt.Text, directory.Text, recurrence, at, day, minutes, DateTime.Now, runMode, policy, actor);
                else tasks.Update(active, prompt.Text, directory.Text, recurrence, at, day, minutes, DateTime.Now, runMode, policy, actor);
                UpdateList();
                Edit(active);
            }
            catch (Exception error) { DesktopUi.Notice(dialog, "定时任务未保存", error.Message); }
        }

        var browse = DesktopUi.Action("选择目录…", async () => { string folder = await DesktopUi.Folder(dialog, "任务工作目录"); if (folder != null) directory.Text = folder; });
        var body = DesktopUi.Stack(prompt, directory, browse, DesktopUi.Row(period, time, weekday, interval),
            DesktopUi.Row(DesktopUi.Label("执行道友"), companion), DesktopUi.Row(mode, overlap),
            DesktopUi.Row(DesktopUi.Action("保存任务", Save), DesktopUi.Action("新任务", () => { Edit(null); UpdateList(); })),
            DesktopUi.Label("全部道友任务"), list,
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
        dialog = DesktopUi.Dialog("Claude Code 定时任务", new ScrollViewer { Content = body }, 700, 640);
        Edit(null);
        UpdateList();
        tasks.Changed += UpdateList;
        dialog.Closed += (_, _) => { tasks.Changed -= UpdateList; foreach (var image in avatars.Values) image.Dispose(); };
        dialog.Show(pet);
    }

    private static void ShowHistory(PetWindow pet, Window owner, ClaudeTaskService tasks, ScheduledClaudeTask task)
    {
        var records = new ListBox { Height = 145 };
        var detail = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 160, MaxHeight = 260 };
        ClaudeTaskRunRecord selected = null;
        ClaudeTaskRunRecord[] displayed = Array.Empty<ClaudeTaskRunRecord>();
        bool refreshing = false;
        var resume = DesktopUi.Action("在 Claude CLI 打开", () =>
        {
            try
            {
                bool background = selected?.RunMode == "claude-background";
                string session = background ? selected?.BackgroundJobId : selected?.BackgroundSessionId;
                if (string.IsNullOrWhiteSpace(session)) return;
                LinuxTerminal.OpenClaude(task.WorkingDirectory, selected?.CharacterId ?? task.CharacterId, session, background);
            }
            catch (Exception error) { DesktopUi.Notice(owner, "无法打开会话", error.Message); }
        });
        Window window = DesktopUi.Dialog("任务运行情况", DesktopUi.Stack(DesktopUi.Label(task.Prompt), DesktopUi.Label(task.WorkingDirectory), records, detail, resume), 645, 520);
        void Refresh()
        {
            refreshing = true;
            displayed = task.History.ToArray();
            string[] rows = displayed.Select(run => $"{PetCharacter.OwnerName(run.CharacterId)} · {run.StatusText} · {ClaudeTaskService.RunModeLabel(run.RunMode)} · {run.StartedAt}").ToArray();
            if (records.ItemsSource is not string[] previous || !previous.SequenceEqual(rows)) records.ItemsSource = rows;
            selected ??= displayed.FirstOrDefault();
            records.SelectedIndex = Array.IndexOf(displayed, selected);
            string output = selected?.Transcript ?? "";
            if (detail.Text != output) detail.Text = output;
            resume.IsEnabled = !string.IsNullOrEmpty(selected?.RunMode == "claude-background" ? selected.BackgroundJobId : selected?.BackgroundSessionId);
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
            var logTimer = new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(250), Avalonia.Threading.DispatcherPriority.Background, (_, _) => ShowOutput());
            logTimer.Start();
            transcript.Closed += (_, _) => { tasks.Changed -= ShowOutput; logTimer.Stop(); };
            transcript.Show(window);
        };
        Refresh();
        tasks.Changed += Refresh;
        var liveTimer = new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(300), Avalonia.Threading.DispatcherPriority.Background, (_, _) => Refresh());
        liveTimer.Start();
        window.Closed += (_, _) => { tasks.Changed -= Refresh; liveTimer.Stop(); };
        window.Show(owner);
    }
}
