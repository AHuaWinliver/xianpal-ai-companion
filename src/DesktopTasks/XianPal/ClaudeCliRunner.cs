using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace XianPal
{
    internal sealed class ClaudeCliRunner : IDisposable
    {
        private Process process;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private bool sawStreamingText;
        private bool sawAssistantText;
        private string currentSessionId;
        private ClaudeTaskStreamRenderer taskStream;
        private string previousTaskStatus;
        private string previousTaskReaction;
        internal event Action<string> OutputReceived;
        internal event Action<string> StatusChanged;
        internal event Action<int> Completed;
        internal event Action<string> SessionIdentified;
        internal event Action<string> ReactionChanged;

        internal bool IsRunning { get { return process != null && !process.HasExited; } }

        internal void Start(string prompt, string workingDirectory)
        {
            Start(prompt, workingDirectory, null);
        }

        internal void Start(string prompt, string workingDirectory, string resumeSessionId)
        {
            Start(prompt, workingDirectory, resumeSessionId, null);
        }

        internal void Start(string prompt, string workingDirectory, string resumeSessionId, string internalTaskId)
        {
            if (IsRunning) throw new InvalidOperationException("Claude 正在处理上一个请求。");
            string claude = FindClaudeCommand();
            if (claude == null) throw new FileNotFoundException("未找到本机 Claude Code CLI。请先确认终端中可以运行 claude。 ");

            ProcessStartInfo info = BuildStartInfo(claude, workingDirectory, resumeSessionId, internalTaskId);
            process = new Process();
            process.StartInfo = info;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) ParseLine(e.Data); };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (!String.IsNullOrWhiteSpace(e.Data)) HandleDiagnostic(e.Data); };
            process.Exited += delegate
            {
                int code = 1;
                // A process can exit before its asynchronous readers deliver the
                // last tool/result event. Drain the owned task's pipes first.
                if (taskStream != null) { try { process.WaitForExit(); } catch (InvalidOperationException) { } }
                try { code = process.ExitCode; } catch { }
                Action<int> completed = Completed;
                if (completed != null) completed(code);
            };

            sawStreamingText = false;
            sawAssistantText = false;
            currentSessionId = null;
            taskStream = String.IsNullOrWhiteSpace(internalTaskId) ? null : new ClaudeTaskStreamRenderer();
            previousTaskStatus = previousTaskReaction = "";
            if (!process.Start()) throw new InvalidOperationException("Claude Code CLI 启动失败。");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            byte[] promptBytes = EncodeInput(prompt);
            Stream input = process.StandardInput.BaseStream;
            input.Write(promptBytes, 0, promptBytes.Length);
            input.Flush();
            input.Close();
            RaiseStatus("Claude 正在思考…");
        }

        internal static ProcessStartInfo BuildStartInfo(string claude, string workingDirectory, string resumeSessionId)
        {
            return BuildStartInfo(claude, workingDirectory, resumeSessionId, null);
        }

        internal static ProcessStartInfo BuildStartInfo(string claude, string workingDirectory, string resumeSessionId, string internalTaskId)
        {
            if (String.IsNullOrWhiteSpace(claude)) throw new ArgumentException("Claude CLI 命令不能为空。", "claude");
            string directory = Directory.Exists(workingDirectory)
                ? Path.GetFullPath(workingDirectory)
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string arguments = BuildArguments(resumeSessionId);
            ProcessStartInfo info = new ProcessStartInfo();
            if (claude.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || claude.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                info.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                info.Arguments = "/d /s /c \"\"" + claude + "\" " + arguments + "\"";
            }
            else
            {
                info.FileName = claude;
                info.Arguments = arguments;
            }
            info.WorkingDirectory = directory;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardInput = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            ConfigureUtf8(info);
            // Some npm/Git-Bash wrappers consult PWD instead of the Windows
            // process current directory. Keep both values in agreement.
            info.EnvironmentVariables["PWD"] = directory;
            info.EnvironmentVariables["FUFU_PET_MANAGED_SESSION"] = "1";
            if (!String.IsNullOrWhiteSpace(internalTaskId))
                info.EnvironmentVariables["FUFU_PET_INTERNAL_TASK_ID"] = internalTaskId;
            return info;
        }

        internal void Stop()
        {
            if (!IsRunning) return;
            try { process.Kill(); } catch { }
        }

        internal void ParseLine(string line)
        {
            if (taskStream != null)
            {
                string output = taskStream.Consume(line);
                if (taskStream.SessionId.Length > 0 && taskStream.SessionId != currentSessionId)
                { currentSessionId = taskStream.SessionId; if (SessionIdentified != null) SessionIdentified(currentSessionId); }
                if (output.Length > 0) RaiseOutput(output);
                if (previousTaskStatus != taskStream.Status) { previousTaskStatus = taskStream.Status; RaiseStatus(previousTaskStatus); }
                if (previousTaskReaction != taskStream.Reaction)
                { previousTaskReaction = taskStream.Reaction; if (ReactionChanged != null) ReactionChanged(previousTaskReaction); }
                return;
            }
            try
            {
                Dictionary<string, object> root = serializer.Deserialize<Dictionary<string, object>>(line);
                CaptureSession(root);
                string type = GetString(root, "type");
                if (type == "assistant")
                {
                    RaiseStatus("Claude 正在回复…");
                    if (!sawStreamingText)
                    {
                        string assistantText = ExtractContent(GetDictionary(root, "message"));
                        if (!String.IsNullOrWhiteSpace(assistantText))
                        {
                            sawAssistantText = true;
                            RaiseOutput(assistantText);
                        }
                    }
                    return;
                }
                if (type == "stream_event")
                {
                    Dictionary<string, object> streamEvent = GetDictionary(root, "event");
                    Dictionary<string, object> delta = GetDictionary(streamEvent, "delta");
                    if (GetString(delta, "type") == "text_delta")
                    {
                        string text = GetString(delta, "text");
                        if (!String.IsNullOrEmpty(text)) { sawStreamingText = true; RaiseOutput(text); }
                    }
                    return;
                }
                if (type == "result")
                {
                    string result = GetString(root, "result");
                    if (!sawStreamingText && !sawAssistantText && !String.IsNullOrWhiteSpace(result)) RaiseOutput(result);
                    return;
                }
                if (type == "system" && GetString(root, "subtype") == "init")
                    RaiseStatus("Claude CLI 已连接，正在处理…");
            }
            catch
            {
                if (!String.IsNullOrWhiteSpace(line) && !line.StartsWith("{")) RaiseOutput(line);
            }
        }

        private void HandleDiagnostic(string line)
        {
            if (ShouldSuppressDiagnostic(line))
            {
                RaiseStatus("Claude CLI 已连接本地模型");
                return;
            }
            RaiseOutput(Environment.NewLine + "[CLI] " + line.Trim() + Environment.NewLine);
        }

        internal static bool ShouldSuppressDiagnostic(string line)
        {
            if (String.IsNullOrWhiteSpace(line)) return true;
            return line.IndexOf("claude-code:unrecognized_model", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("query_source", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static string BuildArguments(string resumeSessionId)
        {
            string arguments = "-p --verbose --output-format stream-json --include-partial-messages";
            if (String.IsNullOrWhiteSpace(resumeSessionId)) return arguments;
            Guid session;
            if (!Guid.TryParse(resumeSessionId, out session))
                throw new ArgumentException("Claude 会话 ID 格式无效。", "resumeSessionId");
            return arguments + " --resume " + session.ToString("D");
        }

        internal static void ConfigureUtf8(ProcessStartInfo info)
        {
            UTF8Encoding utf8 = new UTF8Encoding(false, false);
            info.StandardOutputEncoding = utf8;
            info.StandardErrorEncoding = utf8;
        }

        internal static byte[] EncodeInput(string value)
        {
            return new UTF8Encoding(false, false).GetBytes(UiText.Normalize(value));
        }

        private void CaptureSession(Dictionary<string, object> root)
        {
            string sessionId = GetString(root, "session_id");
            if (String.IsNullOrWhiteSpace(sessionId) || String.Equals(sessionId, currentSessionId, StringComparison.OrdinalIgnoreCase)) return;
            currentSessionId = sessionId;
            Action<string> handler = SessionIdentified;
            if (handler != null) handler(sessionId);
        }

        private static string ExtractContent(Dictionary<string, object> message)
        {
            if (message == null) return "";
            object contentObject;
            if (!message.TryGetValue("content", out contentObject)) return "";
            IEnumerable content = contentObject as IEnumerable;
            if (content == null || contentObject is string) return "";
            StringBuilder text = new StringBuilder();
            foreach (object item in content)
            {
                Dictionary<string, object> block = item as Dictionary<string, object>;
                if (block != null && GetString(block, "type") == "text") text.Append(GetString(block, "text"));
            }
            return text.ToString();
        }

        private static string FindClaudeCommand()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[] candidates = {
                Path.Combine(home, ".local", "bin", "claude.exe"),
                Path.Combine(appData, "npm", "claude.cmd")
            };
            foreach (string candidate in candidates) if (File.Exists(candidate)) return candidate;
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string entry in path.Split(Path.PathSeparator))
            {
                string clean = entry.Trim().Trim('"');
                foreach (string name in new[] { "claude.exe", "claude.cmd", "claude.bat" })
                {
                    string candidate = Path.Combine(clean, name);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            return null;
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> parent, string key)
        {
            object value;
            return parent != null && parent.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }

        private static string GetString(Dictionary<string, object> parent, string key)
        {
            object value;
            return parent != null && parent.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : "";
        }

        private void RaiseOutput(string text)
        {
            Action<string> handler = OutputReceived;
            string clean = UiText.Normalize(text);
            if (handler != null && !String.IsNullOrEmpty(clean)) handler(clean);
        }

        private void RaiseStatus(string text)
        {
            Action<string> handler = StatusChanged;
            if (handler != null) handler(UiText.Normalize(text));
        }

        public void Dispose()
        {
            Stop();
            if (process != null) process.Dispose();
        }
    }
}
