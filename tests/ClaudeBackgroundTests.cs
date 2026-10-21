using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using XianPal;

internal static class ClaudeBackgroundTests
{
    private static string root;
    private static readonly JavaScriptSerializer json = new JavaScriptSerializer();
    private static object Field(object value, string name) { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }
    private static object Call(object value, string name, params object[] args) { return value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args); }
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static bool Until(Func<bool> condition, int milliseconds) { Stopwatch watch = Stopwatch.StartNew(); while (!condition() && watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(20); } return condition(); }
    private static string Arg(string[] args, string flag) { int index = Array.IndexOf(args, flag); return index >= 0 && index + 1 < args.Length ? args[index + 1] : ""; }
    private static void Write(string path, object value) { File.WriteAllText(path, json.Serialize(value), new UTF8Encoding(false)); }
    private static Dictionary<string, object> Read(string path) { return json.Deserialize<Dictionary<string, object>>(File.ReadAllText(path)); }

    [STAThread]
    private static int Main(string[] args)
    {
        root = Environment.GetEnvironmentVariable("FUFU_BG_TEST_ROOT");
        if (args.Length > 0) return Mock(args);
        root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "claude-background-test", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("FUFU_TEST_MODE", "1");
        Environment.SetEnvironmentVariable("FUFU_BG_TEST_ROOT", root);
        Environment.SetEnvironmentVariable("FUFU_STATE_DIR", root);
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string fixtureDirectory = Path.Combine(root, "任务目录 中文 ' 单引号"); Directory.CreateDirectory(fixtureDirectory);
            Require(ClaudeTaskService.NormalizeRunMode("claude-background") == "claude-background", "new mode remains distinct from legacy -p background");
            Dictionary<string, object> blocked = new Dictionary<string, object> { { "id", "abcdef01" }, { "kind", "background" }, { "name", "own" }, { "state", "blocked" }, { "waitingFor", "permission prompt" } };
            Require(!BackgroundTaskSupervisor.ParseSession(blocked).Finished, "blocked sessions never finish or free blocking slot");
            blocked["state"] = "future-state";
            Require(!BackgroundTaskSupervisor.ParseSession(blocked).Finished, "unknown CLI state is not assumed successful");
            Require(BackgroundTaskSupervisor.FindSession(new object[] { blocked }, "different-name", "") == null, "foreign background sessions ignored");
            Require(!BackgroundTaskSupervisor.ValidJobId("abc'; Remove-Item x"), "attach/log job ID injection rejected");
            using (PetForm pet = new PetForm())
            {
                pet.Show(); ((System.Windows.Forms.Timer)Field(pet, "timer")).Stop();
                BackgroundTaskSupervisor supervisor = (BackgroundTaskSupervisor)Field(pet, "backgroundTaskSupervisor");
                supervisor.ClaudeCommand = Assembly.GetExecutingAssembly().Location;
                ClaudeTaskService service = (ClaudeTaskService)Field(pet, "claudeTaskService");
                ScheduledClaudeTask task = service.Add("整理中文文件，保留 emoji ✅\r\n第二行", fixtureDirectory, "interval", TimeSpan.Zero, DayOfWeek.Monday, 1, DateTime.Now, "claude-background", "block");
                service.Toggle(task, DateTime.Now); // manual triggers only; no test-clock race
                Call(pet, "QueueScheduledClaudeTask", task);
                ClaudeTaskRunRecord run = task.History[0];
                Require(Until(delegate { return !String.IsNullOrEmpty(run.BackgroundJobId); }, 18000), "native --bg job ID resolved by unique run name");
                string log = Path.Combine(root, "scheduled-task-runs", run.Id + ".background.log");
                Require(File.Exists(log + ".dispatch-exit") && File.ReadAllText(log + ".dispatch-exit").Trim() == "0", "dispatch acceptance recorded separately from completion");
                Require(run.Status == "running" && String.IsNullOrEmpty(run.CompletedAt), "launcher exit zero does not finish job");
                string[] identity = File.ReadAllText(log + ".started").Split('|'); int pid = Int32.Parse(identity[0]);
                Require(Until(delegate { try { using (Process p = Process.GetProcessById(pid)) { return p.HasExited || p.StartTime.ToUniversalTime().Ticks != Int64.Parse(identity[1]); } } catch (ArgumentException) { return true; } }, 3000), "own startup PowerShell closes automatically after dispatch");
                Dictionary<string, object> capture = Read(Path.Combine(root, run.BackgroundJobName + ".capture"));
                Require((string)capture["cwd"] == fixtureDirectory && !(bool)capture["redirected"] && !(bool)capture["inputRedirected"], "actual mock --bg call keeps TTY and specified Chinese/single-quote directory");
                Require((string)capture["taskId"] == task.Id && ((string)capture["prompt"]).Contains("第二行") && !((string)capture["prompt"]).Contains("\n"), "scoped ownership settings and multiline prompt preserved without cmd splitting");
                string workerPath = Path.Combine(root, run.BackgroundJobName + ".worker");
                Require(File.Exists(workerPath) && !Process.GetProcessById(Int32.Parse(File.ReadAllText(workerPath))).HasExited, "detached mock worker survives startup console exit");
                Call(pet, "QueueScheduledClaudeTask", task);
                Require(task.History.Count == 2 && task.History[0].Status == "skipped", "blocking mode skips while native background still working");
                string statePath = Path.Combine(root, run.BackgroundJobName + ".json");
                Dictionary<string, object> state = Read(statePath); state["state"] = "blocked"; state["status"] = "waiting"; state["waitingFor"] = "permission prompt"; Write(statePath, state);
                Require(Until(delegate { return run.BackgroundState == "blocked"; }, 10000), "permission wait appears without granting permissions");
                Require(run.Status == "running" && run.StatusText == "需要输入", "blocked job remains a live run");
                using (ClaudeTaskService reloaded = new ClaudeTaskService())
                {
                    ClaudeTaskRunRecord restored = reloaded.Tasks[0].History.Find(delegate(ClaudeTaskRunRecord value) { return value.Id == run.Id; });
                    Require(restored.Status == "running" && restored.BackgroundJobId == run.BackgroundJobId, "restart reload preserves detached job and its identity");
                }
                File.WriteAllText(statePath + ".logs", "中文后台回复 ✅ 😊\n需要你确认权限。", new UTF8Encoding(false));
                Require(Until(delegate { return run.Transcript.Contains("中文后台回复"); }, 10000), "owned recent logs and emoji update history after console closed");
                state["state"] = "done"; state["status"] = "idle"; Write(statePath, state);
                Require(Until(delegate { return run.Status == "success"; }, 10000), "done session completes only after authoritative CLI state");
                List<ActivityRecord> activities = (List<ActivityRecord>)Field(pet, "activities");
                Require(activities.Count == 1 && activities[0].NativeInteractive, "one native-owned activity per background run");
                File.WriteAllText(statePath + ".stop-worker", "done");
                task.OverlapMode = "parallel";
                Call(pet, "QueueScheduledClaudeTask", task); Call(pet, "QueueScheduledClaudeTask", task);
                Require(task.History[0].BackgroundJobName != task.History[1].BackgroundJobName, "parallel background runs have separate identities");
                foreach (ClaudeTaskRunRecord parallel in task.History.ToArray())
                {
                    if (parallel.Status != "running") continue;
                    Require(Until(delegate { return !String.IsNullOrEmpty(parallel.BackgroundJobId); }, 18000), "parallel native job accepted");
                    string file = Path.Combine(root, parallel.BackgroundJobName + ".json");
                    Dictionary<string, object> row = Read(file); row["state"] = "failed"; Write(file, row);
                    Require(Until(delegate { return parallel.Status == "failed"; }, 10000), "failed native job reports failure without stopping another run");
                    File.WriteAllText(file + ".stop-worker", "done");
                }
                Environment.SetEnvironmentVariable("FUFU_BG_UNSUPPORTED", "1");
                Call(pet, "QueueScheduledClaudeTask", task); ClaudeTaskRunRecord unsupported = task.History[0];
                Require(Until(delegate { return unsupported.Status == "failed"; }, 10000), "unsupported CLI fails clearly instead of falling back to -p");
                Environment.SetEnvironmentVariable("FUFU_BG_UNSUPPORTED", null);
                task.OverlapMode = "block";
                Call(pet, "QueueScheduledClaudeTask", task); ClaudeTaskRunRecord surviving = task.History[0];
                Require(Until(delegate { return !String.IsNullOrEmpty(surviving.BackgroundJobId); }, 18000), "job accepted before pet shutdown");
                string survivingPath = Path.Combine(root, surviving.BackgroundJobName + ".json");
                pet.Close();
                Require(!Process.GetProcessById(Int32.Parse(File.ReadAllText(Path.Combine(root, surviving.BackgroundJobName + ".worker")))).HasExited, "pet shutdown does not stop detached worker");
                Dictionary<string, object> finished = Read(survivingPath); finished["state"] = "done"; Write(survivingPath, finished);
                using (PetForm restoredPet = new PetForm())
                {
                    ((BackgroundTaskSupervisor)Field(restoredPet, "backgroundTaskSupervisor")).ClaudeCommand = Assembly.GetExecutingAssembly().Location;
                    restoredPet.Show(); ((System.Windows.Forms.Timer)Field(restoredPet, "timer")).Stop();
                    ScheduledClaudeTask restoredTask = ((ClaudeTaskService)Field(restoredPet, "claudeTaskService")).Tasks[0];
                    ClaudeTaskRunRecord restoredRun = restoredTask.History.Find(delegate(ClaudeTaskRunRecord value) { return value.Id == surviving.Id; });
                    Require(Until(delegate { return restoredRun.Status == "success"; }, 15000), "pet restart reconnects and completes existing native job without redispatch");
                    restoredPet.Close();
                }
                File.WriteAllText(survivingPath + ".stop-worker", "done");
            }
            Console.WriteLine("PASS all native Background tests; evidence: " + root); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            foreach (string file in Directory.GetFiles(root, "*.worker"))
                File.WriteAllText(file.Substring(0, file.Length - ".worker".Length) + ".json.stop-worker", "test completed");
        }
    }

    private static int Mock(string[] args)
    {
        if (Environment.GetEnvironmentVariable("FUFU_TEST_MODE") != "1" || String.IsNullOrEmpty(root)) return 99;
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args[0] == "--help") { Console.WriteLine(Environment.GetEnvironmentVariable("FUFU_BG_UNSUPPORTED") == "1" ? "legacy CLI" : "--bg --background"); return 0; }
        if (args[0] == "agents" && Array.IndexOf(args, "--help") >= 0) { Console.WriteLine("--json --all"); return 0; }
        if (args[0] == "agents")
        {
            List<object> rows = new List<object> { new { id = "deadbeef", kind = "background", name = "other-user-job", state = "working", cwd = "C:/unrelated" } };
            foreach (string file in Directory.GetFiles(root, "OrbitBg-*.json")) rows.Add(Read(file));
            Console.WriteLine(json.Serialize(rows)); return 0;
        }
        if (args[0] == "logs")
        {
            foreach (string file in Directory.GetFiles(root, "OrbitBg-*.json"))
                if ((string)Read(file)["id"] == args[1]) { Console.WriteLine(File.Exists(file + ".logs") ? File.ReadAllText(file + ".logs") : "正在执行中文后台任务 ✅"); return 0; }
            return 1;
        }
        if (args[0] == "--mock-worker")
        {
            string file = Path.Combine(root, args[1] + ".json");
            File.WriteAllText(Path.Combine(root, args[1] + ".worker"), Process.GetCurrentProcess().Id.ToString());
            Stopwatch time = Stopwatch.StartNew();
            while (!File.Exists(file + ".stop-worker") && time.ElapsedMilliseconds < 180000) Thread.Sleep(100);
            return 0;
        }
        if (args[0] == "--bg")
        {
            string name = Arg(args, "--name"), settings = Arg(args, "--settings"), file = Path.Combine(root, name + ".json");
            if (Array.IndexOf(args, "-p") >= 0 || String.IsNullOrEmpty(name)) return 2;
            Dictionary<string, object> env = (Dictionary<string, object>)Read(settings)["env"];
            Write(Path.Combine(root, name + ".capture"), new { cwd = Environment.CurrentDirectory, redirected = Console.IsOutputRedirected, inputRedirected = Console.IsInputRedirected, taskId = env["FUFU_PET_INTERNAL_TASK_ID"], prompt = args[args.Length - 1] });
            Write(file, new { id = Guid.NewGuid().ToString("N").Substring(0, 8), kind = "background", name = name, state = "working", status = "busy", cwd = Environment.CurrentDirectory, sessionId = Guid.NewGuid().ToString("D") });
            Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--mock-worker " + name) { UseShellExecute = false, CreateNoWindow = true });
            Console.WriteLine("backgrounded · " + (string)Read(file)["id"] + " · " + name); return 0;
        }
        return 98;
    }
}
