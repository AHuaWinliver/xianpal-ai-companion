// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace XianPal.Linux;

internal sealed class LinuxInstallerWindow : Window
{
    private readonly DispatcherTimer animation;
    private readonly Dictionary<string, Bitmap> frames = new();
    private readonly Image sprite = new() { Height = 250, Stretch = Stretch.Uniform };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private bool installing;

    internal LinuxInstallerWindow()
    {
        Title = "XianPal Setup"; Name = "LinuxInstallerWindow";
        Width = 860; Height = 540; MinWidth = 730; MinHeight = 470;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.Parse("#FAF8F4"));
        var language = new ComboBox { ItemsSource = new[] { "English", "简体中文" }, SelectedIndex = UiLanguage.IsEnglish ? 0 : 1, Width = 150 };
        language.SelectionChanged += (_, _) => UiLanguage.Set(language.SelectedIndex == 1 ? "zh-CN" : "en");
        var title = DesktopUi.Label("XianPal"); title.FontSize = 32; title.FontWeight = FontWeight.Bold;
        var destination = new TextBox { Name = "InstallDirectory", Text = LinuxPackage.DefaultDirectory };
        var status = DesktopUi.DataLabel("");
        var browse = DesktopUi.Action("Browse…", async () => { string folder = await DesktopUi.Folder(this, "Installation folder"); if (folder != null) destination.Text = folder; });
        var launch = DesktopUi.Action("Launch XianPal", () =>
        {
            var info = new ProcessStartInfo("/bin/sh") { UseShellExecute = false, WorkingDirectory = destination.Text };
            info.ArgumentList.Add(Path.Combine(destination.Text, "launch.sh"));
            using var process = Process.Start(info); Close();
        });
        launch.IsVisible = false;
        Button install = null;
        install = DesktopUi.Action("Install", async () =>
        {
            installing = true; install.IsEnabled = browse.IsEnabled = destination.IsEnabled = language.IsEnabled = false;
            status.Text = UiLanguage.Text("Installing XianPal…");
            try
            {
                var progress = new Progress<string>(text => Dispatcher.UIThread.Post(() => status.Text = UiLanguage.Text("Installing XianPal…") + " " + text));
                string target = destination.Text;
                var result = await Task.Run(() => LinuxPackage.Install(AppContext.BaseDirectory, target, progress));
                status.Text = UiLanguage.Text("Installation complete.") + (result.PreviousDirectory.Length == 0 ? "" : "\n" + UiLanguage.Text("Previous installation saved at:") + "\n" + result.PreviousDirectory) + (result.Warning.Length == 0 ? "" : "\n" + result.Warning);
                launch.IsVisible = true;
            }
            catch (Exception error)
            {
                status.Text = UiLanguage.Text("Installation failed.") + " " + UiLanguage.Text(error.Message);
                install.IsEnabled = browse.IsEnabled = destination.IsEnabled = language.IsEnabled = true;
            }
            finally { installing = false; }
        });
        var heading = DesktopUi.Label("Desktop companionship"); heading.FontSize = 20; heading.FontWeight = FontWeight.SemiBold;
        var caption = DesktopUi.Label("Four original companions with expressive animations.");
        var preview = new Border { Padding = new Thickness(18), Background = new SolidColorBrush(Color.Parse("#EEF2E8")), CornerRadius = new CornerRadius(12),
            Child = DesktopUi.Stack(heading, sprite, caption) };
        var body = DesktopUi.Stack(language, title, DesktopUi.Label("An animated desktop companion for your local Claude Code workflow."),
            DesktopUi.Label("Installation folder"), destination, browse,
            DesktopUi.Label("Installs for the current user. Uses your own Claude Code configuration; no model or credentials are bundled."), install, launch, status);
        body.Spacing = 14;
        var grid = new Grid { Margin = new Thickness(28), ColumnDefinitions = new ColumnDefinitions("3*,2*"), ColumnSpacing = 24 };
        grid.Children.Add(new ScrollViewer { Content = body }); Grid.SetColumn(preview, 1); grid.Children.Add(preview); Content = grid;
        string[] states = { "waving", "running-right", "eating", "jumping" };
        string[] headings = { "Desktop companionship", "Coordinated movement", "File interactions", "Character performance" };
        string[] captions = { "Four original companions with expressive animations.", "Smooth steps, turns, and character-specific motion.", "Drop files, then confirm your request in chat.", "Distinct animations for reminders and interactions." };
        animation = new DispatcherTimer(TimeSpan.FromMilliseconds(32), DispatcherPriority.Background, (_, _) =>
        {
            int slide = (int)(clock.ElapsedMilliseconds / 4500 % 4), frame = (int)(clock.ElapsedMilliseconds / 42 % 24);
            var role = PetCharacter.All[slide];
            string root = role.Id == "orbit" ? PetWindow.Assets : Path.Combine(PetWindow.Assets, "characters", role.Id);
            string path = Path.Combine(root, states[slide], frame.ToString("00") + ".png");
            if (File.Exists(path))
            {
                if (!frames.TryGetValue(path, out var image)) frames[path] = image = new Bitmap(path);
                sprite.Source = image;
            }
            heading.Text = UiLanguage.Text(headings[slide]); caption.Text = UiLanguage.Text(captions[slide]);
        });
        Opened += (_, _) => animation.Start();
        Closing += (_, e) => { if (installing) e.Cancel = true; };
        Closed += (_, _) => { animation.Stop(); sprite.Source = null; foreach (var image in frames.Values) image.Dispose(); };
        LinuxLanguageBindings.Window(this);
    }
}

internal sealed class LinuxInstallerApp : Application
{
    public override void Initialize() => Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new LinuxInstallerWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
