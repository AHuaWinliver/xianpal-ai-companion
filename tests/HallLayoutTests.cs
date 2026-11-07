// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using XianPal;

internal static class HallLayoutTests
{
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    private static T Find<T>(Control parent) where T:Control
    {foreach(Control child in parent.Controls){T found=child as T;if(found!=null)return found;found=Find<T>(child);if(found!=null)return found;}return null;}
    private static void Capture(Control control,string path)
    {using(var image=new Bitmap(control.Width,control.Height)){control.DrawToBitmap(image,new Rectangle(Point.Empty,control.Size));image.Save(path,ImageFormat.Png);}}
    private static void Tick(WorkshopForm form)
    {
        var timer=(Timer)typeof(WorkshopForm).GetField("refresh",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
        typeof(Timer).GetMethod("OnTick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(timer,new object[]{EventArgs.Empty});
        Application.DoEvents();
    }
    private static SectRun Record(int count)
    {
        var record=new SectRun {Sect="云栖门 · 协作大堂",Status="running"};
        for(int i=0;i<count;i++)record.Steps.Add(new SectStepRun {Id="task"+i,Member="道友"+i,CharacterId=PetCharacter.All[i%4].Id,Status=i==0?"running":i==1?"success":"pending",Output="成员回复 "+i+"：中文与交接摘要 😀"});
        return record;
    }
    [STAThread]private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();string root=Environment.GetEnvironmentVariable("FUFU_STATE_DIR");Directory.CreateDirectory(root);
            int samples=0;
            foreach(var viewport in new[]{new Size(600,300),new Size(800,500),new Size(1920,900),new Size(2560,1440)})
            foreach(int count in new[]{0,1,2,4,5,8,12})foreach(float unit in new[]{1f,1.5f,2f})
            {
                Size canvas=SectHallLayout.CanvasSize(viewport,count,unit);Rectangle[] seats=SectHallLayout.Seats(canvas,count,unit);
                CheckGeometry(canvas,viewport,seats);samples++;
            }
            Check(samples==84,"84 viewport/member-count/font-scale combinations: valid cells, no overlaps or horizontal overflow");
            Check(SectHallLayout.CanvasSize(new Size(800,380),4,1)==new Size(800,380),"four default companions fit a small scene without the old permanent scroll");
            var data=new WorkshopData();data.Sects.Add(CompanionRolePresets.Create("云栖门",root,false));data.History.Add(Record(4));
            using(var form=new WorkshopForm(data,delegate{},delegate{return null;},delegate{throw new Exception("must never execute Claude");},delegate{}))
            {
                form.Show();var tabs=Find<TabControl>(form);tabs.SelectedIndex=1;Application.DoEvents();Tick(form);
                var hall=Find<SectHallControl>(form);var viewport=(SectHallViewport)hall.Parent;var split=Find<SplitContainer>(tabs.SelectedTab);
                var sprite = typeof(SectHallControl).GetMethod("Sprite", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (PetCharacter character in PetCharacter.All)
                {
                    string assets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
                    if (character.Id != "orbit") assets = Path.Combine(assets, "characters", character.Id);
                    using (Image original = Image.FromFile(Path.Combine(assets, "failed", "07.png")))
                    {
                        var frame = (Image)sprite.Invoke(hall, new object[] { character.Id, "failed", 23 });
                        Check(frame != null && frame.Size == original.Size, character.Name + " hall keeps the original sprite resolution throughout its failure loop");
                    }
                }
                var details=Find<UnicodeRichTextBox>(split.Panel2);
                Check(hall.Dock==DockStyle.None&&viewport.Dock==DockStyle.Fill&&details.Dock==DockStyle.Fill,"scene and details share a flexible split viewport");
                foreach(var size in new[]{new Size(820,600),new Size(1000,740),new Size(1500,900),new Size(1000,740),new Size(820,600)})
                {
                    form.Size=size;form.PerformLayout();Application.DoEvents();Tick(form);
                    Console.WriteLine("INFO resize "+size+" viewport="+viewport.ClientSize+" hall="+hall.Size+" font="+hall.Font.Height+" split="+split.SplitterDistance+"/"+split.Height+" scroll="+viewport.VerticalScroll.Visible+","+viewport.HorizontalScroll.Visible);
                    Capture(tabs.SelectedTab,Path.Combine(root,"hall-"+size.Width+"x"+size.Height+".png"));
                    Check(hall.Width==viewport.ClientSize.Width&&hall.Height==viewport.ClientSize.Height&&!viewport.HorizontalScroll.Visible&&!viewport.VerticalScroll.Visible,"four-companion scene fills resized page without scroll: "+size);
                    double ratio=split.SplitterDistance/(double)(split.ClientSize.Height-split.SplitterWidth);
                    Check(Math.Abs(ratio-.75)<.025,"details proportion survives resize: "+size);
                    Capture(tabs.SelectedTab,Path.Combine(root,"hall-"+size.Width+"x"+size.Height+".png"));
                }
                form.WindowState=FormWindowState.Maximized;Application.DoEvents();Tick(form);
                Check(hall.Size==viewport.ClientSize&&!viewport.HorizontalScroll.Visible,"maximized scene fills the current display");
                Capture(tabs.SelectedTab,Path.Combine(root,"hall-maximized.png"));
                form.WindowState=FormWindowState.Normal;form.Size=new Size(1000,740);Application.DoEvents();Tick(form);
                // Hit testing must use the current grid even before the next paint.
                Rectangle first=SectHallLayout.Seats(hall.ClientSize,4,Math.Max(1,hall.Font.Height/16f))[0];
                typeof(Control).GetMethod("OnMouseClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hall,new object[]{new MouseEventArgs(MouseButtons.Left,1,first.Left+first.Width/2,first.Top+first.Height/2,0)});
                Check(details.Text.Contains("成员回复 0")&&details.Text.Contains("中文"),"resized seat still opens the correct live Chinese reply");
                split.SplitterDistance=(int)((split.ClientSize.Height-split.SplitterWidth)*.6);Application.DoEvents();form.Size=new Size(1200,850);Application.DoEvents();Tick(form);
                Check(Math.Abs(split.SplitterDistance/(double)(split.ClientSize.Height-split.SplitterWidth)-.6)<.025&&details.Text.Contains("成员回复 0"),"manual details resize preserves its ratio and selected reply");
                Font normal=form.Font;
                using(var enlarged=new Font(normal.FontFamily,13.5f))
                {
                    form.Font=enlarged;viewport.PerformLayout();Application.DoEvents();Tick(form);
                    Check(hall.Width==viewport.ClientSize.Width&&!viewport.HorizontalScroll.Visible,"larger UI font keeps workstations inside the available width");
                    Capture(tabs.SelectedTab,Path.Combine(root,"hall-large-font.png"));form.Font=normal;
                }
                viewport.PerformLayout();Application.DoEvents();Tick(form);
                form.Size=new Size(820,600);data.History[0]=Record(12);Tick(form);
                Check(hall.Height>viewport.ClientSize.Height&&viewport.VerticalScroll.Visible&&!viewport.HorizontalScroll.Visible,"twelve companions get vertical scrolling only when needed");
                viewport.AutoScrollPosition=new Point(0,140);Application.DoEvents();Tick(form);
                Check(viewport.AutoScrollPosition.Y<0,"dense hall can scroll to lower rows");
                // Windows may cap top-level height at its monitor track limit;
                // dense content should remain scrollable there. Then reduce
                // the record to verify that no stale native bars remain.
                form.Size=new Size(1800,1800);Application.DoEvents();Tick(form);
                Check(hall.Width==viewport.ClientSize.Width&&!viewport.HorizontalScroll.Visible,"dense hall still fits width after growing within the display limit");
                data.History[0]=Record(4);Tick(form);
                Check(hall.Height==viewport.ClientSize.Height&&!viewport.VerticalScroll.Visible&&hall.Top==0,"smaller roster removes stale scroll range and recenters content");
                tabs.SelectedIndex=0;form.Size=new Size(1000,740);Application.DoEvents();tabs.SelectedIndex=1;Application.DoEvents();Tick(form);
                Check(hall.Size==viewport.ClientSize&&!viewport.HorizontalScroll.Visible,"hidden tab resize is applied on return");
                Capture(tabs.SelectedTab,Path.Combine(root,"hall-final.png"));form.Hide();
            }
            Console.WriteLine("Hall layout checks passed; no model calls or user settings changed. Evidence: "+root);return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static void CheckGeometry(Size canvas,Size viewport,Rectangle[] seats)
    {
        if(canvas.Width!=viewport.Width||canvas.Height<viewport.Height)throw new Exception("invalid canvas");
        Rectangle area=new Rectangle(Point.Empty,canvas);
        for(int i=0;i<seats.Length;i++)
        {
            if(seats[i].Width<1||seats[i].Height<1||!area.Contains(seats[i]))throw new Exception("seat outside viewport");
            for(int j=0;j<i;j++)if(seats[i].IntersectsWith(seats[j]))throw new Exception("overlapping workstations");
        }
    }
}
