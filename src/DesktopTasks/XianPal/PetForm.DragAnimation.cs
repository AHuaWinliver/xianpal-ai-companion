// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;

namespace XianPal
{
    internal sealed partial class PetForm
    {
        private readonly DragGaitClock dragGait=new DragGaitClock();
        private void TickDragAnimation(DateTime now,double elapsedMs)
        {
            if(!dragMoved)return;
            double age=Math.Max(0,(now-lastDragMotionAt).TotalMilliseconds);
            if(reduceMotion||age>=DragGaitClock.StopAfterMs)
            {
                dragGait.Reset();
                if(IsLocomotionState(state)){SetState("idle");RefreshPetVisual();}
                return;
            }
            List<Image> frames;
            if(!IsLocomotionState(state)||!animations.TryGetValue(state,out frames)||frames.Count==0)return;
            frameIndex=dragGait.Advance(elapsedMs,age,frames.Count);
            locomotionSpeedPixelsPerSecond=dragGait.Speed;
            RefreshPetVisual();
        }
    }
}
