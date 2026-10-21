using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using XianPal;

internal static class DesktopVisibilityTests
{
    private static object Field(object value, string name) { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }
    private static void Set(object value, string name, object item) { value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, item); }
    private static object Call(object value, string name, params object[] args) { return value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args); }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static void Pump(int milliseconds) { DateTime end = DateTime.UtcNow.AddMilliseconds(milliseconds); while (DateTime.UtcNow < end) { Application.DoEvents(); Thread.Sleep(15); } }

    [STAThread]
    private static int Main()
    {
        try
        {
            Environment.SetEnvironmentVariable("FUFU_TEST_MODE", "1");
            Environment.SetEnvironmentVariable("FUFU_STATE_DIR", System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "desktop-visibility-test", Guid.NewGuid().ToString("N")));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Rectangle[] screens = { new Rectangle(0, 0, 1920, 1040), new Rectangle(2560, -800, 1280, 1024), new Rectangle(-1280, 0, 1280, 1024) };
            Require(!PetForm.HasVisiblePetArea(new Rectangle(2100, 200, 96, 104), screens), "monitor gap is not a visible desktop");
            Require(PetForm.HasVisiblePetArea(new Rectangle(-300, 250, 96, 104), screens), "negative-coordinate monitor accepted");
            Require(PetForm.HasVisiblePetArea(new Rectangle(-40, 250, 96, 104), screens), "edge dock remains accepted");
            Rectangle fit = PetForm.FitSpriteRectangle(new Size(384, 416), new Size(180, 104));
            Require(fit.Size == new Size(96, 104) && fit.Left == 42, "unexpected wide canvas letterboxed without fat character");
            Require(PetForm.PetCanvasSize(Single.NaN) == new Size(96, 104), "invalid saved scale recovered");
            using (PetForm pet = new PetForm())
            {
                Require(pet.ClientSize == new Size(96, 104), "fresh startup dimensions before HWND");
                Set(pet, "reduceMotion", true);
                pet.Show(); Pump(350);
                Require(pet.ClientSize == new Size(96, 104) && (Size)Field(pet, "cachedCanvasSize") == pet.ClientSize && pet.LayeredRenderSucceeded, "first shown frame uses exact current-size bitmap");
                Require((string)Field(pet, "desktopScopeError") == "", "all-desktop pin or current-desktop fallback succeeds on this Windows");
                string pinError;
                Require(VirtualDesktopService.TrySetWindowPinned(pet.Handle, true, out pinError), "Explorer per-window pin confirmed");
                foreach (float scale in new[] { 0.75f, 1f, 1.25f, 0.5f })
                {
                    Call(pet, "SetPetScale", scale, false); Pump(100);
                    Require(pet.ClientSize == PetForm.PetCanvasSize(scale) && (Size)Field(pet, "cachedCanvasSize") == pet.ClientSize && pet.LayeredRenderSucceeded, "resize " + scale + " retains canvas and cache proportions");
                }
                ToolStripMenuItem show = (ToolStripMenuItem)Field(pet, "showPetItem"), hide = (ToolStripMenuItem)Field(pet, "hidePetItem");
                Require(show.Text == "显示宠物" && hide.Text == "隐藏宠物", "shared pet/tray menu contains explicit show/hide");
                hide.PerformClick(); Pump(1200);
                Require(!pet.Visible && (bool)Field(pet, "userHidden") && show.Enabled, "intentional hiding survives visibility watchdog");
                ReplyBubbleForm bubble = (ReplyBubbleForm)Field(pet, "replyBubble");
                bubble.AppendReply("隐藏期间的新回复"); Call(pet, "ShowReplyBubble", false); Pump(100);
                Require(!bubble.Visible && !pet.Visible, "Claude reply does not reopen hidden pet or bubble");
                show.PerformClick(); Pump(150);
                Require(pet.Visible && !(bool)Field(pet, "userHidden") && !((PromptForm)Field(pet, "promptForm")).Visible, "tray show restores pet without opening chat");
                pet.Hide(); Call(pet, "MaintainPetVisibility");
                Require(pet.Visible, "unexpected hide repaired");
                pet.ClientSize = new Size(160, 104); Call(pet, "MaintainPetVisibility");
                Require(pet.ClientSize == new Size(96, 104) && (Size)Field(pet, "cachedCanvasSize") == pet.ClientSize, "unexpected DPI/size change normalized");
                int taskbarMessage = (int)Field(pet, "taskbarCreatedMessage");
                object[] message = { Message.Create(pet.Handle, taskbarMessage, IntPtr.Zero, IntPtr.Zero) };
                pet.GetType().GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pet, message); Pump(150);
                Require(((NotifyIcon)Field(pet, "tray")).Visible, "TaskbarCreated notification re-registers own tray icon");
                pet.WindowState = FormWindowState.Minimized; Call(pet, "MaintainPetVisibility");
                Require(pet.WindowState == FormWindowState.Normal && pet.Visible, "unexpected minimization repaired");
                pet.Location = new Point(100000, 100000); Call(pet, "MaintainPetVisibility");
                Rectangle[] actual = Array.ConvertAll(Screen.AllScreens, delegate(Screen screen) { return screen.WorkingArea; });
                Require(PetForm.HasVisiblePetArea(pet.Bounds, actual), "removed monitor/off-screen position recovered");
                Call(pet, "SetCharacter", "pippin", false); Pump(100);
                Require(pet.LayeredRenderSucceeded && (Size)Field(pet, "cachedCanvasSize") == pet.ClientSize, "character switch retains correct canvas");
                CheckWalkingExpressions(pet);
                Call(pet, "QueueReminder", new DailyReminder { Text = "可见性回归测试", Effect = "popup" });
                Call(pet, "MaintainPetVisibility");
                Require(!pet.Visible && Field(pet, "reminderOverlay") != null, "watchdog does not display a second pet during reminder overlay");
                Call(pet, "FinishReminder");
                Require(pet.Visible, "reminder dismissal restores visible pet");
                Call(pet, "QueueReminder", new DailyReminder { Text = "隐藏回归测试", Effect = "popup" });
                Call(pet, "HidePet"); Call(pet, "FinishReminder"); Call(pet, "MaintainPetVisibility");
                Require(!pet.Visible, "hiding during reminder is respected after dismissal");
                Call(pet, "ShowPet");
                Set(pet, "desktopScope", "first");
                Require((bool)Call(pet, "ApplyWindowDesktopScope", pet), "desktop 1 move and unpin accepted");
                Require(VirtualDesktopService.GetWindowDesktop(pet.Handle) == VirtualDesktopService.ResolveDesktop(1), "desktop 1 GUID verified (no desktop switch/create)");
                Set(pet, "desktopScope", "all");
                Require((bool)Call(pet, "ApplyWindowDesktopScope", pet), "switch back to all desktops accepted");
                Require(VirtualDesktopService.TrySetWindowPinned(pet.Handle, false, out pinError), "test unpins only own window before disposal");
                pet.Close();
            }
            Console.WriteLine("PASS all visibility/geometry tests; no real Claude invocation, user preferences or other windows modified.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void CheckWalkingExpressions(PetForm pet)
    {
        foreach(string id in new[]{"orbit","spark","moss","pippin"})
        {
            Call(pet,"SetCharacter",id,false);
            var patches=(System.Collections.IDictionary)Field(pet,"walkingExpressionPatches");
            Require(patches.Count==6,id+" loads six local face patches");
            object clock=Field(pet,"walkingExpressionClock");
            foreach(string direction in new[]{"running-left","running-right"})
            {
                Set(pet,"state",direction);
                var animations=(System.Collections.Generic.Dictionary<string,System.Collections.Generic.List<Image>>)Field(pet,"animations");
                for(int f=0;f<24;f++)for(int level=1;level<=3;level++)
                {
                    Set(pet,"frameIndex",f);
                    Call(pet,"PushWalkingExpression",animations[direction][f],level,level);
                    if(!pet.LayeredRenderSucceeded)throw new Exception("Expression layered render failed: "+id);
                }
            }
            var cache=(System.Collections.IDictionary)Field(pet,"walkingExpressionCache");
            Require(cache.Count==72,id+" expressions render both directions with bounded bitmap cache");
            Call(pet,"SetPetScale",pet.ClientSize==PetForm.PetCanvasSize(.75f)?1f:.75f,false);
            Require(((System.Collections.IDictionary)Field(pet,"walkingExpressionCache")).Count<=1&&Object.ReferenceEquals(clock,Field(pet,"walkingExpressionClock")),id+" resize clears face handles without resetting expression cadence");
            Set(pet,"state","idle");
            var states=(System.Collections.Generic.Dictionary<string,System.Collections.Generic.List<Image>>)Field(pet,"animations");
            Require(!(bool)Call(pet,"TryPushWalkingExpression",states["idle"][0]),id+" standing stops walking expressions");
            Call(pet,"RenderCurrentFrame");
        }
        Call(pet,"SetPetScale",.5f,false);
    }
}
