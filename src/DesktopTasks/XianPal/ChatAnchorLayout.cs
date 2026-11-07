// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;

namespace XianPal
{
    internal struct ChatAnchorLayout
    {
        internal Rectangle Pet,Dialog;
        internal static ChatAnchorLayout Below(Rectangle pet,Size preferred,Rectangle area)
        {
            const int margin=8,gap=8,minHeight=350;
            int width=Math.Max(1,Math.Min(preferred.Width,area.Width-margin*2));
            int freeHeight=area.Height-margin*2-pet.Height-gap;
            int availableBelow=area.Bottom-margin-pet.Bottom-gap;
            int desiredHeight=Math.Min(preferred.Height,Math.Max(minHeight,availableBelow));
            int height=Math.Max(1,Math.Min(desiredHeight,freeHeight>=minHeight?freeHeight:area.Height-margin*2));
            int x=pet.Left+(pet.Width-width)/2;
            x=Math.Max(area.Left+margin,Math.Min(x,area.Right-margin-width));
            // Move the companion inward at horizontal edges so the dialog remains
            // genuinely centered below it instead of merely clamping the dialog.
            int petX=x+(width-pet.Width)/2;
            int highest=area.Top+margin;
            int lowest=area.Bottom-margin-height-gap-pet.Height;
            int petY=lowest>=highest?Math.Max(highest,Math.Min(pet.Top,lowest)):highest;
            Rectangle adjusted=new Rectangle(petX,petY,pet.Width,pet.Height);
            int y=adjusted.Bottom+gap;
            // Exceptionally small screens cannot contain both at the chosen pet
            // scale. Keep both visible rather than placing the dialog off-screen.
            if(y+height>area.Bottom-margin)y=Math.Max(highest,area.Bottom-margin-height);
            return new ChatAnchorLayout { Pet=adjusted,Dialog=new Rectangle(x,y,width,height) };
        }
    }
}
