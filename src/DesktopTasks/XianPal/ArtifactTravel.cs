// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace XianPal
{
    internal sealed class ArtifactTravelClock
    {
        internal bool Active {get;private set;}
        internal bool Landing {get;private set;}
        internal double Seconds {get;private set;}
        private double landingAge,landingFrom;
        internal static double Threshold(Rectangle area){return Math.Max(480,Math.Sqrt((double)area.Width*area.Width+(double)area.Height*area.Height)*.4);}
        internal static double RouteLength(Point origin,IEnumerable<Point> path)
        {double length=0;Point previous=origin;foreach(Point p in path){double x=(double)p.X-previous.X,y=(double)p.Y-previous.Y;length+=Math.Sqrt(x*x+y*y);previous=p;}return length;}
        internal void Start(){Active=true;Landing=false;Seconds=landingAge=0;landingFrom=0;}
        internal void Reset(){Active=Landing=false;Seconds=landingAge=landingFrom=0;}
        internal void BeginLanding(){if(!Active||Landing)return;landingFrom=Envelope;landingAge=0;Landing=true;}
        internal float Envelope {get {return !Active?0:Landing?(float)(landingFrom*(1-Smooth(landingAge/.45))):(float)Smooth(Seconds/.55);}}
        internal double Speed {get{return Active&&!Landing?110+250*Envelope:0;}}
        internal double MovementStep(double milliseconds)
        {
            // Sub-pixel intervals must not round both axes to zero and be
            // mistaken for collision with a screen boundary.
            return Active&&!Landing?Math.Max(.75,Speed*Math.Max(1,Math.Min(34,milliseconds))/1000):0;
        }
        internal void Advance(double milliseconds)
        {if(!Active)return;double seconds=Math.Max(0,Math.Min(100,milliseconds))/1000;Seconds+=seconds;if(Landing){landingAge+=seconds;if(landingAge>=.45)Reset();}}
        private static double Smooth(double p){p=Math.Max(0,Math.Min(1,p));return p*p*(3-2*p);}
    }
    internal static class ArtifactTravelArt
    {
        internal static float FootRow(Image source)
        {
            Bitmap bitmap=source as Bitmap;if(bitmap==null)return source.Height*.95f;
            for(int y=bitmap.Height-1;y>=0;y--)for(int x=0;x<bitmap.Width;x+=3)if(bitmap.GetPixel(x,y).A>200)return y;
            return source.Height*.95f;
        }
        internal static string Name(string style)
        {return style=="command"?"青霄飞剑":style=="beam"?"流光符卷":style=="warning"?"碧叶灵舟":"墨玉灵盘";}
        internal static void Render(Graphics g,Size size,Image rider,float footRow,string id,string style,float envelope,double seconds,bool left)
        {
            GraphicsState original=g.Save();
            try
            {
                g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                float fit=Math.Min(size.Width/384f,size.Height/416f);g.TranslateTransform((size.Width-384*fit)/2,(size.Height-416*fit)/2);g.ScaleTransform(fit,fit);
                bool mirror=id=="pippin"?!left:left;if(mirror){g.TranslateTransform(384,0);g.ScaleTransform(-1,1);}
                float p=Math.Max(0,Math.Min(1,envelope)),scale=1-.16f*p;
                float bodyX=(384-384*scale)/2,bodyY=(-3+(float)Math.Sin(seconds*2.5)*1.5f)*p;
                float deck=bodyY+footRow*scale;
                if(p>.001f)
                {
                    GraphicsState vessel=g.Save();
                    // Emerge beneath the soles without clipping the bottom of
                    // the fixed-size alpha canvas during takeoff or landing.
                    g.TranslateTransform(0,deck);
                    g.ScaleTransform(1,Math.Max(.01f,Math.Min(1,(416-deck-3)/48)));
                    DrawVessel(g,style,0,p,seconds);g.Restore(vessel);
                }
                // The same graphics transform controls rider and vessel. The deck is
                // computed from the actual opaque sole row, not a detached guessed socket.
                g.DrawImage(rider,new RectangleF(bodyX,bodyY,384*scale,416*scale));
            }
            finally{g.Restore(original);}
        }
        private static Color Tint(Color color,float alpha){return Color.FromArgb((int)(255*Math.Max(0,Math.Min(1,alpha))),color);}
        private static void DrawVessel(Graphics g,string style,float deck,float opacity,double seconds)
        {
            Color accent=style=="beam"?Color.FromArgb(243,167,109):style=="warning"?Color.FromArgb(122,208,168):style=="stone"?Color.FromArgb(154,173,198):Color.FromArgb(114,212,228);
            using(var glow=new SolidBrush(Tint(accent,opacity*.12f)))g.FillEllipse(glow,28,deck-6,328,38);
            using(var gold=new Pen(Tint(Color.FromArgb(234,194,114),opacity),1.7f))
            using(var rim=new Pen(Tint(accent,opacity*.9f),1.8f))
            {
                if(style=="command")
                {
                    PointF[] blade={new PointF(83,deck),new PointF(323,deck),new PointF(362,deck+7),new PointF(323,deck+15),new PointF(83,deck+15)};
                    using(var metal=new LinearGradientBrush(new PointF(0,deck),new PointF(0,deck+16),Tint(Color.FromArgb(242,255,250),opacity),Tint(Color.FromArgb(75,155,172),opacity)))g.FillPolygon(metal,blade);
                    g.DrawPolygon(rim,blade);g.DrawLine(gold,84,deck+7,342,deck+7);
                    using(var grip=new Pen(Tint(Color.FromArgb(57,83,99),opacity),11)){grip.StartCap=grip.EndCap=LineCap.Round;g.DrawLine(grip,35,deck+7,79,deck+7);}
                    g.DrawLine(gold,79,deck-9,79,deck+23);
                    for(int i=0;i<4;i++)g.DrawLine(gold,40+i*8,deck+3,43+i*8,deck+11);
                }
                else if(style=="beam")
                {
                    using(var paper=new LinearGradientBrush(new PointF(0,deck),new PointF(0,deck+24),Tint(Color.FromArgb(255,236,185),opacity),Tint(Color.FromArgb(210,155,89),opacity)))
                        g.FillRectangle(paper,57,deck,270,24);
                    g.DrawRectangle(gold,57,deck,270,24);
                    using(var roll=new SolidBrush(Tint(Color.FromArgb(173,91,64),opacity))){g.FillEllipse(roll,47,deck-4,18,32);g.FillEllipse(roll,319,deck-4,18,32);}
                    using(var ink=new Pen(Tint(Color.FromArgb(184,66,64),opacity),2))
                        for(int i=0;i<4;i++){float x=103+i*49;g.DrawLine(ink,x,deck+6,x+10,deck+18);g.DrawLine(ink,x+10,deck+7,x,deck+18);}
                }
                else if(style=="warning")
                {
                    using(var leaf=new GraphicsPath())
                    {
                        leaf.AddBezier(35,deck+12,112,deck+48,267,deck+46,349,deck-3);
                        leaf.AddBezier(349,deck-3,263,deck+7,142,deck-7,35,deck+12);
                        using(var jade=new LinearGradientBrush(new PointF(0,deck-3),new PointF(0,deck+35),Tint(Color.FromArgb(169,230,164),opacity),Tint(Color.FromArgb(41,111,92),opacity)))g.FillPath(jade,leaf);
                        g.DrawPath(rim,leaf);g.DrawBezier(gold,50,deck+13,130,deck+18,250,deck+26,336,deck+2);
                        for(int i=0;i<5;i++){float x=100+i*38;g.DrawLine(gold,x,deck+17,x+13,deck+27);}
                    }
                }
                else
                {
                    using(var dark=new LinearGradientBrush(new PointF(0,deck-2),new PointF(0,deck+28),Tint(Color.FromArgb(97,123,139),opacity),Tint(Color.FromArgb(24,40,57),opacity)))g.FillEllipse(dark,43,deck-2,298,31);
                    g.DrawEllipse(gold,43,deck-2,298,31);g.DrawArc(rim,62,deck+6,260,29,0,180);
                    using(var mark=new Pen(Tint(Color.FromArgb(220,188,124),opacity*.7f),1.2f))for(int i=0;i<6;i++)g.DrawArc(mark,69+i*36,deck+8,27,12,10,140);
                }
                // Restrained translucent streamers stay inside the pet's alpha canvas.
                for(int k=0;k<2;k++)
                using(var stream=new Pen(Tint(accent,opacity*(.34f-k*.10f)),2-k*.5f))
                {stream.StartCap=stream.EndCap=LineCap.Round;float y=deck+26+k*8;g.DrawBezier(stream,38,y,91,y+5*(float)Math.Sin(seconds*3+k),133,y-3,161,y);}
            }
        }
    }
}
