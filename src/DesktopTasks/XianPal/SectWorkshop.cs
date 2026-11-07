// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
#if XIANPAL_LINUX
using JavaScriptSerializer = XianPal.Linux.CompatibleJson;
#else
using System.Web.Script.Serialization;
#endif

namespace XianPal
{
    internal sealed class SectMember
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public string Directory { get; set; }
        public string CharacterId { get; set; }
    }
    internal sealed class SectStep
    {
        public string Id { get; set; }
        public string Member { get; set; }
        public string DependsOn { get; set; }
        public string Task { get; set; }
    }
    internal sealed class CompanionSect
    {
        public string Name { get; set; }
        public string Goal { get; set; }
        public string TemplateId { get; set; }
        public List<SectMember> Members = new List<SectMember>();
        public List<SectStep> Steps = new List<SectStep>();
        public override string ToString() { return Name; }
    }
    internal sealed class PlannedEvent
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Title { get; set; }
        public DateTime When { get; set; }
        public string Repeat { get; set; }
        public string Sect { get; set; }
        public bool Done { get; set; }
        public static DateTime Next(DateTime due, DateTime now, string repeat)
        {
            if (repeat != "daily" && repeat != "weekly") return DateTime.MaxValue;
            int days = repeat == "daily" ? 1 : 7;
            double jumps = Math.Floor((now - due).TotalDays / days) + 1;
            return due.AddDays(Math.Max(1, jumps) * days);
        }
    }
    internal sealed class SectRun
    {
        public string Sect = "", Started = "", Finished = "", Status = "running";
        public List<SectStepRun> Steps = new List<SectStepRun>();
        public List<SectTransfer> Transfers = new List<SectTransfer>();
    }
    internal sealed class SectTransfer
    {
        public string From = "", To = "", Text = "", SentAt = "";
    }
    internal sealed class SectStepRun
    {
        public string Id = "", Member = "", Directory = "", Prompt = "", Output = "", Status = "pending";
        public int ExitCode;
        public string CharacterId = "orbit";
    }
    internal sealed class WorkshopData
    {
        public List<CompanionSect> Sects = new List<CompanionSect>();
        public List<PlannedEvent> Events = new List<PlannedEvent>();
        public List<SectRun> History = new List<SectRun>();
    }
    internal static class WorkshopFiles
    {
        internal static void Save<T>(string path, T data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, new JavaScriptSerializer { MaxJsonLength = 8388608 }.Serialize(data), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
        internal static T Load<T>(string path) where T : new()
        {
            if (!File.Exists(path)) return new T();
            // Never silently replace an unreadable plan with an empty document.
            if (new FileInfo(path).Length > 8388608) throw new IOException("规划文件过大，请先备份整理。");
            T data = new JavaScriptSerializer { MaxJsonLength = 8388608 }.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8));
            if (data == null) throw new IOException("规划文件无效，原文件已保留。");
            return data;
        }
    }
    internal interface ISectAgent : IDisposable
    {
        event Action<string> Output;
        event Action<int> Completed;
        void Start(string prompt, string directory, string marker, string characterId);
        void Stop();
    }
    internal sealed class LocalSectAgent : ISectAgent
    {
        private readonly ClaudeCliRunner runner = new ClaudeCliRunner();
        public event Action<string> Output;
        public event Action<int> Completed;
        internal LocalSectAgent()
        {
            runner.OutputReceived += delegate(string s) { if (Output != null) Output(s); };
            runner.Completed += delegate(int code) { if (Completed != null) Completed(code); };
        }
        public void Start(string prompt, string directory, string marker, string characterId) { runner.Start(prompt, directory, null, marker, characterId); }
        public void Stop() { runner.Stop(); }
        public void Dispose() { runner.Dispose(); }
    }
    // UI-thread owned dependency scheduler. Worker callbacks only enqueue immutable messages.
    internal sealed class SectExecution : IDisposable
    {
        private sealed class Live
        {
            internal ISectAgent Agent;
            internal SectStepRun Record;
            internal DateTime Deadline;
        }
        private readonly Func<ISectAgent> factory;
        private readonly Queue<Action> messages = new Queue<Action>();
        private readonly Dictionary<string, Live> live = new Dictionary<string, Live>();
        private readonly CompanionSect snapshot;
        private bool disposed, cancelled;
        internal readonly SectRun Record = new SectRun();
        internal bool IsRunning { get { return Record.Status == "running"; } }
        internal SectExecution(CompanionSect sect, Func<ISectAgent> factory)
        {
            Validate(sect);
            snapshot = new JavaScriptSerializer().Deserialize<CompanionSect>(new JavaScriptSerializer().Serialize(sect));
            this.factory = factory;
            Record.Sect = sect.Name; Record.Started = DateTime.Now.ToString("o");
            foreach (SectStep s in snapshot.Steps)
            {
                SectMember m = Member(s.Member);
                Record.Steps.Add(new SectStepRun { Id = s.Id, Member = m.Name, Directory = Path.GetFullPath(m.Directory), CharacterId = String.IsNullOrEmpty(m.CharacterId) ? PetCharacter.All[snapshot.Members.IndexOf(m)%4].Id : m.CharacterId });
            }
        }
        internal static string[] Dependencies(string text)
        { return (text ?? "").Split(new[] { ',', '，', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries); }
        internal static void Validate(CompanionSect sect)
        {
            if (sect == null || String.IsNullOrWhiteSpace(sect.Name) || sect.Name.Length > 60) throw new ArgumentException("请填写门派名称（最多60字）。");
            if ((sect.Goal ?? "").Length > 4000 || (sect.TemplateId == "four-companions-v1" && String.IsNullOrWhiteSpace(sect.Goal))) throw new ArgumentException("请填写共同目标、允许修改的范围和验收要求（最多4000字）。");
            if (sect.Members == null || sect.Members.Count < 1 || sect.Members.Count > 12 || sect.Steps == null || sect.Steps.Count < 1 || sect.Steps.Count > 24)
                throw new ArgumentException("每个门派需要1–12位成员、1–24项任务。");
            var members = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var memberNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var steps = new Dictionary<string, SectStep>(StringComparer.OrdinalIgnoreCase);
            foreach (SectMember m in sect.Members)
            {
                if (m == null || !ValidId(m.Id) || !members.Add(m.Id) || String.IsNullOrWhiteSpace(m.Name) || m.Name.Length > 60 || String.IsNullOrWhiteSpace(m.Role) || m.Role.Length > 4000)
                    throw new ArgumentException("成员编号须唯一，使用英文、数字或短横线；姓名和职责不能为空。");
                if (!System.IO.Directory.Exists(m.Directory)) throw new ArgumentException("成员工作目录不存在：" + m.Name);
                if (!memberNames.Add(m.Name)) throw new ArgumentException("成员姓名须唯一，方便大堂区分成员。");
            }
            foreach (SectStep s in sect.Steps)
            {
                if (s == null || !ValidId(s.Id) || steps.ContainsKey(s.Id) || !members.Contains(s.Member ?? "") || String.IsNullOrWhiteSpace(s.Task) || s.Task.Length > 8000)
                    throw new ArgumentException("任务编号须唯一，成员编号须存在，任务内容需为1–8000字。");
                steps.Add(s.Id, s);
            }
            var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int pass = 0; pass < steps.Count; pass++)
                foreach (SectStep s in sect.Steps)
                {
                    bool ready = true;
                    foreach (string d in Dependencies(s.DependsOn)) { if (!steps.ContainsKey(d)) throw new ArgumentException("依赖任务不存在：" + d); if (!resolved.Contains(d)) ready = false; }
                    if (ready) resolved.Add(s.Id);
                }
            if (resolved.Count != steps.Count) throw new ArgumentException("任务依赖形成循环，请修改。");
        }
        private static bool ValidId(string s) { return s != null && System.Text.RegularExpressions.Regex.IsMatch(s, "^[a-zA-Z0-9-]{1,32}$"); }
        private SectMember Member(string id) { return snapshot.Members.Find(delegate(SectMember m) { return String.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase); }); }
        private SectStepRun Step(string id) { return Record.Steps.Find(delegate(SectStepRun r) { return String.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase); }); }
        private void Enqueue(Action message) { lock (messages) { if (!disposed) messages.Enqueue(message); } }
        internal void Pump(DateTime utc)
        {
            if (disposed || !IsRunning) return;
            while (true) { Action a; lock (messages) { if (messages.Count == 0) break; a = messages.Dequeue(); } a(); }
            foreach (var pair in new List<KeyValuePair<string, Live>>(live))
                if (utc > pair.Value.Deadline) { pair.Value.Agent.Stop(); Complete(pair.Key, -2, "\n执行超过10分钟，已停止本步骤。", "timeout"); }
            if (!cancelled)
            foreach (SectStep s in snapshot.Steps)
            {
                SectStepRun r = Step(s.Id); if (r.Status != "pending") continue;
                bool ready = true, failed = false;
                foreach (string d in Dependencies(s.DependsOn)) { string status = Step(d).Status; if (status != "success") ready = false; if (status == "failed" || status == "blocked" || status == "cancelled" || status == "timeout") failed = true; }
                if (failed) { r.Status = "blocked"; continue; }
                if (!ready || live.Count >= 2) continue;
                bool sameDirectory = false;
                foreach (Live l in live.Values) if (DirectoriesOverlap(l.Record.Directory, r.Directory)) sameDirectory = true;
                if (sameDirectory) continue; // Never run writers in overlapping worktrees simultaneously.
                SectMember m = Member(s.Member);
                var prompt = new StringBuilder(CompanionRolePresets.Context(snapshot,m) + "\n当前任务：" + s.Task + "\n仅处理当前步骤，不擅自扩大范围。\n");
                int upstreamBudget = 8000;
                var transfers = new List<SectTransfer>();
                foreach (string d in Dependencies(s.DependsOn)) { string output = Step(d).Output; int length = Math.Min(output.Length, upstreamBudget); string excerpt = output.Substring(0, length); prompt.Append("\n<上游结果 id=\"" + d + "\">\n" + excerpt + "\n</上游结果>\n"); upstreamBudget -= length; transfers.Add(new SectTransfer { From = d, To = s.Id, Text = excerpt, SentAt = DateTime.Now.ToString("o") }); }
                if (upstreamBudget == 0) prompt.Append("\n上游摘要已达长度上限，完整记录请在仙伴运行记录查看。\n");
                r.Prompt = prompt.ToString(); r.Status = "running";
                ISectAgent agent = factory(); string id = s.Id;
                live.Add(id, new Live { Agent = agent, Record = r, Deadline = utc.AddMinutes(10) });
                agent.Output += delegate(string text) { Enqueue(delegate { if (r.Status == "running") r.Output = Limit(r.Output + text); }); };
                agent.Completed += delegate(int code) { Enqueue(delegate { Complete(id, code, "", code == 0 ? "success" : "failed"); }); };
                try { if (!System.IO.Directory.Exists(r.Directory)) throw new DirectoryNotFoundException(r.Directory); agent.Start(r.Prompt, r.Directory, "sect-" + Guid.NewGuid().ToString("N"), r.CharacterId); Record.Transfers.AddRange(transfers); }
                catch (Exception ex) { Complete(id, -1, ex.Message, "failed"); }
            }
            if (live.Count == 0 && !Record.Steps.Exists(delegate(SectStepRun r) { return r.Status == "pending"; }))
            {
                Record.Status = cancelled ? "cancelled" : Record.Steps.TrueForAll(delegate(SectStepRun r) { return r.Status == "success"; }) ? "success" : "failed";
                Record.Finished = DateTime.Now.ToString("o");
            }
        }
        internal static bool DirectoriesOverlap(string a, string b)
        {
            char separator = Path.DirectorySeparatorChar;
#if XIANPAL_LINUX
            a = Linux.NativePaths.ResolveDirectory(a);
            b = Linux.NativePaths.ResolveDirectory(b);
            const StringComparison comparison = StringComparison.Ordinal;
#else
            a = Path.GetFullPath(a);
            b = Path.GetFullPath(b);
            const StringComparison comparison = StringComparison.OrdinalIgnoreCase;
#endif
            a = a.TrimEnd(separator) + separator;
            b = b.TrimEnd(separator) + separator;
            return a.StartsWith(b, comparison) || b.StartsWith(a, comparison);
        }
        private static string Limit(string s) { return s.Length <= 16000 ? s : s.Substring(0, 15960) + "\n[输出已达到本地记录上限]"; }
        private void Complete(string id, int code, string message, string status)
        {
            Live l; if (!live.TryGetValue(id, out l)) return;
            live.Remove(id); l.Record.ExitCode = code; l.Record.Output = Limit(l.Record.Output + message); l.Record.Status = status; l.Agent.Dispose();
        }
        internal void Cancel()
        {
            cancelled = true;
            foreach (var pair in new List<KeyValuePair<string, Live>>(live)) { pair.Value.Agent.Stop(); Complete(pair.Key, -3, "\n用户取消。", "cancelled"); }
            foreach (SectStepRun r in Record.Steps) if (r.Status == "pending") r.Status = "cancelled";
            Pump(DateTime.UtcNow);
        }
        public void Dispose() { if (disposed) return; Cancel(); lock (messages) { disposed = true; messages.Clear(); } }
    }
}
