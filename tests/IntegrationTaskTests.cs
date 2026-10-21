using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using XianPal;

internal static class IntegrationTaskTests
{
    private static string root;
    private static string pet;
    private static string installedBridge;
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static bool PumpUntil(Func<bool> ready, int milliseconds)
    {
        Stopwatch watch = Stopwatch.StartNew();
        while (!ready() && watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(20); }
        return ready();
    }

    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 2 && args[0] == "--session-id") return MockNativeCli(args);
        if (args.Length > 0 && args[0] == "-p")
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            if (Array.IndexOf(args, "stream-json") >= 0 && args.Length > 1 && !args[1].Contains("FAIL_TEST"))
            {
                if (Array.IndexOf(args, "--include-partial-messages") < 0) return 12;
                Emit(new { type = "system", subtype = "init", session_id = "11111111-1111-4111-8111-111111111111" });
                Emit(new { type = "stream_event", @event = new { type = "message_start", message = new { id = "m1" } } });
                string reply = "工作目录=" + Environment.CurrentDirectory + "\n中文回复：你好，世界！✅ 😊 🚀\nMANAGED=" + Environment.GetEnvironmentVariable("FUFU_PET_MANAGED_SESSION") + "\nTASK=" + Environment.GetEnvironmentVariable("FUFU_PET_INTERNAL_TASK_ID");
                Emit(new { type = "stream_event", @event = new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text = reply } } });
                Thread.Sleep(1200);
                Emit(new { type = "assistant", message = new { id = "m1", content = new[] { new { type = "text", text = reply } } } });
                Emit(new { type = "stream_event", @event = new { type = "content_block_start", index = 1, content_block = new { type = "tool_use", id = "tool1", name = "Bash", input = new { } } } });
                Emit(new { type = "stream_event", @event = new { type = "content_block_delta", index = 1, delta = new { type = "input_json_delta", partial_json = "{\"command\":\"echo 测试工具\"}" } } });
                Emit(new { type = "stream_event", @event = new { type = "content_block_stop", index = 1 } });
                Thread.Sleep(1600);
                Emit(new { type = "user", message = new { content = new[] { new { type = "tool_result", tool_use_id = "tool1", content = "工具执行成功 ✅", is_error = false } } } });
                Thread.Sleep(500);
                Emit(new { type = "stream_event", @event = new { type = "message_start", message = new { id = "m2" } } });
                Emit(new { type = "stream_event", @event = new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text = "结束：文件处理完成。" } } });
                Emit(new { type = "assistant", message = new { id = "m2", content = new[] { new { type = "text", text = "结束：文件处理完成。" } } } });
                Emit(new { type = "result", result = "结束：文件处理完成。", is_error = false });
                Thread.Sleep(700);
                return 0;
            }
            Console.WriteLine("工作目录=" + Environment.CurrentDirectory);
            Console.WriteLine("中文回复：你好，世界！✅ 😊 🚀");
            Console.WriteLine("MANAGED=" + Environment.GetEnvironmentVariable("FUFU_PET_MANAGED_SESSION"));
            Console.WriteLine("TASK=" + Environment.GetEnvironmentVariable("FUFU_PET_INTERNAL_TASK_ID"));
            Thread.Sleep(1600);
            Console.WriteLine("结束：文件处理完成。");
            return args.Length > 1 && args[1].Contains("FAIL_TEST") ? 7 : 0;
        }
        try
        {
            if (args.Length > 0 && args[0] == "--live-pet")
            {
                root = args[1]; Application.EnableVisualStyles(); TestPetLiveFeedback(); return 0;
            }
            root = Environment.GetEnvironmentVariable("FUFU_STATE_DIR");
            if (String.IsNullOrEmpty(root) || !root.Contains("integration-task-test")) throw new Exception("isolated test state required");
            pet = args[0];
            Environment.SetEnvironmentVariable("FUFU_NATIVE_TEST_PET", Path.GetFullPath(pet));
            if (args.Length > 1) ReadInstalledBridge(args[1]);
            Directory.CreateDirectory(root);
            Application.EnableVisualStyles();
            TestPreferencesAndHooks();
            TestUnicodeForms();
            TestStreamRenderer();
            TestVisibleRuns();
            TestNativeRuns();
            AppDomain domain = AppDomain.CreateDomain("Isolated live pet feedback", null, new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(Path.GetFullPath(pet)) });
            try { Require(domain.ExecuteAssembly(Assembly.GetExecutingAssembly().Location, new[] { "--live-pet", root }) == 0, "live pet feedback end-to-end"); }
            finally { AppDomain.Unload(domain); }
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }

    private static int MockNativeCli(string[] args)
    {
        if (Environment.GetEnvironmentVariable("FUFU_TEST_MODE") != "1") return 90;
        string log = Environment.GetEnvironmentVariable("FUFU_PET_NATIVE_RUN_LOG");
        if (String.IsNullOrEmpty(log) || !log.Contains("integration-task-test")) return 91;
        Console.OutputEncoding = new UTF8Encoding(false);
        JavaScriptSerializer json = new JavaScriptSerializer();
        File.WriteAllText(log + ".console", json.Serialize(new { InputRedirected = Console.IsInputRedirected, OutputRedirected = Console.IsOutputRedirected, Args = args, Cwd = Environment.CurrentDirectory }), new UTF8Encoding(false));
        Console.WriteLine("\u001b[32mMOCK 原生 Claude CLI 交互界面\u001b[0m");
        string session = args[1], reply = "原生中文回复 ✅ 😊";
        NativeHook("SessionStart", new { session_id = session });
        NativeHook("UserPromptSubmit", new { session_id = session, prompt = args[2] });
        NativeHook("MessageDisplay", new { session_id = session, delta = "先检查指定目录。\n", message_id = "intro", index = 0, final = true });
        NativeHook("MessageDisplay", new { session_id = session, delta = reply, message_id = "native-reply", index = 0, final = true });
        NativeHook("PreToolUse", new { session_id = session, tool_name = "Read", tool_input = new { file_path = "原生.txt" } });
        Thread.Sleep(1600);
        NativeHook("PostToolUse", new { session_id = session, tool_name = "Read", tool_response = new { text = "读取成功" } });
        NativeHook("Stop", new { session_id = session, last_assistant_message = reply });
        File.WriteAllText(log + ".waiting", "CLI 仍在等待下一次交互");
        Stopwatch wait = Stopwatch.StartNew();
        while (!File.Exists(log + ".continue") && wait.ElapsedMilliseconds < 12000) Thread.Sleep(50);
        if (!File.Exists(log + ".continue")) return 92;
        string followup = File.ReadAllText(log + ".continue", Encoding.UTF8);
        NativeHook("UserPromptSubmit", new { session_id = session, prompt = followup });
        NativeHook("MessageDisplay", new { session_id = session, delta = "原生继续追问成功 🚀 结束：文件处理完成。", message_id = "followup", index = 0, final = true });
        bool failed = args[2].Contains("FAIL_TEST");
        NativeHook(failed ? "StopFailure" : "Stop", new { session_id = session, last_assistant_message = "原生继续追问成功 🚀 结束：文件处理完成。" });
        NativeHook("SessionEnd", new { session_id = session });
        return failed ? 7 : 0;
    }

    private static void NativeHook(string name, object value)
    {
        string executable = Environment.GetEnvironmentVariable("FUFU_NATIVE_TEST_PET");
        if (String.IsNullOrEmpty(executable)) throw new Exception("native test hook target missing");
        ProcessStartInfo info = new ProcessStartInfo(executable, "--hook " + name) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using (Process hook = Process.Start(info))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(value));
            hook.StandardInput.BaseStream.Write(bytes, 0, bytes.Length); hook.StandardInput.Close();
            if (!hook.WaitForExit(5000) || hook.ExitCode != 0) throw new Exception("native hook failed: " + name);
            if (hook.StandardOutput.ReadToEnd().Length != 0) throw new Exception("native hook must not replace CLI display content");
        }
    }

    private static void TestNativeRuns()
    {
        Assembly package = Assembly.LoadFrom(pet);
        Type taskType = package.GetType("XianPal.ScheduledClaudeTask"), form = package.GetType("XianPal.PetForm");
        MethodInfo build = form.GetMethod("BuildVisibleTaskStartInfo", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] { taskType, typeof(string), typeof(string) }, null);
        string logs = Path.Combine(root, "scheduled-task-runs"); Directory.CreateDirectory(logs);
        foreach (string mode in new[] { "interactive-powershell", "interactive-terminal" })
            for (int variant = 0; variant < 2; variant++)
            {
                object task = Activator.CreateInstance(taskType, true);
                taskType.GetField("RunMode").SetValue(task, mode);
                taskType.GetField("Prompt").SetValue(task, (variant == 1 ? "FAIL_TEST " : "") + "原生交互任务");
                taskType.GetField("WorkingDirectory").SetValue(task, root);
                if (mode == "interactive-terminal" && variant == 1 && VirtualDesktopService.GetDesktopIds().Count >= 2) taskType.GetField("DesktopNumber").SetValue(task, 2);
                string log = Path.Combine(logs, "native-" + Guid.NewGuid().ToString("N") + ".log");
                ProcessStartInfo info = (ProcessStartInfo)build.Invoke(null, new object[] { task, log, Assembly.GetExecutingAssembly().Location });
                string script = File.ReadAllText(log + ".ps1"), token = Regex.Match(script, @"OrbitTask-[a-f0-9]{32}").Value;
                Require(!script.Contains(" -p ") && !script.Contains("--output-format") && !script.Contains("ForEach-Object") && !script.Contains("2>&1") && !script.Contains("RedirectStandard"), mode + " native command has no print mode / TUI capture / pipes");
                bool done = false; int code = -1; string output = "", reaction = "";
                using (VisibleTaskMonitor monitor = new VisibleTaskMonitor(log, delegate(string text) { output = text; }, delegate(int result, string text) { code = result; output = text; done = true; }, delegate(string status, string session, string evt) { reaction = evt; }))
                {
                    try
                    {
                        monitor.Start(info);
                        Require(PumpUntil(delegate { return File.Exists(log + ".waiting") && output.Contains("原生中文回复"); }, 12000), mode + " own native hooks update progress");
                        Require(!done && !File.Exists(log + ".completed"), mode + " first reply does not close interactive session or release blocking slot");
                        Dictionary<string, object> console = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(log + ".console"));
                        Require(!(bool)console["InputRedirected"] && !(bool)console["OutputRedirected"] && (string)console["Cwd"] == root, mode + " keyboard and display handles remain attached to console in selected directory");
                        Require(Regex.Matches(output, "原生中文回复").Count == 1, mode + " native MessageDisplay / Stop are not duplicate replies");
                        Require(PumpUntil(delegate { return reaction == "Stop"; }, 1500), mode + " native first turn can return pet to idle");
                        ValidateNativeIsolation(log);
                        File.WriteAllText(log + ".continue", "用户在原生窗口继续交互", new UTF8Encoding(false));
                        Require(PumpUntil(delegate { return done; }, 8000) && code == (variant == 1 ? 7 : 0), mode + " actual CLI session exit produces correct completion");
                        Require(output.Contains("原生继续追问成功") && Regex.Matches(output, "原生继续追问成功").Count == 1, mode + " same native session accepts another interaction");
                        if (mode == "interactive-terminal")
                        {
                            IntPtr window = VirtualDesktopService.FindTaskTerminalWindow(token);
                            Require(window != IntPtr.Zero, "native Terminal is not auto closed");
                            if (variant == 1 && VirtualDesktopService.GetDesktopIds().Count >= 2) Require(VirtualDesktopService.GetWindowDesktop(window) == VirtualDesktopService.ResolveDesktop(2), "native Terminal Desktop 2 retained");
                        }
                    }
                    finally
                    {
                        // Only our private, positively identified test window /
                        // shell is stopped, after the mock CLI has exited.
                        if (done)
                        {
                            IntPtr window = VirtualDesktopService.FindTaskTerminalWindow(token);
                            if (window != IntPtr.Zero) PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
                            else if (File.Exists(log + ".started"))
                            {
                                string[] identity = File.ReadAllText(log + ".started").Split('|');
                                try { using (Process shell = Process.GetProcessById(Int32.Parse(identity[0]))) if (shell.StartTime.ToUniversalTime().Ticks == Int64.Parse(identity[1])) shell.Kill(); }
                                catch (ArgumentException) { }
                            }
                        }
                        else File.WriteAllText(log + ".continue", "测试收尾退出");
                    }
                }
            }
    }

    private static void ValidateNativeIsolation(string log)
    {
        Dictionary<string, object> owner = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(log + ".interactive"));
        Dictionary<string, string> previous = new Dictionary<string, string>();
        foreach (string name in new[] { "FUFU_PET_NATIVE_RUN_LOG", "FUFU_PET_NATIVE_RUN_TOKEN", "FUFU_PET_INTERNAL_TASK_ID", "FUFU_PET_MANAGED_SESSION" }) previous[name] = Environment.GetEnvironmentVariable(name);
        string before = File.ReadAllText(log);
        try
        {
            Environment.SetEnvironmentVariable("FUFU_PET_NATIVE_RUN_LOG", log);
            Environment.SetEnvironmentVariable("FUFU_PET_NATIVE_RUN_TOKEN", (string)owner["Token"]);
            Environment.SetEnvironmentVariable("FUFU_PET_INTERNAL_TASK_ID", (string)owner["TaskId"]);
            Environment.SetEnvironmentVariable("FUFU_PET_MANAGED_SESSION", "1");
            NativeHook("MessageDisplay", new { session_id = Guid.NewGuid().ToString("D"), delta = "foreign session must not be collected" });
            Require(File.ReadAllText(log) == before, "native hook rejects another / child Claude session");
            Environment.SetEnvironmentVariable("FUFU_PET_NATIVE_RUN_TOKEN", Guid.NewGuid().ToString("N"));
            NativeHook("MessageDisplay", new { session_id = (string)owner["SessionId"], delta = "wrong run token" });
            Require(File.ReadAllText(log) == before, "native hook rejects mismatched run token");
            Environment.SetEnvironmentVariable("FUFU_PET_NATIVE_RUN_TOKEN", (string)owner["Token"]);
            string outside = Path.Combine(root, "unrelated-test.log");
            File.WriteAllText(outside, "preserve"); File.WriteAllText(outside + ".interactive", new JavaScriptSerializer().Serialize(owner));
            Environment.SetEnvironmentVariable("FUFU_PET_NATIVE_RUN_LOG", outside);
            NativeHook("MessageDisplay", new { session_id = (string)owner["SessionId"], delta = "path outside run folder" });
            Require(File.ReadAllText(outside) == "preserve", "native hook cannot write outside owned run directory");
        }
        finally { foreach (KeyValuePair<string, string> item in previous) Environment.SetEnvironmentVariable(item.Key, item.Value); }
    }

    private static void Emit(object value) { Console.WriteLine(new JavaScriptSerializer().Serialize(value)); Console.Out.Flush(); }
    private static string Feed(ClaudeTaskStreamRenderer renderer, object value) { return renderer.Consume(new JavaScriptSerializer().Serialize(value)); }
    private static void TestStreamRenderer()
    {
        ClaudeTaskStreamRenderer renderer = new ClaudeTaskStreamRenderer();
        Feed(renderer, new { type = "stream_event", @event = new { type = "message_start", message = new { id = "a" } } });
        string output = Feed(renderer, new { type = "stream_event", @event = new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text = "中文 😊" } } });
        output += Feed(renderer, new { type = "assistant", message = new { id = "a", content = new[] { new { type = "text", text = "中文 😊" } } } });
        output += Feed(renderer, new { type = "result", result = "中文 😊", is_error = false });
        Require(output == "中文 😊", "streaming / assistant / result are not duplicate replies");
        output = Feed(renderer, new { type = "assistant", message = new { id = "b", content = new[] { new { type = "text", text = "第二轮回复" } } } });
        Require(output == "第二轮回复", "later tool-turn assistant replies are not lost");
        string tool = Feed(renderer, new { type = "assistant", message = new { id = "c", content = new[] { new { type = "tool_use", id = "t", name = "Read", input = new { file_path = "中文.txt" } } } } });
        Require(tool.Contains("[工具] Read") && tool.Contains("中文.txt") && renderer.Reaction == "PreToolUse", "tool name / input and pet reaction exposed");
        Require(Feed(renderer, new { type = "assistant", message = new { id = "c", content = new[] { new { type = "tool_use", id = "t", name = "Read", input = new { file_path = "中文.txt" } } } } }) == "", "duplicate tool events suppressed");
        string failure = Feed(renderer, new { type = "user", message = new { content = new[] { new { type = "tool_result", tool_use_id = "t", content = "没有权限", is_error = true } } } });
        Require(failure.Contains("没有权限") && renderer.Reaction == "PostToolUseFailure", "tool errors visible and trigger failure reaction");
        Feed(renderer, new { type = "result", is_error = false, permission_denials = new[] { new { tool_name = "Bash" } } });
        Require(renderer.Reaction == "PermissionRequest", "permission denial reported without bypassing permission settings");
        ClaudeTaskStreamRenderer blocks = new ClaudeTaskStreamRenderer();
        Feed(blocks, new { type = "stream_event", @event = new { type = "message_start", message = new { id = "multi" } } });
        string blockText = "";
        for (int index = 0; index < 2; index++)
        {
            string text = index == 0 ? "第一段" : "第二段";
            blockText += Feed(blocks, new { type = "stream_event", @event = new { type = "content_block_delta", index = index, delta = new { type = "text_delta", text = text } } });
            blockText += Feed(blocks, new { type = "assistant", message = new { id = "multi", content = new[] { new { type = "text", text = text } } } });
        }
        Require(blockText == "第一段第二段", "multiple assistant blocks sharing message ID remain complete and deduplicated");
        using (ClaudeCliRunner runner = new ClaudeCliRunner())
        {
            typeof(ClaudeCliRunner).GetField("taskStream", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner, new ClaudeTaskStreamRenderer());
            string text = "", reaction = "";
            runner.OutputReceived += delegate(string value) { text += value; };
            runner.ReactionChanged += delegate(string value) { reaction = value; };
            runner.ParseLine(new JavaScriptSerializer().Serialize(new { type = "assistant", message = new { id = "background", content = new[] { new { type = "tool_use", id = "bg", name = "Read", input = new { file_path = "后台.txt" } } } } }));
            Require(text.Contains("后台.txt") && reaction == "PreToolUse", "background scheduled parser forwards tool text and reaction");
        }
    }

    private static object Field(object value, string name) { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }
    private static void TestPetLiveFeedback()
    {
        string bin = Path.Combine(root, "mock-cli"); Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "claude.cmd"), "@echo off\r\n\"" + Assembly.GetExecutingAssembly().Location + "\" %*\r\n", Encoding.ASCII);
        string oldPath = Environment.GetEnvironmentVariable("PATH");
        string oldState = Environment.GetEnvironmentVariable("FUFU_STATE_DIR");
        Environment.SetEnvironmentVariable("PATH", bin + Path.PathSeparator + oldPath);
        try
        {
            foreach (string mode in new[] { "powershell", "terminal", "interactive-powershell", "interactive-terminal" })
            {
                Environment.SetEnvironmentVariable("FUFU_STATE_DIR", Path.Combine(root, "live-" + mode));
                using (PetForm form = new PetForm())
                {
                    ReplyBubbleForm bubble = (ReplyBubbleForm)Field(form, "replyBubble"); bubble.Opacity = 0.01;
                    ClaudeTaskService service = (ClaudeTaskService)Field(form, "claudeTaskService");
                    ScheduledClaudeTask task = service.Add("实时测试 中文任务", root, "interval", TimeSpan.Zero, DayOfWeek.Monday, 60, DateTime.Now, mode, "block");
                    form.GetType().GetMethod("QueueScheduledClaudeTask", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { task });
                    ClaudeTaskRunRecord run = task.History[0];
                    Require(bubble.Visible, mode + " bubble shows at task start, not completion");
                    Require(PumpUntil(delegate { return run.Transcript.Contains("中文回复") && run.Status == "running"; }, 10000), mode + " task history receives reply before completion");
                    Require(((StringBuilder)Field(bubble, "response")).ToString().Contains("中文回复"), mode + " pet bubble receives streamed reply");
                    System.Collections.Generic.List<ActivityRecord> activities = (System.Collections.Generic.List<ActivityRecord>)Field(form, "activities");
                    Require(activities.Count == 1 && activities[0].Output.Contains("中文回复"), mode + " exactly one activity with live output");
                    if (ClaudeTaskService.IsInteractiveMode(mode))
                    {
                        Require(activities[0].NativeInteractive, "native activity continues conversation in CLI, not GUI stream mode");
                        Require(!(bool)form.GetType().GetMethod("RunClaudeReply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { activities[0], "不应另起 -p 会话" }), "native session cannot be resumed concurrently by GUI reply");
                    }
                    Require(PumpUntil(delegate { return (string)Field(form, "state") == "review" && run.Status == "running"; }, 3500), mode + " pet reacts while tool is executing");
                    if (ClaudeTaskService.IsInteractiveMode(mode))
                        File.WriteAllText(Path.Combine(Program.StateDirectory, "scheduled-task-runs", run.Id + ".log.continue"), "继续原生对话", new UTF8Encoding(false));
                    form.ContextMenuStrip.Opacity = 0.01;
                    form.ContextMenuStrip.Show(new Point(1, 1)); Application.DoEvents();
                    Require(!bubble.Visible && bubble.UserDismissed, "right-click menu automatically dismisses reply bubble");
                    form.ContextMenuStrip.Close();
                    using (ClaudeTaskRunTranscriptForm transcript = new ClaudeTaskRunTranscriptForm(task, run))
                    {
                        transcript.Opacity = 0.01; transcript.Show();
                        Require(PumpUntil(delegate { return run.Status == "success"; }, 6000), mode + " live task completes");
                        Require(PumpUntil(delegate
                        {
                            foreach (Control control in transcript.Controls) if (control is RichTextBox && control.Text.Contains("结束：文件处理完成。")) return true;
                            return false;
                        }, 1500), mode + " opened transcript updates without reopening");
                    }
                    string text = ((StringBuilder)Field(bubble, "response")).ToString();
                    Require(Regex.Matches(text, "中文回复").Count == 1 && Regex.Matches(text, "结束：文件处理完成。").Count == 1, mode + " completion does not reset or duplicate bubble content");
                    Require(activities.Count == 1 && run.Transcript.Contains("[工具结果]"), mode + " one conversation preserves tool process");
                    Require(!bubble.Visible && bubble.UserDismissed, mode + " dismissed bubble is not reopened by progress or completion");
                    if (mode == "terminal" || mode == "interactive-terminal")
                    {
                        string script = File.ReadAllText(Path.Combine(Program.StateDirectory, "scheduled-task-runs", run.Id + ".log.ps1"));
                        string token = Regex.Match(script, @"OrbitTask-[a-f0-9]{32}").Value;
                        IntPtr window = VirtualDesktopService.FindTaskTerminalWindow(token);
                        if (window != IntPtr.Zero) PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
                    }
                    else if (mode == "interactive-powershell")
                    {
                        string[] identity = File.ReadAllText(Path.Combine(Program.StateDirectory, "scheduled-task-runs", run.Id + ".log.started")).Split('|');
                        try { using (Process shell = Process.GetProcessById(Int32.Parse(identity[0]))) if (shell.StartTime.ToUniversalTime().Ticks == Int64.Parse(identity[1])) shell.Kill(); } catch (ArgumentException) { }
                    }
                    form.Hide();
                }
            }
        }
        finally { Environment.SetEnvironmentVariable("PATH", oldPath); Environment.SetEnvironmentVariable("FUFU_STATE_DIR", oldState); }
    }

    private static void ReadInstalledBridge(string settingsPath)
    {
        System.Collections.Generic.Dictionary<string, object> settings = new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string, object>>(File.ReadAllText(settingsPath, Encoding.UTF8));
        System.Collections.Generic.Dictionary<string, object> hooks = (System.Collections.Generic.Dictionary<string, object>)settings["hooks"];
        int matches = 0;
        foreach (object group in (System.Collections.IEnumerable)hooks["MessageDisplay"])
            foreach (object handler in (System.Collections.IEnumerable)((System.Collections.Generic.Dictionary<string, object>)group)["hooks"])
            {
                string command = Convert.ToString(((System.Collections.Generic.Dictionary<string, object>)handler)["command"]);
                Match encoded = Regex.Match(command, @"-EncodedCommand\s+([A-Za-z0-9+/=]+)$");
                if (!encoded.Success) continue;
                string script = Encoding.Unicode.GetString(Convert.FromBase64String(encoded.Groups[1].Value));
                if (!script.Contains(pet.Replace('\\', '/').Replace("'", "''"))) continue;
                matches++; installedBridge = command;
                Require(!command.Contains("WindowStyle") && script.Contains("UTF8Encoding"), "installed hook does not hide caller console and explicitly uses UTF-8");
            }
        Require(matches == 1, "reinstall leaves exactly one pet hook, not duplicated encoded commands");
    }

    private static void TestPreferencesAndHooks()
    {
        string preferences = Path.Combine(root, "claude-integration.json");
        if (File.Exists(preferences)) File.Delete(preferences);
        Require(!IntegrationPreferences.Load().MonitorExternalSessions, "external import defaults OFF");
        Require(!IntegrationPreferences.ShouldAcceptHook(null, null, false), "unowned hook rejected");
        Require(IntegrationPreferences.ShouldAcceptHook(null, "1", false), "pet-owned hook accepted");
        Require(!IntegrationPreferences.ShouldAcceptHook("task", "1", true), "scheduled hooks never duplicate run history");
        Require(IntegrationPreferences.ShouldAcceptHook(null, null, true), "external import remains an explicit opt-in");
        IntegrationPreferences settings = new IntegrationPreferences { MonitorExternalSessions = true };
        settings.Save();
        Require(IntegrationPreferences.Load().MonitorExternalSessions, "opt-in preference persists");
        settings.MonitorExternalSessions = false; settings.Save();
        string state = Program.StateFile;
        if (File.Exists(state)) File.Delete(state);
        RunHook(false, null);
        Require(!File.Exists(state), "production hook does not collect another local Claude session");
        RunHook(true, null);
        Require(File.Exists(state) && File.ReadAllText(state).Contains("MessageDisplay"), "production owned hook still publishes");
        RunHook(true, null, true);
        string bridgeMetadata = Encoding.UTF8.GetString(Convert.FromBase64String(File.ReadAllText(state).Split('|')[2]));
        Require(bridgeMetadata.Contains("外部中文回复") && bridgeMetadata.Contains("😊"), "PowerShell hook bridge preserves UTF-8 stdin");
        string owned = File.ReadAllText(state);
        RunHook(true, "scheduled-test");
        Require(File.ReadAllText(state) == owned, "production scheduled hook suppressed without touching owned record");
        settings.MonitorExternalSessions = true; settings.Save();
        RunHook(false, null);
        string metadata = Encoding.UTF8.GetString(Convert.FromBase64String(File.ReadAllText(state).Split('|')[2]));
        Require(metadata.Contains("外部中文回复") && metadata.Contains("pet_managed"), "opt-in hook preserves Chinese metadata");
        settings.MonitorExternalSessions = false; settings.Save();
        ProcessStartInfo background = ClaudeCliRunner.BuildStartInfo("claude.cmd", root, null);
        Require(background.EnvironmentVariables["FUFU_PET_MANAGED_SESSION"] == "1", "background conversations carry ownership marker");
        Require(PetForm.BuildTerminalStartInfo(root).Arguments.Contains("FUFU_PET_MANAGED_SESSION"), "interactive CLI carries ownership marker");
    }

    private static void RunHook(bool managed, string taskId, bool bridge = false)
    {
        string executable = bridge ? "powershell.exe" : pet;
        string script = "$ProgressPreference='SilentlyContinue'; $OutputEncoding=New-Object Text.UTF8Encoding($false); $reader=New-Object IO.StreamReader([Console]::OpenStandardInput(),(New-Object Text.UTF8Encoding($false))); $json=$reader.ReadToEnd(); $json | & '" + pet.Replace('\\', '/').Replace("'", "''") + "' --hook MessageDisplay";
        string arguments = bridge ? "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) : "--hook MessageDisplay";
        if (bridge)
        {
            string bash = @"C:\Program Files\Git\bin\bash.exe";
            string bridgeScript = Path.Combine(root, "hook-bridge.sh");
            File.WriteAllText(bridgeScript, "printf '%s' '{\"session_id\":\"isolated-session\",\"delta\":\"外部中文回复 😊\"}' | " + (installedBridge ?? "powershell.exe " + arguments) + "\n", new UTF8Encoding(false));
            executable = bash;
            arguments = "\"" + bridgeScript.Replace('\\', '/') + "\"";
        }
        ProcessStartInfo info = new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = !bridge, RedirectStandardError = true };
        info.EnvironmentVariables["FUFU_PET_MANAGED_SESSION"] = managed ? "1" : "";
        info.EnvironmentVariables["FUFU_PET_INTERNAL_TASK_ID"] = taskId ?? "";
        using (Process process = Process.Start(info))
        {
            string error = "";
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) error += e.Data; };
            process.BeginErrorReadLine();
            if (!bridge)
            {
                byte[] json = Encoding.UTF8.GetBytes("{\"session_id\":\"isolated-session\",\"delta\":\"外部中文回复 😊\"}");
                process.StandardInput.BaseStream.Write(json, 0, json.Length);
                process.StandardInput.Close();
            }
            bool exited = process.WaitForExit(15000);
            if (!exited) { Console.WriteLine("Isolated hook timeout, PID=" + process.Id); try { process.Kill(); } catch { } }
            if (exited) process.WaitForExit();
            Require(exited && process.ExitCode == 0, "hook exits cleanly" + (exited && process.ExitCode == 0 ? "" : " " + error));
        }
    }

    private static void TestUnicodeForms()
    {
        string chinese = "中文回复：你好，世界！✅ 😊 🚀\r\n文件处理完成。";
        using (Font standard = UiText.Font(9f))
        using (Font content = UiText.ContentFont(9f))
            Require(standard.FontFamily.Name == content.FontFamily.Name, "Chinese body and UI font families match");
        using (UnicodeRichTextBox box = new UnicodeRichTextBox())
        {
            box.Font = UiText.ContentFont(9f); box.Text = chinese;
            UiText.ApplyMixedGlyphFonts(box);
            Require(box.UsesUnicodeRichEdit && box.Text.Contains("你好，世界") && box.Text.Contains("😊"), "RICHEDIT50W preserves Chinese and emoji");
            box.Select(0, 2);
            Require(box.SelectionFont.FontFamily.Name == box.Font.FontFamily.Name, "Chinese is not rendered in emoji font");
        }
        using (ChineseInputTextBox input = new ChineseInputTextBox())
        {
            MethodInfo wndProc = typeof(ChineseInputTextBox).GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic);
            input.CreateControl();
            Message start = Message.Create(input.Handle, 0x010D, IntPtr.Zero, IntPtr.Zero);
            wndProc.Invoke(input, new object[] { start });
            Require(input.IsComposing, "Chinese IME candidate Enter cannot submit a conversation");
            Message end = Message.Create(input.Handle, 0x010E, IntPtr.Zero, IntPtr.Zero);
            wndProc.Invoke(input, new object[] { end });
            Require(input.IsComposing, "IME commit key is guarded");
            Thread.Sleep(120);
            Require(!input.IsComposing, "Enter sending resumes after IME commit");
        }
        ScheduledClaudeTask task = new ScheduledClaudeTask { Prompt = "整理指定目录里的中文文件", WorkingDirectory = root, RunMode = "terminal" };
        ClaudeTaskRunRecord record = new ClaudeTaskRunRecord { Status = "success", RunMode = "terminal", Transcript = chinese, Summary = chinese, StartedAt = DateTime.Now.ToString("o"), CompletedAt = DateTime.Now.ToString("o") };
        task.History.Add(record);
        using (Form history = new ClaudeTaskHistoryForm(task)) Render(history, "history-chinese.png");
        using (Form transcript = new ClaudeTaskRunTranscriptForm(task, record)) Render(transcript, "transcript-chinese.png");
        using (ReplyBubbleForm bubble = new ReplyBubbleForm())
        {
            bubble.AppendReply(chinese); bubble.FlushPendingReply();
            RichTextBox text = typeof(ReplyBubbleForm).GetField("latest", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bubble) as RichTextBox;
            Require(text is UnicodeRichTextBox && ((UnicodeRichTextBox)text).UsesUnicodeRichEdit, "bubble uses modern Unicode engine");
            Render(bubble, "bubble-chinese.png");
        }
        using (ClaudeTaskService service = new ClaudeTaskService())
        {
            ScheduledClaudeTask saved = service.Add("中文任务", root, "interval", TimeSpan.Zero, DayOfWeek.Monday, 5, DateTime.Now, "terminal", "block");
            ClaudeTaskRunRecord run = service.BeginRun(saved, DateTime.Now);
            service.CompleteRun(saved, run.Id, true, 0, chinese + new string('甲', 99990) + "😊", DateTime.Now);
            Require(run.RunMode == "terminal" && !UiText.HasUnpairedSurrogate(run.Summary) && !UiText.HasUnpairedSurrogate(run.Transcript), "Chinese/emoji history truncation preserves surrogate pairs");
            using (ClaudeTaskForm editor = new ClaudeTaskForm(service))
            {
                bool found = false;
                foreach (Control panel in editor.Controls)
                    foreach (Control control in panel.Controls)
                    {
                        ComboBox modes = control as ComboBox;
                        if (modes != null && modes.Items.Contains("终端窗口")) { found = modes.Items.Count == 6 && modes.Items.Contains("CLI 交互·PS") && modes.Items.Contains("CLI 交互·终端") && modes.Items.Contains("Claude BG·关窗"); modes.SelectedIndex = 3; }
                    }
                Require(found, "six run modes including native CLI and Claude Background exposed in task editor");
                Render(editor, "terminal-settings.png");
            }
        }
        using (ClaudeTaskService service = new ClaudeTaskService()) Require(new System.Collections.Generic.List<ScheduledClaudeTask>(service.Tasks).Exists(delegate(ScheduledClaudeTask item) { return item.RunMode == "terminal" && item.History[0].Transcript.Contains("😊"); }), "terminal mode and Unicode history reload");
    }

    private static void Render(Form form, string name)
    {
        form.TopMost = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
        form.Show(); Application.DoEvents();
        using (Bitmap bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(root, name)); }
        form.Hide();
    }

    private static void TestVisibleRuns()
    {
        Assembly package = Assembly.LoadFrom(pet);
        Type taskType = package.GetType("XianPal.ScheduledClaudeTask");
        Type formType = package.GetType("XianPal.PetForm");
        MethodInfo build = formType.GetMethod("BuildVisibleTaskStartInfo", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] { taskType, typeof(string), typeof(string) }, null);
        string directory = Path.Combine(root, "中文 ' 工作目录"); Directory.CreateDirectory(directory);
        int desktopCount = VirtualDesktopService.GetDesktopIds().Count;
        foreach (string mode in new[] { "powershell", "terminal" })
            for (int variant = 0; variant < 2; variant++)
            {
                object task = Activator.CreateInstance(taskType, true);
                string prompt = variant == 1 ? "FAIL_TEST 中文任务" : "中文任务";
                taskType.GetField("Prompt").SetValue(task, prompt);
                taskType.GetField("WorkingDirectory").SetValue(task, directory);
                taskType.GetField("RunMode").SetValue(task, mode);
                taskType.GetField("Id").SetValue(task, "owned-test");
                taskType.GetField("DesktopNumber").SetValue(task, mode == "terminal" && variant == 1 && desktopCount >= 2 ? 2 : 0);
                string path = Path.Combine(root, "visible-" + mode + "-" + variant + "-" + Guid.NewGuid().ToString("N") + ".log");
                ProcessStartInfo info = (ProcessStartInfo)build.Invoke(null, new object[] { task, path, Assembly.GetExecutingAssembly().Location });
                string token = Regex.Match(info.Arguments, @"OrbitTask-[a-f0-9]{32}").Value;
                bool done = false; int code = -99; string output = ""; int completions = 0; bool progress = false, liveBeforeCompleted = false, toolState = false;
                using (VisibleTaskMonitor monitor = new VisibleTaskMonitor(path, delegate(string value) { progress = value.Contains("中文回复"); if (progress && !File.Exists(path + ".completed")) liveBeforeCompleted = true; }, delegate(int result, string value) { code = result; output = value; completions++; done = true; }, delegate(string status, string session, string reaction) { if (reaction == "PreToolUse" && session.Length > 0) toolState = true; }))
                {
                    try
                    {
                        monitor.Start(info);
                        Require(PumpUntil(delegate { return done; }, 25000), mode + " completion handshake");
                        Require(code == (variant == 1 ? 7 : 0) && completions == 1, mode + " actual exit code, exactly one completion");
                        Require(progress && output.Contains("你好，世界！✅ 😊 🚀") && output.Contains(directory) && output.Contains("MANAGED=1") && output.Contains("TASK=owned-test"), mode + " live Chinese/emoji output and selected working directory");
                        if (variant == 0)
                        {
                            Require(liveBeforeCompleted && toolState && output.Contains("[工具] Bash") && output.Contains("echo 测试工具") && output.Contains("[工具结果]"), mode + " text and tool process arrive while Claude is still running");
                            Require(Regex.Matches(output, "中文回复").Count == 1 && Regex.Matches(output, "结束：文件处理完成。").Count == 1, mode + " full events and final result are deduplicated");
                        }
                        if (mode == "terminal")
                        {
                            IntPtr window = VirtualDesktopService.FindTaskTerminalWindow(token);
                            Require(window != IntPtr.Zero, "independent Terminal window still displays completed output");
                            if (variant == 1 && desktopCount >= 2) Require(VirtualDesktopService.GetWindowDesktop(window) == VirtualDesktopService.ResolveDesktop(2), "only test Terminal window placed on Desktop 2");
                        }
                    }
                    finally
                    {
                        if (done && mode == "terminal")
                        {
                            IntPtr ownWindow = VirtualDesktopService.FindTaskTerminalWindow(token);
                            if (ownWindow != IntPtr.Zero) PostMessage(ownWindow, 0x0010, IntPtr.Zero, IntPtr.Zero);
                        }
                    }
                }
            }
        // Disposing the monitor must not stop its still-running shell.
        ScheduledClaudeTask running = new ScheduledClaudeTask { Prompt = "中文任务", WorkingDirectory = directory, RunMode = "powershell" };
        string detachedPath = Path.Combine(root, "detached-" + Guid.NewGuid().ToString("N") + ".log");
        using (VisibleTaskMonitor monitor = new VisibleTaskMonitor(detachedPath, null, null))
        {
            ProcessStartInfo info = PetForm.BuildVisibleTaskStartInfo(running, detachedPath, Assembly.GetExecutingAssembly().Location);
            info.WindowStyle = ProcessWindowStyle.Hidden;
            monitor.Start(info);
            Require(PumpUntil(delegate { return File.Exists(detachedPath + ".started"); }, 5000), "detached test shell started");
        }
        Require(PumpUntil(delegate { return File.Exists(detachedPath + ".completed"); }, 8000), "monitor disposal leaves task shell alive until natural completion");
    }
}
