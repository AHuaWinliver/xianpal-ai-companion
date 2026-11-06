// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed partial class PetForm
    {
        private WorkshopData workshopData;
        private WorkshopForm workshopForm;
        private SectExecution sectExecution;
        private readonly Timer workshopTimer = new Timer { Interval = 250 };
        private DateTime workshopLastSaved;
        private bool workshopSaveFailed;
        private string dueSect;
        private string WorkshopPath { get { return Path.Combine(Program.StateDirectory, "workshop.json"); } }
        private void InitializeWorkshop()
        {
            if (Environment.GetEnvironmentVariable("FUFU_TEST_MODE") == "1") return;
            try
            {
                workshopData = WorkshopFiles.Load<WorkshopData>(WorkshopPath);
                if (workshopData.Sects == null || workshopData.Events == null || workshopData.History == null) throw new InvalidDataException("规划数据不完整。");
                foreach (SectRun r in workshopData.History) if (r.Status == "running")
                { r.Status = "interrupted"; r.Finished = DateTime.Now.ToString("o"); foreach (SectStepRun s in r.Steps) if (s.Status == "running" || s.Status == "pending") s.Status = "interrupted"; }
                workshopTimer.Tick += delegate { TickWorkshop(); };
                tray.BalloonTipClicked += delegate { if (dueSect != null) { ShowWorkshop(); if (workshopForm != null) workshopForm.SelectSect(dueSect); dueSect = null; } };
                workshopTimer.Start();
            }
            catch (Exception ex) { workshopData = null; MessageBox.Show("事件与门派数据读取失败，原文件未覆盖。\n" + ex.Message); }
        }
        private void SaveWorkshop()
        {
            var serializer=new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength=33554432 };
            while(System.Text.Encoding.UTF8.GetByteCount(serializer.Serialize(workshopData))>6000000 && workshopData.History.Count>1) workshopData.History.RemoveAt(workshopData.History.Count-1);
            WorkshopFiles.Save(WorkshopPath, workshopData); workshopLastSaved = DateTime.UtcNow;
        }
        private void ShowWorkshop()
        {
            if (workshopData == null) { MessageBox.Show("事件与门派模块不可用，请检查 workshop.json。"); return; }
            if (workshopForm == null || workshopForm.IsDisposed)
                workshopForm = new WorkshopForm(workshopData, SaveWorkshop, delegate { return sectExecution; }, StartSect, delegate { if (sectExecution != null) { sectExecution.Cancel(); SaveWorkshop(); } });
            workshopForm.Show(); workshopForm.BringToFront();
        }
        private void StartSect(CompanionSect sect)
        {
            if (sectExecution != null && sectExecution.IsRunning) throw new InvalidOperationException("请等待或取消当前门派任务。");
            var next = new SectExecution(sect, delegate { return new LocalSectAgent(); });
            if (sectExecution != null) sectExecution.Dispose(); sectExecution = next;
            workshopData.History.Insert(0, next.Record); if (workshopData.History.Count > 5) workshopData.History.RemoveAt(5);
            try { SaveWorkshop(); } catch { next.Cancel(); throw; }
            next.Pump(DateTime.UtcNow);
        }
        private void TickWorkshop()
        {
            try
            {
                if (sectExecution != null && sectExecution.IsRunning)
                { sectExecution.Pump(DateTime.UtcNow); if (!sectExecution.IsRunning || (DateTime.UtcNow-workshopLastSaved).TotalSeconds >= 5) SaveWorkshop(); }
                foreach (PlannedEvent e in workshopData.Events)
                {
                    if (e.Done || e.When > DateTime.Now) continue;
                    if (e.Repeat == "daily" || e.Repeat == "weekly") e.When = PlannedEvent.Next(e.When, DateTime.Now, e.Repeat); else e.Done = true;
                    SaveWorkshop(); // Persist the acknowledgement before notifying.
                    dueSect = e.Sect ?? "";
                    tray.ShowBalloonTip(12000, "仙伴 · 事件到点", e.Title + (dueSect.Length > 0 ? "\n关联门派：" + dueSect + "；点击查看，确认后执行。" : "\n点击打开事件规划。"), ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                workshopTimer.Stop(); if (sectExecution != null) sectExecution.Cancel();
                if (!workshopSaveFailed) { workshopSaveFailed = true; MessageBox.Show("规划保存或执行出错，已暂停新任务，请检查磁盘后重启。\n" + ex.Message); }
            }
        }
        private void DisposeWorkshop()
        {
            workshopTimer.Stop(); workshopTimer.Dispose();
            if (sectExecution != null) { sectExecution.Dispose(); try { SaveWorkshop(); } catch { } }
            if (workshopForm != null) workshopForm.Dispose();
        }
    }
}
