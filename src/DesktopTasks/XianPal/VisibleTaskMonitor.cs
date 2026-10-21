using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace XianPal
{
    // wt.exe is a launcher, not the running PowerShell process. Completion is
    // reported by our private script, never inferred from a Terminal launcher.
    internal sealed class VisibleTaskMonitor : IDisposable
    {
        private readonly Timer timer = new Timer { Interval = 400 };
        private readonly string logPath;
        private readonly Action<string> progress;
        private readonly Action<int, string> completed;
        private readonly Action<string, string, string> stateChanged;
        private string previousState = "";
        private Process launcher;
        private DateTime started;
        private string previousOutput = "";
        private bool disposed;

        internal VisibleTaskMonitor(string path, Action<string> onProgress, Action<int, string> onCompleted)
            : this(path, onProgress, onCompleted, null) { }

        internal VisibleTaskMonitor(string path, Action<string> onProgress, Action<int, string> onCompleted, Action<string, string, string> onStateChanged)
        {
            logPath = path;
            progress = onProgress;
            completed = onCompleted;
            stateChanged = onStateChanged;
            timer.Tick += delegate { Poll(); };
        }

        internal void Start(ProcessStartInfo info)
        {
            started = DateTime.UtcNow;
            launcher = Process.Start(info);
            timer.Start();
        }

        private string ReadOutput()
        {
            try
            {
                if (!File.Exists(logPath)) return previousOutput;
                using (FileStream file = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(file, Encoding.UTF8, true))
                    return UiText.Normalize(reader.ReadToEnd());
            }
            catch (IOException) { return previousOutput; }
        }

        internal void Poll()
        {
            if (disposed) return;
            try
            {
                if (File.Exists(logPath + ".state"))
                {
                    string state = File.ReadAllText(logPath + ".state", Encoding.UTF8);
                    string[] fields = state.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    if (fields.Length >= 3 && state != previousState)
                    {
                        previousState = state;
                        if (stateChanged != null) stateChanged(fields[0], fields[1], fields[2]);
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            string output = ReadOutput();
            if (output != previousOutput)
            {
                previousOutput = output;
                if (progress != null) progress(output);
            }
            try
            {
                int code;
                if (File.Exists(logPath + ".completed") && Int32.TryParse(File.ReadAllText(logPath + ".completed", Encoding.UTF8).Trim(), out code))
                {
                    Finish(code, ReadOutput());
                    return;
                }
                if (File.Exists(logPath + ".started"))
                {
                    string[] identity = File.ReadAllText(logPath + ".started", Encoding.UTF8).Trim().Split('|');
                    int pid; long ticks;
                    if (identity.Length != 2 || !Int32.TryParse(identity[0], out pid) || !Int64.TryParse(identity[1], out ticks)) return;
                    bool alive = false;
                    try
                    {
                        using (Process shell = Process.GetProcessById(pid))
                            alive = !shell.HasExited && shell.StartTime.ToUniversalTime().Ticks == ticks;
                    }
                    catch (ArgumentException) { }
                    catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception) { return; }
                    if (!alive)
                    {
                        // Allow the final atomic marker to arrive before treating
                        // a manually closed window as interrupted.
                        if (File.Exists(logPath + ".completed")) return;
                        Finish(-1, output + "\r\n[任务中断] 任务窗口已关闭，未收到完成信号。");
                    }
                }
                else if ((DateTime.UtcNow - started).TotalSeconds > 45)
                    Finish(-1, output + "\r\n[启动失败] 45 秒内未收到任务脚本启动信号。请检查终端安装和 PowerShell 执行权限。");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private void Finish(int code, string output)
        {
            Dispose();
            if (completed != null) completed(code, output);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            timer.Stop();
            timer.Dispose();
            if (launcher != null) launcher.Dispose();
            // Never kill a shell or other Claude process when monitoring ends.
        }
    }
}
