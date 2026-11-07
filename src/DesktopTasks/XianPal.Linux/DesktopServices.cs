// SPDX-License-Identifier: Apache-2.0
using System.Text;
using Avalonia.Threading;

namespace XianPal.Linux;

internal sealed class DesktopServices : IDisposable
{
    private readonly Func<PetCharacter> activeCharacter;
    private readonly UiTimer workshopTimer = new() { Interval = 250 };
    private readonly Dictionary<string, ClaudeCliRunner> scheduledRuns = new();
    private DateTime workshopSavedAt;
    private bool disposed;

    internal ClaudeTaskService Tasks { get; } = new();
    internal ReminderService Reminders { get; } = new();
    internal WorkshopData Workshop { get; }
    internal SectExecution Execution { get; private set; }
    internal event Action<PlannedEvent> EventDue;
    internal event Action<string> Failure;

    private static string WorkshopPath => Path.Combine(Program.StateDirectory, "workshop.json");

    internal DesktopServices(Func<PetCharacter> character)
    {
        activeCharacter = character;
        Workshop = WorkshopFiles.Load<WorkshopData>(WorkshopPath);
        if (Workshop.Sects == null || Workshop.Events == null || Workshop.History == null)
            throw new InvalidDataException("事件与门派数据不完整。");
        bool interrupted = false;
        foreach (SectRun run in Workshop.History.Where(run => run.Status == "running"))
        {
            interrupted = true;
            run.Status = "interrupted";
            run.Finished = DateTime.Now.ToString("o");
            foreach (SectStepRun step in run.Steps.Where(step => step.Status is "pending" or "running"))
                step.Status = "interrupted";
        }
        if (interrupted) SaveWorkshop();
        Tasks.TaskDue += LaunchTask;
        workshopTimer.Tick += (_, _) => CheckWorkshop();
    }

    internal void Start()
    {
        Tasks.Start();
        Reminders.Start();
        workshopTimer.Start();
    }

    private void LaunchTask(ScheduledClaudeTask task)
    {
        DateTime now = DateTime.Now;
        if (task.OverlapMode == "block" && task.History.Any(run => scheduledRuns.ContainsKey(run.Id)))
        {
            Tasks.RecordSkipped(task, now, "上一次运行仍在执行。");
            return;
        }
        ClaudeTaskRunRecord record = Tasks.BeginRun(task, now);
        var runner = new ClaudeCliRunner();
        var output = new StringBuilder();
        runner.OutputReceived += text => Dispatcher.UIThread.Post(() =>
        {
            if (disposed || !scheduledRuns.ContainsKey(record.Id)) return;
            output.Append(text);
            if (output.Length > 131072) output.Remove(0, output.Length - 131072);
            Tasks.UpdateRunProgress(task, record.Id, output.ToString());
        });
        runner.Completed += code => Dispatcher.UIThread.Post(() =>
        {
            if (disposed || !scheduledRuns.Remove(record.Id)) return;
            runner.Dispose();
            Tasks.CompleteRun(task, record.Id, code == 0, code, output.ToString(), DateTime.Now);
        });
        scheduledRuns.Add(record.Id, runner);
        try
        {
            if (task.RunMode != "background")
                throw new InvalidOperationException("此运行方式尚未移植到 Linux。");
            runner.Start(task.Prompt, task.WorkingDirectory, null, task.Id, activeCharacter().Id);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            scheduledRuns.Remove(record.Id);
            runner.Dispose();
            Tasks.CompleteRun(task, record.Id, false, -1, error.Message, DateTime.Now);
        }
    }

    internal void SaveWorkshop()
    {
        var json = new CompatibleJson { MaxJsonLength = 33554432 };
        while (Encoding.UTF8.GetByteCount(json.Serialize(Workshop)) > 6000000 && Workshop.History.Count > 1)
            Workshop.History.RemoveAt(Workshop.History.Count - 1);
        WorkshopFiles.Save(WorkshopPath, Workshop);
        workshopSavedAt = DateTime.UtcNow;
    }

    internal void StartSect(CompanionSect sect)
    {
        if (Execution?.IsRunning == true) throw new InvalidOperationException("请等待或取消当前门派任务。");
        var next = new SectExecution(sect, () => new LocalSectAgent());
        Execution?.Dispose();
        Execution = next;
        Workshop.History.Insert(0, next.Record);
        while (Workshop.History.Count > 5) Workshop.History.RemoveAt(Workshop.History.Count - 1);
        try { SaveWorkshop(); }
        catch { next.Cancel(); throw; }
        next.Pump(DateTime.UtcNow);
    }

    internal void CancelSect()
    {
        Execution?.Cancel();
        SaveWorkshop();
    }

    private void CheckWorkshop()
    {
        try
        {
            DateTime now = DateTime.Now;
            if (Execution?.IsRunning == true)
            {
                Execution.Pump(DateTime.UtcNow);
                if (!Execution.IsRunning || DateTime.UtcNow - workshopSavedAt > TimeSpan.FromSeconds(5)) SaveWorkshop();
            }
            foreach (PlannedEvent plan in Workshop.Events.Where(plan => !plan.Done && plan.When <= now).ToArray())
            {
                if (plan.Repeat is "daily" or "weekly") plan.When = PlannedEvent.Next(plan.When, now, plan.Repeat);
                else plan.Done = true;
                SaveWorkshop();
                EventDue?.Invoke(plan);
            }
        }
        catch (Exception error)
        {
            workshopTimer.Stop();
            Execution?.Cancel();
            Failure?.Invoke(error.Message);
        }
    }

    public void Dispose()
    {
        disposed = true;
        workshopTimer.Dispose();
        if (Execution != null)
        {
            Execution.Dispose();
            try { SaveWorkshop(); }
            catch (IOException) { }
        }
        foreach (ClaudeCliRunner runner in scheduledRuns.Values) runner.Dispose();
        scheduledRuns.Clear();
        Reminders.Dispose();
        Tasks.Dispose();
    }
}
