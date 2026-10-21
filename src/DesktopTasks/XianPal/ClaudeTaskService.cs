using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed class ScheduledClaudeTask
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Prompt = "";
        public string WorkingDirectory = "";
        public string Recurrence = "daily";
        public int MinutesAfterMidnight = 9 * 60;
        public int Weekday = 1;
        public int IntervalMinutes = 60;
        public string NextRunAt = "";
        public string LastRunAt = "";
        public bool Enabled = true;
        public string RunMode = "background";
        public string OverlapMode = "block";
        // Zero preserves the current-desktop behavior of existing tasks.
        public int DesktopNumber;
        public List<ClaudeTaskRunRecord> History = new List<ClaudeTaskRunRecord>();

        internal string ScheduleText
        {
            get
            {
                string time = (MinutesAfterMidnight / 60).ToString("00") + ":" + (MinutesAfterMidnight % 60).ToString("00");
                if (Recurrence == "weekly") return WeekdayText(Weekday) + " " + time;
                if (Recurrence == "interval") return "每隔 " + FormatInterval(IntervalMinutes);
                return "每天 " + time;
            }
        }

        internal static string WeekdayText(int value)
        {
            string[] names = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            return names[Math.Max(0, Math.Min(6, value))];
        }

        internal static string FormatInterval(int minutes)
        {
            int safe = Math.Max(1, minutes);
            if (safe % 1440 == 0) return (safe / 1440) + " 天";
            if (safe % 60 == 0) return (safe / 60) + " 小时";
            return safe + " 分钟";
        }
    }

    internal sealed class ClaudeTaskRunRecord
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string StartedAt = "";
        public string CompletedAt = "";
        public string Status = "running";
        public int ExitCode;
        public string Summary = "";
        public string Transcript = "";
        public string RunMode = "background";
        public int DesktopNumber;
        public string BackgroundJobName = "";
        public string BackgroundJobId = "";
        public string BackgroundSessionId = "";
        public string BackgroundState = "";
        public string BackgroundOutput = "";

        internal string StatusText
        {
            get
            {
                if (Status == "success") return "已完成";
                if (Status == "failed") return "失败";
                if (Status == "skipped") return "已跳过";
                if (RunMode == "claude-background" && BackgroundState == "blocked") return "需要输入";
                return "运行中";
            }
        }
    }

    internal sealed class ClaudeTaskStore
    {
        public List<ScheduledClaudeTask> Tasks = new List<ScheduledClaudeTask>();
        public int DefaultDesktopNumber;
    }

    internal sealed class ClaudeTaskService : IDisposable
    {
        private readonly Timer timer = new Timer();
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly List<ScheduledClaudeTask> tasks = new List<ScheduledClaudeTask>();
        private DateTime lastProgressSaveUtc;
        internal event Action Changed;
        internal event Action<ScheduledClaudeTask> TaskDue;

        internal ClaudeTaskService()
        {
            Load();
            timer.Interval = 5000;
            timer.Tick += delegate { CheckNow(DateTime.Now); };
        }

        internal IList<ScheduledClaudeTask> Tasks { get { return new List<ScheduledClaudeTask>(tasks); } }
        internal int DefaultDesktopNumber { get; private set; }

        internal void SetDesktop(ScheduledClaudeTask task, int number)
        {
            if (task == null || !tasks.Contains(task)) return;
            task.DesktopNumber = NormalizeDesktopNumber(number);
            SaveAndNotify();
        }

        internal void SetDefaultDesktop(int number)
        {
            DefaultDesktopNumber = NormalizeDesktopNumber(number);
            SaveAndNotify();
        }

        internal static int NormalizeDesktopNumber(int number) { return Math.Max(0, Math.Min(99, number)); }

        internal void Start()
        {
            CheckNow(DateTime.Now);
            timer.Start();
        }

        internal ScheduledClaudeTask Add(string prompt, string directory, string recurrence, TimeSpan time, DayOfWeek weekday, int intervalMinutes, DateTime now)
        {
            return Add(prompt, directory, recurrence, time, weekday, intervalMinutes, now, "background");
        }

        internal ScheduledClaudeTask Add(string prompt, string directory, string recurrence, TimeSpan time, DayOfWeek weekday, int intervalMinutes, DateTime now, string runMode)
        {
            return Add(prompt, directory, recurrence, time, weekday, intervalMinutes, now, runMode, "block");
        }

        internal ScheduledClaudeTask Add(string prompt, string directory, string recurrence, TimeSpan time, DayOfWeek weekday, int intervalMinutes, DateTime now, string runMode, string overlapMode)
        {
            string cleanPrompt = (prompt ?? "").Trim();
            if (cleanPrompt.Length == 0) throw new ArgumentException("请输入要交给 Claude Code 执行的任务。", "prompt");
            if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) throw new DirectoryNotFoundException("任务工作目录不存在。");
            ScheduledClaudeTask task = new ScheduledClaudeTask
            {
                Prompt = cleanPrompt.Length > 4000 ? cleanPrompt.Substring(0, 4000) : cleanPrompt,
                WorkingDirectory = Path.GetFullPath(directory),
                Recurrence = NormalizeRecurrence(recurrence),
                MinutesAfterMidnight = Math.Max(0, Math.Min(1439, (int)time.TotalMinutes)),
                Weekday = Math.Max(0, Math.Min(6, (int)weekday)),
                IntervalMinutes = Math.Max(1, Math.Min(525600, intervalMinutes)),
                RunMode = NormalizeRunMode(runMode),
                OverlapMode = NormalizeOverlapMode(overlapMode),
                DesktopNumber = DefaultDesktopNumber,
                Enabled = true
            };
            task.NextRunAt = CalculateNextRun(task, now).ToString("o", CultureInfo.InvariantCulture);
            tasks.Add(task);
            Sort();
            SaveAndNotify();
            return task;
        }

        internal void Update(ScheduledClaudeTask task, string prompt, string directory, string recurrence, TimeSpan time, DayOfWeek weekday, int intervalMinutes, DateTime now)
        {
            Update(task, prompt, directory, recurrence, time, weekday, intervalMinutes, now, task == null ? "background" : task.RunMode);
        }

        internal void Update(ScheduledClaudeTask task, string prompt, string directory, string recurrence, TimeSpan time, DayOfWeek weekday, int intervalMinutes, DateTime now, string runMode)
        {
            Update(task, prompt, directory, recurrence, time, weekday, intervalMinutes, now, runMode, task == null ? "block" : task.OverlapMode);
        }

        internal void Update(ScheduledClaudeTask task, string prompt, string directory, string recurrence, TimeSpan time, DayOfWeek weekday, int intervalMinutes, DateTime now, string runMode, string overlapMode)
        {
            if (task == null || !tasks.Contains(task)) return;
            string cleanPrompt = (prompt ?? "").Trim();
            if (cleanPrompt.Length == 0) throw new ArgumentException("请输入要交给 Claude Code 执行的任务。", "prompt");
            if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) throw new DirectoryNotFoundException("任务工作目录不存在。");
            task.Prompt = cleanPrompt.Length > 4000 ? cleanPrompt.Substring(0, 4000) : cleanPrompt;
            task.WorkingDirectory = Path.GetFullPath(directory);
            task.Recurrence = NormalizeRecurrence(recurrence);
            task.MinutesAfterMidnight = Math.Max(0, Math.Min(1439, (int)time.TotalMinutes));
            task.Weekday = Math.Max(0, Math.Min(6, (int)weekday));
            task.IntervalMinutes = Math.Max(1, Math.Min(525600, intervalMinutes));
            task.RunMode = NormalizeRunMode(runMode);
            task.OverlapMode = NormalizeOverlapMode(overlapMode);
            task.NextRunAt = CalculateNextRun(task, now).ToString("o", CultureInfo.InvariantCulture);
            Sort();
            SaveAndNotify();
        }

        internal ClaudeTaskRunRecord BeginRun(ScheduledClaudeTask task, DateTime now)
        {
            if (task == null || !tasks.Contains(task)) return null;
            if (task.History == null) task.History = new List<ClaudeTaskRunRecord>();
            ClaudeTaskRunRecord record = new ClaudeTaskRunRecord
            {
                StartedAt = now.ToString("o", CultureInfo.InvariantCulture),
                Status = "running",
                RunMode = NormalizeRunMode(task.RunMode),
                DesktopNumber = NormalizeDesktopNumber(task.DesktopNumber)
            };
            task.History.Insert(0, record);
            TrimHistory(task);
            SaveAndNotify();
            return record;
        }

        internal void UpdateRunProgress(ScheduledClaudeTask task, string recordId, string output)
        {
            if (task == null || !tasks.Contains(task) || task.History == null) return;
            ClaudeTaskRunRecord record = task.History.Find(delegate(ClaudeTaskRunRecord item) { return item != null && item.Id == recordId; });
            if (record == null || record.Status != "running") return;
            string clean = UiText.Normalize(output ?? "");
            record.Summary = UiText.TailCodeUnits(clean, 1600);
            record.Transcript = UiText.TailCodeUnits(clean, 100000);
            // Keep live windows current without rebuilding the task editor on
            // every token. Persist checkpoints at most once every two seconds.
            if ((DateTime.UtcNow - lastProgressSaveUtc).TotalSeconds >= 2)
            { lastProgressSaveUtc = DateTime.UtcNow; Save(); }
        }

        internal void PersistBackgroundRun(ScheduledClaudeTask task, ClaudeTaskRunRecord run)
        {
            if (task != null && tasks.Contains(task) && task.History.Contains(run)) SaveAndNotify();
        }

        internal void CompleteRun(ScheduledClaudeTask task, string recordId, bool success, int exitCode, string summary, DateTime now)
        {
            if (task == null || !tasks.Contains(task) || task.History == null) return;
            ClaudeTaskRunRecord record = task.History.Find(delegate(ClaudeTaskRunRecord item) { return item != null && String.Equals(item.Id, recordId, StringComparison.Ordinal); });
            if (record == null) return;
            record.CompletedAt = now.ToString("o", CultureInfo.InvariantCulture);
            record.Status = success ? "success" : "failed";
            record.ExitCode = exitCode;
            string clean = UiText.Normalize(summary ?? "").Trim();
            record.Summary = UiText.TailCodeUnits(clean, 1600);
            record.Transcript = UiText.TailCodeUnits(clean, 100000);
            TrimHistory(task);
            SaveAndNotify();
        }

        internal ClaudeTaskRunRecord RecordSkipped(ScheduledClaudeTask task, DateTime now, string reason)
        {
            if (task == null || !tasks.Contains(task)) return null;
            if (task.History == null) task.History = new List<ClaudeTaskRunRecord>();
            ClaudeTaskRunRecord record = new ClaudeTaskRunRecord
            {
                StartedAt = now.ToString("o", CultureInfo.InvariantCulture),
                CompletedAt = now.ToString("o", CultureInfo.InvariantCulture),
                Status = "skipped",
                ExitCode = 0,
                Summary = String.IsNullOrWhiteSpace(reason) ? "上一轮仍在运行，本次触发已跳过。" : reason,
                Transcript = String.IsNullOrWhiteSpace(reason) ? "上一轮仍在运行，本次触发已跳过。" : reason,
                RunMode = NormalizeRunMode(task.RunMode)
            };
            task.History.Insert(0, record);
            TrimHistory(task);
            SaveAndNotify();
            return record;
        }

        internal void Toggle(ScheduledClaudeTask task, DateTime now)
        {
            if (task == null || !tasks.Contains(task)) return;
            task.Enabled = !task.Enabled;
            if (task.Enabled) task.NextRunAt = CalculateNextRun(task, now).ToString("o", CultureInfo.InvariantCulture);
            SaveAndNotify();
        }

        internal void Delete(ScheduledClaudeTask task)
        {
            if (task == null || !tasks.Remove(task)) return;
            SaveAndNotify();
        }

        internal void RunNow(ScheduledClaudeTask task)
        {
            if (task == null || !tasks.Contains(task)) return;
            Action<ScheduledClaudeTask> handler = TaskDue;
            if (handler != null) handler(task);
        }

        internal void CheckNow(DateTime now)
        {
            List<ScheduledClaudeTask> due = new List<ScheduledClaudeTask>();
            foreach (ScheduledClaudeTask task in tasks)
            {
                if (!task.Enabled) continue;
                DateTime next;
                if (!TryReadDate(task.NextRunAt, out next))
                {
                    next = CalculateNextRun(task, now);
                    task.NextRunAt = next.ToString("o", CultureInfo.InvariantCulture);
                }
                if (next > now) continue;
                task.LastRunAt = now.ToString("o", CultureInfo.InvariantCulture);
                task.NextRunAt = CalculateNextRun(task, now.AddSeconds(1)).ToString("o", CultureInfo.InvariantCulture);
                due.Add(task);
            }
            if (due.Count == 0) return;
            SaveAndNotify();
            foreach (ScheduledClaudeTask task in due)
            {
                Action<ScheduledClaudeTask> handler = TaskDue;
                if (handler != null) handler(task);
            }
        }

        internal static DateTime CalculateNextRun(ScheduledClaudeTask task, DateTime now)
        {
            if (task == null) return now.AddMinutes(1);
            string recurrence = NormalizeRecurrence(task.Recurrence);
            if (recurrence == "interval") return now.AddMinutes(Math.Max(1, task.IntervalMinutes));
            DateTime candidate = now.Date.AddMinutes(Math.Max(0, Math.Min(1439, task.MinutesAfterMidnight)));
            if (recurrence == "weekly")
            {
                int target = Math.Max(0, Math.Min(6, task.Weekday));
                int days = (target - (int)now.DayOfWeek + 7) % 7;
                candidate = candidate.AddDays(days);
                if (candidate <= now) candidate = candidate.AddDays(7);
                return candidate;
            }
            if (candidate <= now) candidate = candidate.AddDays(1);
            return candidate;
        }

        internal static string NormalizeRecurrence(string value)
        {
            if (String.Equals(value, "weekly", StringComparison.OrdinalIgnoreCase)) return "weekly";
            if (String.Equals(value, "interval", StringComparison.OrdinalIgnoreCase)) return "interval";
            return "daily";
        }

        internal static string NormalizeRunMode(string value)
        {
            if (String.Equals(value, "claude-background", StringComparison.OrdinalIgnoreCase)) return "claude-background";
            if (String.Equals(value, "interactive-powershell", StringComparison.OrdinalIgnoreCase)) return "interactive-powershell";
            if (String.Equals(value, "interactive-terminal", StringComparison.OrdinalIgnoreCase)) return "interactive-terminal";
            if (String.Equals(value, "terminal", StringComparison.OrdinalIgnoreCase)) return "terminal";
            return String.Equals(value, "powershell", StringComparison.OrdinalIgnoreCase) ? "powershell" : "background";
        }

        internal static string RunModeLabel(string value)
        {
            string mode = NormalizeRunMode(value);
            return mode == "claude-background" ? "Claude BG" : mode == "interactive-terminal" ? "CLI·终端" : mode == "interactive-powershell" ? "CLI·PS" : mode == "terminal" ? "终端" : mode == "powershell" ? "PowerShell" : "后台";
        }

        internal static bool IsInteractiveMode(string value) { return NormalizeRunMode(value).StartsWith("interactive-", StringComparison.Ordinal); }

        internal static string NormalizeOverlapMode(string value)
        {
            return String.Equals(value, "parallel", StringComparison.OrdinalIgnoreCase) ? "parallel" : "block";
        }

        private string StoragePath { get { return Path.Combine(Program.StateDirectory, "claude-tasks.json"); } }

        private void Load()
        {
            try
            {
                if (!File.Exists(StoragePath)) return;
                ClaudeTaskStore store = serializer.Deserialize<ClaudeTaskStore>(File.ReadAllText(StoragePath));
                if (store == null || store.Tasks == null) return;
                DefaultDesktopNumber = NormalizeDesktopNumber(store.DefaultDesktopNumber);
                foreach (ScheduledClaudeTask task in store.Tasks)
                {
                    if (task == null || String.IsNullOrWhiteSpace(task.Prompt) || !Directory.Exists(task.WorkingDirectory)) continue;
                    if (String.IsNullOrWhiteSpace(task.Id)) task.Id = Guid.NewGuid().ToString("N");
                    task.Recurrence = NormalizeRecurrence(task.Recurrence);
                    task.MinutesAfterMidnight = Math.Max(0, Math.Min(1439, task.MinutesAfterMidnight));
                    task.Weekday = Math.Max(0, Math.Min(6, task.Weekday));
                    task.IntervalMinutes = Math.Max(1, Math.Min(525600, task.IntervalMinutes));
                    task.RunMode = NormalizeRunMode(task.RunMode);
                    task.OverlapMode = NormalizeOverlapMode(task.OverlapMode);
                    task.DesktopNumber = NormalizeDesktopNumber(task.DesktopNumber);
                    if (task.History == null) task.History = new List<ClaudeTaskRunRecord>();
                    task.History.RemoveAll(delegate(ClaudeTaskRunRecord record) { return record == null; });
                    foreach (ClaudeTaskRunRecord record in task.History)
                    {
                        if (String.IsNullOrWhiteSpace(record.Id)) record.Id = Guid.NewGuid().ToString("N");
                        record.RunMode = NormalizeRunMode(record.RunMode);
                        if (record.RunMode == "claude-background" && record.Status == "running" && !String.IsNullOrEmpty(record.BackgroundJobName)) continue;
                        if (record.Status != "success" && record.Status != "failed" && record.Status != "skipped")
                        {
                            record.Status = "failed";
                            record.CompletedAt = String.IsNullOrWhiteSpace(record.CompletedAt) ? DateTime.Now.ToString("o", CultureInfo.InvariantCulture) : record.CompletedAt;
                            if (String.IsNullOrWhiteSpace(record.Summary)) record.Summary = "程序上次退出前任务尚未完成。";
                        }
                    }
                    TrimHistory(task);
                    DateTime next;
                    if (!TryReadDate(task.NextRunAt, out next)) task.NextRunAt = CalculateNextRun(task, DateTime.Now).ToString("o", CultureInfo.InvariantCulture);
                    tasks.Add(task);
                }
                Sort();
            }
            catch { }
        }

        private static bool TryReadDate(string value, out DateTime date)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date);
        }

        private void Sort()
        {
            tasks.Sort(delegate(ScheduledClaudeTask left, ScheduledClaudeTask right)
            {
                DateTime leftDate, rightDate;
                TryReadDate(left.NextRunAt, out leftDate);
                TryReadDate(right.NextRunAt, out rightDate);
                return leftDate.CompareTo(rightDate);
            });
        }

        private static void TrimHistory(ScheduledClaudeTask task)
        {
            if (task == null || task.History == null) return;
            const int MaximumHistory = 50;
            for (int i = task.History.Count - 1; i >= MaximumHistory; i--)
                if (task.History[i].RunMode != "claude-background" || task.History[i].Status != "running") task.History.RemoveAt(i);
        }

        private void SaveAndNotify()
        {
            Save();
            Action handler = Changed;
            if (handler != null) handler();
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Program.StateDirectory);
                string temporary = StoragePath + ".tmp";
                File.WriteAllText(temporary, serializer.Serialize(new ClaudeTaskStore { Tasks = new List<ScheduledClaudeTask>(tasks), DefaultDesktopNumber = DefaultDesktopNumber }));
                if (File.Exists(StoragePath)) File.Replace(temporary, StoragePath, null);
                else File.Move(temporary, StoragePath);
            }
            catch { }
        }

        public void Dispose()
        {
            timer.Stop();
            timer.Dispose();
        }
    }
}
