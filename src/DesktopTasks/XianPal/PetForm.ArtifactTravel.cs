// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;

namespace XianPal
{
    internal sealed partial class PetForm
    {
        private readonly ArtifactTravelClock artifactTravel=new ArtifactTravelClock();
        private Bitmap artifactCanvas;
        private Graphics artifactGraphics;
        private Image artifactFootSource;
        private float artifactFootRow;
        private void PrepareArtifactTravel()
        {
            artifactTravel.Reset();artifactFootSource=null;
            if(reduceMotion||wanderPath.Count==0)return;
            Rectangle area=wanderWorkingArea.IsEmpty?System.Windows.Forms.Screen.FromRectangle(Bounds).WorkingArea:wanderWorkingArea;
            if(ArtifactTravelClock.RouteLength(Location,wanderPath)>=ArtifactTravelClock.Threshold(area))artifactTravel.Start();
        }
        private bool TryRenderArtifactTravel()
        {
            if(!artifactTravel.Active||reduceMotion)return false;
            List<Image> idle;if(!animations.TryGetValue("idle",out idle)||idle.Count==0)return false;
            Image rider=idle[0];
            if(!Object.ReferenceEquals(rider,artifactFootSource)){artifactFootSource=rider;artifactFootRow=ArtifactTravelArt.FootRow(rider);}
            if(artifactCanvas==null||artifactCanvas.Size!=ClientSize)
            {DisposeArtifactCanvas();artifactCanvas=new Bitmap(Math.Max(1,Width),Math.Max(1,Height),PixelFormat.Format32bppArgb);artifactGraphics=Graphics.FromImage(artifactCanvas);}
            PetCharacter character=PetCharacter.Find(currentCharacterId);
            ArtifactTravelArt.Render(artifactGraphics,ClientSize,rider,artifactFootRow,character.Id,character.ReminderStyle,artifactTravel.Envelope,artifactTravel.Seconds,wanderFacingLeft);
            AddInputHitHalo(artifactCanvas);PushLayeredBitmap(artifactCanvas);return true;
        }
        private void DisposeArtifactCanvas(){if(artifactGraphics!=null){artifactGraphics.Dispose();artifactGraphics=null;}if(artifactCanvas!=null){artifactCanvas.Dispose();artifactCanvas=null;}}
    }
}
