using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed partial class PetForm
    {
        private readonly Timer visibilityTimer = new Timer();
        private bool userHidden;
        private bool closingPet;
        private bool applyingDesktopScope;
        private bool petOnSelectedDesktop = true;
        private string desktopScope = "all";
        private string desktopScopeError = "";
        private ToolStripMenuItem showPetItem, hidePetItem, desktopOneItem, desktopAllItem;
        private Size cachedCanvasSize;
        private readonly int taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterWindowMessage(string name);

        internal static Size PetCanvasSize(float scale)
        {
            if (Single.IsNaN(scale) || Single.IsInfinity(scale)) scale = 0.5f;
            scale = Math.Max(0.5f, Math.Min(1.25f, scale));
            return new Size((int)Math.Round(192 * scale), (int)Math.Round(208 * scale));
        }

        internal static Rectangle FitSpriteRectangle(Size source, Size canvas)
        {
            double factor = Math.Min((double)canvas.Width / Math.Max(1, source.Width), (double)canvas.Height / Math.Max(1, source.Height));
            int width = Math.Max(1, (int)Math.Round(source.Width * factor));
            int height = Math.Max(1, (int)Math.Round(source.Height * factor));
            return new Rectangle((canvas.Width - width) / 2, (canvas.Height - height) / 2, width, height);
        }

        // An encompassing virtual-screen rectangle can include large empty gaps.
        // Test actual work areas, including monitors left/above the primary one.
        internal static bool HasVisiblePetArea(Rectangle pet, Rectangle[] areas)
        {
            foreach (Rectangle area in areas)
            {
                Rectangle intersection = Rectangle.Intersect(pet, area);
                if (intersection.Width >= Math.Min(24, pet.Width / 3) && intersection.Height >= Math.Min(24, pet.Height / 3)) return true;
            }
            return false;
        }

        private void InitializeDesktopVisibility()
        {
            foreach (Form form in new Form[] { promptForm, replyBubble, quoteBubble, activityTray, reminderForm, claudeTaskForm })
            {
                Form companion = form;
                companion.VisibleChanged += delegate { if (companion.Visible) ApplyWindowDesktopScope(companion); };
            }
            visibilityTimer.Interval = 1000;
            visibilityTimer.Tick += delegate { MaintainPetVisibility(); };
            visibilityTimer.Start();
        }

        private void AddDesktopVisibilityMenu(ContextMenuStrip menu)
        {
            ToolStripMenuItem scope = new ToolStripMenuItem("虚拟桌面显示范围");
            desktopOneItem = new ToolStripMenuItem("仅在桌面 1 显示");
            desktopAllItem = new ToolStripMenuItem("在所有桌面显示");
            desktopOneItem.Click += delegate { SelectDesktopScope("first"); };
            desktopAllItem.Click += delegate { SelectDesktopScope("all"); };
            scope.DropDownItems.Add(desktopOneItem);
            scope.DropDownItems.Add(desktopAllItem);
            menu.Items.Add(scope);
            UpdateVisibilityMenu();
        }

        private void UpdateVisibilityMenu()
        {
            // Always provide a recovery action even if Windows cloaks the HWND
            // while WinForms still reports Visible=true.
            if (showPetItem != null) showPetItem.Enabled = true;
            if (hidePetItem != null) hidePetItem.Enabled = !userHidden;
            if (desktopOneItem != null) desktopOneItem.Checked = desktopScope == "first";
            if (desktopAllItem != null) desktopAllItem.Checked = desktopScope == "all";
        }

        private void SelectDesktopScope(string scope)
        {
            string previous = desktopScope;
            desktopScope = scope;
            if (!ApplyWindowDesktopScope(this))
            {
                string failure = desktopScopeError;
                desktopScope = previous;
                ApplyWindowDesktopScope(this);
                MessageBox.Show("无法设置虚拟桌面显示范围：" + failure, Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else SavePreferences();
            ApplyCompanionDesktopScope();
            UpdateVisibilityMenu();
        }

        private bool ApplyWindowDesktopScope(Form form)
        {
            if (closingPet || form == null || form.IsDisposed || !form.IsHandleCreated || applyingDesktopScope) return false;
            applyingDesktopScope = true;
            try
            {
                string pinError;
                if (desktopScope == "all")
                {
                    // Pin this view only: never pin all application windows or
                    // any user's Claude/Terminal windows by application ID.
                    if (!VirtualDesktopService.TrySetWindowPinned(form.Handle, true, out pinError))
                        VirtualDesktopService.MoveWindow(form.Handle, VirtualDesktopService.GetCurrentDesktop());
                }
                else
                {
                    VirtualDesktopService.TrySetWindowPinned(form.Handle, false, out pinError);
                    // If this Windows has no pin interface, the public move API
                    // still works. MoveWindow verifies the real desktop GUID;
                    // a view that stayed pinned cannot falsely pass the check.
                    VirtualDesktopService.MoveWindow(form.Handle, VirtualDesktopService.ResolveDesktop(1));
                }
                desktopScopeError = "";
                return true;
            }
            catch (Exception ex) { desktopScopeError = ex.Message; return false; }
            finally { applyingDesktopScope = false; }
        }

        private void ApplyCompanionDesktopScope()
        {
            foreach (Form form in new Form[] { promptForm, replyBubble, quoteBubble, activityTray, reminderForm, claudeTaskForm })
                if (form.Visible) ApplyWindowDesktopScope(form);
        }

        private void EnsurePetOnMonitor()
        {
            Rectangle[] areas = Array.ConvertAll(Screen.AllScreens, delegate(Screen screen) { return screen.WorkingArea; });
            if (!HasVisiblePetArea(Bounds, areas))
            {
                CancelWander();
                Rectangle nearest = Screen.FromRectangle(Bounds).WorkingArea;
                Location = new Point(Math.Max(nearest.Left, Math.Min(Left, nearest.Right - Width)), Math.Max(nearest.Top, Math.Min(Top, nearest.Bottom - Height)));
                if (!String.IsNullOrEmpty(dockedEdge)) ApplyEdgeDock(nearest);
                wanderOrigin = Location;
                AnchorAuxiliaryWindows();
            }
        }

        private void ShowPet()
        {
            userHidden = false;
            if (reminderOverlay != null) { petWasVisibleBeforeReminder = true; return; }
            EnsurePetOnMonitor();
            WindowState = FormWindowState.Normal;
            Show();
            ApplyWindowDesktopScope(this);
            RefreshPetVisual();
            ScheduleNextWander();
            UpdateVisibilityMenu();
            if (desktopScope == "first")
            {
                try
                {
                    if (!VirtualDesktopService.IsWindowOnCurrentDesktop(Handle))
                        tray.ShowBalloonTip(2500, Program.ProductName, "宠物已显示在桌面 1；可按 Win+Tab 切换，或改为所有桌面显示。", ToolTipIcon.Info);
                }
                catch { }
            }
        }

        private void MaintainPetVisibility()
        {
            if (closingPet || IsDisposed || !IsHandleCreated) return;
            UpdateVisibilityMenu();
            if (userHidden || reminderOverlay != null) return;
            // Repair unexpected hiding/minimization without reopening an
            // intentionally hidden pet, its bubbles, or the user's dialogs.
            if (!Visible) Show();
            if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
            Size expected = PetCanvasSize(petScale);
            if (ClientSize != expected) SetPetScale(petScale, false);
            EnsurePetOnMonitor();
            ApplyWindowDesktopScope(this);
            ApplyCompanionDesktopScope();
            try { petOnSelectedDesktop = desktopScope == "all" || VirtualDesktopService.IsWindowOnCurrentDesktop(Handle); }
            catch { petOnSelectedDesktop = true; }
            if (!petOnSelectedDesktop) CancelWander();
            if (!layeredRenderSucceeded) RefreshPetVisual();
        }
    }
}
