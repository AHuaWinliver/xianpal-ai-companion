// SPDX-License-Identifier: Apache-2.0
using System;

namespace XianPal
{
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
            double closure=b<0||b>1?0:b<.42?Smooth(b/.42):b<.62?1:1-Smooth((b-.62)/.38);
            double s=(seconds-smileAt)/smileDuration;
            double warmth=s<0||s>1?0:Math.Min(Smooth(s/.24),Smooth((1-s)/.32));
            blink=(int)Math.Round(closure*3);smile=(int)Math.Round(warmth*3);
        }
        private static double Smooth(double value)
        { value = Math.Max(0, Math.Min(1, value)); return value * value * (3 - 2 * value); }

    }
}
