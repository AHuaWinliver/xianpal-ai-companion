// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace XianPal
{
    // Shared by the offline texture renderer and the live companion.
    internal static class CompanionMotion
    {
        internal static PointF NeckAnchor(string id)
        {
            if(id=="pippin")return TurtleNeckPoint(new PointF(144,349),false);
            if(id=="spark")return new PointF(196,201);
            if(id=="moss")return new PointF(183,211);
            return new PointF(202,207);
        }
        internal static PointF WalkingBodyPoint(PointF point)
        {return point;}
        internal static void ApplyWalkingBody(Graphics g)
        {
            // Walking now has actual directional artwork, not a flattened frontal projection.
        }
        internal static PointF WalkingNeckAnchor(string id)
        {return id=="pippin"?TurtleNeckPoint(new PointF(151,316),true):id=="spark"?new PointF(199,187):id=="moss"?new PointF(190,200):new PointF(185,194);}
        internal static PointF TurtleNeckPoint(PointF point,bool profile)
        {
            // Leave the skull and jaw untouched. Only the broad atlas neck
            // below the jaw tapers and retracts into the shell aperture.
            float start=profile?296:322,rootX=profile?151:144;
            if(point.Y<=start)return point;
            double depth=Math.Max(0,point.Y-start);
            float width=(float)(1-.28*(1-Math.Exp(-depth/15)));
            float x=rootX+(point.X-rootX)*width;
            return new PointF(x,start+(float)(20*(1-Math.Exp(-depth/20)))+(x-rootX)*.13f*(float)(1-Math.Exp(-depth/12)));
        }
        internal static PointF[] WalkingOldCollar(string id)
        {
            // Exclude only the old garment attached to the head atlas, not the face or hair silhouette.
            if(id=="spark")return new[]{new PointF(185,184),new PointF(207,191),new PointF(216,201),new PointF(200,204),new PointF(182,196)};
            if(id=="moss")return new[]{new PointF(160,195),new PointF(190,203),new PointF(202,220),new PointF(194,226),new PointF(160,215)};
            return new[]{new PointF(173,190),new PointF(194,201),new PointF(202,214),new PointF(186,216),new PointF(174,206)};
        }
        internal static RectangleF WalkingHeadBox(string id)
        {
            // Keep the approved head placement fixed when refining the anatomical neck socket.
            PointF root=id=="pippin"?new PointF(151,323):id=="spark"?new PointF(204,212):id=="moss"?new PointF(205,214):new PointF(205,212);float width,height,u,v;
            if(id=="pippin"){height=148;width=height*375/356;u=.86f;v=.88f;}
            else if(id=="spark"){height=201;width=height*435/416;u=.65f;v=.90f;}
            else if(id=="moss"){height=201;width=height*473/435;u=.73f;v=.90f;}
            else{height=201;width=height*470/422;u=.72f;v=.88f;}
            return new RectangleF(root.X-u*width,root.Y-v*height,width,height);
        }
        internal static PointF WalkingShoulderAnchor(string id,bool far)
        {
            // Real side-view shoulders overlap in depth, rather than spanning a frontal chest.
            if(id=="moss")return far?new PointF(176,232):new PointF(182,234);
            if(id=="spark")return far?new PointF(181,216):new PointF(187,218);
            return far?new PointF(168,225):new PointF(174,227);
        }
        internal static float WalkingFootCenter(string id,int leg)
        {
            // These narrow side robes need projected hip depth, not the old
            // frontal 28px spread that exposed the thigh outside the garment.
            if(id=="orbit")return leg==0?182:188;
            if(id=="moss")return leg==0?180:186;
            return 192+(leg==0?-14:14);
        }
        internal static PointF WalkingHipPoint(string id,int leg,double phase)
        {
            double t=phase*Math.PI*2;float x=WalkingFootCenter(id,leg);
            float bob=(float)Math.Cos(t*2)*.75f,sway=(float)Math.Sin(t)*.9f;
            if(id!="orbit"&&id!="moss")return new PointF(x+sway,319+bob);
            // Exactly the same roll, bob and sway as the side torso. The soles
            // remain world-space anchors and must not inherit this transform.
            PointF[] socket={new PointF(x,311)};
            using(Matrix parent=new Matrix())
            {
                parent.Translate(sway,bob);parent.Translate(192,260);parent.Rotate((float)Math.Sin(t)*.45f);parent.Translate(-192,-260);
                parent.TransformPoints(socket);
            }
            return socket[0];
        }
        internal static PointF WalkingTorsoNeck(string id)
        {return id=="spark"?new PointF(.651f,.036f):id=="moss"?new PointF(.663f,.033f):new PointF(.630f,.044f);}
        internal static bool NativeHeadFacesLeft(string id)
        {return id=="spark"||id=="moss"||id=="pippin";}
        internal static PointF[] WalkingBootCorners(PointF sole)
        {return new[]{new PointF(sole.X-17,sole.Y-16),new PointF(sole.X+17,sole.Y-16),new PointF(sole.X-9,sole.Y)};}
        internal static float WalkingBootPitch(double phase)
        {return phase<.6?0:(float)(-6*Math.Sin(Math.PI*(phase-.6)/.4));}
        internal static PointF WalkingBootPoint(Rectangle bounds,PointF cuff,PointF source,PointF sole,double phase)
        {
            double angle=WalkingBootPitch(phase)*Math.PI/180;float scale=36f/bounds.Height;
            float x=(source.X-cuff.X)*scale,y=(source.Y-bounds.Bottom)*scale;
            return new PointF(sole.X+(float)(Math.Cos(angle)*x-Math.Sin(angle)*y),sole.Y+(float)(Math.Sin(angle)*x+Math.Cos(angle)*y));
        }
        internal static PointF TurtleLegSocket(int part,double phase,bool walking)
        {
            PointF socket=part==2?new PointF(164,326):part==3?new PointF(180,325):part==4?new PointF(271,320):new PointF(267,322);
            socket.Y+=walking?(float)Math.Cos(phase*Math.PI*4)*.6f:0;return socket;
        }
        internal static PointF[] TurtleLegPath(double phase,bool walking,bool gesture,float height,int segments)
        {return TurtleLegPathWithReach(phase,walking,gesture,height,0,segments);}
        internal static PointF[] TurtleLegPathWithReach(double phase,bool walking,bool gesture,float height,float reach,int segments)
        {
            double t=phase*Math.PI*2;float stride=walking?(float)Math.Cos(t)*8:0,lift=walking?(float)Math.Max(0,Math.Sin(t))*5:0;
            if(gesture){stride=-18+(float)Math.Sin(t)*4;lift=24+(float)Math.Sin(t-.3)*3;}
            PointF[] path=new PointF[segments+1];
            for(int i=0;i<=segments;i++)
            {
                // Root cap stays fixed; extension is confined to the proximal
                // leg. The lower 40% (paw/claws) only translates, never stretches.
                float s=i/(float)segments,w=(float)Smooth((s-.18)/.42),extension=(float)Smooth((s-.18)/.34);
                path[i]=new PointF(stride*w,height*s+reach*extension-lift*w);
            }
            return path;
        }
        internal static PointF TurtleSocket(double phase,bool walking)
        {return new PointF(151,walking?WalkingNeckAnchor("pippin").Y+(float)Math.Cos(phase*Math.PI*4)*.6f:NeckAnchor("pippin").Y-26);}
        internal static void ApplyHeadAttachment(Graphics g,string id,string state,double phase)
        {
            bool walking=state.StartsWith("running",StringComparison.Ordinal);double t=phase*Math.PI*2;
            PointF root=walking?WalkingNeckAnchor(id):NeckAnchor(id),target=id=="pippin"?TurtleSocket(phase,walking):root;
            float angle=walking?WalkingNeckAngle(id,phase):state=="review"?-3+(float)Math.Sin(t)*.6f:state=="failed"?5:state=="eating"?(float)Math.Sin(t)*1.4f:0;
            float sx=id=="pippin"?1+(walking?(float)Math.Sin(t-.45)*2:state=="eating"?(float)Math.Sin(t)*3:0)/143:1;
            float cosine=(float)Math.Cos(angle*Math.PI/180),sine=(float)Math.Sin(angle*Math.PI/180);
            float a=cosine*sx,b=sine*sx,c=-sine,d=cosine;
            // One explicit matrix keeps the anatomical root exact even for
            // near-zero angles, which GDI+ may simplify in chained transforms.
            PointF[] expected={target};using(Matrix before=g.Transform)before.TransformPoints(expected);
            using(Matrix attachment=new Matrix(a,b,c,d,target.X-a*root.X-c*root.Y,target.Y-b*root.X-d*root.Y))g.MultiplyTransform(attachment);
            {
                PointF[] actual={root};using(Matrix after=g.Transform)after.TransformPoints(actual);
                g.TranslateTransform(expected[0].X-actual[0].X,expected[0].Y-actual[0].Y,MatrixOrder.Append);
            }
        }
        internal static PointF ShoulderAnchor(string id,bool left)
        {
            // Reviewed garment sockets, in the unchanged 384 x 416 body canvas.
            if(id=="spark")return left?new PointF(151,213):new PointF(228,213);
            if(id=="moss")return left?new PointF(138,226):new PointF(236,219);
            return left?new PointF(144,218):new PointF(224,217);
        }
        internal static float WalkingNeckAngle(string id,double phase)
        {
            double t=phase*Math.PI*2;
            double amount=id=="pippin"?.70:id=="moss"?.82:id=="spark"?1.08:1;
            // Delayed neck motion is independent of the much smaller torso roll.
            return (float)(amount*(2.1*Math.Sin(t-.38)+.35*Math.Sin(2*t-.55)));
        }
        internal static void ApplyWalkingNeck(Graphics g,string id,double phase)
        {
            ApplyHeadAttachment(g,id,"running",phase);
        }
        internal static double ElbowFlex(double phase, bool left, string state)
        {
            double t=phase*Math.PI*2;
            if(state.StartsWith("running",StringComparison.Ordinal))
                return 24+(left?-10:10)*Math.Cos(t-.32);
            if(state=="waving") return 48+9*Math.Sin(t-.4);
            if(state=="review") return 24+4*Math.Sin(t-.3);
            if(state=="eating") return 18+5*Math.Sin(t-.2);
            if(state=="wiping-mouth") return 22+6*Math.Sin(t-.35);
            return 12+2*Math.Sin(t-.2);
        }
        internal static PointF GestureMouth(string id)
        {
            if(id=="pippin")return new PointF(107,270);
            if(id=="spark")return new PointF(196.36f,174.74f);
            if(id=="moss")return new PointF(170.2f,190.42f);
            return new PointF(219.25f,184.79f);
        }
        internal static float GestureEnvelope(double phase)
        {return (float)Math.Min(Smooth(phase/.28),Smooth((1-phase)/.28));}
        internal static int GesturePlaybackInterval(string id,string state,int fallback)
        {return state=="waving"||state=="wiping-mouth"||(id=="pippin"&&state=="punching")?40:fallback;}
        internal static PointF GestureGrip(string id,string state,double phase)
        {
            bool turtle=id=="pippin";PointF root=turtle?TurtleLegSocket(2,0,false):ShoulderAnchor(id,false),mouth=GestureMouth(id);
            float blend=GestureEnvelope(phase),wave=(float)Math.Sin(phase*Math.PI*4);
            PointF rest=turtle?new PointF(164,377):new PointF(root.X+12,root.Y+107),goal;
            if(state=="wiping-mouth")goal=new PointF(mouth.X+wave*(turtle?7:10),mouth.Y+3+(float)Math.Cos(phase*Math.PI*4));
            else if(turtle&&state=="punching")goal=new PointF(118-20*(float)Math.Pow(Math.Sin(phase*Math.PI),2),325);
            else if(turtle)goal=new PointF(94+wave*5,309+(float)Math.Cos(phase*Math.PI*4)*3);
            else goal=new PointF(root.X+76+wave*9,mouth.Y-38+(float)Math.Cos(phase*Math.PI*4)*3);
            return new PointF(rest.X+(goal.X-rest.X)*blend,rest.Y+(goal.Y-rest.Y)*blend);
        }
        internal static PointF[] GesturePath(string id,string state,double phase,PointF rootUv,PointF gripUv,float width,float height,int segments)
        {
            bool turtle=id=="pippin";PointF root=turtle?TurtleLegSocket(2,0,false):ShoulderAnchor(id,false),goal=GestureGrip(id,state,phase);
            float blend=GestureEnvelope(phase),handStart=turtle?.60f:Math.Max(.34f,gripUv.Y-.14f);
            double raisedAngle=turtle?(state=="wiping-mouth"?-2.35:state=="punching"?-1.65:-1.85):state=="wiping-mouth"?-Math.PI/2:2.20;
            double angle=raisedAngle*blend;float tx=(float)Math.Sin(angle),ty=(float)Math.Cos(angle),nx=ty,ny=-tx;
            float handOffset=(gripUv.X-.5f)*width;
            PointF palm=new PointF(goal.X-nx*handOffset,goal.Y-ny*handOffset);
            PointF wrist=new PointF(palm.X-tx*(gripUv.Y-handStart)*height,palm.Y-ty*(gripUv.Y-handStart)*height);
            PointF cap=new PointF(root.X-(rootUv.X-.5f)*width,root.Y+(.16f-rootUv.Y)*height);
            PointF a=new PointF(cap.X+(turtle?-20:24)*blend,cap.Y+14),b=new PointF(wrist.X-tx*18,wrist.Y-ty*18);
            PointF[] path=new PointF[segments+1];
            for(int i=0;i<=segments;i++)
            {
                float s=i/(float)segments;
                if(s<=.16f)path[i]=new PointF(cap.X,root.Y+(s-rootUv.Y)*height);
                else if(s>=handStart)path[i]=new PointF(palm.X+tx*(s-gripUv.Y)*height,palm.Y+ty*(s-gripUv.Y)*height);
                else {
                    float u=(s-.16f)/(handStart-.16f),v=1-u;
                    path[i]=new PointF(v*v*v*cap.X+3*v*v*u*a.X+3*v*u*u*b.X+u*u*u*wrist.X,v*v*v*cap.Y+3*v*v*u*a.Y+3*v*u*u*b.Y+u*u*u*wrist.Y);
                }
            }
            return path;
        }
        internal static PointF[] ArmPath(float upperDegrees, double flexDegrees, float height, bool left, string state, double phase, int segments)
        {
            bool walk=state.StartsWith("running",StringComparison.Ordinal);
            double bend=state=="failed-outward"?(left?flexDegrees:-flexDegrees):walk||state=="waving"||state=="punching"?-flexDegrees:(left?-flexDegrees:flexDegrees);
            double wrist=3*Math.Sin(phase*Math.PI*2-.55)*(left?1:-1);
            PointF[] points=new PointF[segments+1];
            points[0]=new PointF(0,-6);
            for(int i=0;i<segments;i++)
            {
                double s=(i+.5)/segments;
                // A 22%-length elbow zone distributes sleeve curvature instead
                // of introducing a sharp paper-doll hinge or a visible joint.
                double elbow=Smooth((s-.34)/.22), hand=Smooth((s-.78)/.22);
                // Keep the proximal sleeve sewn to the garment. Rotation grows
                // below the shoulder cap rather than revolving the entire cap.
                double shoulder=Smooth((s-.16)/.18);
                double angle=(upperDegrees*shoulder+bend*elbow+wrist*hand)*Math.PI/180;
                float length=height/segments;
                points[i+1]=new PointF(points[i].X-(float)Math.Sin(angle)*length,points[i].Y+(float)Math.Cos(angle)*length);
            }
            return points;
        }
        internal static double Smooth(double x)
        { x=Math.Max(0,Math.Min(1,x)); return x*x*(3-2*x); }
        internal static bool MirrorWalkingFace(string id,string state)
        { return id=="pippin"?state=="running-right":state=="running-left"; }
    }

    internal sealed class WalkingExpressionClock
    {
        private readonly Random random;
        private readonly double blinkDuration, gapBias;
        private double blinkAt, smileAt, smileDuration;
        internal WalkingExpressionClock(int seed,string id)
        {
            random=new Random(seed);
            blinkDuration=id=="spark"?.19:id=="pippin"?.26:.22;
            gapBias=id=="pippin"?1.2:id=="moss"?.6:0;
            blinkAt=.65+random.NextDouble()*.85;
            smileAt=1.6+random.NextDouble()*1.9;
            smileDuration=1.3+random.NextDouble()*.65;
        }
        internal void Sample(double seconds,out int blink,out int smile)
        {
            if(Double.IsNaN(seconds)||Double.IsInfinity(seconds))seconds=0;
            seconds=Math.Max(0,seconds);
            if(seconds>blinkAt+blinkDuration)blinkAt=seconds+2.5+gapBias+random.NextDouble()*2.6;
            if(seconds>smileAt+smileDuration)
            {smileAt=seconds+4.5+gapBias+random.NextDouble()*4.1;smileDuration=1.3+random.NextDouble()*.65;}
            double b=(seconds-blinkAt)/blinkDuration;
            double closure=b<0||b>1?0:b<.42?CompanionMotion.Smooth(b/.42):b<.62?1:1-CompanionMotion.Smooth((b-.62)/.38);
            double s=(seconds-smileAt)/smileDuration;
            double warmth=s<0||s>1?0:Math.Min(CompanionMotion.Smooth(s/.24),CompanionMotion.Smooth((1-s)/.32));
            blink=(int)Math.Round(closure*3);smile=(int)Math.Round(warmth*3);
        }
    }
}
