// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace XianPal.Linux;

internal static class LinuxTerminal
{
    internal static string CleanOutput(string text) => UiText.Normalize(Regex.Replace(text ?? "", @"\x1b\][^\x07\x1b]*(?:\x07|\x1b\\)", ""));

    internal static ProcessStartInfo BuildStartInfo(string command, IEnumerable<string> arguments, string directory)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("工作目录不存在：" + directory);
        string terminal = Environment.GetEnvironmentVariable("XIANPAL_TERMINAL");
        if (!string.IsNullOrWhiteSpace(terminal))
        {
            terminal = Path.GetFullPath(terminal);
            if (!File.Exists(terminal)) throw new FileNotFoundException("Configured terminal does not exist.", terminal);
        }
        else foreach (string name in new[] { "gnome-terminal", "konsole", "xfce4-terminal", "mate-terminal", "xterm", "x-terminal-emulator" })
            {
                terminal = FindExecutable(name);
                if (terminal != null) break;
            }
        if (terminal == null) throw new FileNotFoundException("未找到图形终端。请安装 GNOME Terminal、Konsole、XFCE Terminal、MATE Terminal 或 xterm。");
        var info = new ProcessStartInfo(terminal) { WorkingDirectory = Path.GetFullPath(directory), UseShellExecute = false };
        switch (Path.GetFileName(terminal))
        {
            case "gnome-terminal": info.ArgumentList.Add("--wait"); info.ArgumentList.Add("--"); break;
            case "konsole": info.ArgumentList.Add("--separate"); info.ArgumentList.Add("-e"); break;
            case "xfce4-terminal": info.ArgumentList.Add("--disable-server"); info.ArgumentList.Add("-x"); break;
            case "mate-terminal": info.ArgumentList.Add("--disable-factory"); info.ArgumentList.Add("-x"); break;
            default: info.ArgumentList.Add("-e"); break;
        }
        info.ArgumentList.Add(command);
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        info.Environment["PWD"] = info.WorkingDirectory;
        return info;
    }

    internal static string FindExecutable(string name)
    {
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrEmpty(directory)) continue;
            string path = Path.Combine(directory, name);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    internal static void OpenClaude(string directory, string characterId, string session = null, bool attach = false)
    {
        string command = ClaudeCliRunner.FindClaudeCommand(characterId) ?? throw new FileNotFoundException("未找到 Claude Code CLI。");
        var arguments = new List<string>();
        if (!string.IsNullOrEmpty(session))
        {
            if (attach)
            {
                if (!BackgroundTaskSupervisor.ValidJobId(session)) throw new ArgumentException("后台会话 ID 无效。");
                arguments.Add("attach"); arguments.Add(session);
            }
            else
            {
                if (!Guid.TryParse(session, out _)) throw new ArgumentException("会话 ID 无效。");
                arguments.Add("--resume"); arguments.Add(session);
            }
        }
        string settings = ClaudeSettingsFile.CurrentPath(characterId);
        if (!string.IsNullOrEmpty(settings)) { arguments.Add("--settings"); arguments.Add(settings); }
        using var process = Process.Start(BuildStartInfo(command, arguments, directory));
    }

    internal static string Query(string command, string directory, params string[] arguments)
        => QueryConfigured(command, directory, "", arguments);

    internal static string QueryConfigured(string command, string directory, string settingsPath, params string[] arguments)
    {
        var info = new ProcessStartInfo(command) { WorkingDirectory = directory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        if (!string.IsNullOrEmpty(settingsPath))
        {
            var selected = ClaudeSettingsFile.Read(settingsPath);
            if (selected.TryGetValue("env", out object environment) && environment is Dictionary<string, object> variables)
                foreach (var pair in variables) info.Environment[pair.Key] = Convert.ToString(pair.Value);
            info.ArgumentList.Add("--settings"); info.ArgumentList.Add(settingsPath);
        }
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        ClaudeCliRunner.ConfigureUtf8(info);
        using var process = new Process { StartInfo = info };
        var output = new StringBuilder(); var errors = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) if (output.Length < 2097152) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (errors) if (errors.Length < 4000) errors.AppendLine(e.Data); };
        process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
        if (!process.WaitForExit(12000))
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new TimeoutException("Claude 状态查询超时；后台任务保持运行。");
        }
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Claude 状态查询失败：" + errors);
        return output.ToString();
    }

    internal static (string Command, List<string> Arguments) SelfCommand(params string[] arguments)
    {
        string command = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve application executable.");
        var options = new List<string>();
        string launcher = Path.Combine(AppContext.BaseDirectory, "launch.sh");
        if (Path.GetFileName(command) == "xianpal" && File.Exists(launcher))
        { command = "/bin/sh"; options.Add(launcher); }
        else if (Path.GetFileNameWithoutExtension(command) == "dotnet") options.Add(typeof(Program).Assembly.Location);
        options.AddRange(arguments);
        return (command, options);
    }

    internal static string ShellQuote(string value) => "'" + value.Replace("'", "'\"'\"'") + "'";
}

internal sealed class LinuxTerminalJob
{
    internal static bool CanRecover(ClaudeTaskRunRecord record)
    {
        try
        {
            string log = LogPath(record);
            if (File.Exists(log + ".completed")) return true;
            string[] fields = File.ReadAllText(log + ".started").Trim().Split('|');
            if (fields.Length != 2 || !int.TryParse(fields[0], out int id) || !long.TryParse(fields[1], out long ticks)) return false;
            using var process = Process.GetProcessById(id);
            return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == ticks;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { return false; }
    }

    public string Prompt { get; set; } = "";
    public string Directory { get; set; } = "";
    public string Mode { get; set; } = "";
    public string CharacterId { get; set; } = "";
    public string TaskId { get; set; } = "";
    public string Token { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string JobName { get; set; } = "";
    public string Log { get; set; } = "";

    internal static string LogPath(ClaudeTaskRunRecord record)
    {
        if (!Guid.TryParse(record.Id, out var id)) throw new ArgumentException("任务运行 ID 无效。");
        return Path.Combine(Program.StateDirectory, "scheduled-task-runs", id.ToString("N") + ".log");
    }

    internal static ProcessStartInfo Prepare(ScheduledClaudeTask task, ClaudeTaskRunRecord record, string characterId)
    {
        string log = LogPath(record);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(log), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var job = new LinuxTerminalJob { Prompt = task.Prompt, Directory = Path.GetFullPath(task.WorkingDirectory),
            Mode = task.RunMode, CharacterId = characterId, TaskId = task.Id, Token = Guid.NewGuid().ToString("N"),
            SessionId = Guid.NewGuid().ToString("D"), JobName = record.BackgroundJobName, Log = log };
        WritePrivate(log + ".interactive", new CompatibleJson().Serialize(job));
        WritePrivate(log, "");
        if (ClaudeTaskService.IsInteractiveMode(job.Mode)) record.BackgroundSessionId = job.SessionId;
        var self = LinuxTerminal.SelfCommand("--terminal-job", log + ".interactive");
        return LinuxTerminal.BuildStartInfo(self.Command, self.Arguments, job.Directory);
    }

    internal static int Execute(string descriptor)
    {
        string expectedRoot = Path.Combine(Program.StateDirectory, "scheduled-task-runs");
        descriptor = Path.GetFullPath(descriptor);
        if (Path.GetDirectoryName(descriptor) != expectedRoot) throw new ArgumentException("任务描述路径无效。");
        var job = new CompatibleJson().Deserialize<LinuxTerminalJob>(File.ReadAllText(descriptor));
        if (job == null || descriptor != job.Log + ".interactive" || !Guid.TryParseExact(job.Token, "N", out _) ||
            !Guid.TryParse(job.SessionId, out _) || !System.IO.Directory.Exists(job.Directory)) throw new ArgumentException("任务描述无效。");
        int code = 1;
        string settings = job.Log + ".settings.json";
        using var own = Process.GetCurrentProcess();
        WritePrivate(job.Log + ".started", own.Id + "|" + own.StartTime.ToUniversalTime().Ticks);
        try
        {
            string cli = ClaudeCliRunner.FindClaudeCommand(job.CharacterId) ?? throw new FileNotFoundException("未找到 Claude Code CLI。");
            var selected = ClaudeSettingsFile.Read(ClaudeSettingsFile.CurrentPath(job.CharacterId));
            bool background = job.Mode == "claude-background";
            if (background && (!LinuxTerminal.Query(cli, job.Directory, "--help").Contains("--bg") ||
                !LinuxTerminal.Query(cli, job.Directory, "agents", "--help").Contains("--all")))
                throw new InvalidOperationException("当前 Claude CLI 不支持 --bg 或 agents --json --all。请使用后台输出模式，或更新本机 CLI。");
            var environment = selected.TryGetValue("env", out object value) ? (Dictionary<string, object>)value : new();
            environment["FUFU_PET_MANAGED_SESSION"] = "1"; environment["FUFU_PET_INTERNAL_TASK_ID"] = job.TaskId;
            environment["FUFU_PET_NATIVE_RUN_LOG"] = background ? "" : job.Log;
            environment["FUFU_PET_NATIVE_RUN_TOKEN"] = background ? "" : job.Token;
            selected["env"] = environment;
            if (!background)
            {
                var hooks = selected.TryGetValue("hooks", out object existing) ? (Dictionary<string, object>)existing : new();
                foreach (string name in new[] { "SessionStart", "UserPromptSubmit", "MessageDisplay", "PreToolUse", "PostToolUse", "PostToolUseFailure", "PermissionRequest", "Notification", "Stop", "SessionEnd" })
                {
                    var self = LinuxTerminal.SelfCommand("--native-task-hook", name);
                    string command = LinuxTerminal.ShellQuote(self.Command) + " " + string.Join(" ", self.Arguments.Select(LinuxTerminal.ShellQuote));
                    var rows = hooks.TryGetValue(name, out object rowsValue) && rowsValue is object[] previous ? previous.ToList() : new List<object>();
                    rows.Add(new { hooks = new[] { new { type = "command", command, timeout = 10 } } });
                    hooks[name] = rows.ToArray();
                }
                selected["hooks"] = hooks;
            }
            WritePrivate(settings, new CompatibleJson().Serialize(selected));
            var info = new ProcessStartInfo(cli) { WorkingDirectory = job.Directory, UseShellExecute = false };
            info.ArgumentList.Add("--settings"); info.ArgumentList.Add(settings);
            foreach (var pair in environment) info.Environment[pair.Key] = Convert.ToString(pair.Value);
            info.Environment["PWD"] = job.Directory;
            if (background) { info.ArgumentList.Add("--bg"); info.ArgumentList.Add("--name"); info.ArgumentList.Add(job.JobName); }
            else { info.ArgumentList.Add("--session-id"); info.ArgumentList.Add(job.SessionId); }
            // Positional prompts stay data, including quotes, newlines and leading dashes.
            info.ArgumentList.Add("--"); info.ArgumentList.Add(job.Prompt);
            using var cliProcess = Process.Start(info) ?? throw new InvalidOperationException("Claude CLI 启动失败。");
            cliProcess.WaitForExit(); code = cliProcess.ExitCode;
        }
        catch (Exception error)
        {
            File.AppendAllText(job.Log, "[启动失败] " + error.Message + "\n", Encoding.UTF8);
            Console.Error.WriteLine(error.Message);
        }
        finally
        {
            string suffix = job.Mode == "claude-background" ? ".dispatch-exit" : ".completed";
            WritePrivate(job.Log + suffix + ".tmp", code.ToString());
            File.Move(job.Log + suffix + ".tmp", job.Log + suffix, true);
            if (job.Mode != "claude-background" || code != 0) File.Delete(settings);
        }
        return code;
    }

    private static void WritePrivate(string path, string value)
    {
        File.WriteAllText(path, value, new UTF8Encoding(false));
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
