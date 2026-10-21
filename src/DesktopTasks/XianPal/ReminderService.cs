using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed class DailyReminder
    {
        public string Id = Guid.NewGuid().ToString("N");
        public int MinutesAfterMidnight;
        public string Text = "";
        public bool Enabled = true;
        public string LastTriggeredDate = "";
        public string SnoozeUntil = "";
        public bool IsCountdown;
        public int CountdownMinutes;
        public string DueAt = "";
        public string Effect = "impact";

        internal string TimeText
        {
            get
            {
                DateTime due;
                if (IsCountdown && DateTime.TryParse(DueAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out due))
                    return due.ToLocalTime().ToString("HH:mm");
                return (MinutesAfterMidnight / 60).ToString("00") + ":" + (MinutesAfterMidnight % 60).ToString("00");
            }
        }
    }

    internal sealed class ReminderStore
    {
        public List<DailyReminder> Reminders = new List<DailyReminder>();
    }

    internal sealed class ReminderService : IDisposable
    {
        private readonly Timer timer = new Timer();
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly List<DailyReminder> reminders = new List<DailyReminder>();
        private DateTime lastCheck = DateTime.Now.AddMinutes(-1);
        internal event Action Changed;
        internal event Action<DailyReminder> ReminderDue;

        internal ReminderService()
        {
            Load();
            timer.Interval = 5000;
            timer.Tick += delegate { CheckNow(DateTime.Now); };
        }

        internal IList<DailyReminder> Reminders
        {
            get { return reminders.FindAll(delegate(DailyReminder item) { return !item.IsCountdown; }); }
        }

        internal DailyReminder Countdown
        {
            get { return reminders.Find(delegate(DailyReminder item) { return item.IsCountdown; }); }
        }

        internal void Start()
        {
            CheckNow(DateTime.Now);
            timer.Start();
        }

        internal DailyReminder Add(TimeSpan time, string text)
        {
            return Add(time, text, "impact");
        }

        internal DailyReminder Add(TimeSpan time, string text, string effect)
        {
            string clean = (text ?? "").Trim();
            if (clean.Length == 0) throw new ArgumentException("请输入提醒事项。", "text");
            DailyReminder reminder = new DailyReminder
            {
                MinutesAfterMidnight = Math.Max(0, Math.Min(1439, ((int)time.TotalMinutes))),
                Text = clean.Length > 300 ? clean.Substring(0, 300) : clean,
                Enabled = true,
                Effect = NormalizeEffect(effect)
            };
            reminders.Add(reminder);
            Sort();
            SaveAndNotify();
            return reminder;
        }

        internal DailyReminder StartCountdown(int minutes)
        {
            return StartCountdown(minutes, DateTime.Now);
        }

        internal DailyReminder StartCountdown(int minutes, DateTime now)
        {
            int safeMinutes = Math.Max(1, Math.Min(10080, minutes));
            reminders.RemoveAll(delegate(DailyReminder item) { return item.IsCountdown; });
            DailyReminder reminder = new DailyReminder
            {
                IsCountdown = true,
                CountdownMinutes = safeMinutes,
                DueAt = now.AddMinutes(safeMinutes).ToString("o", CultureInfo.InvariantCulture),
                Text = safeMinutes.ToString(CultureInfo.InvariantCulture) + "分钟时间到了",
                Enabled = true,
                Effect = "countdown"
            };
            reminders.Add(reminder);
            Sort();
            SaveAndNotify();
            return reminder;
        }

        internal void CancelCountdown()
        {
            if (reminders.RemoveAll(delegate(DailyReminder item) { return item.IsCountdown; }) > 0) SaveAndNotify();
        }

        internal void Delete(DailyReminder reminder)
        {
            if (reminder == null || !reminders.Remove(reminder)) return;
            SaveAndNotify();
        }

        internal void Toggle(DailyReminder reminder)
        {
            if (reminder == null || !reminders.Contains(reminder)) return;
            reminder.Enabled = !reminder.Enabled;
            reminder.SnoozeUntil = "";
            SaveAndNotify();
        }

        internal void Snooze(DailyReminder reminder, int minutes)
        {
            if (reminder == null) return;
            if (reminder.IsCountdown)
            {
                reminders.RemoveAll(delegate(DailyReminder item) { return item.IsCountdown && item != reminder; });
                if (!reminders.Contains(reminder)) reminders.Add(reminder);
                reminder.DueAt = DateTime.Now.AddMinutes(Math.Max(1, minutes)).ToString("o", CultureInfo.InvariantCulture);
                reminder.Enabled = true;
                Sort();
                SaveAndNotify();
                return;
            }
            if (!reminders.Contains(reminder)) return;
            reminder.SnoozeUntil = DateTime.Now.AddMinutes(Math.Max(1, minutes)).ToString("o", CultureInfo.InvariantCulture);
            SaveAndNotify();
        }

        internal void TriggerForPreview(DailyReminder reminder)
        {
            Action<DailyReminder> handler = ReminderDue;
            if (handler != null && reminder != null) handler(reminder);
        }

        internal void CheckNow(DateTime now)
        {
            DateTime previous = lastCheck;
            lastCheck = now;
            List<DailyReminder> due = new List<DailyReminder>();
            List<DailyReminder> completedCountdowns = new List<DailyReminder>();
            string today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            foreach (DailyReminder reminder in reminders)
            {
                if (!reminder.Enabled) continue;
                if (reminder.IsCountdown)
                {
                    DateTime countdownDue;
                    if (DateTime.TryParse(reminder.DueAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out countdownDue) && countdownDue <= now)
                    {
                        due.Add(reminder);
                        completedCountdowns.Add(reminder);
                    }
                    continue;
                }
                DateTime snooze;
                if (!String.IsNullOrWhiteSpace(reminder.SnoozeUntil) && DateTime.TryParse(reminder.SnoozeUntil, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out snooze))
                {
                    if (snooze <= now)
                    {
                        reminder.SnoozeUntil = "";
                        due.Add(reminder);
                    }
                    continue;
                }
                DateTime scheduled = now.Date.AddMinutes(reminder.MinutesAfterMidnight);
                if (!String.Equals(reminder.LastTriggeredDate, today, StringComparison.Ordinal) && scheduled > previous && scheduled <= now)
                {
                    reminder.LastTriggeredDate = today;
                    due.Add(reminder);
                }
            }
            if (due.Count == 0) return;
            foreach (DailyReminder completed in completedCountdowns) reminders.Remove(completed);
            Save();
            Action changed = Changed;
            if (changed != null && completedCountdowns.Count > 0) changed();
            foreach (DailyReminder reminder in due)
            {
                Action<DailyReminder> handler = ReminderDue;
                if (handler != null) handler(reminder);
            }
        }

        internal static bool CrossesScheduledTime(int minutesAfterMidnight, DateTime previous, DateTime now)
        {
            DateTime scheduled = now.Date.AddMinutes(Math.Max(0, Math.Min(1439, minutesAfterMidnight)));
            return scheduled > previous && scheduled <= now;
        }

        private string StoragePath
        {
            get { return Path.Combine(Program.StateDirectory, "reminders.json"); }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(StoragePath)) return;
                ReminderStore store = serializer.Deserialize<ReminderStore>(File.ReadAllText(StoragePath));
                if (store != null && store.Reminders != null)
                {
                    foreach (DailyReminder reminder in store.Reminders)
                    {
                        if (reminder == null || String.IsNullOrWhiteSpace(reminder.Text)) continue;
                        if (String.IsNullOrWhiteSpace(reminder.Id)) reminder.Id = Guid.NewGuid().ToString("N");
                        if (reminder.IsCountdown && String.IsNullOrWhiteSpace(reminder.DueAt)) continue;
                        reminder.Effect = reminder.IsCountdown ? "countdown" : NormalizeEffect(reminder.Effect);
                        reminder.MinutesAfterMidnight = Math.Max(0, Math.Min(1439, reminder.MinutesAfterMidnight));
                        reminders.Add(reminder);
                    }
                }
                Sort();
            }
            catch { }
        }

        private void Sort()
        {
            reminders.Sort(delegate(DailyReminder left, DailyReminder right)
            {
                if (left.IsCountdown != right.IsCountdown) return left.IsCountdown ? -1 : 1;
                if (left.IsCountdown)
                {
                    DateTime leftDue, rightDue;
                    DateTime.TryParse(left.DueAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out leftDue);
                    DateTime.TryParse(right.DueAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out rightDue);
                    return leftDue.CompareTo(rightDue);
                }
                int time = left.MinutesAfterMidnight.CompareTo(right.MinutesAfterMidnight);
                return time != 0 ? time : String.Compare(left.Text, right.Text, StringComparison.CurrentCultureIgnoreCase);
            });
        }

        internal static string NormalizeEffect(string effect)
        {
            return String.Equals(effect, "popup", StringComparison.OrdinalIgnoreCase) ? "popup" : "impact";
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
                string json = serializer.Serialize(new ReminderStore { Reminders = new List<DailyReminder>(reminders) });
                File.WriteAllText(temporary, json);
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
