// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;

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
}
