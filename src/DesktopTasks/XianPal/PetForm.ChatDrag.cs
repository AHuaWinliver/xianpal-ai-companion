// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed partial class PetForm
    {
        private bool chatGroupDragging,chatDragLinked;
        private bool placingConversation;
        private Rectangle chatDragDialog,chatDragPet,chatDragArea;
        private void InitializeChatGroupDrag()
        {
            activityTray.HeaderDragStarted+=BeginChatGroupDrag;
            activityTray.HeaderDragLocationRequested+=MoveChatGroupDrag;
            activityTray.HeaderDragFinished+=FinishChatGroupDrag;
            activityTray.CompanionPositionRequested+=PlacePetAboveConversation;
        }
        private void PlacePetAboveConversation(Point location)
        {
            if(closingPet||IsDisposed||userHidden||!Visible||reminderOverlay!=null||Location==location)return;
            placingConversation=true;
            try
            {
                CancelWander();ReleaseEdgeDock();Location=location;wanderOrigin=Location;
                RefreshPetVisual();SavePreferences();
            }
            finally{placingConversation=false;}
        }
        private void BeginChatGroupDrag()
        {
            chatGroupDragging=true;
            chatDragLinked=Visible&&!userHidden&&reminderOverlay==null;
            if(chatDragLinked){CancelWander();ReleaseEdgeDock();replyBubble.Hide();quoteBubble.Hide();}
            chatDragDialog=activityTray.Bounds;chatDragPet=Bounds;
            chatDragArea=Screen.FromRectangle(activityTray.Bounds).WorkingArea;
        }
        private Point MoveChatGroupDrag(Point requested)
        {
            chatDragArea=Screen.FromPoint(Cursor.Position).WorkingArea;
            return MoveChatGroupToArea(requested,chatDragArea);
        }
        private Point MoveChatGroupToArea(Point requested,Rectangle area)
        {
            if(!chatGroupDragging)return activityTray.Location;
            chatDragArea=area;
            Point actual=ChatWindowDrag.Constrain(chatDragDialog,chatDragLinked?(Rectangle?)chatDragPet:null,requested,area);
            if(chatDragLinked)
            {
                // A single translation preserves the relative offset and suppresses the
                // pet's normal Move -> ShowNear cycle, which would otherwise snap back.
                Location=new Point(chatDragPet.X+actual.X-chatDragDialog.X,chatDragPet.Y+actual.Y-chatDragDialog.Y);
                RefreshPetVisual();
            }
            return actual;
        }
        private void FinishChatGroupDrag()
        {
            if(!chatGroupDragging)return;
            if(closingPet||IsDisposed||userHidden||!Visible||reminderOverlay!=null){chatGroupDragging=false;chatDragLinked=false;return;}
            try
            {
                if(chatDragLinked)
                {
                    if(!chatDragArea.Contains(Bounds)||!chatDragArea.Contains(activityTray.Bounds))
                    {
                        // If the group is larger than the new screen, preserve visibility
                        // rather than leaving a pet/title outside the monitor.
                        Location=new Point(Math.Max(chatDragArea.Left,Math.Min(Left,chatDragArea.Right-Width)),Math.Max(chatDragArea.Top,Math.Min(Top,chatDragArea.Bottom-Height)));
                        activityTray.ShowNear(Bounds);
                    }
                    wanderOrigin=Location;ScheduleNextWander();SavePreferences();
                }
            }
            finally{chatGroupDragging=false;chatDragLinked=false;}
        }
    }
}
