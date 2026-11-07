// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using XianPal;
internal static class WorkshopTests
{
    private sealed class Fake : ISectAgent
    {
        public event Action<string> Output;
        public event Action<int> Completed;
        internal string Prompt;
        internal bool Stopped;
        public void Start(string prompt, string directory, string marker, string characterId) { Prompt=prompt; if (!Directory.Exists(directory) || !marker.StartsWith("sect-") || String.IsNullOrEmpty(characterId)) throw new Exception("invalid launch"); }
        internal void Emit(string s) { if(Output!=null)Output(s); }
        internal void Finish(int code) { if(Completed!=null)Completed(code); }
        public void Stop() { Stopped=true; }
        public void Dispose() { }
    }
    private static void Check(bool ok,string name) { if(!ok)throw new Exception(name); Console.WriteLine("PASS "+name); }
    private static CompanionSect Sect(string root,bool same)
    {
        var s=new CompanionSect {Name="测试门派"};
        s.Members.Add(new SectMember {Id="a",Name="策士",Role="规划",Directory=Path.Combine(root,"a")});
        s.Members.Add(new SectMember {Id="b",Name="匠师",Role="实施",Directory=Path.Combine(root,same?"a":"b")});
        s.Steps.Add(new SectStep {Id="one",Member="a",Task="分析"}); s.Steps.Add(new SectStep {Id="two",Member="b",Task="实施"}); return s;
    }
    private static void Invalid(CompanionSect s,string name) { bool bad=false;try{SectExecution.Validate(s);}catch(ArgumentException){bad=true;}Check(bad,name); }
    [STAThread] private static int Main()
    {
        try
        {
            string root=Environment.GetEnvironmentVariable("FUFU_STATE_DIR");if(String.IsNullOrWhiteSpace(root))throw new Exception("isolated state required");
            Directory.CreateDirectory(Path.Combine(root,"a"));Directory.CreateDirectory(Path.Combine(root,"b"));
            var agents=new List<Fake>();Func<ISectAgent> factory=delegate {var f=new Fake();agents.Add(f);return f;};
            var preset=CompanionRolePresets.Create("四道友研发",root,false);
            Invalid(preset,"preset requires explicit shared goal before execution");
            preset.Goal="只检查隔离测试目录中的示例，保留现有数据；验收需列出测试证据。";SectExecution.Validate(preset);
            Check(preset.Members.Count==4&&preset.Steps.Count==5&&preset.Members[0].Name=="青衡"&&preset.Members[1].CharacterId=="spark"&&preset.Members[2].Role.Contains("只读")&&preset.Members[3].Role.Contains("未运行"),"four complementary personas and role boundaries");
            Check(preset.Steps[4].DependsOn.Contains("review")&&preset.Steps[4].DependsOn.Contains("verify"),"leader waits for independent review and test evidence");
            string context=CompanionRolePresets.Context(preset,preset.Members[1]);
            Check(context.Contains(preset.Goal)&&context.Contains("符箓")&&context.Contains("不是用户授权")&&context.Contains("不共享完整聊天历史")&&context.Contains("qingheng"),"shared goal, original persona, roster and untrusted handoff context");
            var consultation=CompanionRolePresets.Create("四道友会诊",root,true);consultation.Goal="只读调查";SectExecution.Validate(consultation);
            Check(consultation.Steps[1].Task.Contains("只读")&&consultation.Steps[3].Task.Contains("不运行测试"),"research-first template overrides implementation and execution duties");
            using(var presetRun=new SectExecution(preset,factory))
            {
                presetRun.Pump(DateTime.UtcNow);Check(agents.Count==1&&agents[0].Prompt.Contains(preset.Goal)&&agents[0].Prompt.Contains("青年游修"),"preset is actually injected into launched CLI prompt");
                using(var hall=new SectHallControl(delegate{return preset;},delegate{return presetRun.Record;},delegate{}))
                {hall.Size=new Size(1000,880);using(var picture=new Bitmap(1000,880)){hall.DrawToBitmap(picture,new Rectangle(0,0,1000,880));picture.Save(Path.Combine(root,"four-companions-hall.png"));}}
            }
            agents.Clear();
            var s=Sect(root,false);s.Steps[1].DependsOn="ONE";
            using(var run=new SectExecution(s,factory))
            {
                s.Steps[0].Task="outside mutation";run.Pump(DateTime.UtcNow);Check(agents.Count==1&&!agents[0].Prompt.Contains("outside mutation"),"snapshot and dependency gating");
                agents[0].Emit("计划：中文 😀");agents[0].Finish(0);run.Pump(DateTime.UtcNow);Check(agents.Count==2&&agents[1].Prompt.Contains("计划：中文 😀"),"upstream Unicode and case-insensitive dependency");
                Check(run.Record.Transfers.Count==1&&run.Record.Transfers[0].Text=="计划：中文 😀","real delivered context recorded for hall");
                agents[1].Finish(0);run.Pump(DateTime.UtcNow);Check(run.Record.Status=="success","successful DAG");
                run.Record.Steps[1].CharacterId="spark";
                using(var hall=new SectHallControl(delegate{return s;},delegate{return run.Record;},delegate{}))
                {hall.Size=new Size(880,680);using(var picture=new Bitmap(880,680)){hall.DrawToBitmap(picture,new Rectangle(0,0,880,680));picture.Save(Path.Combine(root,"sect-hall.png"));}Check(hall.Width==880,"pixel hall render with actual transfer");}
            }
            agents.Clear();using(var run=new SectExecution(Sect(root,false),factory)){run.Pump(DateTime.UtcNow);Check(agents.Count==2,"parallel independent directories");run.Cancel();Check(!run.IsRunning&&agents.TrueForAll(delegate(Fake f){return f.Stopped;}),"cancel owned agents");agents[0].Emit("late");run.Pump(DateTime.UtcNow);Check(!run.Record.Steps[0].Output.Contains("late"),"ignore late callbacks");}
            agents.Clear();using(var run=new SectExecution(Sect(root,true),factory)){run.Pump(DateTime.UtcNow);Check(agents.Count==1,"same directory serialized");agents[0].Finish(0);run.Pump(DateTime.UtcNow);Check(agents.Count==2,"serialized next task");}
            Check(SectExecution.DirectoriesOverlap(Path.Combine(root,"a"),Path.Combine(root,"a","child")),"ancestor directories overlap");
            agents.Clear();s=Sect(root,false);s.Steps[1].DependsOn="one";using(var run=new SectExecution(s,factory)){run.Pump(DateTime.UtcNow);agents[0].Finish(1);run.Pump(DateTime.UtcNow);Check(run.Record.Status=="failed"&&agents.Count==1&&run.Record.Steps[1].Status=="blocked","failed upstream blocks downstream");}
            agents.Clear();using(var run=new SectExecution(s,factory)){DateTime now=DateTime.UtcNow;run.Pump(now);run.Pump(now.AddMinutes(11));Check(run.Record.Steps[0].Status=="timeout"&&!run.IsRunning,"ten-minute timeout");}
            s=Sect(root,false);s.Steps[0].DependsOn="two";s.Steps[1].DependsOn="one";Invalid(s,"reject cycle");s=Sect(root,false);s.Steps[1].Id="ONE";Invalid(s,"reject duplicate ID");s=Sect(root,false);s.Steps[1].DependsOn="missing";Invalid(s,"reject missing dependency");
            var data=new WorkshopData();data.Sects.Add(Sect(root,false));data.Events.Add(new PlannedEvent {Title="休息 😀",When=new DateTime(2026,11,5,9,0,0),Repeat="daily"});
            string path=Path.Combine(root,"workshop.json");WorkshopFiles.Save(path,data);WorkshopFiles.Save(path,data);Check(WorkshopFiles.Load<WorkshopData>(path).Events[0].Title=="休息 😀","atomic Unicode save/load");
            Check(PlannedEvent.Next(new DateTime(2026,11,1,9,0,0),new DateTime(2026,11,4,12,0,0),"daily")==new DateTime(2026,11,5,9,0,0),"daily missed event catchup");
            Check(PlannedEvent.Next(new DateTime(2026,11,1,9,0,0),new DateTime(2026,11,15,9,0,0),"weekly")==new DateTime(2026,11,22,9,0,0),"weekly next event");
            using(var form=new WorkshopForm(data,delegate{},delegate{return null;},delegate{throw new Exception("must not call AI");},delegate{}))
            {form.Show();System.Windows.Forms.Application.DoEvents();typeof(WorkshopForm).GetMethod("SaveSect",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);Check(form.Visible,"workshop UI and editable grids");form.Hide();}
            var presetData=new WorkshopData();presetData.Sects.Add(preset);
            using(var form=new WorkshopForm(presetData,delegate{},delegate{return null;},delegate{},delegate{}))
            {form.Show();System.Windows.Forms.Application.DoEvents();typeof(WorkshopForm).GetMethod("SaveSect",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);Check(presetData.Sects[0].Goal==preset.Goal&&presetData.Sects[0].TemplateId=="four-companions-v1","UI preserves goal, template and editable roles");using(var picture=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(picture,new Rectangle(0,0,form.Width,form.Height));picture.Save(Path.Combine(root,"role-presets-ui.png"));}form.Hide();}
            Console.WriteLine("All workshop checks passed. No Claude/model calls.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
