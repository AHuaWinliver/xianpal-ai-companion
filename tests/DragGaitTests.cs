// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using XianPal;

internal static class DragGaitTests
{
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    private static object Field(object o,string n){return o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
    private static void Set(object o,string n,object value){o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);}
    private static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);}
    private static DragGaitClock Steady(double speed,int sampleInterval,int frames)
    {
        var clock=new DragGaitClock();int next=0,last=0;var seen=new HashSet<int>();
        for(int time=0;time<6000;time+=16)
        {
            while(time>=next){clock.RecordMotion(speed*sampleInterval/1000,sampleInterval);last=next;next+=sampleInterval;}
            int frame=clock.Advance(16,time-last,frames);if(frame<0||frame>=frames)throw new Exception("invalid pose");seen.Add(frame);
        }
        if(seen.Count!=frames)throw new Exception("missing gait phases");return clock;
    }
    private static void ClockChecks()
    {
        var slow=Steady(60,16,24);var medium=Steady(240,16,24);var fast=Steady(900,16,24);
        Check(slow.CyclePosition<medium.CyclePosition&&medium.CyclePosition<fast.CyclePosition&&fast.CyclePosition>slow.CyclePosition*2,"slow/medium/fast drag yields progressively faster coordinated 24-phase cycles");
        Console.WriteLine("INFO cycles in 6 seconds: "+slow.CyclePosition.ToString("F2")+" / "+medium.CyclePosition.ToString("F2")+" / "+fast.CyclePosition.ToString("F2"));
        Check(Math.Abs(Steady(240,8,24).CyclePosition-Steady(240,32,24).CyclePosition)<.08,"cadence follows velocity, independent of mouse-event frequency");
        Check(Math.Abs(Steady(240,16,8).CyclePosition-medium.CyclePosition)<.001,"external eight-frame artwork keeps the same physical cycle rate");
        double before=fast.CyclePosition,speed=fast.Speed;
        for(int i=0;i<40;i++)
        {
            fast.RecordMotion(.48,16);fast.Advance(16,0,24);
            if(fast.Speed>speed+.01)throw new Exception("deceleration overshoot");speed=fast.Speed;
        }
        Check(fast.Speed<40&&fast.CyclePosition>before,"slowing the cursor eases the limbs down instead of switching cadence abruptly");
        speed=fast.Speed;fast.RecordMotion(48,16);fast.Advance(16,0,24);
        Check(fast.Speed>speed&&fast.Speed<3000,"acceleration is smoothed rather than jumping to maximum speed");
        before=fast.CyclePosition;fast.Advance(500,0,24);Check(fast.CyclePosition-before<=.161,"a delayed scheduler tick cannot produce a burst of skipped cycles");
        before=fast.CyclePosition;fast.Advance(16,200,24);fast.Advance(16,1000,24);
        Check(fast.CyclePosition==before&&fast.Speed==0,"holding the cursor still stops gait progression");
        fast.Reset();Check(fast.CyclePosition==0&&fast.Speed==0,"new drag resets old phase and velocity");
        fast.RecordMotion(Double.NaN,16);fast.RecordMotion(2,Double.PositiveInfinity);fast.Advance(16,0,24);
        Check(fast.Speed==0&&fast.CyclePosition==0,"invalid samples cannot poison velocity or phase");
    }
    private static double Drag(PetForm pet,int pixels,bool stationaryChecks)
    {
        Call(pet,"BeginDrag",pet,new MouseEventArgs(MouseButtons.Left,1,30,30,0));
        Point mouse=(Point)Field(pet,"dragMouse"),cursor=mouse,origin=pet.Location;
        DateTime start=DateTime.UtcNow;Set(pet,"lastDragSampleAt",start);Set(pet,"lastDragMotionAt",start);
        var clock=(DragGaitClock)Field(pet,"dragGait");
        for(int i=1;i<=90;i++)
        {
            int sign=(i/10)%2==0?1:-1;cursor.Offset(sign*pixels,0);DateTime now=start.AddMilliseconds(i*16);
            double phase=clock.CyclePosition;int frame=(int)Field(pet,"frameIndex");
            Call(pet,"ContinueDragAt",cursor,now);
            if(clock.CyclePosition!=phase)throw new Exception("mouse event advanced gait outside the scheduler");
            if(i>6&&(int)Field(pet,"frameIndex")!=frame)throw new Exception("direction change reset the gait");
            if(pet.Location!=new Point(origin.X+cursor.X-mouse.X,origin.Y+cursor.Y-mouse.Y))throw new Exception("drag tracking changed");
            Call(pet,"TickDragAnimation",now,16d);
        }
        double cycles=clock.CyclePosition;
        var animations=(Dictionary<string,List<Image>>)Field(pet,"animations");string state=(string)Field(pet,"state");
        if(animations[state].Count!=24)throw new Exception("approved 24-phase artwork missing");
        if(stationaryChecks)
        {
            DateTime last=start.AddMilliseconds(90*16);double moving=clock.Speed;
            Call(pet,"TickDragAnimation",last.AddMilliseconds(90),16d);
            Check(clock.Speed<moving,"cursor pause starts easing down before standstill");
            Call(pet,"TickDragAnimation",last.AddMilliseconds(180),16d);
            Call(pet,"TickDragAnimation",last.AddMilliseconds(500),16d);
            Check((string)Field(pet,"state")=="idle"&&clock.CyclePosition==0,"held mouse button with stationary cursor returns to a stable standing pose");
            cursor.Offset(12,0);Call(pet,"ContinueDragAt",cursor,last.AddMilliseconds(600));Call(pet,"TickDragAnimation",last.AddMilliseconds(600),16d);
            Check(((string)Field(pet,"state")).StartsWith("running-")&&clock.CyclePosition>0,"moving the cursor again resumes the coordinated gait");
        }
        Call(pet,"EndDrag",pet,new MouseEventArgs(MouseButtons.Left,1,30,30,0));
        if((bool)Field(pet,"dragging")||clock.CyclePosition!=0||(string)Field(pet,"state")!="idle")throw new Exception("release did not stop movement");
        return cycles;
    }
    private static void Integration()
    {
        using(var pet=new PetForm())
        {
            pet.Show();Application.DoEvents();((Timer)Field(pet,"timer")).Stop();((Timer)Field(pet,"visibilityTimer")).Stop();
            Set(pet,"aiCareEnabled",false);Set(pet,"reduceMotion",false);
            Rectangle area=Screen.FromRectangle(pet.Bounds).WorkingArea;
            var defaults = (ToolStripMenuItem)pet.ContextMenuStrip.Items.Cast<ToolStripItem>().Single(item => item.Text == UiLanguage.Text("Default settings"));
            Check(!defaults.HasDropDownItems, "infrequent settings open a separate window");
            defaults.PerformClick(); Application.DoEvents();
            Form settings = Application.OpenForms.Cast<Form>().Single(form => form.Name == "DefaultSettingsWindow");
            ((NumericUpDown)settings.Controls.Find("DockedOpacity", true).Single()).Value = 60;
            ((Button)settings.Controls.Find("ApplySettings", true).Single()).PerformClick(); Application.DoEvents();
            var alpha = typeof(PetForm).GetProperty("PetWindowAlpha", BindingFlags.NonPublic | BindingFlags.Instance);
            Check((byte)alpha.GetValue(pet, null) == 255, "opacity setting keeps an undocked pet fully visible");
            pet.Location = new Point(area.Left, area.Top + 100);
            Check((bool)Call(pet, "TryDockAtScreenEdge") && (byte)alpha.GetValue(pet, null) == 153 && pet.LayeredRenderSucceeded, "docking applies the selected alpha in the layered renderer");
            Call(pet, "ReleaseEdgeDock");
            Check((byte)alpha.GetValue(pet, null) == 255, "releasing edge docking restores full opacity");
            foreach(PetCharacter character in PetCharacter.All)
            {
                Call(pet,"SetCharacter",character.Id,false);pet.Location=new Point(area.Left+200,area.Top+200);
                double slow=Drag(pet,1,false),fast=Drag(pet,12,true);
                Check(fast>slow*2,character.Name+": fast drag increases cadence; left/right turns preserve phase, position tracking and release");
            }
            Call(pet,"BeginDrag",pet,new MouseEventArgs(MouseButtons.Left,1,30,30,0));
            Point cursor=(Point)Field(pet,"dragMouse");cursor.Offset(30,0);Call(pet,"ContinueDragAt",cursor,DateTime.UtcNow.AddMilliseconds(16));
            var clock=(DragGaitClock)Field(pet,"dragGait");double phase=clock.CyclePosition;
            Set(pet,"lastDragMotionAt",DateTime.UtcNow);Set(pet,"lastAnimationTick",DateTime.UtcNow.AddMilliseconds(-16));
            Call(pet,"TickAnimation",pet,EventArgs.Empty);
            Check(clock.CyclePosition>phase,"the actual animation timer advances drag poses instead of discarding them");
            Set(pet,"reduceMotion",true);Call(pet,"TickDragAnimation",DateTime.UtcNow,16d);
            Check(clock.CyclePosition==0&&(string)Field(pet,"state")=="idle","reduced motion suppresses drag gait");
            Call(pet,"EndDrag",pet,new MouseEventArgs(MouseButtons.Left,1,30,30,0));Set(pet,"reduceMotion",false);
            Call(pet,"BeginDrag",pet,new MouseEventArgs(MouseButtons.Left,1,30,30,0));
            cursor=(Point)Field(pet,"dragMouse");cursor.Offset(3,0);Call(pet,"ContinueDragAt",cursor,DateTime.UtcNow.AddMilliseconds(16));
            Check(!(bool)Field(pet,"dragMoved")&&clock.CyclePosition==0,"small click jitter never starts walking");
            Call(pet,"EndDrag",pet,new MouseEventArgs(MouseButtons.Left,1,30,30,0));
            var clickTimer=(Timer)Field(pet,"clickDelayTimer");Check(clickTimer.Enabled,"single-click interaction is preserved");clickTimer.Stop();pet.Close();
        }
    }
    [STAThread]private static int Main()
    {
        try{Application.EnableVisualStyles();ClockChecks();Integration();Console.WriteLine("Drag gait checks passed. No mouse input injected, model calls or user settings changed.");return 0;}
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
