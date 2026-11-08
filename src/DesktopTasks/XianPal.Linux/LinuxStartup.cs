// SPDX-License-Identifier: Apache-2.0
using System.Text;

namespace XianPal.Linux;

internal static class LinuxStartup
{
    private const string Marker = "X-XianPal-Managed=true";
    internal static string EntryPath => Environment.GetEnvironmentVariable("FUFU_TEST_MODE") == "1"
        ? Path.Combine(Program.StateDirectory, "autostart", "xianpal.desktop")
        : Path.Combine(Path.GetDirectoryName(NativePaths.UserDirectory("XDG_CONFIG_HOME", ".config")), "autostart", "xianpal.desktop");

    internal static bool Enabled => File.Exists(EntryPath) && File.ReadAllText(EntryPath).Contains(Marker) && !File.ReadAllText(EntryPath).Contains("Hidden=true");

    // Desktop-entry values are unescaped before Exec's quoted arguments are parsed.
    // https://specifications.freedesktop.org/desktop-entry/latest/exec-variables.html
    internal static string ExecArgument(string value)
    {
        if (value.IndexOfAny(new[] { '\n', '\r', '\0' }) >= 0) throw new ArgumentException("A desktop entry path cannot contain line breaks.");
        string quoted = value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("`", "\\`").Replace("$", "\\$").Replace("%", "%%");
        return "\"" + quoted.Replace("\\", "\\\\") + "\"";
    }

    internal static string Entry(string command, IEnumerable<string> arguments, string icon)
    {
        if (command.Contains('=')) throw new ArgumentException("Desktop entry executable paths cannot contain '='.");
        string exec = string.Join(" ", new[] { command }.Concat(arguments).Select(ExecArgument));
        string iconValue = icon.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");
        return "[Desktop Entry]\nType=Application\nName=XianPal\nName[zh_CN]=仙伴\nComment=An animated desktop companion for Claude Code\nExec=" + exec +
            "\nIcon=" + iconValue + "\nTerminal=false\nCategories=Utility;\n" + Marker + "\n";
    }

    internal static void SetEnabled(bool enabled)
    {
        string path = EntryPath;
        if (File.Exists(path) && (new FileInfo(path).LinkTarget != null || !File.ReadAllText(path).Contains(Marker)))
            throw new IOException("An existing autostart entry is not managed by XianPal.");
        if (!enabled) { if (File.Exists(path)) File.Delete(path); return; }
        var self = LinuxTerminal.SelfCommand();
        string content = Entry(self.Command, self.Arguments, Path.Combine(AppContext.BaseDirectory, "xianpal.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, content, new UTF8Encoding(false)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static void RegisterInstallation(string directory)
    {
        string dataRoot = Environment.GetEnvironmentVariable("FUFU_TEST_MODE") == "1" ? Path.Combine(Program.StateDirectory, "desktop-integration") :
            Path.GetDirectoryName(NativePaths.UserDirectory("XDG_DATA_HOME", ".local", "share"));
        string entry = Entry("/bin/sh", new[] { Path.Combine(directory, "launch.sh") }, Path.Combine(directory, "xianpal.png"));
        WriteManaged(Path.Combine(dataRoot, "applications", "xianpal.desktop"), entry);
        if (Enabled) WriteManaged(EntryPath, entry);
    }

    private static void WriteManaged(string path, string content)
    {
        if (File.Exists(path) && (new FileInfo(path).LinkTarget != null || !File.ReadAllText(path).Contains(Marker)))
            throw new IOException("An existing desktop entry is not managed by XianPal.");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, content, new UTF8Encoding(false)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
