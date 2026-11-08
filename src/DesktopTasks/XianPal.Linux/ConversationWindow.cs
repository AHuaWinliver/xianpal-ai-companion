// SPDX-License-Identifier: Apache-2.0
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace XianPal.Linux;

internal sealed class ConversationWindow : Window
{
    private readonly PetWindow pet;
    private readonly StackPanel messages = new() { Spacing = 10 };
    private readonly TextBox input = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Watermark = "给 Claude Code 一条需求…" };
    private readonly ScrollViewer history;
    private readonly Button send;
    private readonly Button stop;
    private readonly TextBlock status = DesktopUi.Label("");
    private readonly DispatcherTimer updates;
    private ClaudeCliRunner runner;
    private string session;
    private string sessionDirectory;
    private string sessionCharacter;
    private TextBox response;
    private readonly StringBuilder pending = new();
    private bool follow = true;

    internal ConversationWindow(PetWindow pet)
    {
        this.pet = pet;
        Title = "仙伴 · Claude Code";
        LinuxLanguageBindings.Window(this);
        Width = 480; Height = 410; MinWidth = 340; MinHeight = 260;
        ShowInTaskbar = false;
        var heading = DesktopUi.Label("Claude Code");
        heading.FontSize = 18; heading.FontWeight = FontWeight.SemiBold;
        history = new ScrollViewer { Content = messages, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        history.ScrollChanged += (_, _) => follow = history.Offset.Y + history.Viewport.Height >= history.Extent.Height - 28;
        send = DesktopUi.Action("发送", Send);
        stop = DesktopUi.Action("停止", () => runner?.Stop());
        stop.IsEnabled = false;
        input.MinHeight = 54;
        input.MaxHeight = 120;
        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { e.Handled = true; Send(); }
        };
        var grid = new Grid { Margin = new Thickness(14), RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), RowSpacing = 8 };
        grid.Children.Add(heading);
        Grid.SetRow(history, 1); grid.Children.Add(history);
        Grid.SetRow(input, 2); grid.Children.Add(input);
        var footer = DesktopUi.Row(send, stop, DesktopUi.Action("新会话", Clear), status);
        Grid.SetRow(footer, 3); grid.Children.Add(footer);
        Content = grid;
        updates = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) => Flush());
        updates.Start();
        PositionChanged += (_, _) => pet.ConversationMoved(this);
        Closing += (_, e) =>
        {
            if (pet.IsClosing) return;
            e.Cancel = true;
            Hide();
        };
        Closed += (_, _) => { updates.Stop(); runner?.Dispose(); };
    }

    private void Clear()
    {
        if (runner != null) return;
        session = null;
        messages.Children.Clear();
        status.Text = "";
    }

    internal void StageFiles(IReadOnlyList<string> paths)
    {
        string block = "请处理下列文件或文件夹。\n处理要求：\n" + string.Join("\n", paths.Select(path => "• " + path));
        input.Text = string.IsNullOrWhiteSpace(input.Text) ? block : input.Text + "\n\n" + block;
        input.Focus();
    }

    private TextBox Bubble(string speaker, string text, bool user)
    {
        var body = new TextBox
        {
            Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0), Background = Brushes.Transparent,
            MaxWidth = 340, MinWidth = 60
        };
        var bubble = new Border
        {
            Child = DesktopUi.Stack(DesktopUi.Label(speaker), body),
            Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.Parse(user ? "#DDF1E3" : "#F2F3F4")),
            HorizontalAlignment = user ? HorizontalAlignment.Right : HorizontalAlignment.Left
        };
        messages.Children.Add(bubble);
        return body;
    }

    private void Send()
    {
        string prompt = input.Text?.Trim();
        if (runner != null || string.IsNullOrEmpty(prompt)) return;
        PetCharacter character = pet.Character;
        string directory = pet.DirectoryPath;
        if (sessionCharacter != character.Id || sessionDirectory != directory) session = null;
        sessionCharacter = character.Id; sessionDirectory = directory;
        Bubble(UiLanguage.Text("You"), prompt, true);
        response = Bubble(character.Name, "", false);
        pending.Clear();
        input.Text = "";
        follow = true;
        history.ScrollToEnd();
        runner = new ClaudeCliRunner();
        ClaudeCliRunner active = runner;
        send.IsEnabled = false; stop.IsEnabled = true;
        status.Text = "Claude 正在处理…";
        pet.ChatWorking = true;
        active.OutputReceived += text => Dispatcher.UIThread.Post(() => { if (runner == active) pending.Append(text); });
        active.SessionIdentified += id => Dispatcher.UIThread.Post(() => { if (runner == active && Guid.TryParse(id, out _)) session = id; });
        active.Completed += code => Dispatcher.UIThread.Post(() => Complete(active, code));
        try { active.Start(PetBrain.BuildConversationPrompt(character, prompt), directory, session, null, character.Id); }
        catch (Exception error)
        {
            pending.Append(error.Message);
            Complete(active, -1);
        }
    }

    internal void SendMessage(string text)
    {
        input.Text = text;
        Send();
    }

    internal bool IsRunning => runner != null;
    internal string LastReply => response?.Text ?? "";

    private void Flush()
    {
        if (pending.Length == 0 || response == null) return;
        bool scroll = follow;
        response.Text += pending.ToString();
        pending.Clear();
        if (scroll) Dispatcher.UIThread.Post(history.ScrollToEnd, DispatcherPriority.Background);
    }

    private void Complete(ClaudeCliRunner active, int code)
    {
        if (runner != active || pet.IsClosing) return;
        Flush();
        active.Dispose();
        runner = null;
        send.IsEnabled = true; stop.IsEnabled = false;
        status.Text = code == 0 ? "已完成" : "已停止或失败";
        pet.ChatWorking = false;
        if (pet.Character.Id == sessionCharacter) pet.ShowLatestReply(response.Text);
    }
}
