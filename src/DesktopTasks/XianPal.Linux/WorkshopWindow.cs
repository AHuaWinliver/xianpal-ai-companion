// SPDX-License-Identifier: Apache-2.0
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace XianPal.Linux;

internal static class WorkshopWindow
{
    internal static void Show(PetWindow pet, DesktopServices service, string selectedName = null)
    {
        var sects = new ComboBox { ItemsSource = service.Workshop.Sects, SelectedIndex = 0, MinWidth = 220 };
        if (selectedName != null) sects.SelectedItem = service.Workshop.Sects.Find(sect => sect.Name == selectedName);
        var goal = DesktopUi.DataLabel("");
        var hall = new NativeHallView();
        var detail = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, MaxHeight = 240 };
        var logs = new ComboBox { MinWidth = 220 };
        var output = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80 };
        var editor = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 160 };
        var events = new ListBox { Height = 110 };
        var eventTitle = new TextBox { Watermark = "事件名称" };
        var eventDate = new CalendarDatePicker { SelectedDate = DateTime.Today };
        var eventTime = new TimePicker { ClockIdentifier = "24HourClock", SelectedTime = DateTime.Now.TimeOfDay };
        var repeat = new ComboBox { ItemsSource = new[] { "一次", "每日", "每周" }, SelectedIndex = 0 };
        string selectedMember = null;
        SectRun selectedRun = null;
        string logSignature = null;
        bool updatingLogs = false;
        Window window = null;
        var json = new CompatibleJson { MaxJsonLength = 8388608 };
        var hallScroll = new ScrollViewer { Content = hall, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MinHeight = 180 };
        hallScroll.SizeChanged += (_, _) => hall.SetViewport(hallScroll.Viewport);
        hallScroll.ScrollChanged += (_, _) => hall.SetViewport(hallScroll.Viewport);
        var scene = new Border { Child = hallScroll, CornerRadius = new CornerRadius(12) };
        void InspectMember()
        {
            var step = selectedRun?.Steps.LastOrDefault(item => item.Member == selectedMember);
            string text = step == null ? (sects.SelectedItem as CompanionSect)?.Members.Find(member => member.Name == selectedMember)?.Role ?? "" :
                UiLanguage.Text("Task:") + "\n" + step.Prompt + "\n\n" + UiLanguage.Text("Claude reply:") + "\n" + step.Output;
            if (detail.Text != text) detail.Text = text;
        }
        hall.MemberSelected += name => { selectedMember = name; InspectMember(); };
        void RefreshHall()
        {
            if (updatingLogs) return;
            var sect = sects.SelectedItem as CompanionSect;
            goal.Text = sect?.Goal ?? UiLanguage.Text("创建一个门派并填写共同目标。");
            var records = service.Workshop.History.Where(record => record.Sect == sect?.Name).ToArray();
            string signature = string.Join("|", records.Select(record => record.Started + record.Status));
            if (signature != logSignature)
            {
                string previous = selectedRun?.Started;
                bool latest = logs.SelectedIndex <= 0;
                updatingLogs = true;
                try
                {
                    logs.ItemsSource = new[] { UiLanguage.Text("Latest run") }.Concat(records.Select(record => (DateTime.TryParse(record.Started, out var date) ? date.ToString("g") : record.Started) + " · " + record.Status)).ToArray();
                    int index = Array.FindIndex(records, record => record.Started == previous);
                    logs.SelectedIndex = latest || index < 0 ? 0 : index + 1;
                    logSignature = signature;
                }
                finally { updatingLogs = false; }
            }
            selectedRun = logs.SelectedIndex <= 0 ? records.FirstOrDefault() : records.ElementAtOrDefault(logs.SelectedIndex - 1);
            hall.SetScene(sect, selectedRun);
            InspectMember();
            string transfers = selectedRun == null ? "" : string.Join("\n\n", selectedRun.Transfers.Select(transfer => $"{transfer.From} → {transfer.To}\n{transfer.Text}"));
            if (output.Text != transfers) output.Text = transfers;
        }
        logs.SelectionChanged += (_, _) => RefreshHall();
        void RefreshSects(CompanionSect select)
        {
            sects.ItemsSource = service.Workshop.Sects.ToArray();
            sects.SelectedItem = select;
            RefreshHall();
        }
        void SelectSect()
        {
            if (sects.SelectedItem is CompanionSect sect) editor.Text = json.Serialize(sect);
            RefreshHall();
        }
        sects.SelectionChanged += (_, _) => SelectSect();
        var create = DesktopUi.Action("创建四道友门派", async () =>
        {
            string directory = await DesktopUi.Folder(window, "门派工作目录");
            if (directory == null) return;
            CompanionSect sect = CompanionRolePresets.Create("道友门派 " + (service.Workshop.Sects.Count + 1), directory, false);
            service.Workshop.Sects.Add(sect);
            service.SaveWorkshop();
            RefreshSects(sect);
        });
        var runButton = DesktopUi.Action("执行门派任务", () =>
        {
            try { if (sects.SelectedItem is CompanionSect sect) service.StartSect(sect); }
            catch (Exception error) { DesktopUi.Notice(window, "门派未启动", error.Message); }
        });
        var save = DesktopUi.Action("保存门派配置", () =>
        {
            try
            {
                CompanionSect sect = json.Deserialize<CompanionSect>(editor.Text);
                SectExecution.Validate(sect);
                if (service.Execution?.IsRunning == true) throw new InvalidOperationException("请先完成或取消当前任务。");
                var current = sects.SelectedItem as CompanionSect;
                if (service.Workshop.Sects.Any(item => item != current && item.Name == sect.Name))
                    throw new ArgumentException("门派名称重复。");
                int index = service.Workshop.Sects.IndexOf(current);
                if (index < 0) service.Workshop.Sects.Add(sect); else service.Workshop.Sects[index] = sect;
                service.SaveWorkshop();
                RefreshSects(sect);
            }
            catch (Exception error) { DesktopUi.Notice(window, "门派未保存", error.Message); }
        });
        void RefreshEvents() => events.ItemsSource = service.Workshop.Events.Select(plan => $"{(plan.Done ? "已处理" : "待提醒")} · {plan.When:g} · {plan.Title}").ToArray();
        var addEvent = DesktopUi.Action("添加事件", () =>
        {
            if (string.IsNullOrWhiteSpace(eventTitle.Text)) return;
            DateTime when = (eventDate.SelectedDate ?? DateTime.Today).Date + (eventTime.SelectedTime ?? TimeSpan.Zero);
            service.Workshop.Events.Add(new PlannedEvent
            {
                Title = eventTitle.Text.Trim(), When = when, Repeat = new[] { "once", "daily", "weekly" }[repeat.SelectedIndex],
                Sect = (sects.SelectedItem as CompanionSect)?.Name ?? ""
            });
            service.SaveWorkshop();
            RefreshEvents();
        });
        var hallPage = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,5,110,Auto,70,Auto"), RowSpacing = 6 };
        Control[] sections = {
            DesktopUi.Row(sects, logs), goal, scene,
            new GridSplitter { ResizeDirection = GridResizeDirection.Rows, ResizeBehavior = GridResizeBehavior.PreviousAndNext, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, Height = 5, Background = new SolidColorBrush(Color.Parse("#829993")) },
            detail, DesktopUi.Label("交接记录"), output,
            DesktopUi.Row(create, runButton, DesktopUi.Action("取消运行", service.CancelSect))
        };
        for (int i = 0; i < sections.Length; i++) { Grid.SetRow(sections[i], i); hallPage.Children.Add(sections[i]); }
        var tabs = new TabControl
        {
            ItemsSource = new[]
            {
                new TabItem { Header = "门派大堂", Content = hallPage },
                new TabItem { Header = "成员与任务配置", Content = DesktopUi.Stack(DesktopUi.Label("配置门派名称、共同目标、成员职责、目录和任务依赖。"), editor, save) },
                new TabItem { Header = "事件规划", Content = DesktopUi.Stack(eventTitle, DesktopUi.Row(eventDate, eventTime, repeat), addEvent, events,
                    DesktopUi.Action("删除事件", () => { if (events.SelectedIndex >= 0) { service.Workshop.Events.RemoveAt(events.SelectedIndex); service.SaveWorkshop(); RefreshEvents(); } })) }
            }
        };
        window = DesktopUi.Dialog("仙伴 · 事件规划与门派大堂", tabs, 800, 690);
        window.Name = "WorkshopWindow";
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, (_, _) => RefreshHall());
        window.Closed += (_, _) => { timer.Stop(); hall.Dispose(); };
        SelectSect();
        RefreshEvents();
        window.Show(pet);
        timer.Start();
    }
}
