// SPDX-License-Identifier: Apache-2.0
using System.Text;
using Avalonia.Threading;

namespace XianPal.Linux;

internal sealed class DesktopServices : IDisposable
{
    private readonly Func<PetCharacter> activeCharacter;
    private readonly UiTimer workshopTimer = new() { Interval = 250 };
    private readonly Dictionary<string, ClaudeCliRunner> scheduledRuns = new();
    private readonly Dictionary<string, VisibleTaskMonitor> terminalRuns = new();
    private readonly BackgroundTaskSupervisor backgroundSupervisor = new();
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
        Tasks.AssignLegacyTasks(activeCharacter().Id);
        Tasks.Start();
        foreach (ScheduledClaudeTask task in Tasks.Tasks)
            foreach (ClaudeTaskRunRecord record in task.History.Where(record => record.Status == "running").ToArray())
            {
                if (record.RunMode == "claude-background" && record.BackgroundJobName.Length > 0) MonitorBackground(task, record);
                else if (record.RunMode != "background" && LinuxTerminalJob.CanRecover(record)) MonitorTerminal(task, record, null);
            }
        Reminders.Start();
        workshopTimer.Start();
    }

    private void LaunchTask(ScheduledClaudeTask task)
    {
        DateTime now = DateTime.Now;
        if (task.OverlapMode == "block" && task.History.Any(run => run.Status == "running"))
        {
            Tasks.RecordSkipped(task, now, "上一次运行仍在执行。");
            return;
        }
        ClaudeTaskRunRecord record = Tasks.BeginRun(task, now);
        if (task.RunMode != "background")
        {
            try
            {
                if (task.RunMode == "claude-background")
                {
                    record.BackgroundJobName = "XianPalBg-" + record.Id;
                    record.BackgroundState = "dispatching";
                }
                var info = LinuxTerminalJob.Prepare(task, record, task.CharacterId);
                Tasks.PersistBackgroundRun(task, record);
                if (task.RunMode == "claude-background")
                {
                    using var process = System.Diagnostics.Process.Start(info);
                    MonitorBackground(task, record);
                }
                else MonitorTerminal(task, record, info);
            }
            catch (Exception error)
            {
                Tasks.CompleteRun(task, record.Id, false, -1, error.Message, DateTime.Now);
            }
            return;
        }
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
            runner.Start(task.Prompt, task.WorkingDirectory, null, task.Id, task.CharacterId);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            scheduledRuns.Remove(record.Id);
            runner.Dispose();
            Tasks.CompleteRun(task, record.Id, false, -1, error.Message, DateTime.Now);
        }
    }

    private void MonitorTerminal(ScheduledClaudeTask task, ClaudeTaskRunRecord record, System.Diagnostics.ProcessStartInfo launch)
    {
        var monitor = new VisibleTaskMonitor(LinuxTerminalJob.LogPath(record),
            output => Tasks.UpdateRunProgress(task, record.Id, output),
            (code, output) =>
            {
                terminalRuns.Remove(record.Id);
                Tasks.CompleteRun(task, record.Id, code == 0, code, output, DateTime.Now);
            },
            (status, session, reaction) => { record.BackgroundSessionId = session; record.BackgroundState = status; });
        terminalRuns.Add(record.Id, monitor);
        if (launch != null) monitor.Start(launch);
        else monitor.Resume();
    }

    private void MonitorBackground(ScheduledClaudeTask task, ClaudeTaskRunRecord record)
    {
        var profile = IntegrationPreferences.Load();
        string command = profile.ExecutablePathFor(record.CharacterId);
        backgroundSupervisor.Track(new BackgroundTaskWatch
        {
            JobName = record.BackgroundJobName, JobId = record.BackgroundJobId, Directory = ClaudeTaskService.RunDirectory(task, record),
            Command = string.IsNullOrEmpty(command) ? ClaudeCliRunner.FindClaudeCommand(record.CharacterId) : command, SettingsPath = profile.EffectiveSettingsPathFor(record.CharacterId),
            LogPath = LinuxTerminalJob.LogPath(record), Update = snapshot =>
            {
                if (disposed) return;
                if (snapshot.JobId.Length > 0) record.BackgroundJobId = snapshot.JobId;
                if (snapshot.SessionId.Length > 0) record.BackgroundSessionId = snapshot.SessionId;
                record.BackgroundState = snapshot.State;
                if (snapshot.Output.Length > 0) record.BackgroundOutput = snapshot.Output;
                string output = snapshot.Status + "\n" + record.BackgroundOutput;
                Tasks.UpdateRunProgress(task, record.Id, output);
                if (snapshot.Finished)
                {
                    File.Delete(LinuxTerminalJob.LogPath(record) + ".settings.json");
                    Tasks.CompleteRun(task, record.Id, snapshot.Success, snapshot.Success ? 0 : -1, output, DateTime.Now);
                }
            }
        });
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
        foreach (VisibleTaskMonitor monitor in terminalRuns.Values) monitor.Dispose();
        terminalRuns.Clear();
        backgroundSupervisor.Dispose();
        scheduledRuns.Clear();
        Reminders.Dispose();
        Tasks.Dispose();
    }
}
