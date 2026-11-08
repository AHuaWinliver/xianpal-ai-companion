using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed partial class PetForm
    {
        private readonly BackgroundTaskSupervisor backgroundTaskSupervisor = new BackgroundTaskSupervisor();

        private void RestoreClaudeBackgroundTasks()
        {
            foreach (ScheduledClaudeTask task in claudeTaskService.Tasks)
                foreach (ClaudeTaskRunRecord run in task.History)
                    if (run.RunMode == "claude-background" && run.Status == "running" && !String.IsNullOrEmpty(run.BackgroundJobName))
                        MonitorClaudeBackgroundTask(task, run);
        }

        private void RunClaudeBackgroundTask(ScheduledClaudeTask task)
        {
            ClaudeTaskRunRecord run = claudeTaskService.BeginRun(task, DateTime.Now);
            if (run == null) return;
            run.BackgroundJobName = "OrbitBg-" + run.Id;
            run.BackgroundState = "dispatching";
            claudeTaskService.PersistBackgroundRun(task, run);
            string path = BackgroundLogPath(run);
            bool launched = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                ProcessStartInfo info = BuildClaudeBackgroundStartInfo(task, run, path, ClaudeCliRunner.RequireClaudeCommand(task.CharacterId), task.CharacterId);
                claudeTaskService.UpdateRunProgress(task, run.Id, "正在创建 Claude 原生后台会话；创建成功后本次启动窗口自动关闭。");
                using (Process launcher = Process.Start(info)) { }
                launched = true;
                MonitorClaudeBackgroundTask(task, run);
            }
            catch (Exception ex)
            {
                if (!launched) RemoveBackgroundLaunchSettings(path);
                claudeTaskService.CompleteRun(task, run.Id, false, -1, "[创建失败] " + ex.Message, DateTime.Now);
            }
        }

        private static string BackgroundLogPath(ClaudeTaskRunRecord run)
        {
            Guid id;
            if (!Guid.TryParse(run.Id, out id)) throw new InvalidOperationException("任务运行 ID 无效。");
            return Path.Combine(Program.StateDirectory, "scheduled-task-runs", id.ToString("N") + ".background.log");
        }

        private void MonitorClaudeBackgroundTask(ScheduledClaudeTask task, ClaudeTaskRunRecord run)
        {
            MarkScheduledTaskStarted(task);
            IntegrationPreferences profile = IntegrationPreferences.Load();
            string command = profile.ExecutablePathFor(run.CharacterId);
            backgroundTaskSupervisor.Track(new BackgroundTaskWatch
            {
                JobName = run.BackgroundJobName, JobId = run.BackgroundJobId, Directory = ClaudeTaskService.RunDirectory(task, run), LogPath = BackgroundLogPath(run),
                Command = String.IsNullOrEmpty(command) ? ClaudeCliRunner.FindClaudeCommand(run.CharacterId) : command, SettingsPath = profile.EffectiveSettingsPathFor(run.CharacterId),
                Update = delegate(BackgroundSessionSnapshot snapshot)
                {
                    if (!String.IsNullOrEmpty(snapshot.JobId)) run.BackgroundJobId = snapshot.JobId;
                    if (!String.IsNullOrEmpty(snapshot.SessionId)) run.BackgroundSessionId = snapshot.SessionId;
                    run.BackgroundState = snapshot.State;
                    string content = "[Claude Background] " + snapshot.Status + "\r\n任务目录：" + task.WorkingDirectory + "\r\n" +
                        (String.IsNullOrEmpty(run.BackgroundJobId) ? "" : "会话 ID：" + run.BackgroundJobId + "（可在历史对话窗口打开原生会话）\r\n") + "\r\n" +
                        (String.IsNullOrWhiteSpace(snapshot.Output) ? run.BackgroundOutput : snapshot.Output);
                    if (!String.IsNullOrWhiteSpace(snapshot.Output)) run.BackgroundOutput = snapshot.Output;
                    // logs is a recent terminal snapshot, not a stream of deltas.
                    // Replace it rather than appending and duplicating each poll.
                    claudeTaskService.UpdateRunProgress(task, run.Id, content);
                    if (snapshot.Finished)
                    {
                        RemoveBackgroundLaunchSettings(BackgroundLogPath(run));
                        claudeTaskService.CompleteRun(task, run.Id, snapshot.Success, snapshot.Success ? 0 : (snapshot.DispatchCode == 0 ? -1 : snapshot.DispatchCode), content, DateTime.Now);
                        MarkScheduledTaskFinished(task);
                        TryRunNextScheduledClaudeTask();
                    }
                }
            });
        }

        internal static ProcessStartInfo BuildClaudeBackgroundStartInfo(ScheduledClaudeTask task, ClaudeTaskRunRecord run, string logPath, string command, string characterId = null)
        {
            if (task == null || run == null) throw new ArgumentNullException("task/run");
            if (!Directory.Exists(task.WorkingDirectory)) throw new DirectoryNotFoundException("任务目录不存在。");
            Guid runId;
            if (!Guid.TryParse(run.Id, out runId) || run.BackgroundJobName != "OrbitBg-" + run.Id) throw new ArgumentException("后台会话归属标识无效。");
            string directory = Path.GetFullPath(task.WorkingDirectory), path = Path.GetFullPath(logPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string cli = command.Replace("'", "''"), log = path.Replace("'", "''");
            string settings = path + ".settings.json";
            // The detached worker needs both the selected settings and the
            // ownership markers even when hosted by an existing supervisor.
            Dictionary<string, object> selected = ClaudeSettingsFile.Read(ClaudeSettingsFile.CurrentPath(characterId));
            object existingEnvironment;
            var environment = selected.TryGetValue("env", out existingEnvironment)
                ? (Dictionary<string, object>)existingEnvironment
                : new Dictionary<string, object>();
            environment["FUFU_PET_INTERNAL_TASK_ID"] = task.Id;
            environment["FUFU_PET_MANAGED_SESSION"] = "1";
            environment["FUFU_PET_NATIVE_RUN_LOG"] = "";
            environment["FUFU_PET_NATIVE_RUN_TOKEN"] = "";
            selected["env"] = environment;
            var serializer = new JavaScriptSerializer { MaxJsonLength = 2 * 1024 * 1024 };
            string merged = serializer.Serialize(selected);
            if (Encoding.UTF8.GetByteCount(merged) > serializer.MaxJsonLength)
                throw new ArgumentException("合并后的 Claude 后台配置超过 2 MiB。");
            string prompt = "你正在执行桌面宠物的定时任务。从以下目录启动：" + directory + "。遵循 Claude Background 的工作区隔离机制，并在总结中说明实际工作目录。任务：" + task.Prompt;
            prompt = prompt.Replace("\r", " ").Replace("\n", " ");
            string desktopScript = "";
            if (task.DesktopNumber > 0)
            {
                Guid target = VirtualDesktopService.ResolveDesktop(task.DesktopNumber);
                desktopScript = "$placer=Start-Process -FilePath '" + typeof(Program).Assembly.Location.Replace("'", "''") + "' -ArgumentList @('--place-task-console','" + target.ToString("D") + "','\"" + log + ".desktop\"',[string]$PID) -WindowStyle Hidden -Wait -PassThru; if($placer.ExitCode -ne 0){throw '启动窗口虚拟桌面定位失败，未创建任务。'}; ";
            }
            string script = "$code=1; $utf8=New-Object Text.UTF8Encoding($false); $OutputEncoding=$utf8; [Console]::InputEncoding=$utf8; [Console]::OutputEncoding=$utf8; " +
                "try { $ErrorActionPreference='Stop'; Set-Location -LiteralPath '" + directory.Replace("'", "''") + "'; $env:PWD='" + directory.Replace("'", "''") + "'; " +
                "$env:FUFU_PET_INTERNAL_TASK_ID='" + task.Id.Replace("'", "''") + "'; $env:FUFU_PET_MANAGED_SESSION='1'; Remove-Item Env:FUFU_PET_NATIVE_RUN_LOG,Env:FUFU_PET_NATIVE_RUN_TOKEN -ErrorAction SilentlyContinue; " +
                "[IO.File]::WriteAllText('" + log + ".started',([string]$PID+'|'+[string]([Diagnostics.Process]::GetCurrentProcess().StartTime.ToUniversalTime().Ticks)),$utf8); " +
                "$help=(& '" + cli + "' --help | Out-String); if($LASTEXITCODE -ne 0 -or $help -notmatch '--bg'){throw '当前 Claude Code 不支持 --bg / Background；请更新 CLI。'}; " +
                "$help=(& '" + cli + "' agents --help | Out-String); if($LASTEXITCODE -ne 0 -or $help -notmatch '--json' -or $help -notmatch '--all'){throw '当前 CLI 不支持 agents --json --all；无法可靠跟踪后台任务，请更新。'}; " +
                desktopScript +
                ClaudeSettingsFile.PowerShellSetup(settings, command) +
                "$prompt=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + Convert.ToBase64String(Encoding.UTF8.GetBytes(prompt)) + "')); " +
                // No pipe / redirection here: an untrusted workspace can show
                // its native trust dialog. Only this own new console exits.
                "& '" + cli + "' --bg --name '" + run.BackgroundJobName + "' --settings $xianpalSettings $prompt; $code=$LASTEXITCODE; if($null -eq $code){$code=1}; " +
                "[IO.File]::AppendAllText('" + log + "',('后台会话创建命令退出码：'+$code+'；这不是任务完成信号。')+[Environment]::NewLine,$utf8) " +
                "} catch { [IO.File]::AppendAllText('" + log + "',('[创建失败] '+[string]$_)+[Environment]::NewLine,$utf8); Write-Host $_; $code=1 } " +
                "finally { [IO.File]::WriteAllText('" + log + ".dispatch-exit.tmp',[string][int]$code,$utf8); [IO.File]::Move('" + log + ".dispatch-exit.tmp','" + log + ".dispatch-exit') }; " +
                "if($code -ne 0 -and $env:FUFU_TEST_MODE -ne '1'){[void](Read-Host '创建失败，请检查上方错误；按回车关闭本次窗口')}; exit [int]$code";
            File.WriteAllText(path, "正在创建 Claude 托管的原生后台会话。\r\n", new UTF8Encoding(false));
            File.WriteAllText(path + ".ps1", script, new UTF8Encoding(true));
            File.WriteAllText(settings, merged, new UTF8Encoding(false));
            return new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "conhost.exe"), "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"" + path + ".ps1\"")
            { WorkingDirectory = directory, UseShellExecute = true, CreateNoWindow = false, WindowStyle = ProcessWindowStyle.Normal };
        }

        private static void RemoveBackgroundLaunchSettings(string logPath)
        {
            try { File.Delete(logPath + ".settings.json"); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        internal static void OpenBackgroundSession(ScheduledClaudeTask task, ClaudeTaskRunRecord run)
        {
            Process.Start(BuildBackgroundAttachStartInfo(task, run));
        }

        internal static ProcessStartInfo BuildBackgroundAttachStartInfo(ScheduledClaudeTask task, ClaudeTaskRunRecord run)
        {
            if (task == null || run == null || !BackgroundTaskSupervisor.ValidJobId(run.BackgroundJobId)) throw new InvalidOperationException("后台会话 ID 尚未取得；请稍后重试，或在 CLI 运行 claude agents。");
            string directory = ClaudeTaskService.RunDirectory(task, run);
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("任务目录不存在。");
            string command = ClaudeCliRunner.RequireClaudeCommand(run.CharacterId);
            string settings = ClaudeSettingsFile.CurrentPath(run.CharacterId);
            string script = "$ErrorActionPreference='Stop'; $utf8=New-Object Text.UTF8Encoding($false); $OutputEncoding=$utf8; [Console]::InputEncoding=$utf8; [Console]::OutputEncoding=$utf8; Set-Location -LiteralPath '" + directory.Replace("'", "''") + "'; " +
                ClaudeSettingsFile.PowerShellSetup(settings, command) + "try { & '" + command.Replace("'", "''") + "' " + (settings.Length == 0 ? "" : "--settings $xianpalSettings ") + "attach '" + run.BackgroundJobId + "' } finally { Remove-Item Env:FUFU_PET_SETTINGS_FILE -ErrorAction SilentlyContinue }";
            return new ProcessStartInfo("powershell.exe", "-NoLogo -NoProfile -NoExit -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)))
            { WorkingDirectory = directory, UseShellExecute = true, WindowStyle = ProcessWindowStyle.Normal };
        }
    }
}
