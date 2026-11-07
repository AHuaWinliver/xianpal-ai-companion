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
        var goal = DesktopUi.Label("");
        var hall = new UniformGrid { Columns = 2, Margin = new Thickness(20), MinHeight = 220 };
        var detail = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, MaxHeight = 240 };
        var logs = new ComboBox { MinWidth = 220 };
        var output = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80 };
        var editor = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 160 };
        var events = new ListBox { Height = 110 };
        var eventTitle = new TextBox { PlaceholderText = "事件名称" };
        var eventDate = new CalendarDatePicker { SelectedDate = DateTime.Today };
        var eventTime = new TimePicker { ClockIdentifier = "24HourClock", SelectedTime = DateTime.Now.TimeOfDay };
        var repeat = new ComboBox { ItemsSource = new[] { "一次", "每日", "每周" }, SelectedIndex = 0 };
        var cache = new Dictionary<string, Bitmap>();
        string selectedStep = null;
        Window window = null;
        var json = new CompatibleJson { MaxJsonLength = 8388608 };
        var scene = new Border { Child = hall, CornerRadius = new CornerRadius(12) };
        string background = Path.Combine(AppContext.BaseDirectory, "scenes", "sect-hall-v2.png");
        if (File.Exists(background))
        {
            var bitmap = new Bitmap(background);
            cache.Add(background, bitmap);
            scene.Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
        }
        Bitmap Frame(PetCharacter character, string state, int frame)
        {
            string root = character.ResourceRoot ?? (character.Id == "orbit" ? PetWindow.Assets : Path.Combine(PetWindow.Assets, "characters", character.Id));
            string file = Path.Combine(root, state, frame.ToString("00") + ".png");
            if (!cache.TryGetValue(file, out Bitmap image)) cache.Add(file, image = new Bitmap(file));
            return image;
        }

        void RefreshHall()
        {
            CompanionSect sect = sects.SelectedItem as CompanionSect;
            goal.Text = sect?.Goal ?? "创建一个门派并填写共同目标。";
            SectRun run = service.Workshop.History.FirstOrDefault(run => run.Sect == sect?.Name);
            hall.Children.Clear();
            if (sect == null) return;
            long frame = Environment.TickCount64 / 120;
            foreach (SectMember member in sect.Members)
            {
                SectStepRun step = run?.Steps.LastOrDefault(step => step.Member == member.Name || sect.Steps.Find(item => item.Id == step.Id)?.Member == member.Id);
                string state = step?.Status switch { "running" => "waiting", "failed" => "failed", "success" => "waving", _ => "idle" };
                var sprite = new Image { Source = Frame(PetCharacter.Find(member.CharacterId), state, (int)(frame % CharacterPackCatalog.FrameCount(state))), Height = 105 };
                var title = DesktopUi.Label(member.Name + " · " + (step?.Status ?? "就绪"));
                title.Foreground = Brushes.White;
                var desk = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#DB314B48")), Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(8), Child = DesktopUi.Stack(sprite, title)
                };
                desk.PointerPressed += (_, _) =>
                {
                    selectedStep = step?.Id;
                    detail.Text = step == null ? member.Role : step.Output;
                };
                hall.Children.Add(desk);
            }
            SectStepRun selected = run?.Steps.Find(step => step.Id == selectedStep);
            if (selected != null && detail.Text != selected.Output) detail.Text = selected.Output;
            string transfers = run == null ? "" : string.Join("\n\n", run.Transfers.Select(transfer => $"{transfer.From} → {transfer.To}\n{transfer.Text}"));
            if (output.Text != transfers) output.Text = transfers;
        }

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
        var tabs = new TabControl
        {
            ItemsSource = new[]
            {
                new TabItem { Header = "门派大堂", Content = new ScrollViewer { Content = DesktopUi.Stack(sects, goal, scene, detail, DesktopUi.Label("交接记录"), output, DesktopUi.Row(create, runButton, DesktopUi.Action("取消运行", service.CancelSect))) } },
                new TabItem { Header = "成员与任务配置", Content = DesktopUi.Stack(DesktopUi.Label("配置门派名称、共同目标、成员职责、目录和任务依赖。"), editor, save) },
                new TabItem { Header = "事件规划", Content = DesktopUi.Stack(eventTitle, DesktopUi.Row(eventDate, eventTime, repeat), addEvent, events,
                    DesktopUi.Action("删除事件", () => { if (events.SelectedIndex >= 0) { service.Workshop.Events.RemoveAt(events.SelectedIndex); service.SaveWorkshop(); RefreshEvents(); } })) }
            }
        };
        window = DesktopUi.Dialog("仙伴 · 事件规划与门派大堂", tabs, 800, 690);
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, (_, _) => RefreshHall());
        window.Closed += (_, _) => { timer.Stop(); foreach (Bitmap image in cache.Values) image.Dispose(); };
        SelectSect();
        RefreshEvents();
        window.Show(pet);
        timer.Start();
    }
}
