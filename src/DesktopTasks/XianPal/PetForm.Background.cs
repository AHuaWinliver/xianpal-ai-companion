using System;
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
                        MonitorClaudeBackgroundTask(task, run, false);
        }

        private void RunClaudeBackgroundTask(ScheduledClaudeTask task)
        {
            ClaudeTaskRunRecord run = claudeTaskService.BeginRun(task, DateTime.Now);
            if (run == null) return;
            run.BackgroundJobName = "OrbitBg-" + run.Id;
            run.BackgroundState = "dispatching";
            claudeTaskService.PersistBackgroundRun(task, run);
            string path = BackgroundLogPath(run);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                ProcessStartInfo info = BuildClaudeBackgroundStartInfo(task, run, path, backgroundTaskSupervisor.ClaudeCommand);
                claudeTaskService.UpdateRunProgress(task, run.Id, "正在创建 Claude 原生后台会话；创建成功后本次启动窗口自动关闭。");
                using (Process launcher = Process.Start(info)) { }
                MonitorClaudeBackgroundTask(task, run, true);
            }
            catch (Exception ex)
            {
                claudeTaskService.CompleteRun(task, run.Id, false, -1, "[创建失败] " + ex.Message, DateTime.Now);
            }
        }

        private static string BackgroundLogPath(ClaudeTaskRunRecord run)
        {
            Guid id;
            if (!Guid.TryParse(run.Id, out id)) throw new InvalidOperationException("任务运行 ID 无效。");
            return Path.Combine(Program.StateDirectory, "scheduled-task-runs", id.ToString("N") + ".background.log");
        }

        private void MonitorClaudeBackgroundTask(ScheduledClaudeTask task, ClaudeTaskRunRecord run, bool showBubble)
        {
            MarkScheduledTaskStarted(task);
            ActivityRecord activity = new ActivityRecord
            {
                WorkingDirectory = task.WorkingDirectory, CharacterId = currentCharacterId,
                Title = "Claude Background · " + Path.GetFileName(task.WorkingDirectory),
                State = "Claude Background 正在运行", NativeInteractive = true,
                SessionId = run.BackgroundSessionId, Output = run.Transcript, Updated = DateTime.Now
            };
            activities.Insert(0, activity);
            if (showBubble && !userHidden && !claudeRunner.IsRunning && !brainRequestActive)
            {
                lastReplyActivity = activity;
                BeginReplyBubble(task.Prompt);
                replyBubble.SetStatus("正在创建 Claude Background…");
            }
            RefreshActivities();
            backgroundTaskSupervisor.Track(new BackgroundTaskWatch
            {
                JobName = run.BackgroundJobName, JobId = run.BackgroundJobId, Directory = task.WorkingDirectory, LogPath = BackgroundLogPath(run),
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
                    activity.Output = content;
                    activity.SessionId = run.BackgroundSessionId;
                    activity.State = snapshot.Status;
                    activity.Updated = DateTime.Now;
                    claudeTaskService.UpdateRunProgress(task, run.Id, content);
                    if (lastReplyActivity == activity && !claudeRunner.IsRunning && !brainRequestActive)
                    {
                        replyBubble.ReplaceReply(content);
                        replyBubble.SetStatus(snapshot.Status);
                        SetClaudeEvent(snapshot.State == "blocked" ? "PermissionRequest" : snapshot.Finished ? (snapshot.Success ? "Stop" : "StopFailure") : "PreToolUse");
                        if (!replyBubble.Visible && !replyBubble.UserDismissed) ShowReplyBubble();
                    }
                    if (snapshot.Finished)
                    {
                        activity.Unread = true;
                        claudeTaskService.CompleteRun(task, run.Id, snapshot.Success, snapshot.Success ? 0 : (snapshot.DispatchCode == 0 ? -1 : snapshot.DispatchCode), content, DateTime.Now);
                        MarkScheduledTaskFinished(task);
                        if (!claudeRunner.IsRunning && !brainRequestActive && activeScheduledTaskCounts.Count > 0) SetClaudeEvent("PreToolUse");
                        TryRunNextScheduledClaudeTask();
                    }
                    RefreshActivities();
                }
            });
        }

        internal static ProcessStartInfo BuildClaudeBackgroundStartInfo(ScheduledClaudeTask task, ClaudeTaskRunRecord run, string logPath, string command)
        {
            if (task == null || run == null) throw new ArgumentNullException("task/run");
            if (!Directory.Exists(task.WorkingDirectory)) throw new DirectoryNotFoundException("任务目录不存在。");
            Guid runId;
            if (!Guid.TryParse(run.Id, out runId) || run.BackgroundJobName != "OrbitBg-" + run.Id) throw new ArgumentException("后台会话归属标识无效。");
            string directory = Path.GetFullPath(task.WorkingDirectory), path = Path.GetFullPath(logPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string cli = command.Replace("'", "''"), log = path.Replace("'", "''");
            string settings = path + ".settings.json";
            // Session-only environment overrides suppress duplicate generic
            // hooks even when the worker is hosted by an existing supervisor.
            File.WriteAllText(settings, new JavaScriptSerializer().Serialize(new { env = new { FUFU_PET_INTERNAL_TASK_ID = task.Id, FUFU_PET_MANAGED_SESSION = "1", FUFU_PET_NATIVE_RUN_LOG = "", FUFU_PET_NATIVE_RUN_TOKEN = "" } }), new UTF8Encoding(false));
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
                "$prompt=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + Convert.ToBase64String(Encoding.UTF8.GetBytes(prompt)) + "')); " +
                // No pipe / redirection here: an untrusted workspace can show
                // its native trust dialog. Only this own new console exits.
                "& '" + cli + "' --bg --name '" + run.BackgroundJobName + "' --settings '" + settings.Replace("'", "''") + "' $prompt; $code=$LASTEXITCODE; if($null -eq $code){$code=1}; " +
                "[IO.File]::AppendAllText('" + log + "',('后台会话创建命令退出码：'+$code+'；这不是任务完成信号。')+[Environment]::NewLine,$utf8) " +
                "} catch { [IO.File]::AppendAllText('" + log + "',('[创建失败] '+[string]$_)+[Environment]::NewLine,$utf8); Write-Host $_; $code=1 } " +
                "finally { [IO.File]::WriteAllText('" + log + ".dispatch-exit.tmp',[string][int]$code,$utf8); [IO.File]::Move('" + log + ".dispatch-exit.tmp','" + log + ".dispatch-exit') }; " +
                "if($code -ne 0 -and $env:FUFU_TEST_MODE -ne '1'){[void](Read-Host '创建失败，请检查上方错误；按回车关闭本次窗口')}; exit [int]$code";
            File.WriteAllText(path, "正在创建 Claude 托管的原生后台会话。\r\n", new UTF8Encoding(false));
            File.WriteAllText(path + ".ps1", script, new UTF8Encoding(true));
            return new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "conhost.exe"), "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"" + path + ".ps1\"")
            { WorkingDirectory = directory, UseShellExecute = true, CreateNoWindow = false, WindowStyle = ProcessWindowStyle.Normal };
        }

        internal static void OpenBackgroundSession(ScheduledClaudeTask task, ClaudeTaskRunRecord run)
        {
            if (task == null || run == null || !BackgroundTaskSupervisor.ValidJobId(run.BackgroundJobId)) throw new InvalidOperationException("后台会话 ID 尚未取得；请稍后重试，或在 CLI 运行 claude agents。");
            string script = "$ErrorActionPreference='Stop'; $utf8=New-Object Text.UTF8Encoding($false); $OutputEncoding=$utf8; [Console]::InputEncoding=$utf8; [Console]::OutputEncoding=$utf8; Set-Location -LiteralPath '" + task.WorkingDirectory.Replace("'", "''") + "'; claude attach '" + run.BackgroundJobId + "'";
            Process.Start(new ProcessStartInfo("powershell.exe", "-NoLogo -NoProfile -NoExit -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)))
            { WorkingDirectory = Directory.Exists(task.WorkingDirectory) ? task.WorkingDirectory : Program.StateDirectory, UseShellExecute = true, WindowStyle = ProcessWindowStyle.Normal });
        }
    }
}
