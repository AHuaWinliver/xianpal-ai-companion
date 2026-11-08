using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
#if XIANPAL_LINUX
using JavaScriptSerializer = XianPal.Linux.CompatibleJson;
using Timer = XianPal.Linux.UiTimer;
#else
using System.Web.Script.Serialization;
using Timer = System.Windows.Forms.Timer;
#endif

namespace XianPal
{
    internal sealed class BackgroundSessionSnapshot
    {
        internal string JobId = "", SessionId = "", State = "working", Status = "正在创建 Claude 后台会话…", Output = "";
        internal bool Finished;
        internal bool Success;
        internal int DispatchCode;
    }

    internal sealed class BackgroundTaskWatch
    {
        internal string JobName, JobId, Directory, LogPath;
        internal string Command, SettingsPath;
        internal Action<BackgroundSessionSnapshot> Update;
    }

    // One supported agents --json --all query per batch, never one AI request
    // per task. Timers only deliver snapshots on the UI thread. No stop/rm,
    // daemon controls, private Claude state parsing or worker process killing.
    internal sealed class BackgroundTaskSupervisor : IDisposable
    {
        private readonly Timer timer = new Timer { Interval = 500 };
        private readonly List<BackgroundTaskWatch> watches = new List<BackgroundTaskWatch>();
        private readonly object gate = new object();
        private List<KeyValuePair<BackgroundTaskWatch, BackgroundSessionSnapshot>> pending;
        private volatile bool busy, disposed;
        private DateTime nextQuery;
        internal string ClaudeCommand = "claude";

        internal BackgroundTaskSupervisor() { timer.Tick += delegate { Poll(); }; }
        internal void Track(BackgroundTaskWatch watch) { watches.Add(watch); nextQuery = DateTime.MinValue; timer.Start(); }

        internal void Poll()
        {
            if (disposed) return;
            List<KeyValuePair<BackgroundTaskWatch, BackgroundSessionSnapshot>> ready;
            lock (gate) { ready = pending; pending = null; }
            if (ready != null)
                foreach (KeyValuePair<BackgroundTaskWatch, BackgroundSessionSnapshot> item in ready)
                {
                    if (!watches.Contains(item.Key)) continue;
                    if (!String.IsNullOrEmpty(item.Value.JobId)) item.Key.JobId = item.Value.JobId;
                    if (item.Value.Finished) watches.Remove(item.Key);
                    item.Key.Update(item.Value);
                }
            if (watches.Count == 0) { timer.Stop(); return; }
            if (busy || DateTime.UtcNow < nextQuery) return;
            busy = true;
            nextQuery = DateTime.UtcNow.AddSeconds(5);
            BackgroundTaskWatch[] batch = watches.ToArray();
            string command = ClaudeCommand;
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<KeyValuePair<BackgroundTaskWatch, BackgroundSessionSnapshot>> results = new List<KeyValuePair<BackgroundTaskWatch, BackgroundSessionSnapshot>>();
                try
                {
                    var batches = new Dictionary<string, object[]>();
                    var errors = new Dictionary<string, string>();
                    foreach (BackgroundTaskWatch watch in batch)
                    {
                        if (disposed) break;
                        string watchCommand = String.IsNullOrWhiteSpace(watch.Command) ? command : watch.Command;
                        string settings = watch.SettingsPath ?? "";
                        string key = watchCommand + "\0" + settings;
                        bool needsQuery = File.Exists(watch.LogPath + ".dispatch-exit") || !String.IsNullOrEmpty(watch.JobId);
                        if (needsQuery && !batches.ContainsKey(key))
                        {
                            object[] rows = null;
                            try
                            {
                                string json = Query(watchCommand, "agents --json --all", watch.Directory, settings);
                                rows = new JavaScriptSerializer { MaxJsonLength = 2097152 }.DeserializeObject(json.Trim()) as object[];
                                if (rows == null) throw new InvalidOperationException("Claude agents 未返回 JSON 数组。");
                            }
                            catch (Exception ex) { errors[key] = ex.Message; }
                            batches[key] = rows;
                        }
                        object[] sessions;
                        batches.TryGetValue(key, out sessions);
                        string queryError;
                        errors.TryGetValue(key, out queryError);
                        BackgroundSessionSnapshot snapshot = new BackgroundSessionSnapshot();
                        int launchCode;
                        if (ReadExit(watch.LogPath, out launchCode) && launchCode != 0)
                        {
                            snapshot.Finished = true; snapshot.DispatchCode = launchCode; snapshot.State = "failed";
                            snapshot.Status = "Claude Background 创建失败";
                            snapshot.Output = ReadLog(watch.LogPath) + "\r\n[创建失败] CLI 退出码 " + launchCode + "；请查看本次启动窗口并检查目录信任、登录和 CLI 版本。";
                        }
                        else
                        {
                            Dictionary<string, object> found = FindSession(sessions, watch.JobName, watch.JobId);
                            if (found != null)
                            {
                                snapshot = ParseSession(found);
                                if (ValidJobId(snapshot.JobId))
                                {
                                    try { snapshot.Output =
#if XIANPAL_LINUX
                                        XianPal.Linux.LinuxTerminal.CleanOutput(Query(watchCommand, "logs '" + snapshot.JobId + "'", watch.Directory, settings)); }
#else
                                        ActivityTrayForm.NormalizeOutput(Query(watchCommand, "logs '" + snapshot.JobId + "'", watch.Directory, settings)); }
#endif
                                    catch { snapshot.Output = ""; } // state remains authoritative; logs are optional
                                }
                            }
                            else if (needsQuery)
                            {
                                snapshot.State = "unknown";
                                snapshot.Status = String.IsNullOrEmpty(queryError) ? "后台会话状态待确认；可打开 claude agents 查看" : "后台状态暂不可用；保留运行中，稍后重试";
                            }
                            // An untrusted workspace can legitimately wait at
                            // its visible trust dialog for an arbitrary time.
                            // Never interpret dispatch exit 0 as task success.
                            if (!File.Exists(watch.LogPath + ".dispatch-exit") && !String.IsNullOrEmpty(watch.JobId))
                                snapshot.Status = "后台会话状态待确认";
                            if (String.IsNullOrEmpty(snapshot.Output) && String.IsNullOrEmpty(watch.JobId) && String.IsNullOrEmpty(snapshot.JobId))
                                snapshot.Output = ReadLog(watch.LogPath);
                        }
                        results.Add(new KeyValuePair<BackgroundTaskWatch, BackgroundSessionSnapshot>(watch, snapshot));
                    }
                }
                catch { /* Transient I/O/service errors never release a running slot. */ }
                finally { lock (gate) { if (!disposed) pending = results; busy = false; } }
            });
        }

        internal static bool ValidJobId(string id) { return !String.IsNullOrEmpty(id) && Regex.IsMatch(id, "\\A[a-zA-Z0-9_-]{4,80}\\z"); }
        private static string Value(Dictionary<string, object> item, string key) { object value; return item.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : ""; }

        internal static Dictionary<string, object> FindSession(object[] sessions, string name, string id)
        {
            if (sessions == null) return null;
            Dictionary<string, object> match = null;
            foreach (object value in sessions)
            {
                Dictionary<string, object> row = value as Dictionary<string, object>;
                if (row == null || Value(row, "kind") != "background") continue;
                bool own = !String.IsNullOrEmpty(id) ? Value(row, "id") == id : Value(row, "name") == name;
                if (!own || !ValidJobId(Value(row, "id"))) continue;
                if (match != null) return null; // ambiguous identity: never guess
                match = row;
            }
            return match;
        }

        internal static BackgroundSessionSnapshot ParseSession(Dictionary<string, object> row)
        {
            string state = Value(row, "state"), waiting = Value(row, "waitingFor");
            if (waiting == "permission prompt") waiting = "权限确认";
            else if (waiting == "input needed") waiting = "需要回复";
            else if (waiting == "sandbox request") waiting = "沙箱请求";
            else if (waiting == "worker request") waiting = "后台请求";
            else if (waiting == "dialog open") waiting = "CLI 对话框";
            BackgroundSessionSnapshot result = new BackgroundSessionSnapshot { JobId = Value(row, "id"), SessionId = Value(row, "sessionId"), State = state };
            result.Finished = state == "done" || state == "failed" || state == "stopped";
            result.Success = state == "done";
            result.Status = state == "done" ? "Claude Background 已完成" : state == "failed" ? "Claude Background 执行失败" : state == "stopped" ? "Claude Background 已停止" : state == "blocked" ? "Claude Background 需要输入：" + waiting : state == "working" ? "Claude Background 正在运行" : "Claude Background 状态待确认";
            return result;
        }

        internal static string Query(string command, string arguments, string directory)
        {
            return Query(command, arguments, directory, "");
        }

        internal static string Query(string command, string arguments, string directory, string settingsPath)
        {
#if XIANPAL_LINUX
            string[] options;
            if (arguments == "agents --json --all") options = new[] { "agents", "--json", "--all" };
            else if (arguments.StartsWith("logs '", StringComparison.Ordinal) && arguments.EndsWith("'", StringComparison.Ordinal) && ValidJobId(arguments.Substring(6, arguments.Length - 7)))
                options = new[] { "logs", arguments.Substring(6, arguments.Length - 7) };
            else throw new ArgumentException("Unsupported background status query.");
            return XianPal.Linux.LinuxTerminal.QueryConfigured(command, directory, settingsPath, options);
#else
            string script = "$utf8=New-Object Text.UTF8Encoding($false); [Console]::OutputEncoding=$utf8; $OutputEncoding=$utf8; & '" + command.Replace("'", "''") + "' " +
                (String.IsNullOrEmpty(settingsPath) ? "" : "--settings '" + settingsPath.Replace("'", "''") + "' ") + arguments + "; if($null -ne $LASTEXITCODE){exit $LASTEXITCODE}";
            ProcessStartInfo info = new ProcessStartInfo("powershell.exe", "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)))
            { WorkingDirectory = Directory.Exists(directory) ? directory : Program.StateDirectory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            ClaudeCliRunner.ConfigureUtf8(info);
            if (!String.IsNullOrEmpty(settingsPath))
            {
                Dictionary<string, object> selected = ClaudeSettingsFile.Read(settingsPath);
                object environment;
                if (selected.TryGetValue("env", out environment))
                    foreach (KeyValuePair<string, object> pair in (Dictionary<string, object>)environment)
                        info.EnvironmentVariables[pair.Key] = Convert.ToString(pair.Value);
            }
            StringBuilder output = new StringBuilder(), error = new StringBuilder();
            using (Process process = new Process { StartInfo = info })
            {
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (output) if (output.Length < 2097152) output.AppendLine(e.Data); };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (error) if (error.Length < 4000) error.AppendLine(e.Data); };
                process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                if (!process.WaitForExit(12000)) throw new TimeoutException("Claude 状态查询超时；未终止后台任务。");
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException("Claude 状态查询失败：" + error.ToString());
                return output.ToString();
            }
#endif
        }

        private static bool ReadExit(string path, out int code)
        {
            code = 0;
            try { return File.Exists(path + ".dispatch-exit") && Int32.TryParse(File.ReadAllText(path + ".dispatch-exit").Trim(), out code); }
            catch (IOException) { return false; }
        }
        private static string ReadLog(string path) { try { return File.Exists(path) ? UiText.TailCodeUnits(File.ReadAllText(path, Encoding.UTF8), 40000) : ""; } catch { return ""; } }
        public void Dispose() { lock (gate) { disposed = true; pending = null; } timer.Stop(); timer.Dispose(); watches.Clear(); }
    }
}
