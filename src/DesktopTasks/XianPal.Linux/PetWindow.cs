// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace XianPal.Linux;

internal sealed class DesktopPreferences
{
    public string Character { get; set; } = "orbit";
    public string Directory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public bool Topmost { get; set; } = true;
    public bool ReduceMotion { get; set; }
    public double DockedOpacity { get; set; } = .6;
    public int Size { get; set; } = 144;
    public int? X { get; set; }
    public int? Y { get; set; }
}

internal sealed class PetWindow : Window
{
    internal static string Assets => Environment.GetEnvironmentVariable("XIANPAL_ASSETS") ?? Path.Combine(AppContext.BaseDirectory, "assets");
    private static string PreferencesPath => Path.Combine(Program.StateDirectory, "desktop.json");
    private readonly DesktopPreferences preferences;
    private readonly CharacterPackCatalog catalog;
    private readonly DesktopServices services;
    private LinuxDesktopShell shell;
    private bool started;
    private readonly Image sprite = new() { Stretch = Stretch.Uniform };
    private readonly DragGaitClock gait = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly DispatcherTimer timer;
    private readonly Dictionary<string, Bitmap> frames = new();
    private CharacterAnimationSet externalFrames;
    private ConversationWindow conversation;
    private Window reply;
    private TextBlock replyLabel;
    private string currentReply;
    private IPointer dragPointer;
    private PixelPoint dragOrigin, pointerOrigin, lastPointer;
    private long lastMotion, lastTick, gestureEnd, nextRoam, roamStart;
    private bool left, dragging, hovering, positioning;
    private string gesture = "waving";
    private PixelPoint? roamTarget;

    internal PetCharacter Character { get; private set; }
    internal string DirectoryPath => preferences.Directory;
    internal bool ChatWorking { get; set; }
    internal bool IsClosing { get; private set; }
    internal ConversationWindow Conversation => conversation;
    internal DesktopServices Services => services;
    internal void SelectCharacter(PetCharacter character) => LoadCharacter(character);

    internal PetWindow()
    {
        preferences = WorkshopFiles.Load<DesktopPreferences>(PreferencesPath);
        if (!Directory.Exists(preferences.Directory)) preferences.Directory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        catalog = new CharacterPackCatalog(Path.Combine(NativePaths.UserDirectory("XDG_DATA_HOME", ".local", "share"), "Characters"));
        Rescan();
        Character = PetCharacter.Find(preferences.Character);
        LoadCharacter(Character);
        Title = "仙伴 · XianPal";
        Width = Math.Clamp(preferences.Size, 80, 260); Height = Width * 416 / 384;
        CanResize = false;
        DesktopUi.RemoveDecorations(this);
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        TransparencyBackgroundFallback = new SolidColorBrush(Color.Parse("#F6F1E7"));
        Background = Brushes.Transparent;
        Topmost = preferences.Topmost;
        Content = new Border { Background = Brushes.Transparent, Child = sprite };
        PointerEntered += (_, _) => { if (!hovering && !ChatWorking) Gesture("waving", 700); hovering = true; };
        PointerExited += (_, _) => hovering = false;
        PointerPressed += Pressed;
        PointerMoved += Moved;
        PointerReleased += Released;
        PointerCaptureLost += (_, _) => FinishDrag();
        PositionChanged += (_, _) => AnchorReply();
        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, (_, e) => e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None);
        DragDrop.AddDropHandler(this, Dropped);
        ContextMenu = BuildMenu();
        services = new DesktopServices(() => Character);
        services.Reminders.ReminderDue += reminder => ReminderWindows.ShowAlert(this, services.Reminders, reminder, preferences.ReduceMotion);
        services.EventDue += plan =>
        {
            var dialog = DesktopUi.Dialog("事件到点", DesktopUi.Stack(DesktopUi.Label(plan.Title),
                DesktopUi.Action("查看事件和门派", () => WorkshopWindow.Show(this, services, plan.Sect))), 410, 180);
            dialog.Show(this);
        };
        services.Failure += message => DesktopUi.Notice(this, "门派运行暂停", message);
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Background, (_, _) => Animate());
        Opened += (_, _) =>
        {
            if (started) return;
            started = true;
            PixelRect area = Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1024, 768);
            Position = Fit(new PixelPoint(preferences.X ?? area.Right - (int)Width - 30, preferences.Y ?? area.Bottom - (int)Height - 45));
            nextRoam = clock.ElapsedMilliseconds + Random.Shared.Next(12000, 22000);
            timer.Start();
            services.Start();
            shell = new LinuxDesktopShell(this);
        };
        Closing += (_, _) => IsClosing = true;
        Closed += (_, _) =>
        {
            timer.Stop();
            shell?.Dispose();
            services.Dispose();
            externalFrames?.Dispose();
            DisposeFrames();
        };
    }

    private void Save()
    {
        preferences.X = Position.X; preferences.Y = Position.Y;
        WorkshopFiles.Save(PreferencesPath, preferences);
    }

    private void DisposeFrames()
    {
        foreach (Bitmap image in frames.Values) image.Dispose();
        frames.Clear();
    }

    private void LoadCharacter(PetCharacter next)
    {
        CharacterAnimationSet replacement = null;
        if (next.IsExternal) next = CharacterPackCatalog.ReadPack(next.ResourceRoot, true, out replacement);
        sprite.Source = null;
        DisposeFrames();
        externalFrames?.Dispose();
        externalFrames = replacement;
        Character = next;
        preferences.Character = next.Id;
        RenderFrame("idle", 0);
    }

    private void RenderFrame(string state, int index)
    {
        if (externalFrames != null) { sprite.Source = externalFrames.Frames[state][index]; return; }
        string key = state + "/" + index.ToString("00") + ".png";
        if (!frames.TryGetValue(key, out Bitmap frame))
        {
            string root = Character.Id == "orbit" ? Assets : Path.Combine(Assets, "characters", Character.Id);
            frame = new Bitmap(Path.Combine(root, key));
            frames.Add(key, frame);
        }
        sprite.Source = frame;
    }

    private void Rescan()
    {
        using CharacterPackScan scan = catalog.Scan(Character?.Id ?? preferences.Character, Character?.ResourceRevision);
        PetCharacter.SetExternalProfiles(scan.Characters);
        if (Character != null && scan.PreparedId == Character.Id && scan.PreparedAssets != null)
        {
            sprite.Source = null;
            externalFrames?.Dispose();
            externalFrames = scan.PreparedAssets;
            scan.PreparedAssets = null;
            Character = PetCharacter.Find(scan.PreparedId);
            RenderFrame("idle", 0);
        }
    }

    private void Gesture(string name, int milliseconds)
    {
        if (preferences.ReduceMotion) return;
        gesture = name;
        gestureEnd = clock.ElapsedMilliseconds + milliseconds;
    }

    private void Animate()
    {
        long now = clock.ElapsedMilliseconds;
        if (!IsVisible) { lastTick = now; return; }
        double elapsed = Math.Min(50, now - lastTick);
        lastTick = now;
        if (now / 8000 != (now - (long)elapsed) / 8000) Rescan();
        if (preferences.ReduceMotion) { RenderFrame(ChatWorking ? "waiting" : "idle", 0); return; }
        if (dragging && now - lastMotion < DragGaitClock.StopAfterMs)
        {
            RenderFrame(left ? "running-left" : "running-right", gait.Advance(elapsed, now - lastMotion, 24));
            return;
        }
        if (!dragging && !hovering && !ChatWorking && conversation?.IsVisible != true && Opacity == 1)
        {
            if (roamTarget == null && now >= nextRoam) StartRoam();
            if (roamTarget is PixelPoint target)
            {
                double dx = target.X - Position.X, dy = target.Y - Position.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                double advance = Math.Max(1, elapsed * .13);
                if (distance <= advance)
                {
                    Position = target;
                    roamTarget = null;
                }
                else
                {
                    left = dx < 0;
                    Position = Fit(new PixelPoint(Position.X + (int)Math.Round(dx * advance / distance), Position.Y + (int)Math.Round(dy * advance / distance)));
                    RenderFrame(left ? "running-left" : "running-right", (int)((now - roamStart) / 25 % 24));
                    return;
                }
            }
        }
        else roamTarget = null;
        string state = ChatWorking ? "waiting" : now < gestureEnd ? gesture : "idle";
        int count = CharacterPackCatalog.FrameCount(state);
        RenderFrame(state, (int)(now / (count == 8 ? 145 : 37) % count));
    }

    private void StartRoam()
    {
        long now = clock.ElapsedMilliseconds;
        nextRoam = now + Random.Shared.Next(16000, 35000);
        roamStart = now;
        roamTarget = Fit(new PixelPoint(Position.X + Random.Shared.Next(-150, 151), Position.Y + Random.Shared.Next(-50, 51)));
    }

    private PixelPoint Fit(PixelPoint point)
    {
        PixelRect area = (Screens.ScreenFromPoint(point) ?? Screens.ScreenFromWindow(this) ?? Screens.Primary)?.WorkingArea ?? new PixelRect(0, 0, 1024, 768);
        int width = (int)Math.Ceiling(Width * RenderScaling), height = (int)Math.Ceiling(Height * RenderScaling);
        return new PixelPoint(Math.Clamp(point.X, area.X, Math.Max(area.X, area.Right - width)), Math.Clamp(point.Y, area.Y, Math.Max(area.Y, area.Bottom - height)));
    }

    private void Pressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            reply?.Hide();
            ContextMenu = BuildMenu();
            ContextMenu.Open(this);
            e.Handled = true;
            return;
        }
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2) { OpenConversation(); return; }
        dragPointer = e.Pointer;
        pointerOrigin = lastPointer = this.PointToScreen(e.GetPosition(this));
        dragOrigin = Position;
        lastMotion = clock.ElapsedMilliseconds;
        gait.Reset();
        e.Pointer.Capture(this);
    }

    private void Moved(object sender, PointerEventArgs e)
    {
        if (e.Pointer != dragPointer) return;
        PixelPoint pointer = this.PointToScreen(e.GetPosition(this));
        var delta = pointer - pointerOrigin;
        if (!dragging && Math.Abs(delta.X) + Math.Abs(delta.Y) < 6) return;
        var change = pointer - lastPointer;
        if (change.X == 0 && change.Y == 0) return;
        dragging = true;
        Opacity = 1;
        long now = clock.ElapsedMilliseconds;
        gait.RecordMotion(Math.Sqrt((double)change.X * change.X + (double)change.Y * change.Y), now - lastMotion);
        if (change.X != 0) left = change.X < 0;
        lastPointer = pointer;
        lastMotion = now;
        Position = Fit(dragOrigin + delta);
        AnchorConversation();
    }

    private void Released(object sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer != dragPointer) return;
        bool moved = dragging;
        FinishDrag();
        e.Pointer.Capture(null);
        if (!moved) ShowLatestReply(Character.Quotes[Random.Shared.Next(Character.Quotes.Length)]);
    }

    private void FinishDrag()
    {
        dragPointer = null;
        if (!dragging) return;
        dragging = false;
        gait.Reset();
        var screen = Screens.ScreenFromWindow(this);
        if (screen != null)
        {
            PixelRect work = screen.WorkingArea;
            int right = work.Right - (int)Math.Ceiling(Width * RenderScaling);
            int edge = Position.X - work.X < 15 ? work.X : right - Position.X < 15 ? right : Position.X;
            if (edge == work.X || edge == right)
            {
                Position = Fit(new PixelPoint(edge, Position.Y));
                Opacity = Math.Clamp(preferences.DockedOpacity, .2, 1);
            }
        }
        Save();
    }

    internal void OpenConversation()
    {
        reply?.Hide();
        conversation ??= new ConversationWindow(this);
        if (!conversation.IsVisible) { AnchorConversation(); conversation.Show(this); AnchorConversation(); }
        conversation.Activate();
    }

    internal void HideCompanion()
    {
        reply?.Hide(); conversation?.Hide(); roamTarget = null;
        ContextMenu?.Close(); Hide();
    }

    internal void ShowCompanion()
    {
        if (IsClosing) return;
        Position = Fit(Position);
        if (!IsVisible) Show();
        Activate();
    }

    private void AnchorConversation()
    {
        if (conversation == null || positioning) return;
        positioning = true;
        try
        {
            PixelRect area = Screens.ScreenFromWindow(this)?.WorkingArea ?? new PixelRect(0, 0, 1024, 768);
            double scale = RenderScaling;
            int dialogWidth = (int)(conversation.Width * scale), dialogHeight = (int)(conversation.Height * scale);
            int y = Position.Y + (int)(Height * scale) + 8;
            if (!dragging && y + dialogHeight > area.Bottom)
            {
                y = Math.Max(area.Y + (int)(Height * scale), area.Bottom - dialogHeight);
                Position = Fit(new PixelPoint(Position.X, y - (int)(Height * scale) - 8));
            }
            int x = Position.X + ((int)(Width * scale) - dialogWidth) / 2;
            conversation.Position = new PixelPoint(Math.Clamp(x, area.X, Math.Max(area.X, area.Right - dialogWidth)), Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - dialogHeight)));
        }
        finally { positioning = false; }
    }

    internal void ConversationMoved(ConversationWindow window)
    {
        if (positioning || !window.IsVisible || IsClosing) return;
        double scale = RenderScaling;
        Position = Fit(new PixelPoint(window.Position.X + (int)((window.Width - Width) * scale / 2), window.Position.Y - (int)(Height * scale) - 8));
    }

    internal void ShowLatestReply(string text)
    {
        currentReply = text?.Trim();
        if (conversation?.IsVisible == true || string.IsNullOrWhiteSpace(currentReply)) return;
        if (reply == null)
        {
            replyLabel = DesktopUi.DataLabel("");
            reply = DesktopUi.Dialog("Claude", DesktopUi.Stack(replyLabel, DesktopUi.Action("隐藏", () => reply.Hide())), 315, 112);
            reply.MinHeight = 75; reply.MinWidth = 180;
            DesktopUi.RemoveDecorations(reply);
            reply.Topmost = true;
            reply.ShowInTaskbar = false;
            reply.PointerPressed += (_, e) => { if (e.ClickCount == 2) OpenConversation(); };
        }
        replyLabel.Text = currentReply.Length <= 130 ? currentReply : "…" + currentReply[^130..];
        AnchorReply();
        if (!reply.IsVisible) reply.Show(this);
        AnchorReply();
    }

    private void AnchorReply()
    {
        if (reply == null) return;
        var work = Screens.ScreenFromWindow(this)?.WorkingArea;
        if (work == null) return;
        int w = (int)(reply.Width * RenderScaling), h = (int)(reply.Height * RenderScaling);
        int x = Position.X + ((int)(Width * RenderScaling) - w) / 2;
        int y = Position.Y + (int)(Height * RenderScaling) + 5;
        reply.Position = new PixelPoint(Math.Clamp(x, work.Value.X, Math.Max(work.Value.X, work.Value.Right - w)),
            Math.Clamp(y, work.Value.Y, Math.Max(work.Value.Y, work.Value.Bottom - h)));
    }

    private void Dropped(object sender, DragEventArgs e)
    {
        string[] paths = e.DataTransfer.TryGetFiles()?.Select(file => file.TryGetLocalPath()).Where(path => File.Exists(path) || Directory.Exists(path)).ToArray();
        if (paths == null || paths.Length == 0) return;
        e.DragEffects = DragDropEffects.Copy;
        Gesture("eating", 950);
        DispatcherTimer.RunOnce(() =>
        {
            if (IsClosing) return;
            Gesture("wiping-mouth", 850);
            OpenConversation();
            conversation.StageFiles(paths);
        }, TimeSpan.FromMilliseconds(preferences.ReduceMotion ? 0 : 950));
    }

    private MenuItem Item(string label, Action action)
    {
        var item = new MenuItem { Header = label };
        LinuxLanguageBindings.Caption(item);
        item.Click += (_, _) =>
        {
            try { action(); }
            catch (Exception error) { DesktopUi.Notice(this, "仙伴", error.Message); }
        };
        return item;
    }

    private ContextMenu BuildMenu()
    {
        var characters = new MenuItem { Header = "切换形象" };
        LinuxLanguageBindings.Caption(characters);
        foreach (PetCharacter profile in PetCharacter.Available)
            characters.Items.Add(Item(profile.Name, () => { LoadCharacter(profile); Save(); }));
        characters.Items.Add(Item("打开角色资源目录", () => DesktopUi.OpenFolder(catalog.Root)));
        characters.Items.Add(Item("刷新外置角色", () => Rescan()));
        var menu = new ContextMenu();
        menu.Items.Add(Item("对话", OpenConversation));
        menu.Items.Add(Item("打开 Claude Code CLI", () => LinuxTerminal.OpenClaude(DirectoryPath, Character.Id)));
        menu.Items.Add(characters);
        menu.Items.Add(Item("Claude Code 定时任务", () => TaskWindow.Show(this, services.Tasks)));
        menu.Items.Add(Item("提醒与倒计时", () => ReminderWindows.ShowSettings(this, services.Reminders)));
        menu.Items.Add(Item("事件规划 / 门派协作", () => WorkshopWindow.Show(this, services)));
        menu.Items.Add(Item("默认设置", ShowSettings));
        menu.Items.Add(Item("跑一会儿", StartRoam));
        menu.Items.Add(Item("隐藏宠物", HideCompanion));
        menu.Items.Add(Item("退出仙伴", Close));
        return menu;
    }

    internal void ShowSettings()
    {
        var directory = new TextBox { Text = preferences.Directory, Watermark = "Claude 默认工作目录" };
        var top = new CheckBox { Content = "保持置顶", IsChecked = Topmost };
        var motion = new CheckBox { Content = "减少动画", IsChecked = preferences.ReduceMotion };
        var startup = new CheckBox { Content = "开机自动启动", IsChecked = LinuxStartup.Enabled };
        var size = new Slider { Minimum = 80, Maximum = 260, Value = Width };
        var opacity = new Slider { Minimum = 20, Maximum = 100, Value = preferences.DockedOpacity * 100 };
        var integration = IntegrationPreferences.Load();
        var language = new ComboBox { ItemsSource = new[] { "English", "简体中文" }, SelectedIndex = UiLanguage.IsEnglish ? 0 : 1 };
        var scope = new ComboBox { Name = "CompanionScope", ItemsSource = new[] { "全局默认" }.Concat(PetCharacter.Available.Select(p => p.Name)).ToArray(), SelectedIndex = 0 };
        var configuration = new TextBox { Watermark = "继承全局 / CLI 自身配置", Text = integration.ClaudeSettingsPath };
        var executable = new TextBox { Name = "ClaudeExecutable", Watermark = UiLanguage.Text("Leave empty to use the global executable or PATH"), Text = integration.ExecutableOverrideFor(null) };
        var profiles = PetCharacter.Available;
        Window dialog = null;
        string editingScope = null;
        var dirtyScopes = new HashSet<string>();
        void StoreFields()
        {
            string key = editingScope ?? "";
            if (integration.SettingsPathFor(editingScope) != configuration.Text || integration.ExecutableOverrideFor(editingScope) != executable.Text) dirtyScopes.Add(key);
            integration.SetSettingsPath(editingScope, configuration.Text);
            if (editingScope == null) integration.ClaudeExecutablePath = executable.Text;
            else
            {
                integration.CharacterClaudeExecutables ??= new();
                integration.CharacterClaudeExecutables[editingScope] = executable.Text;
            }
        }
        scope.SelectionChanged += (_, _) =>
        {
            if (scope.SelectedIndex < 0) return;
            StoreFields();
            string id = scope.SelectedIndex <= 0 ? null : profiles[scope.SelectedIndex - 1].Id;
            editingScope = id;
            configuration.Text = integration.SettingsPathFor(id);
            executable.Text = integration.ExecutableOverrideFor(id);
        };
        var browse = DesktopUi.Action("选择目录…", async () => { string folder = await DesktopUi.Folder(dialog, "Claude 默认目录"); if (folder != null) directory.Text = folder; });
        var browseJson = DesktopUi.Action("选择 JSON…", async () => { string path = await DesktopUi.JsonFile(dialog, "Claude 配置文件"); if (path != null) configuration.Text = path; });
        var browseExecutable = DesktopUi.Action("Browse…", async () => { string path = await DesktopUi.ExecutableFile(dialog); if (path != null) executable.Text = path; });
        var apply = DesktopUi.Action("保存", () =>
        {
            try
            {
                if (!Directory.Exists(directory.Text)) throw new DirectoryNotFoundException("工作目录不存在。");
                StoreFields();
                foreach (string id in dirtyScopes)
                {
                    integration.SetSettingsPath(id, ClaudeSettingsFile.Validate(integration.SettingsPathFor(id)));
                    integration.SetExecutablePath(id, integration.ExecutableOverrideFor(id));
                }
                if (startup.IsChecked == true != LinuxStartup.Enabled) LinuxStartup.SetEnabled(startup.IsChecked == true);
                integration.Save();
                preferences.Directory = Path.GetFullPath(directory.Text);
                preferences.Topmost = Topmost = top.IsChecked == true;
                preferences.ReduceMotion = motion.IsChecked == true;
                preferences.DockedOpacity = opacity.Value / 100;
                preferences.Size = (int)size.Value;
                Width = preferences.Size; Height = Width * 416 / 384;
                Position = Fit(Position);
                Save();
                UiLanguage.Set(language.SelectedIndex == 1 ? "zh-CN" : "en");
                dialog.Close();
            }
            catch (Exception error) { DesktopUi.Notice(dialog, "设置未保存", error.Message); }
        });
        dialog = DesktopUi.Dialog("默认设置", new ScrollViewer { Content = DesktopUi.Stack(DesktopUi.Label("Language"), language, DesktopUi.Label("Claude 默认工作目录"), directory, browse,
            DesktopUi.Row(top, motion, startup), DesktopUi.Label("角色大小"), size, DesktopUi.Label("贴边不透明度"), opacity,
            DesktopUi.Label("Companions"), scope, DesktopUi.Label("Claude executable"), executable, browseExecutable,
            DesktopUi.Label("Claude 配置文件"), configuration, DesktopUi.Row(browseJson, apply)) }, 590, 650);
        dialog.Name = "DefaultSettingsWindow";
        dialog.Show(this);
    }
}
