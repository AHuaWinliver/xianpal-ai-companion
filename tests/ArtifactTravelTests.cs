// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using XianPal;

internal static class ArtifactTravelTests
{
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    private static object Field(object o,string n){return o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    private static void Set(object o,string n,object v){o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);}
    private static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,a);}
    private static void TransparentEdges(Bitmap b)
    {
        for(int x=0;x<b.Width;x++)if(b.GetPixel(x,0).A!=0||b.GetPixel(x,b.Height-1).A!=0)throw new Exception("vertical clipping");
        for(int y=0;y<b.Height;y++)if(b.GetPixel(0,y).A!=0||b.GetPixel(b.Width-1,y).A!=0)throw new Exception("horizontal clipping");
    }
    private static Bitmap Render(Image rider,PetCharacter c,float amount,bool left)
    {
        var b=new Bitmap(384,416,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(b))ArtifactTravelArt.Render(g,b.Size,rider,ArtifactTravelArt.FootRow(rider),c.Id,c.ReminderStyle,amount,1.2,left);
        return b;
    }
    private static void Art(string output)
    {
        using(var montage=new Bitmap(384*4,416*2))
        using(var mg=Graphics.FromImage(montage))
        {
            mg.Clear(Color.FromArgb(234,240,240));
            for(int i=0;i<PetCharacter.All.Length;i++)
            {
                PetCharacter c=PetCharacter.All[i];
                string relative=c.Id=="orbit"?"assets/idle/00.png":"assets/characters/"+c.Id+"/idle/00.png";
                using(var rider=Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,relative)))
                {
                    foreach(float amount in new[]{0f,.02f,.15f,.35f,.65f,1f})
                    using(var frame=Render(rider,c,amount,false))TransparentEdges(frame);
                    using(var right=Render(rider,c,1,false))
                    using(var left=Render(rider,c,1,true))
                    {
                        mg.DrawImageUnscaled(right,i*384,0);mg.DrawImageUnscaled(left,i*384,416);
                        // The entire vessel and companion must share one facing transform.
                        int mismatch=0;
                        for(int y=0;y<416;y+=3)for(int x=0;x<384;x+=3)
                        {
                            Color a=right.GetPixel(x,y),b=left.GetPixel(383-x,y);
                            // GDI+ reflects vector coordinates and raster pixel
                            // centers differently by up to one antialiased pixel.
                            int difference=Math.Abs(a.A-b.A);
                            for(int offset=-1;offset<=1;offset++)
                            {int mx=383-x+offset;if(mx>=0&&mx<384)difference=Math.Min(difference,Math.Abs(a.A-left.GetPixel(mx,y).A));}
                            if(difference>100)mismatch++;
                            if(a.A>10&&a.R>220&&a.B>220&&a.G<60)throw new Exception("magenta fringe");
                        }
                        Check(mismatch<35,c.Name+": transparent takeoff/flight, shared left/right facing, no clipping or magenta");
                    }
                    // Feet are authored opaque pixels and the vessel deck follows them.
                    Check(ArtifactTravelArt.FootRow(rider)>rider.Height*.6f,c.Name+": opaque sole anchor found");
                }
            }
            montage.Save(Path.Combine(output,"artifact-travel.png"),ImageFormat.Png);
        }
        using(var rider=Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets/idle/00.png")))
        using(var b=new Bitmap(96,104))
        using(var g=Graphics.FromImage(b))
        {
            var watch=Stopwatch.StartNew();
            for(int i=0;i<300;i++)ArtifactTravelArt.Render(g,b.Size,rider,ArtifactTravelArt.FootRow(rider),"orbit","command",1,i*.016,false);
            watch.Stop();Console.WriteLine("INFO 300 small-canvas flight renders: "+watch.ElapsedMilliseconds+"ms");
        }
    }
    private static void Integration()
    {
        using(var pet=new PetForm())
        {
            pet.Show();Application.DoEvents();((Timer)Field(pet,"timer")).Stop();((Timer)Field(pet,"visibilityTimer")).Stop();
            Set(pet,"petOnSelectedDesktop",true);Set(pet,"userHidden",false);Set(pet,"hovering",false);Set(pet,"dockedEdge","");
            Rectangle area=Screen.FromRectangle(pet.Bounds).WorkingArea;
            pet.Location=new Point(area.Left+40,area.Top+Math.Max(40,(area.Height-pet.Height)/2));
            Point origin=pet.Location;
            var path=(Queue<Point>)Field(pet,"wanderPath");var clock=(ArtifactTravelClock)Field(pet,"artifactTravel");
            Set(pet,"wanderWorkingArea",area);path.Enqueue(new Point(origin.X+30,origin.Y));
            Call(pet,"PrepareArtifactTravel");Check(!clock.Active,"short route keeps the original walking renderer");Call(pet,"CancelWander");
            Set(pet,"wanderWorkingArea",area);
            int right=area.Right-pet.Width-40;
            for(int i=0;i<3;i++){path.Enqueue(new Point(right,origin.Y));path.Enqueue(origin);}
            Call(pet,"PrepareArtifactTravel");Set(pet,"playfulRun",true);
            Check(clock.Active&&ArtifactTravelClock.RouteLength(origin,path)>ArtifactTravelClock.Threshold(area),"closed full-screen route mounts even though start equals destination");
            bool movedRight=false,movedLeft=false;int steps=0;
            while(path.Count>0&&steps++<10000)
            {
                int oldX=pet.Left;clock.Advance(16);Call(pet,"TickWander",16d);
                movedRight|=pet.Left>oldX;movedLeft|=pet.Left<oldX;
                if(steps%40==0)Call(pet,"RefreshPetVisual");
                CheckBounds(pet,area);
            }
            Check(path.Count==0&&movedRight&&movedLeft&&pet.Location==origin,"real route moves in both directions, stays on its monitor and finishes at destination");
            Check(clock.Active&&clock.Landing&&(string)Field(pet,"state")=="idle","arrival stops gait and begins landing");
            for(int i=0;i<35;i++)clock.Advance(16);
            Call(pet,"RefreshPetVisual");Check(!clock.Active&&clock.Envelope==0,"landing restores the ordinary standing renderer");
            path.Enqueue(new Point(right,origin.Y));path.Enqueue(origin);Call(pet,"PrepareArtifactTravel");
            Call(pet,"BeginDrag",pet,new MouseEventArgs(MouseButtons.Left,1,30,30,0));
            Check(!clock.Active&&path.Count==0&&(bool)Field(pet,"dragging"),"drag interrupts mounted travel and keeps original mouse interaction");Set(pet,"dragging",false);
            Set(pet,"wanderWorkingArea",area);path.Enqueue(new Point(right,origin.Y));path.Enqueue(origin);Call(pet,"PrepareArtifactTravel");Set(pet,"claudeBusy",true);
            Call(pet,"TickWander",16d);Check(!clock.Active&&path.Count==0,"busy CLI reaction cancels travel");Set(pet,"claudeBusy",false);
            Set(pet,"reduceMotion",true);path.Enqueue(new Point(right,origin.Y));path.Enqueue(origin);Call(pet,"PrepareArtifactTravel");Check(!clock.Active,"reduced motion never mounts");Call(pet,"CancelWander");
            pet.Close();
        }
    }
    private static void CheckBounds(PetForm pet,Rectangle area){if(!area.Contains(pet.Bounds))throw new Exception("route left the active monitor");}
    [STAThread]private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();string output=Environment.GetEnvironmentVariable("FUFU_STATE_DIR");Directory.CreateDirectory(output);
            Point start=new Point(-1400,60);Point[] closed={new Point(-200,60),start};
            Check(ArtifactTravelClock.RouteLength(start,closed)==2400,"length sums segments including return leg on a negative-coordinate monitor");
            Check(ArtifactTravelClock.RouteLength(start,new Point[0])==0,"empty route does not mount");
            Check(ArtifactTravelClock.Threshold(new Rectangle(0,0,320,240))==480,"minimum mount threshold");
            Check(ArtifactTravelClock.Threshold(new Rectangle(-3840,0,3840,2160))>ArtifactTravelClock.Threshold(new Rectangle(0,0,1920,1080)),"threshold follows the current display size");
            var clock=new ArtifactTravelClock();clock.Start();float previous=0;double speed=clock.Speed;
            for(int i=0;i<40;i++){clock.Advance(16);CheckCurve(clock.Envelope,previous,true);previous=clock.Envelope;if(clock.Speed<speed)throw new Exception("acceleration reversed");speed=clock.Speed;}
            Check(clock.Envelope==1&&clock.Speed==360,"smooth takeoff reaches cruising speed");
            Check(clock.MovementStep(1)>=.75&&clock.MovementStep(500)<=360*.034+.001,"movement cannot stall on tiny ticks or jump on delayed ticks");
            clock.BeginLanding();previous=clock.Envelope;
            for(int i=0;i<30;i++){clock.Advance(16);CheckCurve(clock.Envelope,previous,false);previous=clock.Envelope;}
            Check(!clock.Active&&clock.Envelope==0&&clock.Speed==0,"smooth landing ends and does not loop");
            clock.Start();clock.Advance(100);clock.BeginLanding();for(int i=0;i<6;i++)clock.Advance(100);Check(!clock.Active,"early interruption can land before full takeoff");
            clock.Start();clock.Reset();Check(!clock.Active&&!clock.Landing,"cancel clears flight state");
            Art(output);Integration();Console.WriteLine("Artifact travel checks passed; no model calls or user settings changed. Preview: "+Path.Combine(output,"artifact-travel.png"));return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static void CheckCurve(float now,float before,bool rising){if(Math.Abs(now-before)>.065||(rising?now<before:now>before))throw new Exception("discontinuous envelope");}
}
