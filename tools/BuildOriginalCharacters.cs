// SPDX-License-Identifier: Apache-2.0
// Deterministic animation renderer for the approved, AI-assisted original art.
// Texture sources and reviewed regions are shipped in art/rigs. No network call at build time.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using XianPal;

internal static class BuildOriginalCharacters
{
    private static readonly string[] States={"idle","waiting","failed","running","running-left","running-right","waving","review","jumping","punching","eating","wiping-mouth"};
    private static readonly Dictionary<string,Rig> Rigs=new Dictionary<string,Rig>();
    private static readonly Dictionary<string,Rig> SideRigs=new Dictionary<string,Rig>();
    internal static readonly Color Ink=Color.FromArgb(47,58,65);
    private static int Main(string[] args)
    {
        try
        {
            if(args.Length!=3) throw new ArgumentException("Usage: BuildOriginalCharacters assets previews art/rigs");
            string root=Path.GetFullPath(args[0]), previews=Path.GetFullPath(args[1]);
            Directory.CreateDirectory(root); Directory.CreateDirectory(previews);
            var metadata=new JavaScriptSerializer().Deserialize<Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(args[2],"regions.json")));
            foreach(PetCharacter p in PetCharacter.All) Rigs.Add(p.Id,new Rig(Path.Combine(args[2],p.Id+".png"),metadata[p.Id]));
            string sideRoot=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[2])),"side-rigs");
            var sideMetadata=new JavaScriptSerializer().Deserialize<Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(sideRoot,"regions.json")));
            foreach(PetCharacter p in PetCharacter.All)SideRigs.Add(p.Id,new Rig(Path.Combine(sideRoot,p.Id+".png"),sideMetadata[p.Id]));
            int count=0;
            using(Bitmap gallery=new Bitmap(1360,570))
            using(Graphics grid=Graphics.FromImage(gallery))
            {
                grid.Clear(Color.FromArgb(248,245,236)); Quality(grid);
                var html=new StringBuilder("<!doctype html><meta charset='utf-8'><title>仙伴 · AI桌面伴侣 动作预览</title><style>body{font:16px 'Microsoft YaHei UI',sans-serif;background:#f8f5ec;color:#303c41;margin:24px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:16px}section{border:1px solid #dfd9c9;border-radius:20px;padding:14px;text-align:center;background:#fffdf8}img{width:288px;height:312px;object-fit:contain}button{padding:9px 12px;margin:4px;border:1px solid #d2c9b2;border-radius:8px;background:white}h1{font-size:24px}.hint{font-size:13px;color:#6c756f}</style><h1>仙伴 · 国风 AI桌面伴侣</h1><p>透明原画分层动画 · 对侧摆臂与脚步 · 24相位 · 本地离线预览</p><nav>");
                foreach(string state in States) html.Append("<button onclick=\"state='"+state+"';frame=0\">"+state+"</button>");
                html.Append("<button onclick=\"paused=!paused\">暂停 / 播放</button><button onclick=\"step()\">下一帧</button></nav><p id='counter'></p><main>");
                for(int id=0;id<PetCharacter.All.Length;id++)
                {
                    PetCharacter character=PetCharacter.All[id];
                    string characterRoot=id==0?root:Path.Combine(root,"characters",character.Id);
                    foreach(string state in States)
                    {
                        string directory=Path.Combine(characterRoot,state); Directory.CreateDirectory(directory);
                        int n=FrameCount(state);
                        for(int f=0;f<n;f++) using(Bitmap image=Frame(character.Id,state,f/(double)n))
                        { image.Save(Path.Combine(directory,f.ToString("00")+".png"),ImageFormat.Png); count++; }
                    }
                    GenerateExpressionPatches(characterRoot,character.Id,Rigs[character.Id]);
                    GenerateExpressionPatches(characterRoot,character.Id,SideRigs[character.Id]);
                    using(Bitmap portrait=Frame(character.Id,"idle",0))
                    {
                        grid.DrawImage(portrait,new Rectangle(id*340+7,0,326,354));
                        using(Font title=new Font("Microsoft YaHei UI",19,FontStyle.Bold))
                        using(Font subtitle=new Font("Microsoft YaHei UI",11))
                        using(Brush ink=new SolidBrush(Ink))
                        { grid.DrawString(character.Name,title,ink,id*340+110,374); grid.DrawString(new[]{"青衣游修","符箓手艺人","草木药师","墨黑云纹灵龟"}[id],subtitle,ink,id*340+100,420); }
                    }
                    string relative=id==0?"../payload/assets/":"../payload/assets/characters/"+character.Id+"/";
                    html.Append("<section><h2>"+character.Name+"</h2><canvas width='384' height='416' style='width:288px;height:312px' data-id='"+character.Id+"' data-root='"+relative+"'></canvas><p class='hint'>独立眨眼节奏 · 柔和屈肘</p></section>");
                }
                gallery.Save(Path.Combine(previews,"characters.png"),ImageFormat.Png);
                html.Append("</main><p><a href='performances.html'>查看四位角色专属全屏演绎 →</a></p><script src='walking-preview.js?v=foreground-arms-9'></script>");
                File.WriteAllText(Path.Combine(previews,"index.html"),html.ToString(),new UTF8Encoding(false));
                File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","tools","WalkingPreview.js"),Path.Combine(previews,"walking-preview.js"),true);
            }
            // Legacy resource names remain compatible; no current profile uses a punch/crack.
            string effects=Path.Combine(root,"reminder"); Directory.CreateDirectory(effects);
            foreach(string name in new[]{"punch-fist","impact-center"})
                using(Bitmap b=new Bitmap(256,256)) using(Graphics g=Graphics.FromImage(b))
                { g.Clear(Color.Transparent); Quality(g); using(Pen p=new Pen(Color.FromArgb(145,188,158,90),4)) {g.DrawEllipse(p,40,40,176,176);g.DrawEllipse(p,59,59,138,138);} b.Save(Path.Combine(effects,name+".png"),ImageFormat.Png); }
            Console.WriteLine("Generated "+PetCharacter.All.Length+" cultivation companions, "+count+" frames, 48 expression patches and 2 compatibility effects.");
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { foreach(Rig rig in Rigs.Values) rig.Dispose();foreach(Rig rig in SideRigs.Values)rig.Dispose(); }
    }
    private static int FrameCount(string state) { return state=="idle"||state=="waiting"||state=="failed"?8:24; }
    private static void Quality(Graphics g)
    { g.SmoothingMode=SmoothingMode.AntiAlias; g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.PixelOffsetMode=PixelOffsetMode.HighQuality; g.CompositingQuality=CompositingQuality.HighQuality; }
    private static Bitmap Frame(string id,string state,double phase)
    {
        using(Bitmap large=new Bitmap(768,832,PixelFormat.Format32bppPArgb))
        {
            using(Graphics g=Graphics.FromImage(large))
            {
                g.Clear(Color.Transparent); Quality(g); g.ScaleTransform(2,2);
                // Turtle's approved three-quarter silhouette looks left. Humans look right.
                bool mirror=id=="pippin"?state=="running-right":state=="running-left";
                if(mirror){g.TranslateTransform(384,0);g.ScaleTransform(-1,1);}
                bool walking=state.StartsWith("running",StringComparison.Ordinal);
                Rig rig=walking?SideRigs[id]:Rigs[id];
                if(id=="pippin") Turtle(g,rig,state,phase);
                else if(walking)ProfileHuman(g,rig,state,phase,id);
                else Human(g,rig,state,phase,id);
            }
            Bitmap result=new Bitmap(384,416,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(result)) { Quality(g);g.DrawImage(large,new Rectangle(0,0,384,416)); }
            return result;
        }
    }
    private static void ProfileHuman(Graphics g,Rig rig,string state,double phase,string id)
    {
        double t=phase*Math.PI*2;float bob=(float)Math.Cos(t*2)*.75f,sway=(float)Math.Sin(t)*.9f;
        // Keep grounded stance/swing and the knee solver; side-view hips are
        // projected beneath the robe and inherit its upper-body transform.
        for(int order=0;order<2;order++)
        {
            int i=id=="orbit"||id=="moss"?order:1-order; // Far trouser/boot stays behind the near leg.
            double u=(phase+(i==0?0:.5))%1;
            DrawWalkingLeg(g,rig,i,CompanionMotion.WalkingHipPoint(id,i,phase),Foot(CompanionMotion.WalkingFootCenter(id,i),u),u);
        }
        GraphicsState upper=g.Save();g.TranslateTransform(sway,bob);
        g.TranslateTransform(192,260);g.RotateTransform((float)Math.Sin(t)*.45f);g.TranslateTransform(-192,-260);
        RectangleF torso=ProfileTorsoBox(rig,id);DrawTexture(g,rig.Parts[1],torso);
        GraphicsState head=g.Save();CompanionMotion.ApplyHeadAttachment(g,id,state,phase);
        DrawAttachedHead(g,rig,id,0);g.Restore(head);
        // The head's old collar is locally excluded. The real body collar stays
        // behind the head/hair, so no second floating collar is painted over it.
        DrawWalkingForegroundArms(g,rig,id,state,phase);
        g.Restore(upper);
    }
    private static void DrawWalkingForegroundArms(Graphics g,Rig rig,string id,string state,double phase)
    {
        double t=phase*Math.PI*2;float arm=(float)ArmSwing(phase,true);
        DrawArm(g,rig.Parts[2],CompanionMotion.WalkingShoulderAnchor(id,true),arm,state,t,true,rig.ArmAttachments[0],124);
        DrawArm(g,rig.Parts[3],CompanionMotion.WalkingShoulderAnchor(id,false),-arm,state,t,false,rig.ArmAttachments[1],124);
    }
    private static RectangleF ProfileTorsoBox(Rig rig,string id)
    {
        // Preserve the new side drawing's aspect ratio; pin its opaque neck to
        // the unchanged head and keep the hem at the original walking height.
        PointF neck=CompanionMotion.WalkingNeckAnchor(id),source=CompanionMotion.WalkingTorsoNeck(id);
        float height=(368-neck.Y)/(1-source.Y),width=height*rig.Parts[1].Width/rig.Parts[1].Height;
        return new RectangleF(neck.X-source.X*width,neck.Y-source.Y*height,width,height);
    }
    private static void Human(Graphics g,Rig rig,string state,double phase,string id)
    {
        double t=phase*Math.PI*2; bool walk=state.StartsWith("running",StringComparison.Ordinal);
        float jump=state=="jumping"?(float)(-15*Math.Pow(Math.Sin(Math.PI*phase),2)):0;
        float bob=walk?(float)(Math.Cos(t*2)*.75):0;
        float sway=walk?(float)(Math.Sin(t)*.9):0;
        float breathing=!walk?(float)(Math.Sin(t)*.35):0;
        GraphicsState all=g.Save(); g.TranslateTransform(0,jump);
        // Stance soles stay on y=396. Body bob is compensated at the hip, not added to feet.
        for(int i=1;i>=0;i--)
        {
            double u=(phase+(i==0?0:.5))%1;
            float hipX=192+(i==0?-1:1)*(walk?14:20)+sway, hipY=(walk?319:307)+bob;
            PointF foot=walk?Foot(192+(i==0?-14:14),u):new PointF(192+(i==0?-20:20),396);
            DrawLeg(g,walk?rig.WalkingLegs[i]:rig.Parts[4+i],new PointF(hipX,hipY),foot,39,walk);
        }
        GraphicsState upper=g.Save();g.TranslateTransform(sway,bob+breathing);
        g.TranslateTransform(192,260);g.RotateTransform(walk?(float)(Math.Sin(t)*.45):state=="failed"?2:0);g.TranslateTransform(-192,-260);
        GraphicsState clothing=g.Save();if(walk)CompanionMotion.ApplyWalkingBody(g);
        DrawTexture(g,rig.Parts[1],new RectangleF(86,191,212,177));
        g.Restore(clothing);
        int head=state=="eating"&&Math.Sin(t)>-.25?6:((state=="idle"||state=="waiting")&&phase>=.75&&phase<.875?7:0);
        GraphicsState face=g.Save();
        CompanionMotion.ApplyHeadAttachment(g,id,state,phase);
        DrawAttachedHead(g,rig,id,head);g.Restore(face);
        DrawCollarLip(g,rig.Parts[1],id,walk);
        DrawHumanForegroundArms(g,rig,id,state,phase);
        g.Restore(upper);g.Restore(all);
    }
    private static void DrawHumanForegroundArms(Graphics g,Rig rig,string id,string state,double phase)
    {
        // Explicit user-selected layer policy: both arms are always above the
        // body, collar, face and hair. Never repaint a head/body mask over them.
        double t=phase*Math.PI*2;string armState=state=="failed"&&id=="orbit"?"failed-outward":state;
        DrawArm(g,rig.Parts[2],CompanionMotion.ShoulderAnchor(id,true),0,armState,t,true,rig.ArmAttachments[0]);
        if(state=="waving"||state=="wiping-mouth")DrawHumanGesture(g,rig,id,state,phase);
        else DrawArm(g,rig.Parts[3],CompanionMotion.ShoulderAnchor(id,false),0,armState,t,false,rig.ArmAttachments[1]);
    }
    internal static PointF Foot(float center,double u)
    {
        // 60% loaded stance, 40% smooth swing. Contact travels backwards as the window advances.
        if(u<.6) return new PointF(center+46-(float)(92*u/.6),396);
        double s=(u-.6)/.4;
        return new PointF(center-46+(float)(92*s*s*(3-2*s)),396-(float)(10*Math.Sin(Math.PI*s)));
    }
    internal static double ArmSwing(double phase,bool left)
    { return (left?1:-1)*18*Math.Cos(phase*Math.PI*2-.16); }
    private static PointF BootPoint(BootLayout boot,PointF source,PointF sole,double phase)
    {return CompanionMotion.WalkingBootPoint(boot.Bounds,boot.Cuff,source,sole,phase);}
    private static void DrawWalkingLeg(Graphics g,Rig rig,int index,PointF hip,PointF sole,double phase)
    {
        DrawWalkingPants(g,rig,index,hip,sole,phase);DrawWalkingBoot(g,rig,index,sole,phase);
    }
    private static void DrawWalkingPants(Graphics g,Rig rig,int index,PointF hip,PointF sole,double phase)
    {
        Bitmap texture=rig.WalkingLegs[index];BootLayout boot=rig.Boots[index];
        PointF cuff=BootPoint(boot,boot.Cuff,sole,phase);
        float dx=cuff.X-hip.X,dy=cuff.Y-hip.Y,d=(float)Math.Sqrt(dx*dx+dy*dy);
        float bend=(float)Math.Sqrt(Math.Max(0,34*34-d*d/4));
        PointF knee=new PointF((hip.X+cuff.X)/2+dy/Math.Max(1,d)*bend,(hip.Y+cuff.Y)/2-dx/Math.Max(1,d)*bend);
        int end=(int)Math.Round(boot.Cuff.Y),cut=(int)(end*.62);
        float scale=36f/boot.Bounds.Height;
        DrawLegLink(g,texture,0,cut,new PointF(hip.X,hip.Y),new PointF(knee.X,knee.Y+2),scale);
        DrawLegLink(g,texture,cut,end,new PointF(knee.X,knee.Y-2),new PointF(cuff.X,cuff.Y+2),scale);
    }
    private static void DrawWalkingBoot(Graphics g,Rig rig,int index,PointF sole,double phase)
    {
        Bitmap texture=rig.WalkingLegs[index];BootLayout boot=rig.Boots[index];
        Rectangle b=boot.Bounds;
        PointF[] corners={BootPoint(boot,new PointF(b.Left,b.Top),sole,phase),BootPoint(boot,new PointF(b.Right,b.Top),sole,phase),BootPoint(boot,new PointF(b.Left,b.Bottom),sole,phase)};
        g.DrawImage(texture,corners,b,GraphicsUnit.Pixel);
    }
    private static float RowCenter(Bitmap texture,int row)
    {
        double sum=0,xSum=0;
        for(int y=Math.Max(0,row-2);y<=Math.Min(texture.Height-1,row+2);y++)for(int x=0;x<texture.Width;x++)
        {int a=texture.GetPixel(x,y).A;if(a<128)continue;sum+=a;xSum+=(x+.5)*a;}
        return sum>0?(float)(xSum/sum):texture.Width/2f;
    }
    private static void DrawLegLink(Graphics g,Bitmap image,int top,int bottom,PointF from,PointF to,float scale)
    {
        float dx=to.X-from.X,dy=to.Y-from.Y,length=(float)Math.Sqrt(dx*dx+dy*dy);
        PointF normal=new PointF(dy/Math.Max(1,length)*scale,-dx/Math.Max(1,length)*scale);
        float start=RowCenter(image,top),end=RowCenter(image,bottom);
        PointF a=new PointF(from.X-normal.X*start,from.Y-normal.Y*start);
        PointF b=new PointF(a.X+normal.X*image.Width,a.Y+normal.Y*image.Width);
        PointF c=new PointF(a.X+dx-normal.X*(end-start),a.Y+dy-normal.Y*(end-start));
        g.DrawImage(image,new[]{a,b,c},new Rectangle(0,top,image.Width,bottom-top),GraphicsUnit.Pixel);
    }
    private static void DrawLeg(Graphics g,Bitmap texture,PointF hip,PointF sole,float width,bool walking)
    {
        float dx=sole.X-hip.X,dy=sole.Y-hip.Y,d=(float)Math.Sqrt(dx*dx+dy*dy),length=walking?48:44.5f;
        float bend=(float)Math.Sqrt(Math.Max(0,length*length-d*d/4));
        PointF knee=new PointF((hip.X+sole.X)/2+dy/Math.Max(1,d)*bend,(hip.Y+sole.Y)/2-dx/Math.Max(1,d)*bend);
        // The trousers/boot texture is split at a hidden knee, not drawn as a visible hinge.
        int cut=(int)(texture.Height*.51);
        Strip(g,texture,new Rectangle(0,0,texture.Width,cut+3),hip,new PointF(knee.X,knee.Y+2),width);
        if(!walking)Strip(g,texture,new Rectangle(0,cut,texture.Width,texture.Height-cut),new PointF(knee.X,knee.Y-2),sole,width);
        else
        {
            int boot=(int)(texture.Height*.80);
            PointF ankle=new PointF(sole.X,sole.Y-15);
            Strip(g,texture,new Rectangle(0,cut,texture.Width,boot-cut+2),new PointF(knee.X,knee.Y-2),ankle,width);
            g.DrawImage(texture,CompanionMotion.WalkingBootCorners(sole),new Rectangle(0,boot,texture.Width,texture.Height-boot),GraphicsUnit.Pixel);
        }
    }
    internal static double BootToeOffset(Bitmap image)
    {
        double heel=0,toe=0,heelWeight=0,toeWeight=0;
        for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)
        {
            int alpha=image.GetPixel(x,y).A;if(alpha<200)continue;
            if(y>=image.Height*.58&&y<image.Height*.75){heel+=x*alpha;heelWeight+=alpha;}
            if(y>=image.Height*.85&&y<image.Height*.98){toe+=x*alpha;toeWeight+=alpha;}
        }
        if(heelWeight<=0||toeWeight<=0)throw new InvalidDataException("Boot has no heel/toe coverage.");
        return toe/toeWeight-heel/heelWeight;
    }
    private static double ClawToeOffset(Bitmap image)
    {
        double root=0,toe=0,rootWeight=0,toeWeight=0;
        for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)
        {
            int alpha=image.GetPixel(x,y).A;if(alpha<200)continue;
            if(y<image.Height*.20){root+=x*alpha;rootWeight+=alpha;}
            if(y>=image.Height*.80){toe+=x*alpha;toeWeight+=alpha;}
        }
        if(rootWeight<=0||toeWeight<=0)throw new InvalidDataException("Claw has no root/toe coverage.");
        return toe/toeWeight-root/rootWeight;
    }
    private static void DrawCollarLip(Graphics g,Bitmap torso,string id,bool walking)
    {
        PointF root=CompanionMotion.NeckAnchor(id);GraphicsState saved=g.Save();if(walking)CompanionMotion.ApplyWalkingBody(g);
        using(GraphicsPath clip=new GraphicsPath())
        {
            clip.AddPolygon(new[]{new PointF(root.X-16,root.Y+3),new PointF(root.X+16,root.Y+3),new PointF(root.X+25,root.Y+24),new PointF(root.X-25,root.Y+24)});
            g.SetClip(clip,CombineMode.Intersect);DrawTexture(g,torso,new RectangleF(86,191,212,177));
        }
        g.Restore(saved);
    }
    private static void Strip(Graphics g,Bitmap image,Rectangle source,PointF from,PointF to,float width,float overlap=0)
    {
        float dx=to.X-from.X,dy=to.Y-from.Y,len=(float)Math.Sqrt(dx*dx+dy*dy);
        float nx=dy/Math.Max(1,len)*width/2,ny=-dx/Math.Max(1,len)*width/2;
        // Adjacent curved texture bands share coverage at subpixel edges. Without
        // this tiny overlap, independent antialiasing leaves transparent stripes.
        if(overlap>0){float ox=dx/Math.Max(1,len)*overlap,oy=dy/Math.Max(1,len)*overlap;from=new PointF(from.X-ox,from.Y-oy);to=new PointF(to.X+ox,to.Y+oy);}
        PointF[] corners={new PointF(from.X-nx,from.Y-ny),new PointF(from.X+nx,from.Y+ny),new PointF(to.X-nx,to.Y-ny)};
        g.DrawImage(image,corners,source,GraphicsUnit.Pixel);
    }
    private static void DrawArm(Graphics g,Bitmap image,PointF shoulder,float angle,string state,double t,bool left,PointF attachment,float baseHeight=146)
    {
        float height=baseHeight;
        if(state=="waving"&&!left) {angle=-120+(float)Math.Sin(t)*12;height=108;}
        if(state=="review") {angle=left?-43:43;height=108;}
        if(state=="punching"&&!left) {angle=-76+(float)Math.Sin(t)*16;height=108;}
        if(state=="eating") {angle=left?-106:106;height=81+(float)Math.Sin(t)*2;}
        if(state=="wiping-mouth"&&!left) {angle=102+(float)Math.Sin(t)*12;height=80;}
        if(state=="failed"||state=="failed-outward") angle=left?6:-6;
        float width=image.Width/(float)image.Height*height;
        const int segments=24;
        PointF[] path=CompanionMotion.ArmPath(angle,CompanionMotion.ElbowFlex(t/(Math.PI*2),left,state),height,left,state,t/(Math.PI*2),segments);
        // Source sheets have asymmetric shoulder caps (flowers, ribbons, etc.).
        // Align the opaque proximal cap, not the transparent rectangle midpoint.
        float rootX=(attachment.X-.5f)*width,rootY=-6+attachment.Y*height;
        for(int i=0;i<path.Length;i++)path[i]=new PointF(path[i].X-rootX,path[i].Y-rootY);
        GraphicsState saved=g.Save();g.TranslateTransform(shoulder.X,shoulder.Y);
        DrawSleeveMesh(g,image,path,width,height);
        g.Restore(saved);
    }
    private static void DrawSleeveMesh(Graphics g,Bitmap image,PointF[] path,float width,float height)
    {DrawLimbMesh(g,image,path,width,height,false);}
    private static void DrawHumanGesture(Graphics g,Rig rig,string id,string state,double phase)
    {
        Bitmap image=rig.Parts[3];float height=126,width=height*image.Width/image.Height;
        PointF[] path=CompanionMotion.GesturePath(id,state,phase,rig.ArmAttachments[1],rig.ArmGrips[1],width,height,48);
        DrawLimbMesh(g,image,path,width,height,true);
    }
    private static void DrawLimbMesh(Graphics g,Bitmap image,PointF[] path,float width,float height,bool varyingLength)
    {
        int segments=path.Length-1;PointF[] left=new PointF[path.Length],right=new PointF[path.Length];
        for(int i=0;i<path.Length;i++)
        {
            PointF a=path[Math.Max(0,i-1)],b=path[Math.Min(segments,i+1)];
            float dx=b.X-a.X,dy=b.Y-a.Y,length=(float)Math.Sqrt(dx*dx+dy*dy);
            float nx=dy/length*width/2,ny=-dx/length*width/2;
            left[i]=new PointF(path[i].X-nx,path[i].Y-ny);right[i]=new PointF(path[i].X+nx,path[i].Y+ny);
        }
        for(int i=0;i<segments;i++)
        {
            // Adjacent bands use the exact same mesh edge. Draw the full source
            // through each quad: source-rectangle antialiasing cannot cut stripes
            // into flowers, ribbons or semi-transparent cloth at band boundaries.
            float dx=path[i+1].X-path[i].X,dy=path[i+1].Y-path[i].Y;
            float length=(float)Math.Sqrt(dx*dx+dy*dy),tx=dx/length,ty=dy/length,nx=ty*width/2,ny=-tx*width/2;
            float longitudinal=varyingLength?length*segments/height:1;
            PointF origin=new PointF(path[i].X-tx*height*i/segments*longitudinal,path[i].Y-ty*height*i/segments*longitudinal);
            GraphicsState saved=g.Save();
            using(GraphicsPath clip=new GraphicsPath())
            {
                clip.AddPolygon(new[]{left[i],right[i],right[i+1],left[i+1]});g.SetClip(clip,CombineMode.Intersect);
                g.DrawImage(image,new[]{new PointF(origin.X-nx,origin.Y-ny),new PointF(origin.X+nx,origin.Y+ny),new PointF(origin.X-nx+tx*height*longitudinal,origin.Y-ny+ty*height*longitudinal)},new Rectangle(0,0,image.Width,image.Height),GraphicsUnit.Pixel);
            }
            g.Restore(saved);
        }
    }
    private static void Turtle(Graphics g,Rig rig,string state,double phase)
    {
        double t=phase*Math.PI*2; bool walk=state.StartsWith("running",StringComparison.Ordinal);
        float jump=state=="jumping"?(float)(-9*Math.Pow(Math.Sin(Math.PI*phase),2)):0;
        GraphicsState all=g.Save();g.TranslateTransform(0,jump);
        DrawTurtleFoot(g,rig,5,phase,walk,false,"idle");
        GraphicsState tail=g.Save();g.TranslateTransform(308,298);g.RotateTransform((float)Math.Sin(t)*4);
        DrawTexture(g,rig.Parts[8],new RectangleF(0,0,43,35));g.Restore(tail);
        DrawTexture(g,rig.Parts[1],new RectangleF(110,170+(walk?(float)Math.Cos(t*2)*.6f:0),226,178));
        DrawTurtleFoot(g,rig,4,phase+.5,walk,true,"idle");
        DrawTurtleSocketLip(g,rig,4,phase,walk);
        int head=state=="eating"&&Math.Sin(t)>-.3?6:((state=="idle"||state=="waiting")&&phase>=.75&&phase<.875?7:0);
        GraphicsState face=g.Save();
        CompanionMotion.ApplyHeadAttachment(g,"pippin",state,phase);
        DrawAttachedHead(g,rig,"pippin",head);g.Restore(face);
        DrawTurtleNeckLip(g,rig,phase,walk);
        DrawTurtleForegroundArms(g,rig,state,phase);
        g.Restore(all);
    }
    private static void DrawTurtleForegroundArms(Graphics g,Rig rig,string state,double phase)
    {
        bool walk=state.StartsWith("running",StringComparison.Ordinal);
        DrawTurtleFoot(g,rig,3,phase+.5,walk,false,"idle");
        if(state=="waving"||state=="wiping-mouth"||state=="punching")DrawTurtleGesture(g,rig,state,phase);
        else DrawTurtleFoot(g,rig,2,phase,walk,true,state);
    }
    private static void DrawTurtleGesture(Graphics g,Rig rig,string state,double phase)
    {
        Bitmap image=rig.Parts[2];PointF size=TurtleFootSize(rig,2,true);
        PointF[] path=CompanionMotion.GesturePath("pippin",state,phase,rig.ClawAttachments[0],rig.ClawGrip,size.X,size.Y,48);
        DrawLimbMesh(g,image,path,size.X,size.Y,true);
    }
    private static void DrawTurtleNeckLip(Graphics g,Rig rig,double phase,bool walking)
    {
        RectangleF box=new RectangleF(110,170+(walking?(float)Math.Cos(phase*Math.PI*4)*.6f:0),226,178);
        // Feather the occlusion into the real shell rim. A hard rectangular or
        // polygonal clip used to leave a visible notch across the grey neck.
        for(int y=290;y<354;y+=2)
        {
            GraphicsState saved=g.Save();g.SetClip(new RectangleF(110,y,100,2),CombineMode.Intersect);
            using(ImageAttributes attributes=new ImageAttributes())
            {
                ColorMatrix alpha=new ColorMatrix();alpha.Matrix33=(float)CompanionMotion.Smooth((y+1-290)/12.0);attributes.SetColorMatrix(alpha);
                g.DrawImage(rig.Parts[1],new[]{new PointF(box.Left,box.Top),new PointF(box.Right,box.Top),new PointF(box.Left,box.Bottom)},new Rectangle(0,0,rig.Parts[1].Width,rig.Parts[1].Height),GraphicsUnit.Pixel,attributes);
            }
            g.Restore(saved);
        }
    }
    private static void DrawTurtleSocketLip(Graphics g,Rig rig,int part,double phase,bool walking)
    {
        PointF root=CompanionMotion.TurtleLegSocket(part,phase,walking);GraphicsState saved=g.Save();
        using(GraphicsPath clip=new GraphicsPath())
        {
            // Repaint the actual opaque belly silhouette over the proximal leg,
            // not just its tiny cap. The limb must emerge below the shell rim.
            clip.AddEllipse(root.X-28,root.Y-10,56,42);g.SetClip(clip,CombineMode.Intersect);
            DrawTexture(g,rig.Parts[1],new RectangleF(110,170+(walking?(float)Math.Cos(phase*Math.PI*4)*.6f:0),226,178));
        }
        g.Restore(saved);
    }
    private static void DrawTurtleFoot(Graphics g,Rig rig,int part,double phase,bool walking,bool near,string state)
    {
        Bitmap texture=rig.Parts[part];PointF size=TurtleFootSize(rig,part,near);float width=size.X,height=size.Y;
        bool gesture=near&&(state=="waving"||state=="wiping-mouth"||state=="punching");
        PointF attachment=rig.ClawAttachments[part-2],root=CompanionMotion.TurtleLegSocket(part,phase,walking);
        float reach=(near?380:366)-CompanionMotion.TurtleLegSocket(part,0,false).Y-(rig.ClawSoles[part-2]-attachment.Y)*height;
        PointF[] path=CompanionMotion.TurtleLegPathWithReach(phase,walking,gesture,height,reach,24);
        for(int i=0;i<path.Length;i++)path[i]=new PointF(path[i].X-(attachment.X-.5f)*width,path[i].Y-attachment.Y*height);
        GraphicsState saved=g.Save();g.TranslateTransform(root.X,root.Y);
        DrawLimbMesh(g,texture,path,width,height,true);g.Restore(saved);
    }
    private static PointF TurtleFootSize(Rig rig,int part,bool near)
    {
        float width=near?52:44;return new PointF(width,width*rig.Parts[part].Height/rig.Parts[part].Width);
    }
    private static void DrawTexture(Graphics g,Image image,RectangleF box)
    { g.DrawImage(image,box,new RectangleF(0,0,image.Width,image.Height),GraphicsUnit.Pixel); }
    private static PointF[] FaceEyes(string id,int part)
    {
        if(id=="orbit") return part==7?new[]{new PointF(.54f,.66f),new PointF(.735f,.74f)}:part==6?new[]{new PointF(.55f,.66f),new PointF(.765f,.74f)}:new[]{new PointF(.515f,.65f),new PointF(.725f,.722f)};
        if(id=="spark") return part==7?new[]{new PointF(.36f,.68f),new PointF(.615f,.59f)}:part==6?new[]{new PointF(.36f,.675f),new PointF(.615f,.565f)}:new[]{new PointF(.36f,.67f),new PointF(.60f,.58f)};
        if(id=="moss") return part==7?new[]{new PointF(.29f,.77f),new PointF(.515f,.68f)}:part==6?new[]{new PointF(.285f,.765f),new PointF(.515f,.68f)}:new[]{new PointF(.282f,.763f),new PointF(.491f,.679f)};
        return part==7?new[]{new PointF(.26f,.32f),new PointF(.69f,.46f)}:part==6?new[]{new PointF(.24f,.31f),new PointF(.69f,.47f)}:new[]{new PointF(.225f,.311f),new PointF(.68f,.46f)};
    }
    private static PointF ProfileEye(string id,int part)
    {
        if(id=="orbit")return part==7?new PointF(.785f,.635f):part==6?new PointF(.759f,.628f):new PointF(.751f,.623f);
        if(id=="spark")return part==7?new PointF(.785f,.531f):part==6?new PointF(.800f,.524f):new PointF(.794f,.531f);
        if(id=="moss")return part==7?new PointF(.792f,.630f):part==6?new PointF(.813f,.642f):new PointF(.815f,.668f);
        return part==7?new PointF(.281f,.353f):part==6?new PointF(.261f,.350f):new PointF(.260f,.328f);
    }
    private static RectangleF HeadBox(string id,Rig rig)
    {return rig.Profile?CompanionMotion.WalkingHeadBox(id):id=="pippin"?new RectangleF(43,211,143,148):new RectangleF(83,26,218,201);}
    private static PointF InFace(RectangleF box,PointF point)
    {return new PointF(box.X+point.X*box.Width,box.Y+point.Y*box.Height);}
    private static Bitmap FaceCanvas(string id,Rig rig,int part)
    {
        RectangleF box=HeadBox(id,rig);
        Bitmap canvas=new Bitmap(384,416,PixelFormat.Format32bppArgb);
        using(Graphics g=Graphics.FromImage(canvas))
        {
            Quality(g);g.Clear(Color.Transparent);
            if(part!=0&&rig.Profile)
            {
                PointF target=InFace(box,ProfileEye(id,0)),source=InFace(box,ProfileEye(id,part));
                // The alternate side head has a shorter eye-to-mouth distance.
                // Only its local mouth patch is used: pin the upper lip to the
                // neutral smile, not to an unrelated eye on the alternate head.
                if(id=="orbit"&&part==6)
                {
                    target=InFace(box,new PointF(.813f,.723f));
                    source=InFace(box,new PointF(.831f,.675f));
                }
                g.TranslateTransform(target.X-source.X,target.Y-source.Y);
            }
            else if(part!=0)
            {
                PointF[] target=FaceEyes(id,0),source=FaceEyes(id,part);
                PointF t0=InFace(box,target[0]),t1=InFace(box,target[1]),s0=InFace(box,source[0]),s1=InFace(box,source[1]);
                float sx=s1.X-s0.X,sy=s1.Y-s0.Y,tx=t1.X-t0.X,ty=t1.Y-t0.Y,den=sx*sx+sy*sy;
                float a=(tx*sx+ty*sy)/den,b=(ty*sx-tx*sy)/den;
                using(Matrix matrix=new Matrix(a,b,-b,a,t0.X-a*s0.X+b*s0.Y,t0.Y-b*s0.X-a*s0.Y))g.Transform=matrix;
            }
            DrawTexture(g,rig.Parts[part],box);
        }
        return canvas;
    }
    private static double OvalMask(float x,float y,PointF center,float radiusX,float radiusY)
    {
        double dx=(x-center.X)/radiusX,dy=(y-center.Y)/radiusY;
        double distance=Math.Sqrt(dx*dx+dy*dy);
        return 1-CompanionMotion.Smooth((distance-.72)/.28);
    }
    private static void GenerateExpressionPatches(string root,string id,Rig rig)
    {
        string directory=Path.Combine(root,rig.Profile?"profile-expressions":"expressions");Directory.CreateDirectory(directory);
        CreateExpressionPatches(id,rig);
        for(int i=0;i<6;i++)rig.HeadPatches[i].Save(Path.Combine(directory,(i+1).ToString("00")+".png"),ImageFormat.Png);
    }
    private static void DrawAttachedHead(Graphics g,Rig rig,string id,int expression)
    {
        GraphicsState saved=g.Save();
        if(rig.Profile&&id!="pippin")using(GraphicsPath oldCollar=new GraphicsPath())
        {oldCollar.AddPolygon(CompanionMotion.WalkingOldCollar(id));g.SetClip(oldCollar,CombineMode.Exclude);}
        if(id=="pippin")DrawTurtleHead(g,rig);else DrawTexture(g,rig.Parts[0],HeadBox(id,rig));
        if(expression!=0)
        {
            CreateExpressionPatches(id,rig);
            g.DrawImage(rig.HeadPatches[expression==7?2:5],0,0,384,416);
        }
        g.Restore(saved);
    }
    private static void DrawTurtleHead(Graphics g,Rig rig)
    {
        if(rig.TurtleHead==null)rig.TurtleHead=CreateTurtleHead(rig);
        // The atlas remains unchanged. A cached, supersampled inverse warp
        // avoids seams where a short neck would collapse dozens of mesh bands.
        g.DrawImage(rig.TurtleHead,new RectangleF(0,0,384,416),new RectangleF(0,0,768,832),GraphicsUnit.Pixel);
    }
    private static Bitmap CreateTurtleHead(Rig rig)
    {
        RectangleF box=HeadBox("pippin",rig);const int width=768,height=832;
        float start=rig.Profile?296:322,rootX=rig.Profile?151:144;
        byte[] source=new byte[width*height*4],result;
        using(Bitmap canvas=new Bitmap(width,height,PixelFormat.Format32bppArgb))
        {
            using(Graphics paint=Graphics.FromImage(canvas)){Quality(paint);paint.ScaleTransform(2,2);DrawTexture(paint,rig.Parts[0],box);}
            BitmapData data=canvas.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{Marshal.Copy(data.Scan0,source,0,source.Length);}finally{canvas.UnlockBits(data);}
        }
        result=(byte[])source.Clone();Array.Clear(result,(int)(start*2)*width*4,(height-(int)(start*2))*width*4);
        double maxDepth=box.Bottom-start;
        for(int y=(int)(start*2);y<Math.Min(height,(int)((start+26)*2));y++)for(int x=Math.Max(0,(int)(box.Left*2));x<Math.Min(width,(int)Math.Ceiling(box.Right*2));x++)
        {
            double px=(x+.5)/2,py=(y+.5)/2;
            if(py>=start+20*(1-Math.Exp(-maxDepth/20))+(px-rootX)*.13*(1-Math.Exp(-maxDepth/12)))continue;
            double lo=0,hi=maxDepth;
            for(int i=0;i<22;i++)
            {
                double mid=(lo+hi)/2,my=start+20*(1-Math.Exp(-mid/20))+(px-rootX)*.13*(1-Math.Exp(-mid/12));
                if(my<py)lo=mid;else hi=mid;
            }
            double depth=(lo+hi)/2,sx=(rootX+(px-rootX)/(1-.28*(1-Math.Exp(-depth/15))))*2-.5,sy=(start+depth)*2-.5;
            int ix=(int)Math.Floor(sx),iy=(int)Math.Floor(sy);if(ix<0||iy<0||ix+1>=width||iy+1>=height)continue;
            double fx=sx-ix,fy=sy-iy,alpha=0,blue=0,green=0,red=0;
            for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)
            {
                int p=((iy+dy)*width+ix+dx)*4;double w=(dx==0?1-fx:fx)*(dy==0?1-fy:fy)*source[p+3];
                alpha+=w;blue+=source[p]*w;green+=source[p+1]*w;red+=source[p+2]*w;
            }
            int target=(y*width+x)*4;if(alpha<1)continue;
            result[target]=(byte)Math.Round(blue/alpha);result[target+1]=(byte)Math.Round(green/alpha);result[target+2]=(byte)Math.Round(red/alpha);result[target+3]=(byte)Math.Round(alpha);
        }
        Bitmap output=new Bitmap(width,height,PixelFormat.Format32bppArgb);BitmapData pixels=output.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
        try{Marshal.Copy(result,0,pixels.Scan0,result.Length);}finally{output.UnlockBits(pixels);}return output;
    }
    private static void CreateExpressionPatches(string id,Rig rig)
    {
        if(rig.HeadPatches[0]!=null)return;
        RectangleF box=HeadBox(id,rig);
        PointF[] eyes=rig.Profile?new[]{ProfileEye(id,0),ProfileEye(id,0)}:FaceEyes(id,0);
        PointF mouth=id=="orbit"?new PointF(.625f,.79f):id=="spark"?new PointF(.52f,.74f):id=="moss"?new PointF(.40f,.818f):new PointF(.40f,.58f);
        if(rig.Profile)mouth=id=="pippin"?new PointF(.105f,.49f):id=="spark"?new PointF(.86f,.64f):id=="moss"?new PointF(.86f,.755f):new PointF(.813f,.748f);
        float mouthX=id=="pippin"?.22f:rig.Profile?.12f:.17f,mouthY=id=="pippin"?.14f:rig.Profile?.08f:.11f;
        if(rig.Profile&&id=="orbit"){mouthX=.085f;mouthY=.055f;} // Keep the nose and chin outside the mouth replacement.
        using(Bitmap neutral=FaceCanvas(id,rig,0))
        using(Bitmap blink=FaceCanvas(id,rig,7))
        using(Bitmap smile=FaceCanvas(id,rig,6))
            for(int component=0;component<2;component++)for(int level=1;level<=3;level++)
            {
                Bitmap patch=new Bitmap(384,416,PixelFormat.Format32bppArgb);
                {
                    Bitmap target=component==0?blink:smile;
                    for(int y=(int)box.Top;y<(int)box.Bottom;y++)for(int x=(int)box.Left;x<(int)box.Right;x++)
                    {
                        float nx=(x-box.X)/box.Width,ny=(y-box.Y)/box.Height;
                        float eyeX=id=="pippin"?.15f:rig.Profile?.09f:.13f,eyeY=id=="pippin"?.15f:rig.Profile?.10f:.12f;
                        double mask=component==0?
                            Math.Max(OvalMask(nx,ny,eyes[0],eyeX,eyeY),OvalMask(nx,ny,eyes[1],eyeX,eyeY)):
                            OvalMask(nx,ny,mouth,mouthX,mouthY);
                        if(mask<=0)continue;
                        Color from=neutral.GetPixel(x,y),to=target.GetPixel(x,y);if(from.A<240||to.A<240)continue;
                        double weight=level/3.0;
                        patch.SetPixel(x,y,Color.FromArgb((int)Math.Round(mask*255),(int)Math.Round(from.R+(to.R-from.R)*weight),(int)Math.Round(from.G+(to.G-from.G)*weight),(int)Math.Round(from.B+(to.B-from.B)*weight)));
                    }
                    rig.HeadPatches[component*3+level-1]=patch;
                }
            }
    }
    private static PointF OpaqueRoot(Bitmap image)
    {
        double sum=0,xSum=0,ySum=0;
        for(int y=0;y<Math.Max(1,(int)(image.Height*.14));y++)for(int x=0;x<image.Width;x++)
        {int a=image.GetPixel(x,y).A;if(a<128)continue;sum+=a;xSum+=(x+.5)*a;ySum+=(y+.5)*a;}
        if(sum<=0)throw new InvalidDataException("Missing opaque limb attachment.");
        return new PointF((float)(xSum/sum/image.Width),(float)(ySum/sum/image.Height));
    }
    private static PointF HandGrip(Bitmap image,bool claw)
    {
        int bottom=-1;double count=0,sx=0,sy=0;
        for(int y=(int)(image.Height*(claw?.8:.68));y<image.Height;y++)for(int x=0;x<image.Width;x++)
        {Color c=image.GetPixel(x,y);if(c.A>=200&&(claw||(c.R>175&&c.G>90&&c.R>c.G+14&&c.G>c.B+10)))bottom=Math.Max(bottom,y);}
        if(bottom<0)throw new InvalidDataException("Missing visible hand / claw landmark.");
        for(int y=Math.Max(0,bottom-(int)(image.Height*(claw?.1:.14)));y<=bottom;y++)for(int x=0;x<image.Width;x++)
        {Color c=image.GetPixel(x,y);if(c.A<200||(!claw&&!(c.R>175&&c.G>90&&c.R>c.G+14&&c.G>c.B+10)))continue;count++;sx+=x+.5;sy+=y+.5;}
        return new PointF((float)(sx/count/image.Width),(float)(sy/count/image.Height));
    }
    private sealed class BootLayout
    {
        internal readonly Rectangle Bounds;
        internal readonly PointF Cuff;
        internal BootLayout(Bitmap image,string id)
        {
            int start=(int)(image.Height*(id=="spark"?.55:id=="moss"?.51:.48));
            int left=image.Width,top=image.Height,right=0,bottom=0;
            for(int y=start;y<image.Height;y++)for(int x=0;x<image.Width;x++)if(image.GetPixel(x,y).A>=128)
            {left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
            if(right<=left||bottom<=top)throw new InvalidDataException("Walking boot has no opaque silhouette.");
            left=Math.Max(0,left-2);top=Math.Max(start,top-2);right=Math.Min(image.Width,right+3);bottom=Math.Min(image.Height,bottom+3);
            Bounds=new Rectangle(left,top,right-left,bottom-top);
            using(Bitmap cap=image.Clone(Bounds,PixelFormat.Format32bppArgb))
            {PointF root=OpaqueRoot(cap);Cuff=new PointF(left+root.X*Bounds.Width,top+root.Y*Bounds.Height);}
        }
    }
    private sealed class Rig:IDisposable
    {
        internal readonly Bitmap[] Parts=new Bitmap[9];
        internal readonly bool Profile;
        internal readonly Bitmap[] HeadPatches=new Bitmap[6];
        internal Bitmap TurtleHead;
        internal readonly Bitmap[] WalkingLegs=new Bitmap[2];
        internal readonly BootLayout[] Boots=new BootLayout[2];
        internal readonly PointF[] ClawAttachments=new PointF[4];
        internal readonly float[] ClawSoles=new float[4];
        internal readonly PointF[] ArmAttachments=new PointF[2];
        internal readonly PointF[] ArmGrips=new PointF[2];
        internal PointF ClawGrip;
        internal Rig(string path,int[][] regions)
        {
            Profile=String.Equals(Path.GetFileName(Path.GetDirectoryName(path)),"side-rigs",StringComparison.OrdinalIgnoreCase);
            using(Bitmap atlas=new Bitmap(path))
                for(int i=0;i<9;i++)
                {
                    int[] r=regions[i];Rectangle bounds=new Rectangle(r[0],r[1],r[2],r[3]);
                    if(bounds.Left<0||bounds.Top<0||bounds.Right>atlas.Width||bounds.Bottom>atlas.Height) throw new InvalidDataException("Invalid region: "+path);
                    Parts[i]=atlas.Clone(bounds,PixelFormat.Format32bppArgb);
                    // The generator's alpha contains near-invisible RGB dust at A=1.
                    // Remove only sub-6% coverage during texture import; retain real antialiasing.
                    var data=Parts[i].LockBits(new Rectangle(0,0,bounds.Width,bounds.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
                    byte[] bytes=new byte[data.Stride*bounds.Height];Marshal.Copy(data.Scan0,bytes,0,bytes.Length);
                    for(int p=0;p<bytes.Length;p+=4) if(bytes[p+3]<16) {bytes[p]=bytes[p+1]=bytes[p+2]=bytes[p+3]=0;}
                    Marshal.Copy(bytes,0,data.Scan0,bytes.Length);Parts[i].UnlockBits(data);
            }
            if(Path.GetFileNameWithoutExtension(path)=="pippin")
            {
                // Review the actual opaque root/toe direction in each atlas.
                // AI sheets can still contain one oppositely drawn hind claw.
                for(int i=2;i<=5;i++)if(ClawToeOffset(Parts[i])>0)Parts[i].RotateFlip(RotateFlipType.RotateNoneFlipX);
                for(int i=2;i<=5;i++)
                {
                    ClawAttachments[i-2]=OpaqueRoot(Parts[i]);int sole=0;
                    for(int y=0;y<Parts[i].Height;y++)for(int x=0;x<Parts[i].Width;x++)if(Parts[i].GetPixel(x,y).A>=128)sole=Math.Max(sole,y+1);
                    ClawSoles[i-2]=sole/(float)Parts[i].Height;
                }
                ClawGrip=HandGrip(Parts[2],true);
                return;
            }
            if(Profile)
            {
                string id=Path.GetFileNameWithoutExtension(path),torsoRoot=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(path)),"side-torsos");
                var torsoRegions=new JavaScriptSerializer().Deserialize<Dictionary<string,int[]>>(File.ReadAllText(Path.Combine(torsoRoot,"regions.json")));
                int[] r=torsoRegions[id];
                using(Bitmap torso=new Bitmap(Path.Combine(torsoRoot,id+".png")))
                {
                    Rectangle bounds=new Rectangle(r[0],r[1],r[2],r[3]);
                    if(bounds.Left<0||bounds.Top<0||bounds.Right>torso.Width||bounds.Bottom>torso.Height)throw new InvalidDataException("Invalid side torso region: "+id);
                    Parts[1].Dispose();Parts[1]=torso.Clone(bounds,PixelFormat.Format32bppArgb);
                    var data=Parts[1].LockBits(new Rectangle(0,0,bounds.Width,bounds.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
                    byte[] bytes=new byte[data.Stride*bounds.Height];Marshal.Copy(data.Scan0,bytes,0,bytes.Length);
                    for(int p=0;p<bytes.Length;p+=4)if(bytes[p+3]<16){bytes[p]=bytes[p+1]=bytes[p+2]=bytes[p+3]=0;}
                    Marshal.Copy(bytes,0,data.Scan0,bytes.Length);Parts[1].UnlockBits(data);
                }
            }
            for(int i=0;i<2;i++)
            {
                WalkingLegs[i]=(Bitmap)Parts[4+i].Clone();
                if(BootToeOffset(WalkingLegs[i])<0)WalkingLegs[i].RotateFlip(RotateFlipType.RotateNoneFlipX);
                Boots[i]=new BootLayout(WalkingLegs[i],Path.GetFileNameWithoutExtension(path));
                Bitmap arm=Parts[i+2];double sum=0,xSum=0,ySum=0;
                for(int y=0;y<Math.Max(1,(int)(arm.Height*.14));y++)for(int x=0;x<arm.Width;x++)
                {
                    int alpha=arm.GetPixel(x,y).A;if(alpha<128)continue;
                    sum+=alpha;xSum+=(x+.5)*alpha;ySum+=(y+.5)*alpha;
                }
                if(sum<=0)throw new InvalidDataException("Sleeve has no opaque shoulder cap: "+path);
                ArmAttachments[i]=new PointF((float)(xSum/sum/arm.Width),(float)(ySum/sum/arm.Height));
                ArmGrips[i]=HandGrip(arm,false);
                // Reviewed palm centers, not the lower edge of the flared cuff.
                // Skin-colored embroidery at the hem must not become the hand.
                if(!Profile&&i==1)ArmGrips[i]=Path.GetFileNameWithoutExtension(path)=="orbit"?new PointF(.60f,.66f):Path.GetFileNameWithoutExtension(path)=="spark"?new PointF(.20f,.80f):new PointF(.46f,.80f);
            }
        }
        public void Dispose() { foreach(Bitmap part in Parts) if(part!=null) part.Dispose();foreach(Bitmap patch in HeadPatches)if(patch!=null)patch.Dispose();foreach(Bitmap leg in WalkingLegs)if(leg!=null)leg.Dispose();if(TurtleHead!=null)TurtleHead.Dispose(); }
    }
}
