// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;

namespace XianPal
{
    internal static class ChatWindowDrag
    {
        internal static Point Constrain(Rectangle dialog, Rectangle? pet, Point requested, Rectangle area)
        {
            Rectangle group=pet.HasValue?Rectangle.Union(dialog,pet.Value):dialog;
            // A smaller destination monitor may not fit the original arrangement. Keep
            // the pet and title reachable while dragging; the owner reflows on release.
            if(group.Width>area.Width||group.Height>area.Height)
            {
                Rectangle title=new Rectangle(dialog.X,dialog.Y,dialog.Width,Math.Min(48,dialog.Height));
                group=pet.HasValue?Rectangle.Union(title,pet.Value):title;
                if(group.Width>area.Width||group.Height>area.Height)group=title;
            }
            int dx=requested.X-dialog.X,dy=requested.Y-dialog.Y;
            int lowX=area.Left-group.Left,highX=area.Right-group.Right;
            int lowY=area.Top-group.Top,highY=area.Bottom-group.Bottom;
            dx=lowX<=highX?Math.Max(lowX,Math.Min(highX,dx)):lowX;
            dy=lowY<=highY?Math.Max(lowY,Math.Min(highY,dy)):lowY;
            return new Point(dialog.Left+dx,dialog.Top+dy);
        }
    }
}
