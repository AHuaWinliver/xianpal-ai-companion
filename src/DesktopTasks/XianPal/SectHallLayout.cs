// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Windows.Forms;

namespace XianPal
{
    internal static class SectHallLayout
    {
        internal const float WorkstationWidth=220,WorkstationHeight=270;
        private static int Margin(int width,float unit){return Math.Min(width/4,(int)Math.Max(12*unit,Math.Min(80*unit,width*.055)));}
        private static int Columns(int width,int count,float unit)
        {return Math.Max(1,Math.Min(Math.Min(4,count),(int)((width-2*Margin(width,unit))/(WorkstationWidth*.7f*unit))));}
        internal static Size CanvasSize(Size viewport,int count,float unit)
        {
            int width=Math.Max(1,viewport.Width),height=Math.Max(1,viewport.Height);
            if(count>0)
            {
                int columns=Columns(width,count,unit),rows=(count+columns-1)/columns;
                // Scrolling is a fallback for dense sects, not a permanent
                // 680px scene that overrides the size of every window.
                height=Math.Max(height,(int)Math.Ceiling((rows*WorkstationHeight*.7f*unit+32*unit)/.68));
            }
            return new Size(width,height);
        }
        internal static Rectangle[] Seats(Size canvas,int count,float unit)
        {
            if(count<=0)return new Rectangle[0];
            int margin=Margin(canvas.Width,unit),inside=Math.Max(1,canvas.Width-2*margin);
            int top=(int)(canvas.Height*.32),bottom=Math.Max(1,(int)(32*unit));
            int available=Math.Max(1,canvas.Height-top-bottom),columns=1;float best=-1;
            for(int c=1;c<=Columns(canvas.Width,count,unit);c++)
            {
                int rows=(count+c-1)/c;
                float scale=Math.Min(inside/(float)c/WorkstationWidth,available/(float)rows/WorkstationHeight);
                if(scale>best+.001f){best=scale;columns=c;}
            }
            int rowCount=(count+columns-1)/columns,cellWidth=Math.Max(1,inside/columns),cellHeight=Math.Max(1,available/rowCount);
            int gap=Math.Max(1,(int)(4*unit));var result=new Rectangle[count];
            for(int i=0;i<count;i++)
            {
                int row=i/columns,inRow=Math.Min(columns,count-row*columns);
                int x=margin+(inside-inRow*cellWidth)/2+(i%columns)*cellWidth;
                result[i]=new Rectangle(x+gap,top+row*cellHeight,Math.Max(1,cellWidth-gap*2),Math.Max(1,cellHeight-gap));
            }
            return result;
        }
    }

    internal sealed class SectHallViewport : Panel
    {
        private readonly SectHallControl hall;
        private bool arranging;
        private int lastCount=-1;
        internal SectHallViewport(SectHallControl hall)
        {
            this.hall=hall;Dock=DockStyle.Fill;AutoScroll=true;
            hall.Dock=DockStyle.None;hall.Location=Point.Empty;Controls.Add(hall);
        }
        internal void RefreshMembers()
        {int count=hall.MemberCount;if(count!=lastCount){lastCount=count;PerformLayout();}hall.Invalidate();}
        protected override void OnLayout(LayoutEventArgs e)
        {
            if(arranging||hall==null){base.OnLayout(e);return;}
            arranging=true;
            try
            {
                lastCount=hall.MemberCount;
                // Width/Height are the space allotted by the split panel.
                // ClientSize can already exclude stale native scrollbars from
                // the previous window size, creating a self-sustaining pair of
                // unnecessary horizontal/vertical scrollbars if used as input.
                Size viewport=new Size(Math.Max(1,Width),Math.Max(1,Height));
                float unit=Math.Max(1,hall.Font.Height/16f);
                Size canvas=SectHallLayout.CanvasSize(viewport,lastCount,unit);
                bool scrolling=canvas.Height>viewport.Height;
                if(scrolling)canvas=SectHallLayout.CanvasSize(new Size(Math.Max(1,viewport.Width-SystemInformation.VerticalScrollBarWidth),viewport.Height),lastCount,unit);
                if(!scrolling)AutoScrollPosition=Point.Empty;
                hall.Bounds=new Rectangle(new Point(0,AutoScrollPosition.Y),canvas);
                AutoScrollMinSize=new Size(0,scrolling?canvas.Height:0);
                base.OnLayout(e);
            }
            finally{arranging=false;}
        }
    }
}
