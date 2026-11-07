// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using XianPal;
internal static class ChatDragTests
{
    private static object Call(object target,string method,params object[] args){return target.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);}
    private static object Field(object target,string field){return target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);}
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    private static bool HasRemovedMenu(ToolStripItemCollection items)
    {foreach(ToolStripItem item in items){if(item.Text.Contains("捏脸")||item.Text.ToLowerInvariant().Contains("sculpt"))return true;var menu=item as ToolStripMenuItem;if(menu!=null&&HasRemovedMenu(menu.DropDownItems))return true;}return false;}
    private static void Mouse(Control c,string method,int x,int y){typeof(Control).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,y,0)});}
    private static void Fits(Rectangle dialog,Rectangle pet,Point requested,Rectangle area,string label)
    {Point actual=ChatWindowDrag.Constrain(dialog,pet,requested,area);int dx=actual.X-dialog.X,dy=actual.Y-dialog.Y;dialog.Offset(dx,dy);pet.Offset(dx,dy);Check(area.Contains(dialog)&&area.Contains(pet),label);}
    [STAThread]private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Rectangle desktop=new Rectangle(0,0,1920,1040);
            Rectangle middle=new Rectangle(900,120,96,104);
            ChatAnchorLayout centered=ChatAnchorLayout.Below(middle,new Size(640,650),desktop);
            Check(centered.Pet==middle&&centered.Dialog.Top==middle.Bottom+8&&Math.Abs((centered.Dialog.Left*2+centered.Dialog.Width)-(middle.Left*2+middle.Width))<=1,"normal opening is centered directly below pet without moving it");
            Rectangle lower=new Rectangle(900,500,96,104);ChatAnchorLayout shorter=ChatAnchorLayout.Below(lower,new Size(640,650),desktop);
            Check(shorter.Pet==lower&&shorter.Dialog.Height<650&&shorter.Dialog.Top==lower.Bottom+8,"window shortens below pet before moving pet");
            Rectangle bottom=new Rectangle(1800,930,96,104);ChatAnchorLayout edge=ChatAnchorLayout.Below(bottom,new Size(640,650),desktop);
            Check(desktop.Contains(edge.Pet)&&desktop.Contains(edge.Dialog)&&edge.Dialog.Top==edge.Pet.Bottom+8&&edge.Dialog.Left+edge.Dialog.Width/2==edge.Pet.Left+edge.Pet.Width/2,"bottom/right opening moves group inward and never flips above");
            foreach(Rectangle monitor in new[]{new Rectangle(-1280,0,1280,1024),new Rectangle(2560,-800,1280,1024),new Rectangle(0,0,800,600)})
            {var anchored=ChatAnchorLayout.Below(new Rectangle(monitor.Left+40,monitor.Bottom-120,96,104),new Size(640,650),monitor);Check(monitor.Contains(anchored.Pet)&&monitor.Contains(anchored.Dialog)&&anchored.Dialog.Top==anchored.Pet.Bottom+8,"below placement stays on target monitor: "+monitor);}
            Check(typeof(PetForm).Assembly.GetType("XianPal.FaceSculpt")==null&&typeof(PetForm).Assembly.GetType("XianPal.FaceSculptForm")==null&&typeof(PetForm).GetMethod("LoadFaceShapes",BindingFlags.Instance|BindingFlags.NonPublic)==null,"removed feature types and startup hooks absent from assembly");
            string legacyFile=Path.Combine(Environment.GetEnvironmentVariable("FUFU_STATE_DIR"),"face-shapes.json");Directory.CreateDirectory(Path.GetDirectoryName(legacyFile));File.WriteAllText(legacyFile,"{ malformed legacy file intentionally ignored");
            Rectangle dialog=new Rectangle(500,300,640,650),petBox=new Rectangle(772,190,96,104),area=new Rectangle(0,0,1920,1040);
            Check(ChatWindowDrag.Constrain(dialog,petBox,new Point(540,335),area)==new Point(540,335),"free group movement preserves requested delta");
            Fits(dialog,petBox,new Point(-3000,-3000),area,"top/left edge keeps both windows visible");
            Fits(dialog,petBox,new Point(9000,9000),area,"bottom/right edge keeps both windows visible");
            Fits(dialog,petBox,new Point(-900,80),new Rectangle(-1280,0,1280,1024),"negative-coordinate second monitor supported");
            Fits(dialog,petBox,new Point(2700,-700),new Rectangle(2560,-800,1280,1024),"offset monitor supported without centering in monitor gap");
            Point small=ChatWindowDrag.Constrain(dialog,petBox,new Point(20,20),new Rectangle(0,0,800,540));Check(small.Y>=0,"smaller monitor keeps title reachable pending release reflow");
            using(var form=new ActivityTrayForm())
            {
                form.ShowNear(new Rectangle(500,100,96,104));Application.DoEvents();int starts=0,ends=0;form.HeaderDragStarted+=delegate{starts++;};form.HeaderDragFinished+=delegate{ends++;};
                Check(form.StartPosition==FormStartPosition.Manual,"first show cannot be relocated by Windows default placement");
                Mouse(form,"OnMouseDown",40,90);Check(!(bool)Field(form,"headerArmed"),"body click never starts window dragging");
                var heading=(Control)Field(form,"heading");Mouse(heading,"OnMouseDown",5,5);Mouse(heading,"OnMouseMove",6,6);Mouse(heading,"OnMouseUp",6,6);Check(starts==0&&ends==0,"title click without movement has no drag side effects");
                Point original=form.Location;Mouse(heading,"OnMouseDown",5,5);Mouse(heading,"OnMouseMove",25,20);Mouse(heading,"OnMouseUp",25,20);Check(starts==1&&ends==1&&form.Location!=original,"title text drags with capture and one completion");
                Mouse(heading,"OnMouseDown",5,5);Mouse(heading,"OnMouseMove",25,20);heading.Capture=false;Check(!form.HeaderDragInProgress&&ends==2,"lost capture ends drag instead of sticking");form.DismissForTest();
            }
            using(var pet=new PetForm())
            {
                pet.Show();Application.DoEvents();var form=(ActivityTrayForm)Field(pet,"activityTray");
                Check(!HasRemovedMenu(pet.ContextMenuStrip.Items),"removed editor has no pet/tray menu entry");
                Rectangle live=Screen.FromPoint(Cursor.Position).WorkingArea;
                pet.Location=new Point(live.Left+live.Width/2-pet.Width/2,live.Top+40);form.ShowNear(pet.Bounds);Application.DoEvents();
                Check(form.Top==pet.Bottom+8&&Math.Abs((form.Left*2+form.Width)-(pet.Left*2+pet.Width))<=1,"live dialog opens directly below actual pet");
                form.DismissForTest();Call(pet,"OpenReplyConversation");
                Check(form.Top==pet.Bottom+8&&form.PinnedOpen,"double-click bubble route uses the same below-pet anchor");
                Point oldPet=pet.Location,oldChat=form.Location;Call(pet,"BeginChatGroupDrag");
                Point wanted=new Point(oldChat.X+25,oldChat.Y+25);Point actual=(Point)Call(pet,"MoveChatGroupToArea",wanted,live);form.Location=actual;
                Check(pet.Left-oldPet.X==form.Left-oldChat.X&&pet.Top-oldPet.Y==form.Top-oldChat.Y,"pet and dialog receive the same translation");
                Check(form.Location==actual&&(bool)Field(pet,"chatGroupDragging"),"pet Move does not snap dragged dialog back through ShowNear");
                Call(pet,"FinishChatGroupDrag");Check(!(bool)Field(pet,"chatGroupDragging")&&form.PinnedOpen,"release clears linkage and leaves chat open");
                // An oversized old arrangement is compacted onto the destination display on release.
                Call(pet,"BeginChatGroupDrag");pet.Location=new Point(live.Left-1000,live.Top-1000);Call(pet,"FinishChatGroupDrag");Check(live.Contains(pet.Bounds)&&live.Contains(form.Bounds),"release repairs off-screen pair and keeps title/pet visible");
                Call(pet,"BeginChatGroupDrag");Call(pet,"HidePet");Call(pet,"FinishChatGroupDrag");Check(!pet.Visible&&!form.Visible,"hiding during drag never reopens pet or conversation");
                pet.Close();
            }
            Check(File.ReadAllText(legacyFile)=="{ malformed legacy file intentionally ignored","legacy preferences are ignored and never deleted or rewritten");
            Console.WriteLine("Chat drag tests passed; no real Claude calls or user settings changed.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
