// SPDX-License-Identifier: Apache-2.0
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using XianPal.Linux;

namespace XianPal;

internal static class Program
{
    internal static string StateDirectory =>
        Environment.GetEnvironmentVariable("FUFU_TEST_MODE") == "1" && Path.IsPathFullyQualified(Environment.GetEnvironmentVariable("FUFU_STATE_DIR") ?? "")
            ? Environment.GetEnvironmentVariable("FUFU_STATE_DIR")
            : NativePaths.UserDirectory("XDG_STATE_HOME", ".local", "state");
    internal static bool ShouldSuppressHook(string internalTaskId) => !string.IsNullOrWhiteSpace(internalTaskId);

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--self-test") return LinuxSelfTest.Run();
        if (args.Length == 2 && args[0] == "--desktop-test")
            return DesktopValidation.Start(args[1]);
        if (!OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine("This executable targets Linux. Use the Windows build on Windows.");
            return 1;
        }
        if (args.Length == 1 && args[0] == "--version")
        {
            Console.WriteLine(typeof(Program).Assembly.GetName().Version?.ToString(3));
            return 0;
        }
        Directory.CreateDirectory(StateDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        using var lease = File.Open(Path.Combine(StateDirectory, "desktop.lock"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
        try { lease.Lock(0, 1); }
        catch (IOException)
        {
            Console.Error.WriteLine("仙伴已在当前用户的桌面运行。");
            return 1;
        }
        return AppBuilder.Configure<DesktopApp>().UsePlatformDetect().WithDesktopFonts().WithLinuxRendering().StartWithClassicDesktopLifetime(args);
    }
}

internal sealed class DesktopApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = new PetWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
