// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed class WorkshopForm : Form
    {
        private readonly WorkshopData data;
        private readonly Action save;
        private readonly ComboBox sects = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        private readonly TextBox name = new TextBox { Width = 180 };
        private readonly TextBox goal = new TextBox { Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Vertical };
        private readonly DataGridView members = Grid(), steps = Grid(), events = Grid();
        private readonly ListBox history = new ListBox { Dock = DockStyle.Left, Width = 245 };
        private readonly UnicodeRichTextBox output = new UnicodeRichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White };
        private readonly Timer refresh = new Timer { Interval = 300 };
        private readonly Button run = new Button { Text = "确认并执行协作", AutoSize = true }, stop = new Button { Text = "停止本轮", AutoSize = true };
        private readonly List<Control> editors = new List<Control>();
        private readonly Func<SectExecution> active;
        private readonly Action<CompanionSect> execute;
        private readonly Action cancel;
        private string lastOutput;
        private List<PlannedEvent> eventBaseline;
        internal WorkshopForm(WorkshopData data, Action save, Func<SectExecution> active, Action<CompanionSect> execute, Action cancel)
        {
            this.data = data; this.save = save; this.active = active; this.execute = execute; this.cancel = cancel;
            Text = "仙伴 · 事件规划与门派协作"; Size = new Size(1000, 740); MinimumSize = new Size(820, 600); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Microsoft YaHei UI", 9F);
            var tabs = new TabControl { Dock = DockStyle.Fill };
            TabPage sectPage = new TabPage("门派 / 多 Agent"), eventPage = new TabPage("事件规划"), logPage = new TabPage("运行记录");
            var hallPage = new TabPage("门派大堂");
            var hallDetails = new UnicodeRichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, Text = "点击大堂中的成员查看实时回复与交接摘要。执行记录会保存在本机。" };
            var hall = new SectHallControl(delegate { return sects.SelectedItem as CompanionSect; }, delegate { return history.SelectedIndex >= 0 && history.SelectedIndex < data.History.Count ? data.History[history.SelectedIndex] : null; }, delegate(string text) { hallDetails.Text = text; });
            var hallScroll = new SectHallViewport(hall);
            var hallSplit = new SplitContainer { Size=new Size(800,600), Orientation=Orientation.Horizontal, Dock=DockStyle.Fill, SplitterWidth=6, Panel1MinSize=120, Panel2MinSize=80, SplitterDistance=440 };
            hallSplit.Panel1.Controls.Add(hallScroll);hallSplit.Panel2.Controls.Add(hallDetails);hallPage.Controls.Add(hallSplit);
            // Keep the scene/details proportion during resize, including when
            // the tab is hidden. Only a user splitter move changes the ratio.
            double hallRatio=.75;bool arrangingHall=false;
            hallSplit.SizeChanged+=delegate
            {
                int room=hallSplit.ClientSize.Height-hallSplit.SplitterWidth;
                if(room<hallSplit.Panel1MinSize+hallSplit.Panel2MinSize)return;
                arrangingHall=true;
                try{hallSplit.SplitterDistance=Math.Max(hallSplit.Panel1MinSize,Math.Min(room-hallSplit.Panel2MinSize,(int)(room*hallRatio)));}
                finally{arrangingHall=false;}
            };
            hallSplit.SplitterMoved+=delegate
            {if(!arrangingHall&&hallSplit.ClientSize.Height>hallSplit.SplitterWidth)hallRatio=hallSplit.SplitterDistance/(double)(hallSplit.ClientSize.Height-hallSplit.SplitterWidth);};
            tabs.TabPages.AddRange(new[] { sectPage, hallPage, eventPage, logPage }); Controls.Add(tabs);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 82, Padding = new Padding(5) };
            var add = Button("四道友·研发", delegate { NewSect(false); }); var consult = Button("四道友·会诊", delegate { NewSect(true); }); var commit = Button("保存门派", SaveSect);
            toolbar.Controls.AddRange(new Control[] { sects, name, add, consult, commit, run, stop });
            var goalPanel=new Panel { Dock=DockStyle.Top,Height=72,Padding=new Padding(8) };
            goalPanel.Controls.Add(goal);goalPanel.Controls.Add(new Label { Dock=DockStyle.Top,Height=21,Text="共同目标 / 授权范围 / 验收要求（每位道友都能收到，不共享完整聊天历史）" });
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(8) };
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); table.RowStyles.Add(new RowStyle(SizeType.Percent, 40)); table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); table.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            table.Controls.Add(Label("每位成员是独立 Claude CLI 会话。最多2个并发；目录相同或互相包含时串行。\n可点击工作目录单元格后使用下方按钮选目录；任务依赖填写编号，以逗号分隔。"));
            table.Controls.Add(members); var line = new FlowLayoutPanel { Dock = DockStyle.Fill };
            line.Controls.Add(Button("为选中成员选目录", ChooseDirectory));
            var rolePreset=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Width=200 };rolePreset.Items.AddRange(CompanionRolePresets.Titles);rolePreset.SelectedIndex=0;
            line.Controls.Add(rolePreset);line.Controls.Add(Button("套用到选中成员",delegate { ApplyPreset(rolePreset.SelectedIndex); }));
            line.Controls.Add(new Label { Text = "下表：任务与依赖", AutoSize = true, Padding = new Padding(8) }); table.Controls.Add(line); table.Controls.Add(steps);
            sectPage.Controls.Add(table); sectPage.Controls.Add(goalPanel);sectPage.Controls.Add(toolbar);
            Columns(members, new[] { "Id", "Name", "Role", "Directory" }, new[] { "成员编号", "姓名", "职责 / 系统提示", "工作目录" });
            var characterColumn=new DataGridViewComboBoxColumn { DataPropertyName="CharacterId", HeaderText="人物形象", DisplayMember="Value",ValueMember="Key", DataSource=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("orbit","青衡"),new KeyValuePair<string,string>("spark","绯笺"),new KeyValuePair<string,string>("moss","苍芷"),new KeyValuePair<string,string>("pippin","小墨")}};
            members.Columns.Add(characterColumn);
            Columns(steps, new[] { "Id", "Member", "DependsOn", "Task" }, new[] { "任务编号", "成员编号", "依赖任务编号", "任务内容" });
            Columns(events, new[] { "Title", "When", "Repeat", "Sect", "Done" }, new[] { "事件", "本地时间，如2026-11-05 09:00", "重复：once/daily/weekly", "关联门派名称（可空）", "已完成" });
            var eventToolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 55, Padding = new Padding(6) };
            eventToolbar.Controls.Add(Button("保存事件", SaveEvents)); eventToolbar.Controls.Add(new Label { Text = "到点提醒，不自动执行。关联门派仅作为执行入口；补发过期提醒一次。", AutoSize = true, Padding = new Padding(5) });
            eventPage.Controls.Add(events); eventPage.Controls.Add(eventToolbar);
            logPage.Controls.Add(output); logPage.Controls.Add(history);
            ReloadEvents();
            sects.SelectedIndexChanged += delegate { LoadSect(); };
            run.Click += delegate { Guard(delegate { SaveSect(); CompanionSect s = sects.SelectedItem as CompanionSect; if (s == null) return;
                string dirs = ""; foreach (SectMember m in s.Members) dirs += "\n" + m.Name + "：" + m.Directory;
                if (MessageBox.Show(this, "将调用本机 Claude 执行 " + s.Steps.Count + " 项任务，可能读写以下目录并消耗模型额度。\n本轮每步最多10分钟；沿用CLI权限，不自动跳过授权。" + dirs + "\n\n确认开始？", "确认协作范围", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                execute(s); tabs.SelectedTab = hallPage; RefreshRuns(); if(history.Items.Count > 0) history.SelectedIndex = 0; }); };
            stop.Click += delegate { cancel(); RefreshRuns(); };
            history.SelectedIndexChanged += delegate { lastOutput = null; RefreshOutput(); };
            editors.AddRange(new Control[] { sects, name, goal, add, consult, commit, members, steps, line });
            refresh.Tick += delegate { bool busy = active() != null && active().IsRunning; foreach (Control c in editors) c.Enabled = !busy; run.Enabled = !busy; stop.Enabled = busy; RefreshRuns();
                hallScroll.RefreshMembers();
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            VisibleChanged += delegate { if (Visible) { RefreshRuns(); refresh.Start(); } else refresh.Stop(); };
            foreach (CompanionSect s in data.Sects) sects.Items.Add(s); if (sects.Items.Count > 0) sects.SelectedIndex = 0;
        }
        private static List<T> Clone<T>(List<T> list) { var j = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 8388608 }; return j.Deserialize<List<T>>(j.Serialize(list)); }
        private static DataGridView Grid() { return new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, AllowUserToAddRows = true }; }
        private static void Columns(DataGridView g, string[] fields, string[] titles) { for (int i = 0; i < fields.Length; i++) g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = fields[i], HeaderText = titles[i], SortMode = DataGridViewColumnSortMode.NotSortable }); }
        private static Label Label(string text) { return new Label { Text = text, Dock = DockStyle.Fill, AutoSize = false }; }
        private Button Button(string text, Action action) { var b = new Button { Text = text, AutoSize = true }; b.Click += delegate { Guard(action); }; return b; }
        private void Guard(Action action) { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "请检查设置", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
        private void NewSect(bool reviewOnly)
        {
            using(var picker=new FolderBrowserDialog { Description="为四道友选择项目目录；不会立即执行任务。" })
            {
                if(picker.ShowDialog(this)!=DialogResult.OK)return;
                string title="四道友"+(reviewOnly?"会诊":"研发")+(data.Sects.Count+1);
                while(data.Sects.Exists(delegate(CompanionSect item){return item.Name==title;}))title+="新";
                var s=CompanionRolePresets.Create(title,picker.SelectedPath,reviewOnly);
                data.Sects.Add(s); sects.Items.Add(s); sects.SelectedItem=s;goal.Focus();
            }
        }
        private void ApplyPreset(int index)
        {
            var member=members.CurrentRow==null?null:members.CurrentRow.DataBoundItem as SectMember;
            if(member==null)throw new ArgumentException("请先选中成员行。");
            if(MessageBox.Show(this,"将替换选中成员的姓名、形象和职责，保留编号、目录及任务分配。是否继续？","套用道友预设",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
            member.Name=PetCharacter.All[index].Name;member.CharacterId=PetCharacter.All[index].Id;member.Role=CompanionRolePresets.Role(index);members.Refresh();
        }
        private void LoadSect()
        {
            var s = sects.SelectedItem as CompanionSect; if (s == null) return;
            name.Text = s.Name;
            goal.Text=s.Goal??"";
            members.DataSource = new System.ComponentModel.BindingList<SectMember>(Clone(s.Members));
            steps.DataSource = new System.ComponentModel.BindingList<SectStep>(Clone(s.Steps));
        }
        private void ChooseDirectory()
        {
            var m = members.CurrentRow == null ? null : members.CurrentRow.DataBoundItem as SectMember; if (m == null) return;
            using (var picker = new FolderBrowserDialog { SelectedPath = Directory.Exists(m.Directory) ? m.Directory : "" }) if (picker.ShowDialog(this) == DialogResult.OK) { m.Directory = picker.SelectedPath; members.Refresh(); }
        }
        private void SaveSect()
        {
            if (active() != null && active().IsRunning) throw new InvalidOperationException("请先停止本轮协作。");
            members.EndEdit(); steps.EndEdit(); BindingContext[members.DataSource].EndCurrentEdit(); BindingContext[steps.DataSource].EndCurrentEdit();
            var old = sects.SelectedItem as CompanionSect; if (old == null) throw new ArgumentException("请先新建门派。");
            var draft = new CompanionSect { Name = name.Text.Trim(), Goal=goal.Text.Trim(),TemplateId=old.TemplateId, Members = new List<SectMember>((IList<SectMember>)members.DataSource), Steps = new List<SectStep>((IList<SectStep>)steps.DataSource) };
            SectExecution.Validate(draft);
            if (data.Sects.Exists(delegate(CompanionSect s) { return s != old && s.Name == draft.Name; })) throw new ArgumentException("门派名称已存在。");
            int index = data.Sects.IndexOf(old); data.Sects[index] = draft;
            foreach (PlannedEvent e in data.Events) if (e.Sect == old.Name) e.Sect = draft.Name;
            foreach (PlannedEvent e in (IList<PlannedEvent>)events.DataSource) if (e.Sect == old.Name) e.Sect = draft.Name;
            save(); sects.Items[index] = draft; sects.SelectedIndex = index;
        }
        private void SaveEvents()
        {
            events.EndEdit(); BindingContext[events.DataSource].EndCurrentEdit();
            var copy = new List<PlannedEvent>((IList<PlannedEvent>)events.DataSource);
            // A reminder may have fired while this editor was open. Preserve its acknowledged
            // schedule unless the user actually edited the time/repeat/completed fields.
            foreach (PlannedEvent e in copy)
            {
                PlannedEvent before=eventBaseline.Find(delegate(PlannedEvent item){return item.Id==e.Id;});
                PlannedEvent current=data.Events.Find(delegate(PlannedEvent item){return item.Id==e.Id;});
                if(before!=null&&current!=null&&e.When==before.When&&e.Repeat==before.Repeat&&e.Done==before.Done){e.When=current.When;e.Done=current.Done;}
            }
            if (copy.Count > 100) throw new ArgumentException("最多保存100项事件。");
            foreach (PlannedEvent e in copy)
            {
                if (String.IsNullOrWhiteSpace(e.Title) || e.Title.Length > 200 || e.When.Year < 2000 || e.When.Year > 2090 || (e.Repeat != "once" && e.Repeat != "daily" && e.Repeat != "weekly")) throw new ArgumentException("请填写事件、有效时间和重复类型（once/daily/weekly）。");
                if (!String.IsNullOrWhiteSpace(e.Sect) && !data.Sects.Exists(delegate(CompanionSect s) { return s.Name == e.Sect; })) throw new ArgumentException("关联门派不存在：" + e.Sect);
            }
            data.Events = copy; save(); ReloadEvents();
        }
        private void ReloadEvents() { eventBaseline=Clone(data.Events); events.DataSource=new System.ComponentModel.BindingList<PlannedEvent>(Clone(data.Events)); }
        private void RefreshRuns()
        {
            if (history.Items.Count != data.History.Count || (data.History.Count > 0 && (history.Items.Count == 0 || ((string)history.Items[0]).IndexOf(data.History[0].Status, StringComparison.Ordinal) < 0)))
            { int selected = history.SelectedIndex; history.Items.Clear(); foreach (SectRun r in data.History) history.Items.Add(r.Started + " " + r.Sect + " [" + r.Status + "]"); if (history.Items.Count > 0) history.SelectedIndex = Math.Max(0, Math.Min(selected, history.Items.Count - 1)); }
            RefreshOutput();
        }
        private void RefreshOutput()
        {
            int i = history.SelectedIndex; if (i < 0 || i >= data.History.Count) return;
            var text = new System.Text.StringBuilder(); SectRun r = data.History[i];
            text.AppendLine(r.Sect + " / " + r.Status + "\n开始：" + r.Started + "\n结束：" + r.Finished);
            foreach (SectStepRun s in r.Steps) text.AppendLine("\n──── " + s.Id + " · " + s.Member + " [" + s.Status + "] ────\n目录：" + s.Directory + "\n任务：" + s.Prompt + "\n回复：\n" + s.Output);
            string value = text.ToString(); if (lastOutput != value) { output.Text = value; lastOutput = value; }
        }
        internal void SelectSect(string sect) { foreach (CompanionSect s in sects.Items) if (s.Name == sect) { sects.SelectedItem = s; break; } }
        protected override void Dispose(bool disposing) { if (disposing) refresh.Dispose(); base.Dispose(disposing); }
    }
}
