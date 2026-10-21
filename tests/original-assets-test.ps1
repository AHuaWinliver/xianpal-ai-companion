# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$assetRoot=Join-Path $projectRoot 'build/payload/assets'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing,System.Web.Extensions -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
public static class OriginalAssetCheck {
    public static void CheckForegroundArms(Type generator,string artRoot) {
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;var instance=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var type=generator.GetNestedType("Rig",System.Reflection.BindingFlags.NonPublic);int checkedPoses=0;
        foreach(string id in new[]{"orbit","spark","moss","pippin"})foreach(bool profile in new[]{false,true}) {
            string root=Path.Combine(artRoot,profile?"side-rigs":"rigs");
            var metadata=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(root,"regions.json")));
            object rig=Activator.CreateInstance(type,instance,null,new object[]{Path.Combine(root,id+".png"),metadata[id]},null);
            try {
                string[] states=profile?new[]{"running-left","running-right"}:new[]{"idle","waiting","failed","waving","review","jumping","punching","eating","wiping-mouth"};
                foreach(string state in states) {
                    int samples=state=="waving"||state=="punching"||state=="wiping-mouth"?24:4;
                    for(int f=0;f<samples;f++)using(Bitmap scene=new Bitmap(384,416))using(Bitmap arms=new Bitmap(384,416)) {
                        double phase=f/(double)samples,t=phase*Math.PI*2;bool mirror=id=="pippin"?state=="running-right":state=="running-left";
                        using(Graphics g=Graphics.FromImage(scene)) {
                            generator.GetMethod("Quality",flags).Invoke(null,new object[]{g});if(mirror){g.TranslateTransform(384,0);g.ScaleTransform(-1,1);}
                            generator.GetMethod(id=="pippin"?"Turtle":profile?"ProfileHuman":"Human",flags).Invoke(null,id=="pippin"?new object[]{g,rig,state,phase}:new object[]{g,rig,state,phase,id});
                        }
                        using(Graphics g=Graphics.FromImage(arms)) {
                            generator.GetMethod("Quality",flags).Invoke(null,new object[]{g});if(mirror){g.TranslateTransform(384,0);g.ScaleTransform(-1,1);}
                            if(state=="jumping")g.TranslateTransform(0,(float)(-(id=="pippin"?9:15)*Math.Pow(Math.Sin(Math.PI*phase),2)));
                            if(id!="pippin") {
                                g.TranslateTransform(profile?(float)Math.Sin(t)*.9f:0,profile?(float)Math.Cos(t*2)*.75f:(float)Math.Sin(t)*.35f);
                                g.TranslateTransform(192,260);g.RotateTransform(profile?(float)Math.Sin(t)*.45f:state=="failed"?2:0);g.TranslateTransform(-192,-260);
                            }
                            generator.GetMethod(id=="pippin"?"DrawTurtleForegroundArms":profile?"DrawWalkingForegroundArms":"DrawHumanForegroundArms",flags).Invoke(null,id=="pippin"?new object[]{g,rig,state,phase}:new object[]{g,rig,id,state,phase});
                        }
                        int opaque=0;
                        for(int y=0;y<416;y+=3)for(int x=0;x<384;x+=3) {
                            Color a=arms.GetPixel(x,y);if(a.A!=255)continue;opaque++;Color b=scene.GetPixel(x,y);
                            if(b.A<250||Math.Abs(a.R-b.R)>2||Math.Abs(a.G-b.G)>2||Math.Abs(a.B-b.B)>2)throw new Exception("Arm was covered by head/body/clothing: "+id+" "+state+" phase="+f+" at "+x+","+y);
                        }
                        if(opaque<20)throw new Exception("Missing foreground arms: "+id+" "+state);checkedPoses++;
                    }
                }
            }finally{((IDisposable)rig).Dispose();}
        }
        Console.WriteLine("PASS "+checkedPoses+" rendered poses: both arms / forelegs remain above head, body, clothing and shell");
    }
    public static void CheckGestures(Type generator,string artRoot) {
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;var instance=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var metadata=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(artRoot,"regions.json")));
        var type=generator.GetNestedType("Rig",System.Reflection.BindingFlags.NonPublic);var motion=generator.Assembly.GetType("XianPal.CompanionMotion");
        foreach(string id in new[]{"orbit","spark","moss","pippin"}) {
            object rig=Activator.CreateInstance(type,instance,null,new object[]{Path.Combine(artRoot,id+".png"),metadata[id]},null);
            try {
                foreach(string state in id=="pippin"?new[]{"waving","punching","wiping-mouth"}:new[]{"waving","wiping-mouth"})for(int f=0;f<24;f++)using(Bitmap limb=new Bitmap(384,416)) {
                    double phase=f/24.0;PointF target=(PointF)motion.GetMethod("GestureGrip",flags).Invoke(null,new object[]{id,state,phase});
                    using(Graphics g=Graphics.FromImage(limb)) {
                        generator.GetMethod("Quality",flags).Invoke(null,new object[]{g});
                        generator.GetMethod(id=="pippin"?"DrawTurtleGesture":"DrawHumanGesture",flags).Invoke(null,id=="pippin"?new object[]{g,rig,state,phase}:new object[]{g,rig,id,state,phase});
                    }
                    int visible=0;for(int y=(int)target.Y-7;y<=(int)target.Y+7;y++)for(int x=(int)target.X-7;x<=(int)target.X+7;x++) {
                        Color c=limb.GetPixel(x,y);if(c.A>=160&&(id=="pippin"||(c.R>175&&c.G>90&&c.R>c.G+14&&c.G>c.B+10)))visible++;
                    }
                    if(visible<4)throw new Exception("Real hand / claw not visible at gesture target: "+id+" "+state+" phase="+f);
                    if(id=="pippin") {
                        for(int y=210;y<400;y++)for(int x=45;x<215;x++) {
                            int safe=state=="wiping-mouth"?240:state=="waving"?275:290;
                            if(y<safe&&limb.GetPixel(x,y).A>=96)throw new Exception("Turtle forelimb crosses eyes / upper face: "+state+" phase="+f);
                        }
                    }
                }
            }finally{((IDisposable)rig).Dispose();}
        }
    }
    public static void CheckWalkingHipJoins(Type generator,string artRoot) {
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var instance=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var metadata=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(artRoot,"regions.json")));
        var type=generator.GetNestedType("Rig",System.Reflection.BindingFlags.NonPublic);var motion=generator.Assembly.GetType("XianPal.CompanionMotion");
        foreach(string id in new[]{"orbit","moss"}) {
            object rig=Activator.CreateInstance(type,instance,null,new object[]{Path.Combine(artRoot,id+".png"),metadata[id]},null);
            try {
                var parts=(Bitmap[])type.GetField("Parts",instance).GetValue(rig);RectangleF torso=(RectangleF)generator.GetMethod("ProfileTorsoBox",flags).Invoke(null,new object[]{rig,id});
                foreach(bool mirror in new[]{false,true})for(int f=0;f<24;f++)using(Bitmap body=new Bitmap(384,416)) {
                    double phase=f/24.0,t=phase*Math.PI*2;
                    using(Graphics g=Graphics.FromImage(body)) {
                        generator.GetMethod("Quality",flags).Invoke(null,new object[]{g});if(mirror){g.TranslateTransform(384,0);g.ScaleTransform(-1,1);}
                        g.TranslateTransform((float)Math.Sin(t)*.9f,(float)Math.Cos(t*2)*.75f);g.TranslateTransform(192,260);g.RotateTransform((float)Math.Sin(t)*.45f);g.TranslateTransform(-192,-260);g.DrawImage(parts[1],torso);
                    }
                    for(int leg=0;leg<2;leg++)using(Bitmap limb=new Bitmap(384,416)) {
                        double u=(phase+(leg==0?0:.5))%1;float center=(float)motion.GetMethod("WalkingFootCenter",flags).Invoke(null,new object[]{id,leg});
                        PointF hip=(PointF)motion.GetMethod("WalkingHipPoint",flags).Invoke(null,new object[]{id,leg,phase}),sole=(PointF)generator.GetMethod("Foot",flags).Invoke(null,new object[]{center,u});
                        using(Graphics g=Graphics.FromImage(limb)){generator.GetMethod("Quality",flags).Invoke(null,new object[]{g});if(mirror){g.TranslateTransform(384,0);g.ScaleTransform(-1,1);}generator.GetMethod("DrawWalkingLeg",flags).Invoke(null,new object[]{g,rig,leg,hip,sole,u});}
                        PointF socket=hip;if(mirror)socket.X=384-socket.X;
                        for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)if(body.GetPixel((int)Math.Round(socket.X)+dx,(int)Math.Round(socket.Y)+dy).A<240)throw new Exception("Hip root outside opaque garment: "+id+" phase="+f);
                        int exposed=0,overlap=0;
                        bool[] visited=new bool[384*416];int[] queue=new int[384*416];
                        for(int y=290;y<416;y++)for(int x=80;x<305;x++) {
                            Color a=limb.GetPixel(x,y),b=body.GetPixel(x,y);
                            if(y<346&&a.A>=160&&b.A<32)exposed++;
                            if(a.A>=160&&b.A>=160)overlap++;
                            if(a.A<128||visited[y*384+x])continue;
                            int take=0,put=1,count=0,joined=0,bottom=y;queue[0]=y*384+x;visited[queue[0]]=true;
                            while(take<put) {
                                int p=queue[take++],px=p%384,py=p/384;count++;bottom=Math.Max(bottom,py);if(body.GetPixel(px,py).A>=160)joined++;
                                for(int ny=Math.Max(0,py-1);ny<=Math.Min(415,py+1);ny++)for(int nx=Math.Max(0,px-1);nx<=Math.Min(383,px+1);nx++) {
                                    int q=ny*384+nx;if(visited[q]||limb.GetPixel(nx,ny).A<128)continue;visited[q]=true;queue[put++]=q;
                                }
                            }
                            if(count>40&&bottom>=365&&joined<120)throw new Exception("Boot / trouser component detached from torso: "+id+" phase="+f+" leg="+leg);
                        }
                        // Upper thigh must stay inside the robe. A small natural
                        // edge sliver is allowed, not the old floating knee cap.
                        if(exposed>96||overlap<400)throw new Exception("Thigh escapes garment: "+id+" phase="+f+" leg="+leg+" exposed="+exposed+" overlap="+overlap);
                    }
                }
            }finally{((IDisposable)rig).Dispose();}
        }
    }
    public static void CheckTurtleNeck(Type generator,string artRoot) {
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var instance=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var metadata=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(artRoot,"regions.json")));
        var type=generator.GetNestedType("Rig",System.Reflection.BindingFlags.NonPublic);
        object rig=Activator.CreateInstance(type,instance,null,new object[]{Path.Combine(artRoot,"pippin.png"),metadata["pippin"]},null);
        try {
            bool profile=Path.GetFileName(artRoot)=="side-rigs";int start=profile?296:322;
            RectangleF box=(RectangleF)generator.GetMethod("HeadBox",flags).Invoke(null,new object[]{"pippin",rig});
            var parts=(Bitmap[])type.GetField("Parts",instance).GetValue(rig);
            using(Bitmap original=new Bitmap(768,832,System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using(Bitmap head=(Bitmap)generator.GetMethod("CreateTurtleHead",flags).Invoke(null,new object[]{rig})) {
                using(Graphics g=Graphics.FromImage(original)) {
                    generator.GetMethod("Quality",flags).Invoke(null,new object[]{g});g.ScaleTransform(2,2);
                    g.DrawImage(parts[0],box,new RectangleF(0,0,parts[0].Width,parts[0].Height),GraphicsUnit.Pixel);
                }
                int before=0,after=0;
                for(int y=0;y<832;y++)for(int x=0;x<768;x++) {
                    Color a=original.GetPixel(x,y),b=head.GetPixel(x,y);
                    if(y<start*2&&a!=b)throw new Exception("Turtle neck correction changes face pixels: "+artRoot);
                    if(y>=start*2){if(a.A>=128)before++;if(b.A>=128)after++;}
                }
                if(before<100||after>before*.55||after<before*.18)throw new Exception("Turtle neck not reduced appropriately: "+after+" / "+before);
                var map=generator.Assembly.GetType("XianPal.CompanionMotion").GetMethod("TurtleNeckPoint",flags);
                // Sample opaque source neck points through the inverse warp;
                // no internal mesh seam may become transparent.
                for(int y=start+2;y<box.Bottom-3;y+=2)for(int x=(int)box.Left+3;x<box.Right-3;x+=2)if(original.GetPixel(x*2,y*2).A>=250) {
                    PointF p=(PointF)map.Invoke(null,new object[]{new PointF(x,y),profile});int strongest=0;
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)strongest=Math.Max(strongest,head.GetPixel((int)Math.Round(p.X*2)+dx,(int)Math.Round(p.Y*2)+dy).A);
                    if(strongest<128)throw new Exception("Transparent tear inside turtle neck");
                }
            }
        }finally{((IDisposable)rig).Dispose();}
    }
    public static void CheckOrbitMouth(Type generator,string artRoot) {
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var instance=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var metadata=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(artRoot,"regions.json")));
        var rigType=generator.GetNestedType("Rig",System.Reflection.BindingFlags.NonPublic);
        object rig=Activator.CreateInstance(rigType,instance,null,new object[]{Path.Combine(artRoot,"orbit.png"),metadata["orbit"]},null);
        try {
            generator.GetMethod("CreateExpressionPatches",flags).Invoke(null,new object[]{"orbit",rig});
            var patches=(Bitmap[])rigType.GetField("HeadPatches",instance).GetValue(rig);
            using(Bitmap neutral=(Bitmap)generator.GetMethod("FaceCanvas",flags).Invoke(null,new object[]{"orbit",rig,0})) {
                for(int level=0;level<3;level++)using(Bitmap face=(Bitmap)neutral.Clone()) {
                    Bitmap patch=patches[3+level];using(Graphics g=Graphics.FromImage(face))g.DrawImageUnscaled(patch,0,0);
                    int mouthPixels=0;double totalY=0;
                    for(int y=0;y<416;y++)for(int x=0;x<384;x++) {
                        // Independent reviewed mouth ROI. Preserve opaque facial
                        // pixels and all silhouette alpha; GDI+ can round RGB on
                        // translucent hair when it premultiplies an overlay.
                        bool mouthArea=x>=205&&x<=247&&y>=174&&y<=198;
                        Color before=neutral.GetPixel(x,y),after=face.GetPixel(x,y);
                        if(!mouthArea&&(patch.GetPixel(x,y).A!=0||before.A!=after.A||(before.A==255&&before!=after)))throw new Exception("Qingheng mouth patch changes nose / head silhouette: level="+level+" at "+x+","+y);
                        if(level==2&&x>=215&&x<239&&y>=176&&y<196) {
                            Color c=face.GetPixel(x,y);
                            if(c.A>240&&c.R>100&&c.R>c.G+45&&c.G<155&&c.B<130){mouthPixels++;totalY+=y;}
                        }
                    }
                    // The old eye-aligned patch opened at y~170, above the actual
                    // neutral lip at y~180. Its oral cavity must now sit below it.
                    if(level==2&&(mouthPixels<20||totalY/mouthPixels<181||totalY/mouthPixels>190))throw new Exception("Qingheng open mouth is not attached to neutral lip");
                }
            }
        }finally{((IDisposable)rig).Dispose();}
    }
    public static void CheckFootJoins(Type generator,string artRoot) {
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var instance=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var metadata=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(artRoot,"regions.json")));
        var rigType=generator.GetNestedType("Rig",System.Reflection.BindingFlags.NonPublic);var motion=generator.Assembly.GetType("XianPal.CompanionMotion");
        foreach(string id in new[]{"orbit","spark","moss","pippin"}) {
            if(id!="pippin"&&Path.GetFileName(artRoot)=="rigs")continue; // Original human art is used only for non-walking states.
            object rig=Activator.CreateInstance(rigType,instance,null,new object[]{Path.Combine(artRoot,id+".png"),metadata[id]},null);
            try {
                if(id!="pippin") {
                    var boots=(Array)rigType.GetField("Boots",instance).GetValue(rig);
                    for(int i=0;i<2;i++)for(int f=0;f<24;f++) {
                        double phase=f/24.0,u=(phase+(i==0?0:.5))%1;
                        PointF hip=(PointF)motion.GetMethod("WalkingHipPoint",flags).Invoke(null,new object[]{id,i,phase});
                        float center=(float)motion.GetMethod("WalkingFootCenter",flags).Invoke(null,new object[]{id,i});
                        PointF sole=(PointF)generator.GetMethod("Foot",flags).Invoke(null,new object[]{center,u});
                        object boot=boots.GetValue(i);var type=boot.GetType();PointF source=(PointF)type.GetField("Cuff",instance).GetValue(boot);
                        Rectangle bounds=(Rectangle)type.GetField("Bounds",instance).GetValue(boot);
                        PointF cuff=(PointF)generator.GetMethod("BootPoint",flags).Invoke(null,new object[]{boot,source,sole,u});
                        using(Bitmap pants=new Bitmap(384,416))using(Bitmap shoe=new Bitmap(384,416)) {
                            using(Graphics g=Graphics.FromImage(pants)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;generator.GetMethod("DrawWalkingPants",flags).Invoke(null,new object[]{g,rig,i,hip,sole,u});}
                            using(Graphics g=Graphics.FromImage(shoe)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;generator.GetMethod("DrawWalkingBoot",flags).Invoke(null,new object[]{g,rig,i,sole,u});}
                            int overlap=0;for(int y=(int)cuff.Y-8;y<=(int)cuff.Y+8;y++)for(int x=(int)cuff.X-9;x<=(int)cuff.X+9;x++)if(pants.GetPixel(x,y).A>=160&&shoe.GetPixel(x,y).A>=160)overlap++;
                            if(overlap<8)throw new Exception("Pants / boot cuff disconnect: "+id+" leg="+i+" phase="+f+" overlap="+overlap);
                            if(u<.6)for(int y=398;y<416;y++)for(int x=0;x<384;x++)if(shoe.GetPixel(x,y).A>=128)throw new Exception("Loaded boot penetrates floor: "+id);
                        }
                        PointF top=(PointF)generator.GetMethod("BootPoint",flags).Invoke(null,new object[]{boot,new PointF(bounds.Left,bounds.Top),sole,u});
                        PointF right=(PointF)generator.GetMethod("BootPoint",flags).Invoke(null,new object[]{boot,new PointF(bounds.Right,bounds.Top),sole,u});
                        PointF bottom=(PointF)generator.GetMethod("BootPoint",flags).Invoke(null,new object[]{boot,new PointF(bounds.Left,bounds.Bottom),sole,u});
                        double w=Math.Sqrt(Math.Pow(right.X-top.X,2)+Math.Pow(right.Y-top.Y,2)),h=Math.Sqrt(Math.Pow(bottom.X-top.X,2)+Math.Pow(bottom.Y-top.Y,2));
                        if(Math.Abs(w/h-bounds.Width/(double)bounds.Height)>.0001)throw new Exception("Complete boot aspect ratio changed: "+id);
                    }
                }else {
                    var parts=(Bitmap[])rigType.GetField("Parts",instance).GetValue(rig);
                    if(Path.GetFileName(artRoot)=="side-rigs")for(int f=0;f<24;f++) {
                        double phase=f/24.0;PointF socket=(PointF)motion.GetMethod("TurtleSocket",flags).Invoke(null,new object[]{phase,true});
                        using(Bitmap body=new Bitmap(384,416))using(Bitmap head=new Bitmap(384,416)) {
                            using(Graphics g=Graphics.FromImage(body)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(parts[1],new RectangleF(110,170+(float)Math.Cos(phase*Math.PI*4)*.6f,226,178));}
                            using(Graphics g=Graphics.FromImage(head)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;motion.GetMethod("ApplyHeadAttachment",flags).Invoke(null,new object[]{g,id,"running-right",phase});generator.GetMethod("DrawAttachedHead",flags).Invoke(null,new object[]{g,rig,id,0});}
                            int overlap=0;for(int y=(int)socket.Y-9;y<=(int)socket.Y+9;y++)for(int x=(int)socket.X-9;x<=(int)socket.X+9;x++)if(body.GetPixel(x,y).A>=160&&head.GetPixel(x,y).A>=160)overlap++;
                            if(overlap<12)throw new Exception("Turtle neck was joined through a moving leg instead of body: "+f);
                        }
                    }
                    foreach(string state in new[]{"idle","running-right","waving"})for(int part=2;part<=5;part++)for(int f=0;f<24;f++) {
                        bool walk=state.StartsWith("running"),near=part==2||part==4;double phase=f/24.0+(part==3||part==4?.5:0);
                        PointF root=(PointF)motion.GetMethod("TurtleLegSocket",flags).Invoke(null,new object[]{part,phase,walk});
                        PointF size=(PointF)generator.GetMethod("TurtleFootSize",flags).Invoke(null,new object[]{rig,part,near});
                        if(Math.Abs(size.X/size.Y-parts[part].Width/(double)parts[part].Height)>.00001)throw new Exception("Turtle limb squeezed");
                        using(Bitmap body=new Bitmap(384,416))using(Bitmap foot=new Bitmap(384,416)) {
                            using(Graphics g=Graphics.FromImage(body)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(parts[1],new RectangleF(110,170+(walk?(float)Math.Cos(phase*Math.PI*4)*.6f:0),226,178));}
                            using(Graphics g=Graphics.FromImage(foot)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;generator.GetMethod("DrawTurtleFoot",flags).Invoke(null,new object[]{g,rig,part,phase,walk,near,part==2?state:"idle"});}
                            int overlap=0;for(int y=(int)root.Y-10;y<=(int)root.Y+10;y++)for(int x=(int)root.X-12;x<=(int)root.X+12;x++)if(body.GetPixel(x,y).A>=160&&foot.GetPixel(x,y).A>=160)overlap++;
                            if(overlap<12)throw new Exception("Turtle limb outside body socket: "+state+" part="+part+" phase="+f+" overlap="+overlap);
                        }
                    }
                }
            }finally{((IDisposable)rig).Dispose();}
        }
    }
    private static bool Skin(Color c) {return c.A>=200&&c.R>120&&c.R>c.G+10&&c.G>c.B+5;}
    public static void CheckHeadBodyJoins(Type generator,string artRoot) {
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var instance=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var metadata=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(artRoot,"regions.json")));
        var rigType=generator.GetNestedType("Rig",System.Reflection.BindingFlags.NonPublic);
        var motion=generator.Assembly.GetType("XianPal.CompanionMotion");
        foreach(string id in new[]{"orbit","spark","moss"}) {
            object rig=Activator.CreateInstance(rigType,instance,null,new object[]{Path.Combine(artRoot,id+".png"),metadata[id]},null);
            try {
                var parts=(Bitmap[])rigType.GetField("Parts",instance).GetValue(rig);
                RectangleF box=(RectangleF)generator.GetMethod("ProfileTorsoBox",flags).Invoke(null,new object[]{rig,id});
                PointF root=(PointF)motion.GetMethod("WalkingNeckAnchor",flags).Invoke(null,new object[]{id});
                using(Bitmap body=new Bitmap(384,416)) {
                    using(Graphics g=Graphics.FromImage(body)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(parts[1],box);}
                    for(int f=0;f<24;f++)using(Bitmap head=new Bitmap(384,416)) {
                        using(Graphics g=Graphics.FromImage(head)) {
                            g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            motion.GetMethod("ApplyHeadAttachment",flags).Invoke(null,new object[]{g,id,"running-right",f/24.0});
                            generator.GetMethod("DrawAttachedHead",flags).Invoke(null,new object[]{g,rig,id,0});
                        }
                        int overlap=0;
                        for(int y=(int)root.Y-12;y<=(int)root.Y+12;y++)for(int x=(int)root.X-12;x<=(int)root.X+12;x++)
                            if(Skin(head.GetPixel(x,y))&&Skin(body.GetPixel(x,y)))overlap++;
                        if(overlap<12)throw new Exception("Head/body touch through clothing only, or neck gap: "+id+" frame="+f+" shared skin="+overlap);
                    }
                }
                using(Bitmap neutral=new Bitmap(384,416)) {
                    using(Graphics g=Graphics.FromImage(neutral))generator.GetMethod("DrawAttachedHead",flags).Invoke(null,new object[]{g,rig,id,0});
                    Point removed=id=="spark"?new Point(199,195):id=="moss"?new Point(179,208):new Point(187,204);
                    if(neutral.GetPixel(removed.X,removed.Y).A>16)throw new Exception("Old collar remains attached to the head: "+id);
                }
            }finally{((IDisposable)rig).Dispose();}
        }
    }
    public static void CheckSideTorsos(Type generator,string artRoot) {
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var instance=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var metadata=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(artRoot,"regions.json")));
        string torsoRoot=Path.Combine(Path.GetDirectoryName(artRoot),"side-torsos");
        var torsoRegions=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,int[]>>(File.ReadAllText(Path.Combine(torsoRoot,"regions.json")));
        var rigType=generator.GetNestedType("Rig",System.Reflection.BindingFlags.NonPublic);
        var motion=generator.Assembly.GetType("XianPal.CompanionMotion");
        foreach(string id in new[]{"orbit","spark","moss"}) {
            object rig=Activator.CreateInstance(rigType,instance,null,new object[]{Path.Combine(artRoot,id+".png"),metadata[id]},null);
            try {
                Bitmap torso=((Bitmap[])rigType.GetField("Parts",instance).GetValue(rig))[1];int[] reviewed=torsoRegions[id];
                if(torso.Width!=reviewed[2]||torso.Height!=reviewed[3])throw new Exception("Walking still uses the frontal atlas torso: "+id);
                RectangleF box=(RectangleF)generator.GetMethod("ProfileTorsoBox",flags).Invoke(null,new object[]{rig,id});
                if(Math.Abs(box.Width/box.Height-torso.Width/(double)torso.Height)>.00001)throw new Exception("Side torso was horizontally squeezed: "+id);
                if(Math.Abs(box.Bottom-368)>.001)throw new Exception("Side torso hem height drifted: "+id);
                if(torso.Width/(double)torso.Height>=metadata[id][1][2]/(double)metadata[id][1][3]*.90)throw new Exception("Side artwork is still as broad as the rejected frontal garment: "+id);
                PointF neck=(PointF)motion.GetMethod("WalkingNeckAnchor",flags).Invoke(null,new object[]{id});
                PointF source=(PointF)motion.GetMethod("WalkingTorsoNeck",flags).Invoke(null,new object[]{id});
                if(Math.Abs(box.X+box.Width*source.X-neck.X)>.001||Math.Abs(box.Y+box.Height*source.Y-neck.Y)>.001)throw new Exception("Side torso neck does not meet head socket: "+id);
                if(torso.GetPixel((int)(source.X*torso.Width),(int)(source.Y*torso.Height)).A<200)throw new Exception("Reviewed torso neck point is transparent: "+id);
            }finally{((IDisposable)rig).Dispose();}
        }
    }
    public static void CheckHeadOutlines(Type generator,string artRoot) {
        var metadata=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(artRoot,"regions.json")));
        var draw=generator.GetMethod("DrawAttachedHead",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var rigType=generator.GetNestedType("Rig",System.Reflection.BindingFlags.NonPublic);
        foreach(string id in new[]{"orbit","spark","moss","pippin"}) {
            object rig=Activator.CreateInstance(rigType,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{Path.Combine(artRoot,id+".png"),metadata[id]},null);
            try { using(Bitmap neutral=new Bitmap(384,416)) {
                using(Graphics g=Graphics.FromImage(neutral))draw.Invoke(null,new object[]{g,rig,id,0});
                foreach(int expression in new[]{6,7})using(Bitmap face=new Bitmap(384,416)) {
                    using(Graphics g=Graphics.FromImage(face))draw.Invoke(null,new object[]{g,rig,id,expression});
                    for(int y=0;y<416;y++)for(int x=0;x<384;x++) {
                        Color a=neutral.GetPixel(x,y),b=face.GetPixel(x,y);
                        if((a.A>=128)!=(b.A>=128))throw new Exception("Expression changes head silhouette: "+id);
                        // Source-over compositing can round premultiplied RGB at
                        // transparent edges; compare solid neck pixels, not noise.
                        if(y>=(id=="pippin"?326:218)&&a.A>=240&&b.A>=240&&(Math.Abs(a.R-b.R)>2||Math.Abs(a.G-b.G)>2||Math.Abs(a.B-b.B)>2))throw new Exception("Expression changes neck attachment: "+id);
                    }
                }
            }} finally {((IDisposable)rig).Dispose();}
            if(id!="pippin") {
                object bootRig=Activator.CreateInstance(rigType,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{Path.Combine(artRoot,id+".png"),metadata[id]},null);
                try {
                    var boots=(Bitmap[])rigType.GetField("WalkingLegs",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(bootRig);
                    var offset=generator.GetMethod("BootToeOffset",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                    foreach(Bitmap boot in boots)if((double)offset.Invoke(null,new object[]{boot})<5)throw new Exception("Walking boot artwork points away from travel: "+id);
                }finally{((IDisposable)bootRig).Dispose();}
            }
            if(id=="pippin") {
                object feetRig=Activator.CreateInstance(rigType,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{Path.Combine(artRoot,id+".png"),metadata[id]},null);
                try {
                var feet=(Bitmap[])rigType.GetField("Parts",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(feetRig);
                for(int part=2;part<=5;part++) {
                    int[] box=metadata[id][part];double heel=0,toe=0,heelWeight=0,toeWeight=0;
                    for(int y=0;y<box[3];y++)for(int x=0;x<box[2];x++) {
                        int a=feet[part].GetPixel(x,y).A;if(a<200)continue;
                        if(y<box[3]*.20){heel+=x*a;heelWeight+=a;}
                        if(y>=box[3]*.80){toe+=x*a;toeWeight+=a;}
                    }
                    if(heelWeight<=0||toeWeight<=0||toe/toeWeight>=heel/heelWeight-5)throw new Exception("Turtle native claws do not face the same way as head/shell: "+part+" heel="+(heel/heelWeight)+" toe="+(toe/toeWeight));
                }
                }finally{((IDisposable)feetRig).Dispose();}
            }
        }
    }
    public static void CheckNeckSockets(Type generator,string assets) {
        var motion=generator.Assembly.GetType("XianPal.CompanionMotion");
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        foreach(string id in new[]{"orbit","spark","moss","pippin"})foreach(string state in new[]{"running-left","running-right"})for(int f=0;f<24;f++) {
            double phase=f/24.0,t=phase*Math.PI*2;
            PointF root=(PointF)motion.GetMethod("WalkingNeckAnchor",flags).Invoke(null,new object[]{id});
            PointF point=id=="pippin"?(PointF)motion.GetMethod("TurtleSocket",flags).Invoke(null,new object[]{phase,true}):(PointF)motion.GetMethod("WalkingBodyPoint",flags).Invoke(null,new object[]{root});
            if(id!="pippin") {
                using(Bitmap canvas=new Bitmap(1,1))using(Graphics g=Graphics.FromImage(canvas)) {
                    g.TranslateTransform((float)Math.Sin(t)*.9f,(float)Math.Cos(t*2)*.75f);g.TranslateTransform(192,260);g.RotateTransform((float)Math.Sin(t)*.45f);g.TranslateTransform(-192,-260);
                    PointF[] points={point};using(var matrix=g.Transform)matrix.TransformPoints(points);point=points[0];
                }
            }
            if(id=="pippin"?state=="running-right":state=="running-left")point.X=384-point.X;
            string folder=id=="orbit"?assets:Path.Combine(assets,"characters",id);
            using(Bitmap image=new Bitmap(Path.Combine(folder,state,f.ToString("00")+".png")))
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(image.GetPixel((int)Math.Round(point.X)+dx,(int)Math.Round(point.Y)+dy).A<128)
                    throw new Exception("Transparent neck/body socket: "+id+" "+state+" "+f);
        }
    }
    public static void CheckSleeveMesh(Type generator) {
        var draw=generator.GetMethod("DrawSleeveMesh",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var motion=generator.Assembly.GetType("XianPal.CompanionMotion").GetMethod("ArmPath",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        foreach(float angle in new[]{-120f,-45f,0f,18f,105f})
        using(Bitmap source=new Bitmap(60,200))using(Graphics paint=Graphics.FromImage(source))
        using(Bitmap result=new Bitmap(512,512))using(Graphics g=Graphics.FromImage(result)) {
            paint.Clear(Color.SteelBlue);g.Clear(Color.Transparent);g.TranslateTransform(200,200);
            PointF[] path=(PointF[])motion.Invoke(null,new object[]{angle,32.0,146f,false,"waving",.25,24});
            draw.Invoke(null,new object[]{g,source,path,44f,146f});g.Flush();
            for(int i=1;i<24;i++)for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++) {
                if(result.GetPixel((int)Math.Round(path[i].X)+200+x,(int)Math.Round(path[i].Y)+200+y).A<250)
                    throw new Exception("Transparent seam inside continuous sleeve mesh: "+angle+" / "+i);
            }
        }
    }
    public static void Check(string file) {
        using (Bitmap bitmap=new Bitmap(file)) {
            bool effect=file.IndexOf(Path.DirectorySeparatorChar+"reminder"+Path.DirectorySeparatorChar,StringComparison.Ordinal)>=0;
            int width=effect?256:384, height=effect?256:416;
            if(bitmap.Width!=width||bitmap.Height!=height) throw new Exception("Incorrect canvas: "+file);
            BitmapData data=bitmap.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try {
                byte[] row=new byte[width*4]; bool ink=false;
                for(int y=0;y<height;y++) {
                    Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);
                    for(int x=0;x<width;x++) {
                        int p=x*4; if(row[p+3]==0) continue;
                        ink=true;
                        if(x==0||x==width-1||y==0||y==height-1) throw new Exception("Clipped image boundary: "+file);
                        if(row[p+3]>16&&row[p]>190&&row[p+2]>190&&row[p+1]<80) throw new Exception("Magenta edge: "+file);
                    }
                }
                if(!ink) throw new Exception("Empty animation frame: "+file);
            } finally {bitmap.UnlockBits(data);}
        }
    }
}
'@
$roots=@((Get-Item -LiteralPath $assetRoot)) + @(Get-ChildItem -LiteralPath (Join-Path $assetRoot 'characters') -Directory)
if($roots.Count -ne 4){throw 'Expected 4 cultivation companions'}
foreach($root in $roots){
    if(@(Get-ChildItem -LiteralPath (Join-Path $root.FullName 'expressions') -Filter '*.png').Count -ne 6){throw 'Missing walking expression patches'}
    if(@(Get-ChildItem -LiteralPath (Join-Path $root.FullName 'profile-expressions') -Filter '*.png').Count -ne 6){throw 'Missing side-facing walking expression patches'}
    foreach($state in @('running','running-left','running-right')){
        $frames=@(Get-ChildItem -LiteralPath (Join-Path $root.FullName $state) -Filter '*.png')
        if($frames.Count -ne 24){throw ('Expected 24 gait phases: '+$root.Name+'/'+$state)}
        $hashes=@($frames | Get-FileHash -Algorithm SHA256 | Select-Object -ExpandProperty Hash -Unique)
        if($hashes.Count -ne 24){throw ('Duplicate gait phases: '+$root.Name+'/'+$state)}
    }
}
$images=@(Get-ChildItem -LiteralPath $assetRoot -Recurse -Filter '*.png' -File)
if($images.Count -ne 1010){throw ('Unexpected image count: '+$images.Count)}
foreach($file in $images){[OriginalAssetCheck]::Check($file.FullName)}
$rig=[Reflection.Assembly]::LoadFrom((Join-Path $projectRoot 'build/BuildOriginalCharacters.exe')).GetType('BuildOriginalCharacters')
[OriginalAssetCheck]::CheckForegroundArms($rig,(Join-Path $projectRoot 'art'))
[OriginalAssetCheck]::CheckGestures($rig,(Join-Path $projectRoot 'art/rigs'))
Write-Output 'PASS real rendered hands / claws at gesture targets in all 24 phases, without cutting the forelimb against the shell or face'
[OriginalAssetCheck]::CheckWalkingHipJoins($rig,(Join-Path $projectRoot 'art/side-rigs'))
Write-Output 'PASS orbit / moss 24-phase both-direction hips inside the garment, covered upper thighs and connected boot / trouser components'
[OriginalAssetCheck]::CheckTurtleNeck($rig,(Join-Path $projectRoot 'art/rigs'))
[OriginalAssetCheck]::CheckTurtleNeck($rig,(Join-Path $projectRoot 'art/side-rigs'))
Write-Output 'PASS original / side turtle neck reduction, unchanged face pixels and no internal inverse-warp seams'
[OriginalAssetCheck]::CheckOrbitMouth($rig,(Join-Path $projectRoot 'art/side-rigs'))
Write-Output 'PASS Qingheng mouth registration: cavity below neutral lip; eyes, nose and head silhouette unchanged at all three expression levels'
[OriginalAssetCheck]::CheckSleeveMesh($rig)
[OriginalAssetCheck]::CheckHeadOutlines($rig,(Join-Path $projectRoot 'art/rigs'))
[OriginalAssetCheck]::CheckHeadOutlines($rig,(Join-Path $projectRoot 'art/side-rigs'))
[OriginalAssetCheck]::CheckSideTorsos($rig,(Join-Path $projectRoot 'art/side-rigs'))
[OriginalAssetCheck]::CheckHeadBodyJoins($rig,(Join-Path $projectRoot 'art/side-rigs'))
[OriginalAssetCheck]::CheckFootJoins($rig,(Join-Path $projectRoot 'art/side-rigs'))
[OriginalAssetCheck]::CheckFootJoins($rig,(Join-Path $projectRoot 'art/rigs'))
[OriginalAssetCheck]::CheckNeckSockets($rig,$assetRoot)
Write-Output 'PASS full boot / pants overlap, original boot ratios, floor contact and all turtle sockets during standing / walking / waving'
Write-Output 'PASS 24-phase skin-to-skin neck overlap and removal of duplicate head collars'
Write-Output 'PASS real side torso source selection, natural aspect ratios, narrower side silhouettes and pinned opaque necks'
Write-Output 'PASS stable head/neck silhouettes across expressions and opaque neck/body sockets in all walking frames'
Write-Output 'PASS continuous sleeve mesh coverage without internal transparent seams'
$foot=$rig.GetMethod('Foot',[Reflection.BindingFlags]'Static,NonPublic')
$arm=$rig.GetMethod('ArmSwing',[Reflection.BindingFlags]'Static,NonPublic')
foreach($phase in @(0.0,0.5)){
    $leg=$foot.Invoke($null,@([single]0,[double]$phase))
    $own=$arm.Invoke($null,@([double]$phase,$true))
    $opposite=$arm.Invoke($null,@([double]$phase,$false))
    # Positive arm rotation moves the hand backwards; opposite arm follows the front leg.
    if($leg.X*$own -le 0 -or $leg.X*$opposite -ge 0){throw 'Contralateral phase relation failed'}
}
for($i=0;$i -lt 240;$i++){
    $phase=$i/240.0
    $left=$foot.Invoke($null,@([single]0,[double]$phase))
    $right=$foot.Invoke($null,@([single]0,[double](($phase+0.5)%1)))
    if($left.Y -lt 395.99 -and $right.Y -lt 395.99){throw 'Both walking feet airborne'}
}
Write-Output 'PASS opposing arms/legs, arm phase offset and continuous loaded stance'
Write-Output 'PASS 1010 cultivation images: canvas, transparent/unclipped boundaries, no magenta pixels, 24 unique gait phases per character/direction and separate original / side face patches'
