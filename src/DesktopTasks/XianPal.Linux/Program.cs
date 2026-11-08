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
        if (args.Length > 0 && args[0] == "--install")
        {
            try
            {
                if (args.Contains("--silent"))
                {
                    int index = Array.IndexOf(args, "--install-dir");
                    if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Silent installation requires --install-dir followed by a directory.");
                    var result = LinuxPackage.Install(AppContext.BaseDirectory, args[index + 1]);
                    Console.WriteLine("XianPal installed: " + Path.GetFullPath(args[index + 1]));
                    if (result.Warning.Length > 0) Console.Error.WriteLine(result.Warning);
                    return 0;
                }
                return AppBuilder.Configure<LinuxInstallerApp>().UsePlatformDetect().WithDesktopFonts().WithLinuxRendering().StartWithClassicDesktopLifetime(Array.Empty<string>());
            }
            catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
        }
        if (args.Length == 2 && args[0] == "--terminal-job") return LinuxTerminalJob.Execute(args[1]);
        if (args.Length == 2 && args[0] == "--native-task-hook")
        {
            if (NativeTaskHooks.IsNativeRun) NativeTaskHooks.Publish(args[1], Console.In.ReadToEnd());
            return 0;
        }
        Directory.CreateDirectory(StateDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        using var lease = File.Open(Path.Combine(StateDirectory, "desktop.lock"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
        try { lease.Lock(0, 1); }
        catch (IOException)
        {
            File.WriteAllText(LinuxDesktopShell.RestoreRequest, "show");
            return 0;
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
