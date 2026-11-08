// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace XianPal
{
    // Hall animation visualizes recorded execution states and handoffs.
    internal sealed class SectHallControl : Control
    {
        private readonly Func<CompanionSect> selected;
        private readonly Func<SectRun> record;
        private readonly Action<string> details;
        private readonly Timer clock = new Timer { Interval = 50 };
        private readonly System.Diagnostics.Stopwatch elapsed = System.Diagnostics.Stopwatch.StartNew();
        private readonly Dictionary<string, Rectangle> seats = new Dictionary<string, Rectangle>();
        private string inspectedMember, lastInspection;
        private int ticks;
        private Image background;
        private readonly Dictionary<string,Image> spriteCache=new Dictionary<string,Image>();
        private readonly Queue<string> spriteOrder=new Queue<string>();
        private readonly string contentRoot;
        internal bool HasBackgroundImage { get { return background!=null; } }
        internal string BackgroundSource { get; private set; }
        internal const string BackgroundResourceName="XianPal.SectHall.Background.png";
        internal int MemberCount {get{return Labels(selected(),record()).Count;}}
        internal SectHallControl(Func<CompanionSect> selected, Func<SectRun> record, Action<string> details)
        {
            this.selected = selected; this.record = record; this.details = details;
            string root=AppDomain.CurrentDomain.BaseDirectory;
            contentRoot=Directory.Exists(Path.Combine(root,"assets"))?root:Path.Combine(root,"payload");
            string scene=Path.Combine(contentRoot,"scenes","sect-hall-v2.png");
            LoadBackground(scene);
            DoubleBuffered = true; Dock = DockStyle.Fill; BackColor = Color.FromArgb(25, 33, 40);
            SetStyle(ControlStyles.ResizeRedraw,true);
            clock.Tick += delegate { if (Visible) { Invalidate(); if (++ticks % 6 == 0 && inspectedMember != null) InspectMember(inspectedMember); } };
            VisibleChanged += delegate { if (Visible) clock.Start(); else clock.Stop(); };
            MouseClick += Inspect;
        }
        private void LoadBackground(string path)
        {
            try
            {
                if(File.Exists(path))using(Image source=Image.FromFile(path))
                {if(source.Width<320||source.Height<200||source.Width>8192||source.Height>8192)throw new InvalidDataException("背景图尺寸无效。");background=new Bitmap(source);BackgroundSource="file";return;}
            }
            catch(Exception ex)
            {
                // GDI+ also reports an invalid image file as OutOfMemoryException.
                if(!(ex is IOException)&&!(ex is ArgumentException)&&!(ex is OutOfMemoryException)&&!(ex is System.Runtime.InteropServices.ExternalException))throw;
            }
            using(Stream resource=typeof(SectHallControl).Assembly.GetManifestResourceStream(BackgroundResourceName))
                if(resource!=null)using(Image source=Image.FromStream(resource)){background=new Bitmap(source);BackgroundSource="embedded";return;}
            BackgroundSource="missing";
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None; g.InterpolationMode = InterpolationMode.NearestNeighbor;
            if(Width<=0||Height<=0)return;
            if(background!=null)
            {
                // Cover the viewport without stretching the architecture.
                // Bottom anchoring keeps the floor behind the workstations.
                float fit=Math.Max(Width/(float)background.Width,Height/(float)background.Height);
                float sourceWidth=Width/fit,sourceHeight=Height/fit;
                g.DrawImage(background,ClientRectangle,new RectangleF((background.Width-sourceWidth)/2,background.Height-sourceHeight,sourceWidth,sourceHeight),GraphicsUnit.Pixel);
            }
            else g.Clear(Color.FromArgb(24,49,51));
            if(background==null)TextRenderer.DrawText(g,"大堂背景资源缺失，请安装完整新版以修复。",Font,new Point(18,145),Color.LightGoldenrodYellow);
            CompanionSect sect = selected(); SectRun run = record();
            string title = run != null ? run.Sect : sect != null ? sect.Name : "仙伴 · 门派大堂";
            using(var font=new Font("Microsoft YaHei UI",Math.Max(10,Math.Min(18,Math.Min(Width/35f,Height/22f))),FontStyle.Bold))
            {
                var banner=new Rectangle(Width/5,Math.Max(6,(int)(Height*.04)),Width*3/5,font.Height+10);
                using(var shade=new SolidBrush(Color.FromArgb(215,28,30,28)))g.FillRectangle(shade,banner);
                using(var edge=new Pen(Color.FromArgb(164,129,66)))g.DrawRectangle(edge,banner);
                TextRenderer.DrawText(g,title,font,banner,Color.FromArgb(246,215,140),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            }
            var labels=Labels(sect,run);UpdateSeats(labels);
            if(labels.Count==0) {TextRenderer.DrawText(g,"先在“门派 / 多 Agent”中新建门派并分配任务",Font,new Point(25,120),Color.Gainsboro);return;}
            // Show real upstream submissions only. A repeated packet animation depicts this logged connection,
            // not repeated API calls or token-level telemetry.
            if(run!=null && run.Transfers!=null)
            {
                int edge=0;
                foreach(SectTransfer transfer in run.Transfers.GetRange(Math.Max(0,run.Transfers.Count-24),Math.Min(24,run.Transfers.Count)))
                {
                    SectStepRun from=run.Steps.Find(delegate(SectStepRun s){return String.Equals(s.Id,transfer.From,StringComparison.OrdinalIgnoreCase);});
                    SectStepRun to=run.Steps.Find(delegate(SectStepRun s){return String.Equals(s.Id,transfer.To,StringComparison.OrdinalIgnoreCase);});
                    if(from==null||to==null||from.Member==to.Member||!seats.ContainsKey(from.Member)||!seats.ContainsKey(to.Member))continue;
                    Rectangle a=seats[from.Member],b=seats[to.Member]; Point p=new Point(a.Left+a.Width/2,a.Top+a.Height/2),q=new Point(b.Left+b.Width/2,b.Top+b.Height/2);
                    using(var pen=new Pen(Color.FromArgb(100,186,197),2)){g.DrawLine(pen,p,q);}
                    int ax=(int)(p.X+(q.X-p.X)*.75),ay=(int)(p.Y+(q.Y-p.Y)*.75);Fill(g,Color.FromArgb(100,186,197),ax-3,ay-3,6,6);
                    if(run.Status=="running" && to.Status=="running")
                    {double t=(elapsed.Elapsed.TotalSeconds*.45+edge*.23)%1;int x=(int)(p.X+(q.X-p.X)*t),y=(int)(p.Y+(q.Y-p.Y)*t);Fill(g,Color.FromArgb(250,213,115),x-4,y-4,8,8);}
                    edge++;
                }
            }
            foreach(var pair in seats)
            {
                Rectangle box=pair.Value;string status="待命";
                if(run!=null)
                {
                    var own=run.Steps.FindAll(delegate(SectStepRun s){return s.Member==pair.Key;});
                    status=own.Exists(delegate(SectStepRun s){return s.Status=="running";})?"running":own.Exists(delegate(SectStepRun s){return s.Status=="failed"||s.Status=="timeout";})?"failed":own.Exists(delegate(SectStepRun s){return s.Status=="pending";})?"pending":own.TrueForAll(delegate(SectStepRun s){return s.Status=="success";})?"success":run.Status;
                }
                Color color=status=="running"?Color.FromArgb(102,214,194):status=="success"?Color.FromArgb(149,209,121):status=="failed"?Color.FromArgb(235,133,120):Color.FromArgb(173,182,195);
                const int cx=110,feet=236,size=192;
                float unit=Math.Max(1,Font.Height/16f);
                float scale=Math.Max(.01f,Math.Min(1.5f*unit,Math.Min(box.Width/SectHallLayout.WorkstationWidth,box.Height/SectHallLayout.WorkstationHeight)));
                float originX=box.Left+(box.Width-SectHallLayout.WorkstationWidth*scale)/2,originY=box.Top+(box.Height-SectHallLayout.WorkstationHeight*scale)/2;
                string character="orbit";
                if(run!=null){var own=run.Steps.Find(delegate(SectStepRun s){return s.Member==pair.Key;});if(own!=null)character=own.CharacterId;}
                else if(sect!=null){int index=labels.IndexOf(pair.Key);character=String.IsNullOrEmpty(sect.Members[index].CharacterId)?PetCharacter.All[index%4].Id:sect.Members[index].CharacterId;}
                string pose=status=="running"?"review":status=="success"?"waving":status=="failed"?"failed":"idle";
                Image sprite=Sprite(character,pose,(int)(elapsed.Elapsed.TotalSeconds*(status=="running"?18:12)));
                GraphicsState transform=g.Save();g.TranslateTransform(originX,originY);g.ScaleTransform(scale,scale);
                using(var shadow=new SolidBrush(Color.FromArgb(85,7,24,26)))g.FillEllipse(shadow,cx-size/3,feet-12,size*2/3,18);
                if (sprite != null)
                {
                    Rectangle frame = PetForm.FitSpriteRectangle(sprite.Size, new Size(size, size));
                    frame.Offset(cx - size / 2, feet - size);
                    GraphicsState spriteState = g.Save();
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.DrawImage(sprite, frame);
                    g.Restore(spriteState);
                }
                DrawDesk(g,cx,feet,174,status=="running");
                g.Restore(transform);
                var nameBox=new Rectangle((int)(originX+(cx-85)*scale),(int)(originY+(feet-size-26)*scale),(int)(170*scale),Math.Max(Font.Height+2,(int)(23*scale)));
                var statusBox=new Rectangle((int)(originX+(cx-75)*scale),(int)(originY+(feet+3)*scale),(int)(150*scale),Math.Max(Font.Height+2,(int)(23*scale)));
                using(var badge=new SolidBrush(Color.FromArgb(205,16,38,40))){g.FillRectangle(badge,nameBox);g.FillRectangle(badge,statusBox);}
                const TextFormatFlags flags=TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix;
                TextRenderer.DrawText(g,pair.Key,Font,nameBox,Color.FromArgb(251,224,166),flags);
                TextRenderer.DrawText(g,UiLanguage.Text(StatusText(status)),Font,statusBox,color,flags);
            }
            TextRenderer.DrawText(g,UiLanguage.Text("点击成员查看任务、回复和交接数据 · 光点表示已记录的传递关系，不代表实时传输速度"),Font,new Rectangle(8,Height-24,Width-16,22),Color.Gainsboro,TextFormatFlags.EndEllipsis);
        }
        private static List<string> Labels(CompanionSect sect,SectRun run)
        {
            var labels=new List<string>();
            if(run!=null)foreach(SectStepRun step in run.Steps){if(!labels.Contains(step.Member))labels.Add(step.Member);}
            else if(sect!=null)foreach(SectMember member in sect.Members)labels.Add(member.Name+" ("+member.Id+")");
            return labels;
        }
        private void UpdateSeats(List<string> labels)
        {
            seats.Clear();Rectangle[] boxes=SectHallLayout.Seats(ClientSize,labels.Count,Math.Max(1,Font.Height/16f));
            for(int i=0;i<labels.Count;i++)seats[labels[i]]=boxes[i];
        }
        private void DrawDesk(Graphics g,int cx,int feet,int width,bool working)
        {
            int left=cx-width/2,top=feet-57;
            using(var shadow=new SolidBrush(Color.FromArgb(85,7,17,18)))g.FillEllipse(shadow,left-8,feet-12,width+16,18);
            // Layered rosewood desk: beveled top, apron, brass drawer pulls and carved legs.
            Fill(g,Color.FromArgb(47,26,24),left+9,top+15,10,44);Fill(g,Color.FromArgb(47,26,24),left+width-19,top+15,10,44);
            Fill(g,Color.FromArgb(139,89,48),left+10,top+18,3,36);Fill(g,Color.FromArgb(139,89,48),left+width-17,top+18,3,36);
            Fill(g,Color.FromArgb(72,38,31),left+5,top+12,width-10,26);
            Fill(g,Color.FromArgb(115,64,40),left+9,top+17,width-18,16);
            Fill(g,Color.FromArgb(49,29,26),cx-2,top+17,4,16);
            Fill(g,Color.FromArgb(214,166,83),cx-width/4-5,top+22,10,3);Fill(g,Color.FromArgb(214,166,83),cx+width/4-5,top+22,10,3);
            Fill(g,Color.FromArgb(60,33,28),left-4,top+6,width+8,10);
            Fill(g,Color.FromArgb(163,101,53),left-4,top,width+8,9);
            Fill(g,Color.FromArgb(221,166,89),left-4,top,width+8,2);
            Fill(g,Color.FromArgb(99,52,35),left,top+5,width,2);
            // Scroll with jade paperweight, brush and porcelain cup. No simulated task text.
            Fill(g,Color.FromArgb(127,91,52),cx-38,top-4,72,6);Fill(g,Color.FromArgb(242,219,166),cx-33,top-6,62,8);
            for(int x=cx-23;x<cx+18;x+=9)Fill(g,Color.FromArgb(151,127,91),x,top-3,5,1);
            Fill(g,Color.FromArgb(50,109,100),cx+18,top-7,14,3);
            Fill(g,Color.FromArgb(222,229,206),left+12,top-11,11,9);Fill(g,Color.FromArgb(84,115,115),left+14,top-10,7,2);
            Fill(g,Color.FromArgb(205,216,196),left+23,top-8,3,4);
            Fill(g,Color.FromArgb(30,43,45),left+width-26,top-9,12,8);
            Fill(g,Color.FromArgb(185,132,63),left+width-21,top-20,3,16);
            if(working)
            {
                int frame=(int)(elapsed.Elapsed.TotalSeconds*4)%3;
                Fill(g,Color.FromArgb(222,192,114),cx-8+frame*5,top-10,2,5);
            }
        }
        private Image Sprite(string id,string state,int tick)
        {
            if(String.IsNullOrEmpty(id))id="orbit";
            string root=Path.Combine(contentRoot,"assets"); if(id!="orbit")root=Path.Combine(root,"characters",id);
            int count = state == "review" || state == "waving" ? 24 : 8;
            string path = Path.Combine(root, state, (tick % count).ToString("00") + ".png");
            Image cached;
            if (spriteCache.TryGetValue(path, out cached)) return cached;
            if (!File.Exists(path)) return null;

            Image frame = PetForm.LoadDetachedImage(path);
            if (spriteCache.Count >= 96)
            {
                string oldest = spriteOrder.Dequeue();
                spriteCache[oldest].Dispose();
                spriteCache.Remove(oldest);
            }
            spriteCache.Add(path, frame);
            spriteOrder.Enqueue(path);
            return frame;
        }
        private void Inspect(object sender,MouseEventArgs e)
        {
            UpdateSeats(Labels(selected(),record()));
            SectRun run=record();if(run==null){details("尚未执行：请先在门派配置中保存任务，再确认执行。\n大堂中的成员当前处于待命状态。");return;}
            foreach(var pair in seats) if(pair.Value.Contains(e.Location))
            {
                inspectedMember=pair.Key;lastInspection=null;InspectMember(pair.Key);break;
            }
        }
        private void InspectMember(string member)
        {
                SectRun run=record();if(run==null)return;
                var text=new System.Text.StringBuilder(member+"\n");
                foreach(SectStepRun s in run.Steps) if(s.Member==member) text.AppendLine("\n任务 "+s.Id+" ["+StatusText(s.Status)+"]\n目录："+s.Directory+"\n指令：\n"+s.Prompt+"\n回复：\n"+s.Output);
                if(run.Transfers!=null) foreach(SectTransfer t in run.Transfers)
                {var a=run.Steps.Find(delegate(SectStepRun s){return s.Id==t.From;});var b=run.Steps.Find(delegate(SectStepRun s){return s.Id==t.To;});if((a!=null&&a.Member==member)||(b!=null&&b.Member==member))text.AppendLine("\n交接 "+t.From+" → "+t.To+"\n提交时间："+t.SentAt+"\n实际传递摘要：\n"+t.Text);}
                string value=text.ToString();if(value!=lastInspection){lastInspection=value;details(value);}
        }
        private static void Fill(Graphics g,Color c,int x,int y,int w,int h){using(var b=new SolidBrush(c))g.FillRectangle(b,x,y,w,h);}
        internal static string StatusText(string status)
        {switch(status){case "running":return "执行中";case "pending":return "等待依赖 / 工位";case "success":return "已完成";case "failed":return "失败";case "timeout":return "超时";case "cancelled":return "已取消";case "blocked":return "上游失败，已阻断";case "interrupted":return "上次运行中断";default:return status;}}
        protected override void Dispose(bool disposing){if(disposing){clock.Dispose();if(background!=null)background.Dispose();foreach(Image sprite in spriteCache.Values)sprite.Dispose();spriteCache.Clear();}base.Dispose(disposing);}
    }
}
