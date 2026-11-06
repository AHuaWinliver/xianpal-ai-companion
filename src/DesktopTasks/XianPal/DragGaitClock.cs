// SPDX-License-Identifier: Apache-2.0
using System;

namespace XianPal
{
    // Input samples estimate velocity; only animation ticks advance the pose.
    // One cycle position drives the already coordinated arm/leg artwork.
    internal sealed class DragGaitClock
    {
        internal const double StopAfterMs=140;
        private double targetSpeed;
        internal double Speed {get;private set;}
        internal double CyclePosition {get;private set;}
        internal void Reset(){targetSpeed=Speed=CyclePosition=0;}
        internal void RecordMotion(double pixels,double elapsedMs)
        {
            if(Double.IsNaN(pixels)||Double.IsInfinity(pixels)||pixels<=0||Double.IsNaN(elapsedMs)||Double.IsInfinity(elapsedMs))return;
            double elapsed=Math.Max(1,Math.Min(80,elapsedMs));
            double measured=Math.Min(3000,pixels*1000/elapsed);
            double blend=1-Math.Exp(-elapsed/35);
            targetSpeed=targetSpeed<=0?measured:targetSpeed+(measured-targetSpeed)*blend;
        }
        internal static double CyclesPerSecond(double speed)
        {
            if(Double.IsNaN(speed)||speed<=0)return 0;
            speed=Math.Min(3000,speed);
            // Continuous, monotonic speed response with a readable fast-run cap.
            return 3.2*speed/(speed+240);
        }
        internal int Advance(double elapsedMs,double motionAgeMs,int frameCount)
        {
            if(motionAgeMs>=StopAfterMs){Speed=0;return Frame(frameCount);}
            double elapsed=Math.Max(0,Math.Min(50,elapsedMs));
            double age=Math.Max(0,motionAgeMs);
            double desired=targetSpeed*Math.Exp(-Math.Max(0,age-40)/40);
            double previous=Speed;
            Speed+=(desired-Speed)*(1-Math.Exp(-elapsed/60));
            double angle=(CyclePosition%1)*Math.PI*2;
            double dwell=1+.14*Math.Abs(Math.Cos(angle))-.10*Math.Abs(Math.Sin(angle));
            CyclePosition+=(CyclesPerSecond(previous)+CyclesPerSecond(Speed))*.5*elapsed/1000/dwell;
            return Frame(frameCount);
        }
        private int Frame(int frameCount)
        {return frameCount<=1?0:Math.Min(frameCount-1,(int)((CyclePosition%1)*frameCount));}
    }
}
