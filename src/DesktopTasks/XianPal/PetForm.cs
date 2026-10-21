using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed partial class PetForm : Form
    {
        private readonly Dictionary<string, List<Image>> animations = new Dictionary<string, List<Image>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<IntPtr>> layeredFrameCache = new Dictionary<string, List<IntPtr>>(StringComparer.OrdinalIgnoreCase);
        private readonly Timer timer = new Timer();
        private readonly Timer clickDelayTimer = new Timer();
        private readonly Timer fileWipeTimer = new Timer();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly ContextMenuStrip clickActionsMenu = new ContextMenuStrip();
        private readonly Random random = new Random();
        private readonly ClaudeCliRunner claudeRunner = new ClaudeCliRunner();
        private readonly PromptForm promptForm = new PromptForm();
        private readonly ReplyBubbleForm replyBubble = new ReplyBubbleForm();
        private readonly PetQuoteForm quoteBubble = new PetQuoteForm();
        private readonly ActivityTrayForm activityTray = new ActivityTrayForm();
        private readonly ReminderService reminderService = new ReminderService();
        private readonly ReminderForm reminderForm;
        private readonly ClaudeTaskService claudeTaskService = new ClaudeTaskService();
        private readonly ClaudeTaskForm claudeTaskForm;
        private readonly TrailOverlayForm trailOverlay = new TrailOverlayForm();
        private readonly List<ActivityRecord> activities = new List<ActivityRecord>();
        private readonly Queue<Point> wanderPath = new Queue<Point>();
        private readonly Queue<PetPathPlan> aiPathPool = new Queue<PetPathPlan>();
        private readonly Queue<DailyReminder> reminderQueue = new Queue<DailyReminder>();
        private readonly Queue<ScheduledClaudeTask> scheduledClaudeTaskQueue = new Queue<ScheduledClaudeTask>();
        private readonly List<ToolStripMenuItem> characterMenuItems = new List<ToolStripMenuItem>();
        private string loadedCharacterRevision = "";
        private readonly List<VisibleTaskMonitor> visibleTaskMonitors = new List<VisibleTaskMonitor>();
        private readonly IntegrationPreferences integrationPreferences = IntegrationPreferences.Load();
        private readonly Dictionary<string, int> activeScheduledTaskCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private string state = "idle";
        private string currentCharacterId = "orbit";
        private string fallbackState = "idle";
        private int frameIndex;
        private DateTime stateUntil = DateTime.MaxValue;
        private DateTime nextIdleAction = DateTime.UtcNow.AddSeconds(8);
        private DateTime nextWander = DateTime.UtcNow.AddSeconds(18);
        private DateTime lastEventWrite = DateTime.MinValue;
        private DateTime lastAnimationTick = DateTime.UtcNow;
        private double animationAccumulatorMs;
        private bool claudeBusy;
        private bool paused;
        private bool dragging;
        private bool dragMoved;
        private bool reduceMotion;
        private bool hovering;
        private bool playfulRun;
        private bool brainRequestActive;
        private bool wanderFacingLeft;
        private readonly StringBuilder brainResponse = new StringBuilder();
        private string brainCharacterId = "orbit";
        private Color[] trailColors = PetBrain.DefaultColors("orbit");
        private string trailStyle = "rainbow";
        private float trailOpacity = 0.42f;
        private string movementPathMode = "builtin";
        private string aiPathCurve = "smooth";
        private PointF[] aiPathControlPoints = new PointF[0];
        private bool aiCareEnabled = true;
        private int breakReminderMinutes = 60;
        private bool screenSessionActive = true;
        private DateTime screenSessionStartedAt = DateTime.UtcNow;
        private DateTime nextBreakReminderAt = DateTime.UtcNow.AddMinutes(60);
        private DateTime nextAiChatterAt = DateTime.UtcNow.AddMinutes(18);
        private DateTime nextAiCareCheckAt = DateTime.MinValue;
        private string brainRequestReason = "doubleclick";
        private int brainScreenMinutes;
        private IntPtr displayStateNotification;
        private string dockedEdge = "";
        private float petScale = 0.5f;
        private Point dragMouse;
        private Point dragWindow;
        private Point lastDragCursor;
        private DateTime lastDragFrameAt;
        private DateTime lastDragMotionAt;
        private double dragFrameDistance;
        private double locomotionSpeedPixelsPerSecond;
        private Point defaultLocation;
        private Point wanderOrigin;
        private Rectangle wanderWorkingArea;
        private ActivityRecord currentActivity;
        private ActivityRecord lastReplyActivity;
        private ScheduledClaudeTask currentScheduledClaudeTask;
        private string currentScheduledRunId;
        private ReminderOverlayForm reminderOverlay;
        private FileEatOverlayForm fileEatOverlay;
        private string[] pendingDroppedPaths = new string[0];
        private bool petWasVisibleBeforeReminder;
        private bool layeredRenderSucceeded;
        private int layeredRenderError;
        private int layeredRenderCount;
        private ToolStripMenuItem pauseItem;
        private ToolStripMenuItem startupItem;
        private ToolStripMenuItem topmostItem;
        private ToolStripMenuItem reduceMotionItem;
        private const int HotkeyId = 0x4B41;
        private const int WmHotkey = 0x0312;
        private const int WmPowerBroadcast = 0x0218;
        private const int PbtPowerSettingChange = 0x8013;
        private const int DeviceNotifyWindowHandle = 0;
        private const uint ModAlt = 0x0001;
        private const uint ModWin = 0x0008;
        private const int GwlExStyle = -20;
        private const int WsExLayered = 0x00080000;
        private const int UlwAlpha = 0x00000002;
        private const byte AcSrcAlpha = 0x01;
        private static readonly Guid ConsoleDisplayStateGuid = new Guid("6FE69556-704A-47A0-8F24-C28D936FDA47");
        internal const int AnimationFrameIntervalMs = 40;
        internal const int LocomotionFrameIntervalMs = 28;
        internal const int LocomotionFastFrameIntervalMs = 17;
        internal const int SmoothInteractionFrameIntervalMs = 16;
        internal const int AnimationTimerIntervalMs = 15;
        internal const int AiPathBatchSize = 24;
        internal const int AiPathRefillThreshold = 4;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction
        {
            internal byte BlendOp;
            internal byte BlendFlags;
            internal byte SourceConstantAlpha;
            internal byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            internal int X;
            internal int Y;
            internal NativePoint(int x, int y) { X = x; Y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize
        {
            internal int Width;
            internal int Height;
            internal NativeSize(int width, int height) { Width = width; Height = height; }
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr handle, int id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr handle, int index);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr handle, int index, int value);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr handle, IntPtr destinationDc, ref NativePoint destination, ref NativeSize size, IntPtr sourceDc, ref NativePoint source, int colorKey, ref BlendFunction blend, int flags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr handle, IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr value);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr dc);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr RegisterPowerSettingNotification(IntPtr recipient, ref Guid powerSettingGuid, int flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterPowerSettingNotification(IntPtr handle);

        internal PetForm()
        {
            Text = Program.ProductName;
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(192, 208);
            BackColor = Color.Black;
            TransparencyKey = Color.Empty;
            TopMost = true;
            AllowDrop = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            InitializeCharacterPacks();
            LoadAnimations();
            reminderForm = new ReminderForm(reminderService);
            claudeTaskForm = new ClaudeTaskForm(claudeTaskService);
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            defaultLocation = new Point(area.Right - Width - 32, area.Bottom - Height - 24);
            LoadPreferences();
            BuildTrayMenu();
            BuildClickActionsMenu();
            InitializeDesktopVisibility();
            RestoreClaudeBackgroundTasks();
            promptForm.PromptSubmitted += RunClaudePrompt;
            promptForm.ActivityRequested += ShowActivities;
            promptForm.ReminderRequested += ShowReminderSettings;
            activityTray.ActivityViewed += ApplyActivityPriority;
            activityTray.ReplySubmitted += RunClaudeReply;
            replyBubble.ConversationRequested += OpenReplyConversation;
            reminderService.ReminderDue += QueueReminder;
            claudeTaskService.TaskDue += QueueScheduledClaudeTask;
            claudeRunner.SessionIdentified += delegate(string sessionId)
            {
                RunOnUi(delegate
                {
                    if (currentActivity == null) return;
                    currentActivity.SessionId = sessionId;
                    currentActivity.Updated = DateTime.Now;
                    RefreshActivities();
                });
            };
            claudeRunner.OutputReceived += delegate(string text)
            {
                RunOnUi(delegate
                {
                    if (brainRequestActive)
                    {
                        brainResponse.Append(text);
                        return;
                    }
                    replyBubble.AppendReply(text);
                    // Streaming output can arrive many times per frame. Only the
                    // first chunk needs to show and position the window; the
                    // bubble itself coalesces subsequent text paints.
                    if (!replyBubble.Visible && !replyBubble.UserDismissed) ShowReplyBubble();
                    if (currentActivity != null)
                    {
                        currentActivity.AppendOutput(text);
                        currentActivity.Updated = DateTime.Now;
                        if (currentScheduledClaudeTask != null) claudeTaskService.UpdateRunProgress(currentScheduledClaudeTask, currentScheduledRunId, currentActivity.Output);
                        RefreshActivities();
                    }
                });
            };
            claudeRunner.StatusChanged += delegate(string text)
            {
                RunOnUi(delegate
                {
                    string brainStatus = brainRequestReason == "history-today"
                        ? PetCharacter.Find(currentCharacterId).Name + " 正在整理今天的重要事件…"
                        : (brainRequestReason == "break" ? PetCharacter.Find(currentCharacterId).Name + " 正在准备休息提醒…" : PetCharacter.Find(currentCharacterId).Name + " 正在组织想说的话…");
                    replyBubble.SetStatus(brainRequestActive ? brainStatus : text);
                    if (claudeRunner.IsRunning) ShowReplyBubble();
                    if (currentActivity != null) { currentActivity.State = "正在运行"; currentActivity.Updated = DateTime.Now; RefreshActivities(); }
                });
            };
            claudeRunner.ReactionChanged += delegate(string reaction)
            {
                RunOnUi(delegate
                {
                    if (currentScheduledClaudeTask != null && reaction != "Stop" && reaction != "StopFailure") SetClaudeEvent(reaction);
                });
            };
            claudeRunner.Completed += delegate(int code)
            {
                RunOnUi(delegate
                {
                    if (brainRequestActive)
                    {
                        FinishPetBrainInteraction(code);
                        TryRunNextScheduledClaudeTask();
                        return;
                    }
                    replyBubble.FlushPendingReply();
                    if (currentScheduledClaudeTask != null)
                    {
                        string summary = currentActivity == null ? "" : currentActivity.Output;
                        claudeTaskService.CompleteRun(currentScheduledClaudeTask, currentScheduledRunId, code == 0, code, summary, DateTime.Now);
                        MarkScheduledTaskFinished(currentScheduledClaudeTask);
                    }
                    if (code == 0)
                    {
                        replyBubble.SetStatus("Claude 已完成");
                        CompleteCurrentActivity("就绪");
                        SetClaudeEvent(activeScheduledTaskCounts.Count > 0 ? "UserPromptSubmit" : "Stop");
                    }
                    else
                    {
                        replyBubble.SetStatus("Claude CLI 已退出，代码 " + code.ToString());
                        CompleteCurrentActivity("受阻");
                        SetClaudeEvent(activeScheduledTaskCounts.Count > 0 ? "UserPromptSubmit" : "StopFailure");
                    }
                    if (currentScheduledClaudeTask != null) currentScheduledClaudeTask = null;
                    currentScheduledRunId = null;
                    TryRunNextScheduledClaudeTask();
                });
            };

            // A 15 ms scheduler keeps window movement smooth. Gait cadence is
            // calculated independently from actual travel speed and gait phase.
            timer.Interval = AnimationTimerIntervalMs;
            timer.Tick += TickAnimation;
            timer.Start();
            clickDelayTimer.Interval = SystemInformation.DoubleClickTime;
            // A normal click is character interaction only. Feature actions belong
            // exclusively to the right-click menu so they never interrupt play.
            clickDelayTimer.Tick += delegate { clickDelayTimer.Stop(); ShowRandomQuote(); };
            fileWipeTimer.Interval = 1650;
            fileWipeTimer.Tick += delegate { fileWipeTimer.Stop(); OpenDroppedFilesPromptAfterWipe(); };
            reminderService.Start();
            claudeTaskService.Start();
            SystemEvents.SessionSwitch += SystemSessionSwitch;
            SystemEvents.PowerModeChanged += SystemPowerModeChanged;
            ResetScreenSession(true);

            MouseDown += BeginDrag;
            MouseMove += ContinueDrag;
            MouseUp += EndDrag;
            MouseDoubleClick += PetMouseDoubleClick;
            MouseEnter += PetMouseEnter;
            MouseLeave += PetMouseLeave;
            DragEnter += PetDragEnter;
            DragOver += PetDragOver;
            DragLeave += PetDragLeave;
            DragDrop += PetDragDrop;
            Move += delegate { if (!dragging) AnchorAuxiliaryWindows(); };
            FormClosing += delegate { SavePreferences(); DisposeResources(); };
            SetTemporaryState("waving", 2200, "idle");
            StartCharacterPackWatching();
        }

        internal static bool ValidateAssets(string baseDirectory)
        {
            string root = Path.Combine(baseDirectory, "assets");
            string[] required = { "idle", "running-left", "running-right", "waving", "jumping", "failed", "waiting", "running", "review", "punching", "eating", "wiping-mouth" };
            foreach (string name in required)
            {
                string dir = Path.Combine(root, name);
                if (!Directory.Exists(dir) || Directory.GetFiles(dir, "*.png").Length == 0)
                    return false;
            }
            foreach (PetCharacter character in PetCharacter.All)
            {
                if (character.Id == "orbit") continue;
                string characterRoot = Path.Combine(root, "characters", character.Id);
                foreach (string name in required)
                {
                    string stateRoot = Path.Combine(characterRoot, name);
                    int expectedFrames = IsSmoothInteractionState(name) || IsLocomotionState(name) || name == "running" ? 24 : 8;
                    if (!Directory.Exists(stateRoot) || Directory.GetFiles(stateRoot, "*.png").Length != expectedFrames) return false;
                }
            }
            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            RenderCurrentFrame();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            SetPetScale(petScale, false);
            EnsurePetOnMonitor();
            ApplyWindowDesktopScope(this);
            RenderCurrentFrame();
        }

        private void RenderCurrentFrame()
        {
            if (animations.Count == 0) return;
            List<Image> frames;
            if (!animations.TryGetValue(state, out frames) || frames.Count == 0)
                frames = animations["idle"];
            if (TryPushWalkingExpression(frames[frameIndex % frames.Count])) return;
            List<IntPtr> cached;
            string cacheState = animations.ContainsKey(state) ? state : "idle";
            if (cachedCanvasSize == ClientSize && layeredFrameCache.TryGetValue(cacheState, out cached) && cached.Count == frames.Count && cached.Count > 0)
            {
                int index = frameIndex % cached.Count;
                if (cached[index] == IntPtr.Zero)
                    using (Bitmap surface = RenderSpriteBitmap(frames[index], ClientSize)) cached[index] = surface.GetHbitmap(Color.FromArgb(0));
                PushLayeredHandle(cached[index], ClientSize);
                return;
            }
            using (Bitmap surface = RenderSpriteBitmap(frames[frameIndex % frames.Count], ClientSize)) PushLayeredBitmap(surface);
        }

        private void RefreshPetVisual()
        {
            // Per-pixel-alpha layered windows are not repainted reliably by Invalidate().
            // Submit every animation frame explicitly so movement and pose stay in sync.
            if (IsHandleCreated && !IsDisposed)
                RenderCurrentFrame();
            else
                Invalidate();
        }

        internal static void ClearSpriteCanvas(Graphics graphics)
        {
            if (graphics != null) graphics.Clear(Color.Transparent);
        }

        internal static Bitmap RenderSpriteBitmap(Image image, Size size)
        {
            Bitmap surface = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(surface))
            {
                ClearSpriteCanvas(graphics);
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawImage(image, FitSpriteRectangle(image.Size, surface.Size));
            }
            AddInputHitHalo(surface);
            return surface;
        }

        private static void AddInputHitHalo(Bitmap surface)
        {
            int width = surface.Width, height = surface.Height;
            int radius = Math.Max(3, Math.Min(width, height) / 16);
            bool[,] solid = new bool[width, height];
            bool[,] horizontal = new bool[width, height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    solid[x, y] = surface.GetPixel(x, y).A > 16;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int sourceX = x + dx;
                        if (sourceX >= 0 && sourceX < width && solid[sourceX, y]) { horizontal[x, y] = true; break; }
                    }
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (surface.GetPixel(x, y).A != 0) continue;
                    bool nearCharacter = false;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int sourceY = y + dy;
                        if (sourceY >= 0 && sourceY < height && horizontal[x, sourceY]) { nearCharacter = true; break; }
                    }
                    // SetPixel preserves alpha 1 exactly. It is invisible in the
                    // layered window, unlike the previous full-canvas brush, while
                    // keeping a generous drag/right-click halo around the silhouette.
                    if (nearCharacter) surface.SetPixel(x, y, Color.FromArgb(1, 0, 0, 0));
                }
        }

        internal bool LayeredRenderSucceeded
        {
            get { return layeredRenderSucceeded; }
        }

        internal int LayeredRenderError
        {
            get { return layeredRenderError; }
        }

        internal bool LayeredStylePresent
        {
            get { return IsHandleCreated && (GetWindowLong(Handle, GwlExStyle) & WsExLayered) != 0; }
        }

        internal bool ValidateLayeredAnimationAdvance()
        {
            if (!IsHandleCreated || !animations.ContainsKey("running-right") || animations["running-right"].Count < 2)
                return false;

            string previousState = state;
            int previousFrame = frameIndex;
            int before = layeredRenderCount;
            state = "running-right";
            frameIndex = 0;
            RefreshPetVisual();
            frameIndex = 1;
            RefreshPetVisual();
            bool advanced = layeredRenderSucceeded && layeredRenderCount >= before + 2;
            state = previousState;
            frameIndex = previousFrame;
            RefreshPetVisual();
            return advanced;
        }

        internal bool ValidateAllCharacterAnimations()
        {
            string original = currentCharacterId;
            try
            {
                foreach (PetCharacter character in PetCharacter.All)
                {
                    SetCharacter(character.Id, false);
                    foreach (string required in new[] { "idle", "waving", "review", "jumping", "waiting", "failed", "running-left", "running-right", "running", "punching", "eating", "wiping-mouth" })
                    {
                        List<Image> frames;
                        if (!animations.TryGetValue(required, out frames) || frames.Count == 0) return false;
                        foreach (Image frame in frames)
                            if (frame.Width != 384 || frame.Height != 416) return false;
                    }
                    if (animations["running-left"].Count != 24 || animations["running-right"].Count != 24) return false;
                    if (walkingExpressionPatches.Count != 6) return false;
                    if (character.Id != "orbit")
                        foreach (string required in new[] { "idle", "waving", "review", "jumping", "waiting", "failed", "running", "punching", "eating", "wiping-mouth" })
                            if (animations[required].Count != (IsSmoothInteractionState(required) || IsLocomotionState(required) || required == "running" ? 24 : 8)) return false;
                    if (!ValidateLayeredFrameCache()) return false;
                }
                return true;
            }
            finally { SetCharacter(original, false); }
        }

        private static bool IsSmoothInteractionState(string stateName)
        {
            return stateName == "waving" || stateName == "review" || stateName == "jumping" || stateName == "punching" || stateName == "eating" || stateName == "wiping-mouth";
        }

        private static bool IsLocomotionState(string stateName)
        {
            return stateName == "running-left" || stateName == "running-right";
        }

        private bool ValidateLayeredFrameCache()
        {
            foreach (KeyValuePair<string, List<Image>> pair in animations)
            {
                List<IntPtr> handles;
                if (!layeredFrameCache.TryGetValue(pair.Key, out handles) || handles.Count != pair.Value.Count) return false;
                foreach (IntPtr handle in handles) if (handle == IntPtr.Zero) return false;
            }
            return true;
        }

        private void PushLayeredBitmap(Bitmap bitmap)
        {
            if (!IsHandleCreated || IsDisposed || bitmap == null) return;
            IntPtr bitmapHandle = bitmap.GetHbitmap(Color.FromArgb(0));
            try { PushLayeredHandle(bitmapHandle, bitmap.Size); }
            finally { DeleteObject(bitmapHandle); }
        }

        private void PushLayeredHandle(IntPtr bitmapHandle, Size bitmapSize)
        {
            if (!IsHandleCreated || IsDisposed || bitmapHandle == IntPtr.Zero) return;
            int extendedStyle = GetWindowLong(Handle, GwlExStyle);
            if ((extendedStyle & WsExLayered) == 0) SetWindowLong(Handle, GwlExStyle, extendedStyle | WsExLayered);
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memoryDc = CreateCompatibleDC(screenDc);
            IntPtr previous = IntPtr.Zero;
            try
            {
                previous = SelectObject(memoryDc, bitmapHandle);
                NativePoint destination = new NativePoint(Left, Top);
                NativePoint source = new NativePoint(0, 0);
                NativeSize size = new NativeSize(bitmapSize.Width, bitmapSize.Height);
                BlendFunction blend = new BlendFunction { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AcSrcAlpha };
                layeredRenderSucceeded = UpdateLayeredWindow(Handle, screenDc, ref destination, ref size, memoryDc, ref source, 0, ref blend, UlwAlpha);
                layeredRenderError = layeredRenderSucceeded ? 0 : Marshal.GetLastWin32Error();
                if (layeredRenderSucceeded) layeredRenderCount++;
            }
            finally
            {
                if (previous != IntPtr.Zero) SelectObject(memoryDc, previous);
                DeleteDC(memoryDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int extendedStyle = GetWindowLong(Handle, GwlExStyle);
            SetWindowLong(Handle, GwlExStyle, extendedStyle | WsExLayered);
            RegisterHotKey(Handle, HotkeyId, ModWin | ModAlt, (uint)Keys.P);
            Guid displayGuid = ConsoleDisplayStateGuid;
            displayStateNotification = RegisterPowerSettingNotification(Handle, ref displayGuid, DeviceNotifyWindowHandle);
            RenderCurrentFrame();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterHotKey(Handle, HotkeyId);
            if (displayStateNotification != IntPtr.Zero)
            {
                UnregisterPowerSettingNotification(displayStateNotification);
                displayStateNotification = IntPtr.Zero;
            }
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == taskbarCreatedMessage && taskbarCreatedMessage != 0 && !closingPet)
            {
                tray.Visible = false;
                tray.Visible = true;
                BeginInvoke((MethodInvoker)MaintainPetVisibility);
            }
            if ((message.Msg == 0x007E || message.Msg == 0x02E0) && !closingPet && IsHandleCreated)
                BeginInvoke((MethodInvoker)MaintainPetVisibility); // display/DPI topology changed
            if (message.Msg == WmPowerBroadcast && message.WParam.ToInt32() == PbtPowerSettingChange && message.LParam != IntPtr.Zero)
            {
                Guid setting = (Guid)Marshal.PtrToStructure(message.LParam, typeof(Guid));
                if (setting == ConsoleDisplayStateGuid)
                {
                    int value = Marshal.ReadInt32(message.LParam, 20);
                    UpdateDisplayState(value != 0);
                }
            }
            if (message.Msg == WmHotkey && message.WParam.ToInt32() == HotkeyId)
            {
                ShowQuickChat();
                return;
            }
            base.WndProc(ref message);
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            int step = 8;
            if (keyData == Keys.Left) { MovePetWithKeyboard(-step, 0); return true; }
            if (keyData == Keys.Right) { MovePetWithKeyboard(step, 0); return true; }
            if (keyData == Keys.Up) { MovePetWithKeyboard(0, -step); return true; }
            if (keyData == Keys.Down) { MovePetWithKeyboard(0, step); return true; }
            if (keyData == Keys.Escape) { ResetPosition(); SavePreferences(); return true; }
            return base.ProcessCmdKey(ref message, keyData);
        }

        private void MovePetWithKeyboard(int dx, int dy)
        {
            ReleaseEdgeDock();
            Rectangle area = Screen.FromRectangle(Bounds).WorkingArea;
            Left = Math.Max(area.Left, Math.Min(Left + dx, area.Right - Width));
            Top = Math.Max(area.Top, Math.Min(Top + dy, area.Bottom - Height));
            if (dx != 0) wanderFacingLeft = dx < 0;
            locomotionSpeedPixelsPerSecond = 150.0;
            SetTemporaryState(KeyboardLocomotionState(dx, wanderFacingLeft), 190, "idle");
            wanderOrigin = Location;
            SavePreferences();
        }

        internal static string KeyboardLocomotionState(int horizontalDelta, bool wasFacingLeft)
        {
            bool facingLeft = horizontalDelta == 0 ? wasFacingLeft : horizontalDelta < 0;
            return facingLeft ? "running-left" : "running-right";
        }

        private void LoadAnimations()
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
            if (!ValidateAssets(AppDomain.CurrentDomain.BaseDirectory))
                throw new InvalidOperationException("宠物动画资源不完整，请重新安装。\n" + root);
            LoadCharacterAnimations(currentCharacterId);
        }

        private void LoadCharacterAnimations(string characterId, CharacterAnimationSet prepared = null)
        {
            PetCharacter character = PetCharacter.Find(characterId);
            CharacterAnimationSet incoming = prepared ?? new CharacterAnimationSet();
            var expressions = new Dictionary<int, Image>();
            Dictionary<string, List<IntPtr>> handles = null;
            try
            {
                if (character.IsExternal)
                {
                    if (prepared == null)
                    {
                        incoming.Dispose();
                        PetCharacter verified = CharacterPackCatalog.ReadPack(character.ResourceRoot, true, out incoming);
                        if (verified.ResourceRevision != character.ResourceRevision) throw new IOException("角色资源已变化，请刷新角色后重试。");
                    }
                }
                else
                {
                    string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
                    if (character.Id != "orbit") root = Path.Combine(root, "characters", character.Id);
                    foreach (string name in CharacterPackCatalog.States)
                    {
                        var frames = new List<Image>(); incoming.Frames.Add(name, frames);
                        string[] files = Directory.GetFiles(Path.Combine(root, name), "*.png"); Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                        foreach (string file in files) frames.Add(LoadDetachedImage(file));
                    }
                    for (int i = 1; i <= 6; i++) expressions.Add(i, LoadDetachedImage(Path.Combine(root, "profile-expressions", i.ToString("00") + ".png")));
                }
                // Stage all resources before releasing the live character. External
                // native frame handles are filled lazily to avoid blocking the UI.
                handles = CreateLayeredFrameCache(incoming.Frames, character.IsExternal);
                DisposeWalkingExpressions(); DisposeLayeredFrameCache();
                foreach (List<Image> existing in animations.Values) foreach (Image image in existing) image.Dispose();
                animations.Clear();
                foreach (var pair in incoming.Frames) animations.Add(pair.Key, pair.Value);
                incoming.Frames.Clear();
                foreach (var pair in handles) layeredFrameCache.Add(pair.Key, pair.Value);
                handles = null; cachedCanvasSize = ClientSize;
                AdoptWalkingExpressions(expressions, character.Id); expressions.Clear();
                loadedCharacterRevision = character.ResourceRevision ?? "";
            }
            finally
            {
                if (incoming != null) incoming.Dispose();
                foreach (Image image in expressions.Values) image.Dispose();
                if (handles != null) foreach (var pair in handles) foreach (IntPtr handle in pair.Value) if (handle != IntPtr.Zero) DeleteObject(handle);
            }
        }

        private Dictionary<string, List<IntPtr>> CreateLayeredFrameCache(IDictionary<string, List<Image>> frames, bool lazy)
        {
            var result = new Dictionary<string, List<IntPtr>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var pair in frames)
                {
                    var list = new List<IntPtr>(); result.Add(pair.Key, list);
                    foreach (Image image in pair.Value)
                        if (lazy) list.Add(IntPtr.Zero);
                        else using (Bitmap surface = RenderSpriteBitmap(image, ClientSize)) list.Add(surface.GetHbitmap(Color.FromArgb(0)));
                }
                return result;
            }
            catch { foreach (var pair in result) foreach (IntPtr handle in pair.Value) if (handle != IntPtr.Zero) DeleteObject(handle); throw; }
        }

        private void RebuildLayeredFrameCache()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            var staged = CreateLayeredFrameCache(animations, PetCharacter.Find(currentCharacterId).IsExternal);
            DisposeLayeredFrameCache(); cachedCanvasSize = ClientSize;
            foreach (var pair in staged) layeredFrameCache.Add(pair.Key, pair.Value);
        }

        private void DisposeLayeredFrameCache()
        {
            DisposeWalkingExpressionCache();
            foreach (List<IntPtr> handles in layeredFrameCache.Values)
                foreach (IntPtr handle in handles)
                    if (handle != IntPtr.Zero) DeleteObject(handle);
            layeredFrameCache.Clear();
        }

        private static Image LoadDetachedImage(string file)
        {
            using (FileStream stream = File.OpenRead(file))
            using (Image source = Image.FromStream(stream)) return new Bitmap(source);
        }

        private static List<Image> MakeFrames(Image[] poses, int[] indexes, bool mirror, bool bob)
        {
            List<Image> frames = new List<Image>();
            for (int index = 0; index < indexes.Length; index++)
            {
                Bitmap frame = new Bitmap(384, 416, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(frame))
                {
                    ClearSpriteCanvas(graphics);
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    int offsetY = bob ? new[] { 0, -2, -3, -1 }[index % 4] : 0;
                    if (mirror)
                    {
                        graphics.TranslateTransform(384, 0);
                        graphics.ScaleTransform(-1, 1);
                    }
                    graphics.DrawImage(poses[indexes[index]], new Rectangle(0, offsetY, 384, 416));
                }
                frames.Add(frame);
            }
            return frames;
        }

        private static List<Image> MakeTweenFrames(Image[] poses, int first, int second, int count, bool mirror, bool bob)
        {
            List<Image> frames = new List<Image>();
            for (int index = 0; index < count; index++)
            {
                double cycle = index / (double)count;
                float mix = (float)(0.5 - (0.5 * Math.Cos(cycle * Math.PI * 2.0)));
                int offsetY = bob ? -(int)Math.Round(Math.Sin(cycle * Math.PI * 2.0) * 2.5) : 0;
                Bitmap frame = new Bitmap(384, 416, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(frame))
                {
                    ClearSpriteCanvas(graphics);
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    if (mirror)
                    {
                        graphics.TranslateTransform(384, 0);
                        graphics.ScaleTransform(-1, 1);
                    }
                    DrawWithOpacity(graphics, poses[first], new Rectangle(0, offsetY, 384, 416), 1f - mix);
                    DrawWithOpacity(graphics, poses[second], new Rectangle(0, offsetY, 384, 416), mix);
                }
                frames.Add(frame);
            }
            return frames;
        }

        private static void DrawWithOpacity(Graphics graphics, Image image, Rectangle destination, float opacity)
        {
            if (opacity <= 0.001f) return;
            ColorMatrix matrix = new ColorMatrix();
            matrix.Matrix33 = Math.Max(0f, Math.Min(1f, opacity));
            using (ImageAttributes attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                graphics.DrawImage(image, destination, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        private void BuildTrayMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem status = new ToolStripMenuItem("Claude Code：已连接");
            status.Enabled = false;
            menu.Items.Add(status);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem quickChat = new ToolStripMenuItem("新建文字对话    Win+Alt+P");
            quickChat.Click += delegate { ShowQuickChat(); };
            menu.Items.Add(quickChat);

            ToolStripMenuItem activity = new ToolStripMenuItem("查看动态");
            activity.Click += delegate { ShowActivities(); };
            menu.Items.Add(activity);

            ToolStripMenuItem historyToday = new ToolStripMenuItem("历史上的今天");
            historyToday.Click += delegate { StartPetBrainInteraction("history-today", 0); };
            menu.Items.Add(historyToday);

            ToolStripMenuItem reminders = new ToolStripMenuItem("提醒与倒计时…");
            reminders.Click += delegate { ShowReminderSettings(); };
            menu.Items.Add(reminders);

            ToolStripMenuItem scheduledTasks = new ToolStripMenuItem("Claude Code 定时任务…");
            scheduledTasks.Click += delegate { ShowClaudeTaskSettings(); };
            menu.Items.Add(scheduledTasks);
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(BuildCharacterMenu(true));

            pauseItem = new ToolStripMenuItem("暂停 Claude 联动");
            pauseItem.Click += delegate
            {
                paused = !paused;
                pauseItem.Checked = paused;
                if (paused) { claudeBusy = false; CancelWander(); SetState("idle"); }
                else TryRunNextScheduledClaudeTask();
            };
            menu.Items.Add(pauseItem);

            startupItem = new ToolStripMenuItem("开机自动启动");
            startupItem.Checked = IsStartupEnabled();
            startupItem.Click += delegate { SetStartup(!startupItem.Checked); startupItem.Checked = IsStartupEnabled(); };
            menu.Items.Add(startupItem);

            topmostItem = new ToolStripMenuItem("保持置顶");
            topmostItem.Checked = TopMost;
            topmostItem.Click += delegate { TopMost = !TopMost; topmostItem.Checked = TopMost; };
            menu.Items.Add(topmostItem);
            AddDesktopVisibilityMenu(menu);

            ToolStripMenuItem sizeMenu = new ToolStripMenuItem("宠物大小");
            foreach (int percent in new[] { 50, 75, 100, 125 })
            {
                int selectedPercent = percent;
                ToolStripMenuItem sizeItem = new ToolStripMenuItem(percent.ToString() + "%");
                sizeItem.Click += delegate { SetPetScale(selectedPercent / 100.0f, true); };
                sizeMenu.DropDownItems.Add(sizeItem);
            }
            ToolStripMenuItem resetSize = new ToolStripMenuItem("重置大小和位置");
            resetSize.Click += delegate { SetPetScale(0.5f, false); ResetPosition(); SavePreferences(); };
            sizeMenu.DropDownItems.Add(new ToolStripSeparator());
            sizeMenu.DropDownItems.Add(resetSize);
            menu.Items.Add(sizeMenu);

            menu.Items.Add(BuildTrailOpacityMenu());
            menu.Items.Add(BuildPathModeMenu());
            menu.Items.Add(BuildAiCareMenu());

            reduceMotionItem = new ToolStripMenuItem("减少动画");
            reduceMotionItem.Checked = reduceMotion;
            reduceMotionItem.Click += delegate
            {
                reduceMotion = !reduceMotion;
                reduceMotionItem.Checked = reduceMotion;
                if (reduceMotion) CancelWander();
                frameIndex = 0;
                SavePreferences();
            };
            menu.Items.Add(reduceMotionItem);

            ToolStripMenuItem openClaude = new ToolStripMenuItem("在 PowerShell 打开 Claude Code CLI");
            openClaude.Click += delegate { OpenClaudeCli(promptForm.WorkingDirectory); };
            menu.Items.Add(openClaude);
            ToolStripMenuItem defaultDirectory = new ToolStripMenuItem("设置 Claude CLI 默认目录…");
            defaultDirectory.Click += delegate { ChooseDefaultClaudeDirectory(); };
            menu.Items.Add(defaultDirectory);
            ToolStripMenuItem externalSessions = new ToolStripMenuItem("收集其他 Claude 会话（默认关闭）");
            externalSessions.Checked = integrationPreferences.MonitorExternalSessions;
            externalSessions.Click += delegate
            {
                bool previous = integrationPreferences.MonitorExternalSessions;
                integrationPreferences.MonitorExternalSessions = !previous;
                try { integrationPreferences.Save(); externalSessions.Checked = !previous; }
                catch (Exception ex)
                {
                    integrationPreferences.MonitorExternalSessions = previous;
                    MessageBox.Show(ex.Message, "保存会话监控设置失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            menu.Items.Add(externalSessions);
            menu.Items.Add(new ToolStripSeparator());

            showPetItem = new ToolStripMenuItem("显示宠物");
            showPetItem.Click += delegate { ShowPet(); };
            menu.Items.Add(showPetItem);
            hidePetItem = new ToolStripMenuItem("隐藏宠物");
            hidePetItem.Click += delegate { HidePet(); };
            menu.Items.Add(hidePetItem);

            ToolStripMenuItem about = new ToolStripMenuItem("关于桌面宠物");
            about.Click += delegate
            {
                MessageBox.Show("仙伴 · AI桌面伴侣 3.1.4\n\n青衡、绯笺、苍芷、小墨 · 国风原创形象\n透明分层动画 · Claude Code · 提醒与定时任务\n代码采用 Apache-2.0；素材来源及授权范围见 ASSET-LICENSE.md。\n本项目与任何模型厂商或影视作品无隶属关系。", "关于仙伴", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            menu.Items.Add(about);

            ToolStripMenuItem exit = new ToolStripMenuItem("退出");
            exit.Click += delegate { Close(); };
            menu.Items.Add(exit);

            tray.Text = Program.ProductName;
            tray.ContextMenuStrip = menu;
            ContextMenuStrip = menu;
            menu.Opening += delegate { replyBubble.Dismiss(); quoteBubble.Hide(); UpdateVisibilityMenu(); };
            tray.Visible = true;
            RefreshTrayIcon();
            tray.DoubleClick += delegate { ShowQuickChat(); };
        }

        private void BuildClickActionsMenu()
        {
            clickActionsMenu.ShowImageMargin = false;
            clickActionsMenu.Font = new Font("Microsoft YaHei UI", 9f);
            clickActionsMenu.Padding = new Padding(4);
            clickActionsMenu.Opening += delegate { replyBubble.Dismiss(); quoteBubble.Hide(); };
        }

        private void ShowClickActions()
        {
            clickActionsMenu.Items.Clear();
            ToolStripMenuItem title = new ToolStripMenuItem("选择 Claude Code 打开方式");
            title.Enabled = false;
            clickActionsMenu.Items.Add(title);
            clickActionsMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem primary = new ToolStripMenuItem(claudeRunner.IsRunning ? "查看当前回复" : "内置快捷对话");
            primary.Click += delegate
            {
                if (claudeRunner.IsRunning) ShowReplyBubble(true);
                else ShowQuickChat();
            };
            clickActionsMenu.Items.Add(primary);

            ToolStripMenuItem terminal = new ToolStripMenuItem("PowerShell Claude Code CLI");
            terminal.ToolTipText = "工作目录：" + promptForm.WorkingDirectory;
            terminal.Click += delegate { OpenClaudeCli(promptForm.WorkingDirectory); };
            clickActionsMenu.Items.Add(terminal);

            ToolStripMenuItem activity = new ToolStripMenuItem("查看 Claude 动态");
            activity.Click += delegate { ShowActivities(); };
            clickActionsMenu.Items.Add(activity);
            ToolStripMenuItem historyToday = new ToolStripMenuItem("历史上的今天");
            historyToday.Click += delegate { StartPetBrainInteraction("history-today", 0); };
            clickActionsMenu.Items.Add(historyToday);
            ToolStripMenuItem scheduledTasks = new ToolStripMenuItem("Claude Code 定时任务…");
            scheduledTasks.Click += delegate { ShowClaudeTaskSettings(); };
            clickActionsMenu.Items.Add(scheduledTasks);
            ToolStripMenuItem defaultDirectory = new ToolStripMenuItem("设置 Claude CLI 默认目录…");
            defaultDirectory.Click += delegate { ChooseDefaultClaudeDirectory(); };
            clickActionsMenu.Items.Add(defaultDirectory);
            clickActionsMenu.Items.Add(new ToolStripSeparator());
            clickActionsMenu.Items.Add(BuildCharacterMenu());
            clickActionsMenu.Items.Add(BuildTrailOpacityMenu());
            clickActionsMenu.Items.Add(BuildPathModeMenu());
            clickActionsMenu.Items.Add(BuildAiCareMenu());
            clickActionsMenu.Show(Cursor.Position);
        }

        private ToolStripMenuItem BuildAiCareMenu()
        {
            ToolStripMenuItem menu = new ToolStripMenuItem("AI 主动关怀");
            ToolStripMenuItem enabled = new ToolStripMenuItem("允许主动说话与休息提醒");
            enabled.Checked = aiCareEnabled;
            enabled.Click += delegate
            {
                aiCareEnabled = !aiCareEnabled;
                enabled.Checked = aiCareEnabled;
                if (aiCareEnabled) ResetScreenSession(screenSessionActive);
                SavePreferences();
            };
            menu.DropDownItems.Add(enabled);
            menu.DropDownItems.Add(new ToolStripSeparator());
            foreach (int minutes in new[] { 30, 45, 60, 90 })
            {
                int selectedMinutes = minutes;
                ToolStripMenuItem item = new ToolStripMenuItem("连续开屏 " + minutes.ToString() + " 分钟提醒");
                item.Checked = breakReminderMinutes == selectedMinutes;
                item.Tag = selectedMinutes;
                item.Click += delegate
                {
                    breakReminderMinutes = selectedMinutes;
                    ResetScreenSession(screenSessionActive);
                    SavePreferences();
                };
                menu.DropDownItems.Add(item);
            }
            menu.DropDownOpening += delegate
            {
                enabled.Checked = aiCareEnabled;
                foreach (ToolStripItem child in menu.DropDownItems)
                {
                    ToolStripMenuItem option = child as ToolStripMenuItem;
                    if (option != null && option.Tag is int) option.Checked = (int)option.Tag == breakReminderMinutes;
                }
            };
            return menu;
        }

        private ToolStripMenuItem BuildPathModeMenu()
        {
            ToolStripMenuItem menu = new ToolStripMenuItem("全屏跑动路线");
            ToolStripMenuItem builtIn = new ToolStripMenuItem("内置随机曲线");
            ToolStripMenuItem aiDriven = new ToolStripMenuItem("Claude AI 路线池（一次生成24条）");
            builtIn.Checked = movementPathMode == "builtin";
            aiDriven.Checked = movementPathMode == "ai";
            builtIn.Click += delegate { SetMovementPathMode("builtin"); builtIn.Checked = true; aiDriven.Checked = false; };
            aiDriven.Click += delegate { SetMovementPathMode("ai"); builtIn.Checked = false; aiDriven.Checked = true; };
            menu.DropDownOpening += delegate
            {
                builtIn.Checked = movementPathMode == "builtin";
                aiDriven.Checked = movementPathMode == "ai";
                aiDriven.Text = "Claude AI 路线池（剩余 " + aiPathPool.Count.ToString() + " 条）";
            };
            menu.DropDownItems.Add(builtIn);
            menu.DropDownItems.Add(aiDriven);
            return menu;
        }

        private void SetMovementPathMode(string mode)
        {
            movementPathMode = String.Equals(mode, "ai", StringComparison.OrdinalIgnoreCase) ? "ai" : "builtin";
            if (movementPathMode == "ai") ResetScreenSession(screenSessionActive);
            SavePreferences();
        }

        internal static bool ShouldRefillAiPathPool(string mode, int remaining)
        {
            return String.Equals(mode, "ai", StringComparison.OrdinalIgnoreCase) && remaining <= AiPathRefillThreshold;
        }

        private ToolStripMenuItem BuildTrailOpacityMenu()
        {
            ToolStripMenuItem menu = new ToolStripMenuItem("光轨透明度");
            foreach (int percent in new[] { 25, 40, 55, 70 })
            {
                int selected = percent;
                ToolStripMenuItem item = new ToolStripMenuItem(percent.ToString() + "%");
                item.Checked = Math.Abs(trailOpacity - (selected / 100f)) < 0.02f;
                item.Click += delegate
                {
                    trailOpacity = TrailOverlayForm.ClampOpacity(selected / 100f);
                    foreach (ToolStripItem child in menu.DropDownItems)
                    {
                        ToolStripMenuItem option = child as ToolStripMenuItem;
                        if (option != null) option.Checked = Object.ReferenceEquals(option, item);
                    }
                    trailOverlay.Configure(trailColors, trailStyle, trailOpacity);
                    SavePreferences();
                };
                menu.DropDownItems.Add(item);
            }
            return menu;
        }

        private ToolStripMenuItem BuildCharacterMenu(bool trackItems = false)
        {
            ToolStripMenuItem menu = new ToolStripMenuItem("切换形象");
            menu.DropDownOpening += delegate { PopulateCharacterMenu(menu, trackItems); };
            PopulateCharacterMenu(menu, trackItems);
            return menu;
        }

        private void PopulateCharacterMenu(ToolStripMenuItem menu, bool trackItems)
        {
            foreach (ToolStripItem old in menu.DropDownItems) { var item = old as ToolStripMenuItem; if (item != null) characterMenuItems.Remove(item); }
            while (menu.DropDownItems.Count > 0) { ToolStripItem old = menu.DropDownItems[0]; menu.DropDownItems.RemoveAt(0); old.Dispose(); }
            foreach (PetCharacter character in PetCharacter.Available)
            {
                PetCharacter selected = character;
                ToolStripMenuItem item = new ToolStripMenuItem(character.Name + (character.IsExternal ? "（外置）" : ""));
                item.Tag = character.Id;
                item.Checked = String.Equals(character.Id, currentCharacterId, StringComparison.OrdinalIgnoreCase);
                item.Click += delegate { SetCharacter(selected.Id, true); };
                if (trackItems) characterMenuItems.Add(item);
                menu.DropDownItems.Add(item);
            }
            menu.DropDownItems.Add(new ToolStripSeparator());
            var open = new ToolStripMenuItem("打开角色资源目录"); open.Click += delegate { OpenCharacterPackDirectory(); }; menu.DropDownItems.Add(open);
            var refresh = new ToolStripMenuItem("刷新外置角色 / 查看校验结果"); refresh.Click += delegate { RequestCharacterPackRefresh(true); }; menu.DropDownItems.Add(refresh);
        }

        private void ShowRandomQuote()
        {
            if (userHidden || !Visible || !petOnSelectedDesktop || reminderOverlay != null) return;
            string[] quotes = PetCharacter.Find(currentCharacterId).Quotes;
            if (quotes.Length == 0) return;
            quoteBubble.ShowQuote(quotes[random.Next(quotes.Length)], Bounds);
        }

        private void TickAnimation(object sender, EventArgs e)
        {
            PollClaudeEvent();
            CheckAiCare();
            if (userHidden || !petOnSelectedDesktop || reminderOverlay != null) { lastAnimationTick = DateTime.UtcNow; return; }
            DateTime animationNow = DateTime.UtcNow;
            double elapsedMs = Math.Max(0.0, Math.Min(250.0, (animationNow - lastAnimationTick).TotalMilliseconds));
            lastAnimationTick = animationNow;
            animationAccumulatorMs += elapsedMs;
            int frameAdvances = 0;
            if (IsLocomotionState(state))
            {
                // Contact/extreme poses dwell a little longer than passing poses.
                // This sinusoidal-style easing removes the instant reversal that
                // made arms and legs look like a shared mechanical pendulum.
                while (frameAdvances < 8)
                {
                    // Keep the approved eight-frame artwork's existing cadence.
                    List<Image> gaitFrames;
                    int interval = animations.TryGetValue(state, out gaitFrames) && gaitFrames.Count == 8
                        ? LocomotionFrameIntervalMs : GaitFrameIntervalMs(locomotionSpeedPixelsPerSecond, frameIndex + frameAdvances);
                    if (animationAccumulatorMs < interval) break;
                    animationAccumulatorMs -= interval;
                    frameAdvances++;
                }
            }
            else
            {
                // Every character uses the same render cadence for equivalent
                // 24-phase actions. Character identity changes the authored
                // gesture, not the perceived smoothness or playback latency.
                int frameInterval = IsSmoothInteractionState(state) && FramesForCurrentStateAreSmooth()
                    ? SmoothInteractionFrameIntervalMs : AnimationFrameIntervalMs;
                // A complete raise / gesture / lower cycle needs readable time;
                // do not play the new full-range paths as a 384ms twitch loop.
                frameInterval = CompanionMotion.GesturePlaybackInterval(currentCharacterId,state,frameInterval);
                frameAdvances = Math.Min(8, (int)(animationAccumulatorMs / frameInterval));
                if (frameAdvances > 0) animationAccumulatorMs -= frameAdvances * frameInterval;
            }
            if (dragging)
            {
                // Releasing neither button nor window does not mean the pet is
                // still walking. Settle naturally to its standing pose shortly
                // after the cursor stops, and resume the gait on the next move.
                if (dragMoved && IsLocomotionState(state) &&
                    (DateTime.UtcNow - lastDragMotionAt).TotalMilliseconds >= 140.0)
                    SetState("idle");
                return;
            }

            // Advance position on every scheduler tick instead of coupling it
            // to pose changes. The body moves near 60 FPS while gait poses keep
            // a readable cadence, eliminating the stepwise sliding impression.
            bool wandered = !reduceMotion && TickWander(elapsedMs);
            if (wandered)
            {
                if (frameAdvances > 0) frameIndex += frameAdvances;
                RefreshPetVisual();
                return;
            }
            if (frameAdvances <= 0) return;
            if (reduceMotion)
            {
                frameIndex = 0;
                RefreshPetVisual();
                return;
            }
            if (DateTime.UtcNow >= stateUntil)
            {
                stateUntil = DateTime.MaxValue;
                SetState(fallbackState);
                if (state == "idle") ScheduleNextIdleAction();
            }

            List<Image> frames;
            if (state == "idle")
            {
                frameIndex = 0;
                if (CanWander() && DateTime.UtcNow >= nextWander)
                {
                    StartRandomWander();
                }
                else if (DateTime.UtcNow >= nextIdleAction)
                {
                    int idleChoice = random.Next(3);
                    string idleAction = idleChoice == 0 ? "review" : (idleChoice == 1 ? "waving" : "jumping");
                    int duration = idleAction == "review" ? 1800 : (idleAction == "jumping" ? 1200 : 2200);
                    SetTemporaryState(idleAction, duration, "idle");
                }
            }
            else if (animations.TryGetValue(state, out frames) && frames.Count > 0)
                frameIndex = (frameIndex + frameAdvances) % frames.Count;
            RefreshPetVisual();
        }

        private bool FramesForCurrentStateAreSmooth()
        {
            List<Image> frames;
            return animations.TryGetValue(state, out frames) && frames.Count >= 24;
        }

        private void PollClaudeEvent()
        {
            try
            {
                if (!File.Exists(Program.StateFile)) return;
                DateTime write = File.GetLastWriteTimeUtc(Program.StateFile);
                if (write <= lastEventWrite) return;
                lastEventWrite = write;
                string content = File.ReadAllText(Program.StateFile);
                string[] parts = content.Split(new[] { '|' }, 3);
                string eventName = parts[0];
                string sessionId = null;
                string cwd = null;
                string delta = null;
                bool managed = false;
                if (parts.Length == 3 && !String.IsNullOrEmpty(parts[2]))
                {
                    string metadataJson = Encoding.UTF8.GetString(Convert.FromBase64String(parts[2]));
                    Dictionary<string, object> metadata = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(metadataJson);
                    object value;
                    if (metadata.TryGetValue("session_id", out value)) sessionId = Convert.ToString(value);
                    if (metadata.TryGetValue("cwd", out value)) cwd = Convert.ToString(value);
                    if (metadata.TryGetValue("delta", out value)) delta = Convert.ToString(value);
                    if (metadata.TryGetValue("pet_managed", out value)) managed = String.Equals(Convert.ToString(value), "True", StringComparison.OrdinalIgnoreCase);
                }
                if (!String.Equals(eventName, "Show", StringComparison.OrdinalIgnoreCase) && !managed && !integrationPreferences.MonitorExternalSessions) return;
                SetClaudeEvent(eventName, sessionId, cwd, delta);
            }
            catch { }
        }

        private void SetClaudeEvent(string eventName, string sessionId = null, string cwd = null, string delta = null)
        {
            if (String.Equals(eventName, "Show", StringComparison.OrdinalIgnoreCase))
            {
                ShowPet();
                WindowState = FormWindowState.Normal;
                TopMost = TopMost;
                SetTemporaryState("waving", 1600, claudeBusy ? RunningState() : "idle");
                return;
            }
            if (paused) return;

            if (!String.Equals(eventName, "MessageDisplay", StringComparison.OrdinalIgnoreCase))
                CancelWander();

            bool externalTracked = !claudeRunner.IsRunning && !String.IsNullOrEmpty(sessionId);
            if (externalTracked) TrackExternalActivity(eventName, sessionId, cwd);

            switch (eventName)
            {
                case "MessageDisplay":
                    if (!claudeRunner.IsRunning && !String.IsNullOrWhiteSpace(delta))
                    {
                        replyBubble.AppendReply(delta);
                        replyBubble.SetStatus("Claude 最新回复");
                        ShowReplyBubble();
                        ActivityRecord replyActivity = activities.Find(delegate(ActivityRecord item) { return item.SessionId == sessionId; });
                        if (replyActivity != null)
                        {
                            lastReplyActivity = replyActivity;
                            replyActivity.AppendOutput(delta);
                            replyActivity.Updated = DateTime.Now;
                            RefreshActivities();
                        }
                    }
                    break;
                case "SessionStart":
                    claudeBusy = false;
                    SetTemporaryState("waving", 1800, "idle");
                    SetTrayText("Claude Code 会话已连接");
                    break;
                case "UserPromptSubmit":
                    claudeBusy = true;
                    if (externalTracked) BeginReplyBubble("本机 Claude Code CLI");
                    SetState(RunningState());
                    SetTrayText("Claude 正在工作…");
                    break;
                case "PreToolUse":
                    claudeBusy = true;
                    SetTemporaryState("review", 900, RunningState());
                    SetTrayText("Claude 正在使用工具…");
                    break;
                case "PostToolUse":
                    claudeBusy = true;
                    SetState(RunningState());
                    break;
                case "PermissionRequest":
                case "Notification":
                    SetState("waiting");
                    SetTrayText("Claude 正在等待你");
                    break;
                case "PostToolUseFailure":
                    SetTemporaryState("failed", 2800, claudeBusy ? RunningState() : "idle");
                    SetTrayText("工具执行遇到问题");
                    break;
                case "StopFailure":
                    claudeBusy = false;
                    SetTemporaryState("failed", 4500, "idle");
                    SetTrayText("Claude 本轮执行失败");
                    break;
                case "Stop":
                case "TaskCompleted":
                    claudeBusy = false;
                    SetTemporaryState("jumping", 2600, "idle");
                    SetTrayText("Claude 已完成");
                    break;
                case "SessionEnd":
                    claudeBusy = false;
                    SetTemporaryState("waving", 1800, "idle");
                    SetTrayText("Claude Code：已连接");
                    break;
            }
            if (externalTracked) ApplyActivityPriority();
        }

        private string RunningState()
        {
            return "running";
        }

        private void ScheduleNextIdleAction()
        {
            nextIdleAction = DateTime.UtcNow.AddSeconds(random.Next(9, 22));
        }

        private void ScheduleNextWander()
        {
            nextWander = DateTime.UtcNow.AddSeconds(random.Next(14, 41));
        }

        private void CheckAiCare()
        {
            DateTime now = DateTime.UtcNow;
            if (now < nextAiCareCheckAt) return;
            nextAiCareCheckAt = now.AddSeconds(5);
            if (userHidden || !Visible || !aiCareEnabled || movementPathMode != "ai" || !screenSessionActive || paused || claudeBusy || claudeRunner.IsRunning || dragging || hovering || reminderOverlay != null || fileEatOverlay != null || promptForm.Visible || activityTray.Visible || reminderForm.Visible) return;

            if (ShouldTriggerAiBreak(aiCareEnabled, movementPathMode, screenSessionActive, now, nextBreakReminderAt))
            {
                int minutes = Math.Max(1, (int)Math.Floor((now - screenSessionStartedAt).TotalMinutes));
                nextBreakReminderAt = now.AddMinutes(breakReminderMinutes);
                nextAiChatterAt = now.AddMinutes(random.Next(12, 29));
                StartPetBrainInteraction("break", minutes);
                return;
            }
            if (now >= nextAiChatterAt)
            {
                nextAiChatterAt = now.AddMinutes(random.Next(12, 29));
                StartPetBrainInteraction("idle-talk", Math.Max(1, (int)Math.Floor((now - screenSessionStartedAt).TotalMinutes)));
            }
        }

        internal static bool ShouldTriggerAiBreak(bool enabled, string pathMode, bool screenActive, DateTime now, DateTime due)
        {
            return enabled && screenActive && String.Equals(pathMode, "ai", StringComparison.OrdinalIgnoreCase) && now >= due;
        }

        private void ResetScreenSession(bool active)
        {
            screenSessionActive = active;
            if (!active) return;
            screenSessionStartedAt = DateTime.UtcNow;
            nextBreakReminderAt = screenSessionStartedAt.AddMinutes(Math.Max(30, breakReminderMinutes));
            nextAiChatterAt = screenSessionStartedAt.AddMinutes(random.Next(12, 29));
            nextAiCareCheckAt = DateTime.MinValue;
        }

        private void SystemSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.RemoteDisconnect)
                RunOnUi(delegate { ResetScreenSession(false); });
            else if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.RemoteConnect)
                RunOnUi(delegate { ResetScreenSession(true); });
        }

        private void SystemPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend) RunOnUi(delegate { ResetScreenSession(false); });
            else if (e.Mode == PowerModes.Resume) RunOnUi(delegate { ResetScreenSession(true); });
        }

        private void UpdateDisplayState(bool displayOn)
        {
            if (displayOn == screenSessionActive) return;
            ResetScreenSession(displayOn);
        }

        private bool CanWander()
        {
            return !userHidden && Visible && petOnSelectedDesktop && String.IsNullOrEmpty(dockedEdge) && (playfulRun || !hovering) && !dragging && !claudeBusy && !promptForm.Visible && !activityTray.Visible && !reminderForm.Visible && reminderOverlay == null && fileEatOverlay == null;
        }

        private void StartRandomWander()
        {
            wanderPath.Clear();
            Rectangle area = Screen.FromPoint(Location).WorkingArea;
            wanderWorkingArea = area;
            int margin = 12;
            int points = random.Next(2, 5);
            Point previous = Location;
            for (int i = 0; i < points; i++)
            {
                int x = wanderOrigin.X + random.Next(-70, 71);
                int y = wanderOrigin.Y + random.Next(-24, 25);
                x = Math.Max(area.Left + margin, Math.Min(x, area.Right - Width - margin));
                y = Math.Max(area.Top + margin, Math.Min(y, area.Bottom - Height - margin));
                Point target = new Point(x, y);
                if (Math.Abs(target.X - previous.X) + Math.Abs(target.Y - previous.Y) < 15) continue;
                wanderPath.Enqueue(target);
                previous = target;
            }
            if (wanderPath.Count == 0)
            {
                ScheduleNextWander();
                return;
            }
            Point first = wanderPath.Peek();
            wanderFacingLeft = first.X < Left;
            SetState(wanderFacingLeft ? "running-left" : "running-right");
        }

        private bool TickWander(double elapsedMs)
        {
            if (wanderPath.Count == 0) return false;
            if (!CanWander()) { CancelWander(); return false; }
            Point target = wanderPath.Peek();
            double dx = target.X - Left;
            double dy = target.Y - Top;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));
            double movementStep = MovementPixelsForElapsed(playfulRun, elapsedMs);
            locomotionSpeedPixelsPerSecond = playfulRun ? 210.0 : 58.0;
            if (HasReachedWanderTarget(distance, playfulRun))
            {
                Location = target;
                wanderPath.Dequeue();
                if (wanderPath.Count == 0)
                {
                    playfulRun = false;
                    wanderOrigin = Location;
                    SetState("idle");
                    ScheduleNextWander();
                    return false;
                }
                target = wanderPath.Peek();
                dx = target.X - Left;
                dy = target.Y - Top;
                distance = Math.Sqrt((dx * dx) + (dy * dy));
            }
            int stepX = (int)Math.Round((dx / Math.Max(1.0, distance)) * movementStep);
            int stepY = (int)Math.Round((dy / Math.Max(1.0, distance)) * movementStep);
            Rectangle area = wanderWorkingArea.IsEmpty ? Screen.FromPoint(Location).WorkingArea : wanderWorkingArea;
            int nextX = Math.Max(area.Left, Math.Min(Left + stepX, area.Right - Width));
            int nextY = Math.Max(area.Top, Math.Min(Top + stepY, area.Bottom - Height));
            if (nextX == Left && nextY == Top && distance > movementStep)
            {
                CancelWander();
                SetState("idle");
                return false;
            }
            Location = new Point(nextX, nextY);
            AddTrailPoint();
            if (Math.Abs(dx) >= 2.0) wanderFacingLeft = dx < 0;
            SetState(wanderFacingLeft ? "running-left" : "running-right");
            return true;
        }

        internal static double MovementPixelsForElapsed(bool fastRun, double elapsedMs)
        {
            double safeElapsed = Math.Max(1.0, Math.Min(34.0, elapsedMs));
            double pixelsPerSecond = fastRun ? 210.0 : 58.0;
            return Math.Max(0.55, pixelsPerSecond * safeElapsed / 1000.0);
        }

        internal static int GaitFrameIntervalMs(double pixelsPerSecond, int phase)
        {
            // SmoothStep keeps cadence changes continuous when movement speed
            // varies. Slow walking is deliberate; a playful run accelerates but
            // never turns into unreadable limb flicker.
            double ratio = Math.Max(0.0, Math.Min(1.0, (pixelsPerSecond - 30.0) / 220.0));
            double easedSpeed = ratio * ratio * (3.0 - (2.0 * ratio));
            double baseInterval = LocomotionFrameIntervalMs +
                ((LocomotionFastFrameIntervalMs - LocomotionFrameIntervalMs) * easedSpeed);
            int normalizedPhase = ((phase % 24) + 24) % 24;
            double angle = normalizedPhase / 24.0 * Math.PI * 2.0;
            // Smoothly dwell at the two stride extremes (0/12) and pass more
            // quickly through the neutral crossings (6/18). A tiny sine bias
            // keeps the two halves from feeling perfectly clockwork-symmetric.
            double phaseDwell = 1.0 + (0.14 * Math.Abs(Math.Cos(angle))) -
                (0.10 * Math.Abs(Math.Sin(angle))) + (0.018 * Math.Sin(angle));
            double duration = baseInterval * phaseDwell;
            return (int)Math.Round(Math.Max(15.0, Math.Min(36.0, duration)));
        }

        internal static bool PreservesGaitPhase(string previousState, string nextState)
        {
            return IsLocomotionState(previousState) && IsLocomotionState(nextState);
        }

        internal static bool HasReachedWanderTarget(double distance, bool fastMovement)
        {
            return distance <= (fastMovement ? 7.0 : 3.0);
        }

        private void CancelWander()
        {
            playfulRun = false;
            wanderWorkingArea = Rectangle.Empty;
            if (wanderPath.Count == 0) return;
            wanderPath.Clear();
            ScheduleNextWander();
        }

        private void SetTemporaryState(string newState, int milliseconds, string after)
        {
            fallbackState = after;
            stateUntil = DateTime.UtcNow.AddMilliseconds(milliseconds);
            SetState(newState);
        }

        private void SetState(string newState)
        {
            if (!animations.ContainsKey(newState)) newState = "idle";
            bool changed = !String.Equals(state, newState, StringComparison.OrdinalIgnoreCase);
            if (changed)
            {
                // Keep the same anatomical step when a curved path changes from
                // left-facing to right-facing. Resetting to contact phase zero at
                // every turn caused visible foot pops and same-pose repetition.
                if (!PreservesGaitPhase(state, newState))
                {
                    frameIndex = 0;
                    animationAccumulatorMs = 0;
                    lastAnimationTick = DateTime.UtcNow;
                }
            }
            state = newState;
            if (!IsLocomotionState(state)) locomotionSpeedPixelsPerSecond = 0.0;
            if (changed) RefreshPetVisual();
        }

        private void SetTrayText(string value)
        {
            tray.Text = value.Length > 63 ? value.Substring(0, 63) : value;
        }

        private static string ExecutablePath
        {
            get { return Process.GetCurrentProcess().MainModule.FileName; }
        }

        private static bool IsStartupEnabled()
        {
            return StartupRegistration.IsEnabled();
        }

        private static void SetStartup(bool enabled)
        {
            StartupRegistration.SetEnabled(enabled, ExecutablePath);
        }

        internal static ProcessStartInfo BuildTerminalStartInfo(string workingDirectory)
        {
            string directory = Directory.Exists(workingDirectory)
                ? workingDirectory
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string literalDirectory = directory.Replace("'", "''");
            return new ProcessStartInfo("powershell.exe", "-NoLogo -NoExit -Command \"$env:FUFU_PET_MANAGED_SESSION='1'; $utf8=New-Object Text.UTF8Encoding($false); $OutputEncoding=$utf8; [Console]::InputEncoding=$utf8; [Console]::OutputEncoding=$utf8; Set-Location -LiteralPath '" + literalDirectory + "'; claude\"")
            {
                WorkingDirectory = directory,
                UseShellExecute = true
            };
        }

        private static void OpenClaudeCli(string workingDirectory)
        {
            try
            {
                Process.Start(BuildTerminalStartInfo(workingDirectory));
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法启动本机 Claude Code CLI：\n" + ex.Message, Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BeginDrag(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                CancelWander();
                dragging = true;
                dragMoved = false;
                dragMouse = Cursor.Position;
                dragWindow = Location;
                lastDragCursor = dragMouse;
                lastDragFrameAt = DateTime.UtcNow;
                lastDragMotionAt = lastDragFrameAt;
                dragFrameDistance = 0;
            }
        }

        private void ContinueDrag(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            Point now = Cursor.Position;
            if (!dragMoved && Math.Abs(now.X - dragMouse.X) + Math.Abs(now.Y - dragMouse.Y) > 5)
            {
                dragMoved = true;
                ReleaseEdgeDock();
            }
            Location = new Point(dragWindow.X + now.X - dragMouse.X, dragWindow.Y + now.Y - dragMouse.Y);
            int deltaX = now.X - lastDragCursor.X;
            int deltaY = now.Y - lastDragCursor.Y;
            double distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (dragMoved && !reduceMotion && distance > 0.1)
            {
                AddTrailPoint();
                SetState(deltaX < 0 ? "running-left" : "running-right");
                DateTime current = DateTime.UtcNow;
                double elapsed = Math.Max(1.0, (current - lastDragFrameAt).TotalMilliseconds);
                double measuredSpeed = Math.Min(3000.0, distance * 1000.0 / elapsed);
                locomotionSpeedPixelsPerSecond = locomotionSpeedPixelsPerSecond <= 0.0
                    ? measuredSpeed
                    : (locomotionSpeedPixelsPerSecond * 0.68) + (measuredSpeed * 0.32);
                lastDragMotionAt = current;
                dragFrameDistance += distance;
                double pixelsPerFrame = DragPixelsPerFrame(locomotionSpeedPixelsPerSecond / 1000.0);
                int advances = (int)Math.Floor(dragFrameDistance / pixelsPerFrame);
                if (advances == 0 && elapsed >= 180.0) advances = 1;
                if (advances > 0)
                {
                    List<Image> frames;
                    if (animations.TryGetValue(state, out frames) && frames.Count > 0)
                        frameIndex = (frameIndex + Math.Min(advances, 5)) % frames.Count;
                    dragFrameDistance = advances > 0 ? dragFrameDistance % pixelsPerFrame : dragFrameDistance;
                    lastDragFrameAt = current;
                }
            }
            lastDragCursor = now;
            RefreshPetVisual();
        }

        internal static double DragPixelsPerFrame(double pixelsPerMillisecond)
        {
            double speed = Math.Max(0.0, Math.Min(3.0, pixelsPerMillisecond));
            return Math.Max(3.0, 9.0 - (speed * 2.0));
        }

        private void EndDrag(object sender, MouseEventArgs e)
        {
            bool openPrompt = dragging && !dragMoved && e.Button == MouseButtons.Left;
            dragging = false;
            if (dragMoved)
            {
                bool docked = TryDockAtScreenEdge();
                SetState(docked ? "waving" : "idle");
                wanderOrigin = Location;
                if (!docked) ScheduleNextWander();
                ScheduleNextIdleAction();
                SavePreferences();
            }
            if (dragMoved) AnchorAuxiliaryWindows();
            if (openPrompt)
            {
                clickDelayTimer.Stop();
                clickDelayTimer.Start();
            }
        }

        private void PetDragEnter(object sender, DragEventArgs e)
        {
            if (!CanAcceptFileDrop(e.Data)) { e.Effect = DragDropEffects.None; return; }
            e.Effect = DragDropEffects.Copy;
            CancelWander();
            SetState("eating");
            SetTrayText(PetCharacter.Find(currentCharacterId).Name + " 张大嘴，准备吃掉文件…");
        }

        private void PetDragOver(object sender, DragEventArgs e)
        {
            e.Effect = CanAcceptFileDrop(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void PetDragLeave(object sender, EventArgs e)
        {
            if (fileEatOverlay == null && !claudeBusy) SetState("idle");
        }

        private void PetDragDrop(object sender, DragEventArgs e)
        {
            if (!CanAcceptFileDrop(e.Data)) { SetState("idle"); return; }
            string[] raw = e.Data.GetData(DataFormats.FileDrop) as string[];
            string[] paths = NormalizeDroppedPaths(raw, 32);
            if (paths.Length == 0) { SetState("idle"); return; }

            promptForm.Hide();
            replyBubble.Hide();
            activityTray.HideUnlessPinned();
            reminderForm.Hide();
            quoteBubble.Hide();
            pendingDroppedPaths = paths;
            if (fileEatOverlay != null)
            {
                fileEatOverlay.Dispose();
                fileEatOverlay = null;
            }
            fileEatOverlay = new FileEatOverlayForm();
            fileEatOverlay.Completed += FinishFileEating;
            string clean = paths[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string label = Path.GetFileName(clean);
            if (String.IsNullOrEmpty(label)) label = clean;
            bool folder = Directory.Exists(paths[0]);
            PetCharacter eatingCharacter = PetCharacter.Find(currentCharacterId);
            Point mouth = new Point(Left + (int)Math.Round(Width * eatingCharacter.MouthX), Top + (int)Math.Round(Height * eatingCharacter.MouthY));
            SetTemporaryState("eating", FileEatOverlayForm.EatingDurationMs + 180, "idle");
            SetTrayText(PetCharacter.Find(currentCharacterId).Name + " 正在吃掉文件卡片…");
            fileEatOverlay.Begin(Cursor.Position, mouth, label, folder, paths.Length);
            BringToFront();
        }

        private bool CanAcceptFileDrop(IDataObject data)
        {
            return data != null && data.GetDataPresent(DataFormats.FileDrop) && !paused && !claudeBusy && !claudeRunner.IsRunning && reminderOverlay == null && fileEatOverlay == null;
        }

        private void FinishFileEating()
        {
            FileEatOverlayForm completed = fileEatOverlay;
            fileEatOverlay = null;
            if (completed != null)
            {
                completed.Completed -= FinishFileEating;
                completed.Dispose();
            }
            if (pendingDroppedPaths == null || pendingDroppedPaths.Length == 0) return;
            SetTemporaryState("wiping-mouth", 1650, "idle");
            SetTrayText(PetCharacter.Find(currentCharacterId).Name + " 吃完了，正在夸张地抹嘴…");
            fileWipeTimer.Stop();
            fileWipeTimer.Start();
        }

        private void OpenDroppedFilesPromptAfterWipe()
        {
            string[] paths = pendingDroppedPaths;
            pendingDroppedPaths = new string[0];
            if (paths == null || paths.Length == 0) return;
            string directory = ResolveDroppedWorkingDirectory(paths);
            string preparedPrompt = BuildDroppedFilesPrompt(paths);
            SetState("idle");
            SetTrayText("文件已吃掉，请输入处理需求（原文件仍安全保留）");
            promptForm.ShowForDroppedPaths(Bounds, directory, preparedPrompt);
        }

        internal static string[] NormalizeDroppedPaths(string[] values, int maximum)
        {
            List<string> result = new List<string>();
            if (values == null) return result.ToArray();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in values)
            {
                if (result.Count >= Math.Max(1, maximum)) break;
                if (String.IsNullOrWhiteSpace(value)) continue;
                try
                {
                    string full = Path.GetFullPath(value);
                    if ((File.Exists(full) || Directory.Exists(full)) && seen.Add(full)) result.Add(full);
                }
                catch { }
            }
            return result.ToArray();
        }

        internal static string ResolveDroppedWorkingDirectory(string[] paths)
        {
            string[] normalized = NormalizeDroppedPaths(paths, 32);
            if (normalized.Length == 0) return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string firstDirectory = Directory.Exists(normalized[0]) ? normalized[0] : Path.GetDirectoryName(normalized[0]);
            if (String.IsNullOrWhiteSpace(firstDirectory)) return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string candidate = Path.GetFullPath(firstDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            while (!String.IsNullOrEmpty(candidate))
            {
                bool containsAll = true;
                string prefix = candidate + Path.DirectorySeparatorChar;
                foreach (string path in normalized)
                {
                    if (!String.Equals(path, candidate, StringComparison.OrdinalIgnoreCase) && !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { containsAll = false; break; }
                }
                if (containsAll && Directory.Exists(candidate)) return candidate;
                DirectoryInfo parent = Directory.GetParent(candidate);
                if (parent == null) break;
                candidate = parent.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return firstDirectory;
        }

        internal static string BuildDroppedFilesPrompt(string[] paths)
        {
            string[] normalized = NormalizeDroppedPaths(paths, 32);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("请根据我接下来补充的需求处理以下文件或文件夹：");
            foreach (string path in normalized)
            {
                builder.Append(Directory.Exists(path) ? "- 文件夹：" : "- 文件：");
                builder.AppendLine(path);
            }
            builder.AppendLine();
            builder.Append("我的具体需求：");
            return builder.ToString();
        }

        private void PetMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            clickDelayTimer.Stop();
            dragging = false;
            dragMoved = true;
            StartPetBrainInteraction("doubleclick", 0);
        }

        private void StartPlayfulRun()
        {
            if (claudeBusy || paused || reminderOverlay != null) return;
            ReleaseEdgeDock();
            wanderPath.Clear();
            Rectangle area = Screen.FromPoint(Location).WorkingArea;
            wanderWorkingArea = area;
            Point origin = Location;
            List<Point> selectedPath = new List<Point>();
            if (movementPathMode == "ai" && aiPathPool.Count > 0)
            {
                PetPathPlan plan = aiPathPool.Dequeue();
                aiPathCurve = plan.Curve;
                aiPathControlPoints = plan.ControlPoints ?? new PointF[0];
                selectedPath = GenerateAiPath(area, Size, origin, aiPathCurve, aiPathControlPoints);
            }
            if (selectedPath.Count < 10) selectedPath = GeneratePlayfulPath(area, Size, origin, random.Next());
            foreach (Point target in selectedPath) wanderPath.Enqueue(target);
            if (wanderPath.Count == 0) return;
            playfulRun = true;
            trailOverlay.Configure(trailColors, trailStyle, trailOpacity);
            trailOverlay.Begin(Screen.FromPoint(Location).Bounds);
            AddTrailPoint();
            Point first = wanderPath.Peek();
            wanderFacingLeft = first.X < Left;
            SetState(wanderFacingLeft ? "running-left" : "running-right");
        }

        internal static List<Point> GeneratePlayfulPath(Rectangle area, Size petSize, Point origin, int seed)
        {
            List<Point> result = new List<Point>();
            Random generator = new Random(seed);
            int margin = 14;
            int left = area.Left + margin, top = area.Top + margin;
            int right = Math.Max(left, area.Right - petSize.Width - margin);
            int bottom = Math.Max(top, area.Bottom - petSize.Height - margin);
            int width = Math.Max(1, right - left), height = Math.Max(1, bottom - top);
            int cx = left + width / 2, cy = top + height / 2;
            int pattern = Math.Abs(seed % 4);
            if (pattern == 0)
            {
                for (int i = 0; i < 24; i++)
                {
                    double angle = (Math.PI * 2.0 * i) / 23.0;
                    result.Add(new Point(cx + (int)(Math.Sin(angle) * width * 0.43), cy + (int)(Math.Sin(angle * 2.0) * height * 0.38)));
                }
            }
            else if (pattern == 1)
            {
                for (int i = 0; i < 26; i++)
                {
                    double ratio = i / 25.0, angle = ratio * Math.PI * 4.5;
                    result.Add(new Point(cx + (int)(Math.Cos(angle) * width * 0.44 * ratio), cy + (int)(Math.Sin(angle) * height * 0.42 * ratio)));
                }
            }
            else if (pattern == 2)
            {
                for (int i = 0; i < 14; i++)
                {
                    int x = (i % 2 == 0) ? left + generator.Next(0, Math.Max(1, width / 5)) : right - generator.Next(0, Math.Max(1, width / 5));
                    int y = top + (int)Math.Round(height * (i / 13.0));
                    result.Add(new Point(x, y));
                }
            }
            else
            {
                const int starPoints = 20;
                for (int i = 0; i < starPoints; i++)
                {
                    double angle = (-Math.PI / 2.0) + (i * Math.PI * 4.0 / starPoints);
                    double radius = i % 2 == 0 ? 0.46 : 0.20;
                    result.Add(new Point(cx + (int)(Math.Cos(angle) * width * radius), cy + (int)(Math.Sin(angle) * height * radius)));
                }
            }
            Point safeOrigin = new Point(Math.Max(left, Math.Min(origin.X, right)), Math.Max(top, Math.Min(origin.Y, bottom)));
            result.Add(safeOrigin);
            return result;
        }

        internal static List<Point> GenerateAiPath(Rectangle area, Size petSize, Point origin, string curve, PointF[] normalizedControls)
        {
            List<Point> result = new List<Point>();
            if (normalizedControls == null || normalizedControls.Length < 4) return result;
            int margin = 14;
            int left = area.Left + margin, top = area.Top + margin;
            int right = Math.Max(left, area.Right - petSize.Width - margin);
            int bottom = Math.Max(top, area.Bottom - petSize.Height - margin);
            int width = Math.Max(1, right - left), height = Math.Max(1, bottom - top);
            List<PointF> controls = new List<PointF>();
            foreach (PointF normalized in normalizedControls)
            {
                float nx = Math.Max(0.04f, Math.Min(0.96f, normalized.X));
                float ny = Math.Max(0.04f, Math.Min(0.96f, normalized.Y));
                controls.Add(new PointF(left + (nx * width), top + (ny * height)));
            }
            if (String.Equals(curve, "orbit", StringComparison.OrdinalIgnoreCase)) controls.Add(controls[0]);

            for (int segment = 0; segment < controls.Count - 1; segment++)
            {
                PointF p0 = controls[Math.Max(0, segment - 1)];
                PointF p1 = controls[segment];
                PointF p2 = controls[segment + 1];
                PointF p3 = controls[Math.Min(controls.Count - 1, segment + 2)];
                for (int sample = 0; sample < 10; sample++)
                {
                    float t = sample / 10f;
                    float t2 = t * t, t3 = t2 * t;
                    float x = 0.5f * ((2f * p1.X) + ((-p0.X + p2.X) * t) + ((2f * p0.X - 5f * p1.X + 4f * p2.X - p3.X) * t2) + ((-p0.X + 3f * p1.X - 3f * p2.X + p3.X) * t3));
                    float y = 0.5f * ((2f * p1.Y) + ((-p0.Y + p2.Y) * t) + ((2f * p0.Y - 5f * p1.Y + 4f * p2.Y - p3.Y) * t2) + ((-p0.Y + 3f * p1.Y - 3f * p2.Y + p3.Y) * t3));
                    result.Add(new Point(Math.Max(left, Math.Min((int)Math.Round(x), right)), Math.Max(top, Math.Min((int)Math.Round(y), bottom))));
                }
            }
            result.Add(new Point((int)Math.Round(controls[controls.Count - 1].X), (int)Math.Round(controls[controls.Count - 1].Y)));

            int minX = Int32.MaxValue, maxX = Int32.MinValue, minY = Int32.MaxValue, maxY = Int32.MinValue;
            foreach (Point point in result) { minX = Math.Min(minX, point.X); maxX = Math.Max(maxX, point.X); minY = Math.Min(minY, point.Y); maxY = Math.Max(maxY, point.Y); }
            if (maxX - minX < width * 0.55 || maxY - minY < height * 0.40) { result.Clear(); return result; }
            result.Add(new Point(Math.Max(left, Math.Min(origin.X, right)), Math.Max(top, Math.Min(origin.Y, bottom))));
            return result;
        }

        private void AddTrailPoint()
        {
            if (reduceMotion) return;
            Rectangle screen = Screen.FromRectangle(Bounds).Bounds;
            // Anchor inside the torso. The pet window is brought above the trail,
            // so the thick head is hidden under the body and appears to emerge
            // naturally from behind the character.
            Point center = new Point(Left + Width / 2, Top + (int)Math.Round(Height * 0.58));
            trailOverlay.Configure(trailColors, trailStyle, trailOpacity);
            trailOverlay.AddPoint(center, screen);
            BringToFront();
        }

        private bool TryDockAtScreenEdge()
        {
            Rectangle area = Screen.FromRectangle(Bounds).WorkingArea;
            int threshold = Math.Max(24, Math.Min(48, Math.Max(Width, Height) / 3));
            int leftDistance = Math.Abs(Left - area.Left);
            int rightDistance = Math.Abs(Right - area.Right);
            int bottomDistance = Math.Abs(Bottom - area.Bottom);
            int nearest = Math.Min(leftDistance, Math.Min(rightDistance, bottomDistance));
            if (nearest > threshold) { dockedEdge = ""; return false; }
            dockedEdge = nearest == leftDistance ? "left" : (nearest == rightDistance ? "right" : "bottom");
            ApplyEdgeDock(area);
            CancelWander();
            return true;
        }

        private void ApplyEdgeDock(Rectangle area)
        {
            if (String.IsNullOrEmpty(dockedEdge)) return;
            Location = CalculateDockedLocation(area, Size, Location, dockedEdge);
        }

        private void ReleaseEdgeDock()
        {
            dockedEdge = "";
        }

        internal static Point CalculateDockedLocation(Rectangle area, Size petSize, Point current, string edge)
        {
            int x = Math.Max(area.Left, Math.Min(current.X, area.Right - petSize.Width));
            int y = Math.Max(area.Top, Math.Min(current.Y, area.Bottom - petSize.Height));
            if (edge == "left") x = area.Left - (int)Math.Round(petSize.Width * 0.42);
            else if (edge == "right") x = area.Right - (int)Math.Round(petSize.Width * 0.58);
            else if (edge == "bottom") y = area.Bottom - (int)Math.Round(petSize.Height * 0.56);
            return new Point(x, y);
        }

        internal bool ValidatePlayfulRun()
        {
            StartPlayfulRun();
            Rectangle area = Screen.FromPoint(Location).WorkingArea;
            List<Point> sample = GeneratePlayfulPath(area, Size, Location, 42);
            int minimumX = Int32.MaxValue, maximumX = Int32.MinValue;
            foreach (Point point in sample) { minimumX = Math.Min(minimumX, point.X); maximumX = Math.Max(maximumX, point.X); }
            bool valid = playfulRun && wanderPath.Count >= 10 && maximumX - minimumX > Math.Max(100, area.Width / 2) && (state == "running-left" || state == "running-right");
            CancelWander();
            return valid;
        }

        internal bool ValidateTrailOverlay()
        {
            Rectangle screen = Screen.PrimaryScreen.Bounds;
            trailOverlay.Configure(PetBrain.DefaultColors(currentCharacterId), "rainbow", 0.40f);
            trailOverlay.Begin(screen);
            Point first = new Point(screen.Left + 80, screen.Top + 80);
            trailOverlay.AddPoint(first, screen);
            trailOverlay.AddPoint(new Point(first.X + 90, first.Y + 35), screen);
            Application.DoEvents();
            return trailOverlay.LayeredRenderSucceeded;
        }

        private void PetMouseEnter(object sender, EventArgs e)
        {
            hovering = true;
            CancelWander();
            if (!claudeBusy && !paused)
            {
                string hoverAction = random.Next(2) == 0 ? "waving" : "review";
                SetTemporaryState(hoverAction, hoverAction == "waving" ? 1800 : 1500, "idle");
            }
        }

        private void PetMouseLeave(object sender, EventArgs e)
        {
            hovering = false;
            ScheduleNextIdleAction();
            ScheduleNextWander();
        }

        private void StartPetBrainInteraction(string reason, int screenMinutes)
        {
            if (paused || reminderOverlay != null) { ShowRandomQuote(); return; }
            if (claudeRunner.IsRunning || claudeBusy)
            {
                replyBubble.SetStatus("Claude 正在处理上一条消息");
                ShowReplyBubble(true);
                return;
            }

            PetCharacter character = PetCharacter.Find(currentCharacterId);
            brainCharacterId = character.Id;
            brainRequestReason = String.IsNullOrWhiteSpace(reason) ? "doubleclick" : reason;
            brainScreenMinutes = Math.Max(0, screenMinutes);
            brainResponse.Length = 0;
            brainRequestActive = true;
            bool historyToday = brainRequestReason == "history-today";
            string title = brainRequestReason == "break" ? character.Name + " 休息提醒" : (historyToday ? character.Name + " 历史上的今天" : character.Name + " 主动发言");
            currentActivity = new ActivityRecord
            {
                Title = title,
                CharacterId = character.Id,
                WorkingDirectory = promptForm.WorkingDirectory,
                State = "正在运行",
                Output = brainRequestReason == "doubleclick"
                    ? "双击唤醒了 " + character.Name + " 的 AI 大脑。\r\n\r\n"
                    : (brainRequestReason == "break" ? "连续开屏休息提醒。\r\n\r\n" : (historyToday ? "正在查询并整理历史上的今天。\r\n\r\n" : "AI 宠物主动发言。\r\n\r\n")),
                Updated = DateTime.Now,
                Unread = false
            };
            activities.Insert(0, currentActivity);
            lastReplyActivity = currentActivity;
            while (activities.Count > 30) activities.RemoveAt(activities.Count - 1);
            RefreshActivities();
            BeginReplyBubble(brainRequestReason == "break" ? character.Name + " 正在准备休息提醒" : (historyToday ? character.Name + " 正在翻历史日历" : character.Name + " 正在想和你说什么"));
            replyBubble.SetStatus(brainRequestReason == "break" ? character.Name + " 正在关心你的用屏时间…" : (historyToday ? character.Name + " 正在整理今天的重要事件…" : character.Name + " 正在组织想说的话…"));
            SetTemporaryState(historyToday ? "review" : "waving", 2400, RunningState());
            SetClaudeEvent("UserPromptSubmit");
            try
            {
                string directory = Directory.Exists(promptForm.WorkingDirectory)
                    ? promptForm.WorkingDirectory
                    : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                bool refillAiPaths = ShouldRefillAiPathPool(movementPathMode, aiPathPool.Count);
                string brainPrompt = brainRequestReason == "doubleclick"
                    ? PetBrain.BuildProactivePrompt(character, DateTime.Now, refillAiPaths)
                    : (historyToday ? PetBrain.BuildHistoryTodayPrompt(character, DateTime.Now) : PetBrain.BuildCarePrompt(character, DateTime.Now, brainRequestReason, brainScreenMinutes));
                claudeRunner.Start(brainPrompt, directory);
            }
            catch (Exception ex)
            {
                brainResponse.Append(ex.Message);
                FinishPetBrainInteraction(-1);
            }
        }

        private void FinishPetBrainInteraction(int exitCode)
        {
            PetCharacter character = PetCharacter.Find(brainCharacterId);
            PetBrainResponse response = exitCode == 0
                ? PetBrain.Parse(brainResponse.ToString(), character)
                : PetBrain.Parse("", character);
            string completedReason = brainRequestReason;
            int completedScreenMinutes = brainScreenMinutes;
            brainRequestActive = false;
            brainResponse.Length = 0;
            if (completedReason == "doubleclick")
            {
                trailColors = response.TrailColors;
                trailStyle = response.TrailStyle;
                if (response.PathPlans != null)
                    foreach (PetPathPlan plan in response.PathPlans)
                        if (plan != null && plan.ControlPoints != null && plan.ControlPoints.Length >= 4 && aiPathPool.Count < 64)
                            aiPathPool.Enqueue(plan);
                trailOverlay.Configure(trailColors, trailStyle, trailOpacity);
            }

            if (exitCode != 0 && completedReason == "break")
                response.Speech = "已经连续看屏幕约" + Math.Max(1, completedScreenMinutes).ToString() + "分钟啦，先眺望远处、喝口水，再回来继续吧。";
            if (exitCode != 0 && completedReason == "history-today")
                response.Speech = "历史日历暂时没有连上 Claude Code，等连接恢复后我再认真讲给你听，绝不拿不确定的年份糊弄你。";

            BeginReplyBubble(completedReason == "history-today" ? character.Name + " · 历史上的今天" : character.Name + " 的主动发言");
            replyBubble.AppendReply(response.Speech);
            string routeStatus = completedReason == "doubleclick" && movementPathMode == "ai" ? " · AI路线池 " + aiPathPool.Count.ToString() + " 条" : "";
            string actionStatus = completedReason == "break" ? " · 休息提醒" : (completedReason == "history-today" ? " · 历史上的今天" : " · AI 主动发言");
            replyBubble.SetStatus(exitCode == 0 ? character.Name + actionStatus + routeStatus : character.Name + " · 本地备用发言");
            ShowReplyBubble(true);
            if (currentActivity != null)
                currentActivity.AppendOutput(character.Name + "：" + response.Speech + "\r\n");
            CompleteCurrentActivity(exitCode == 0 ? "就绪" : "受阻");
            SetClaudeEvent(exitCode == 0 ? "Stop" : "StopFailure");
            if (completedReason == "doubleclick" && !paused && reminderOverlay == null) StartPlayfulRun();
            else if (!paused && reminderOverlay == null) SetTemporaryState(completedReason == "break" || completedReason == "history-today" ? "review" : "waving", completedReason == "break" ? 3600 : (completedReason == "history-today" ? 4200 : 2400), "idle");
        }

        private void RunClaudePrompt(string prompt, string workingDirectory)
        {
            try
            {
                currentActivity = new ActivityRecord
                {
                    Title = prompt.Length > 42 ? prompt.Substring(0, 42) + "…" : prompt,
                    CharacterId = currentCharacterId,
                    WorkingDirectory = workingDirectory,
                    State = "正在运行",
                    Output = "你：" + prompt + "\r\n\r\n",
                    Updated = DateTime.Now,
                    Unread = false
                };
                activities.Insert(0, currentActivity);
                lastReplyActivity = currentActivity;
                while (activities.Count > 30) activities.RemoveAt(activities.Count - 1);
                RefreshActivities();
                BeginReplyBubble(prompt);
                SetClaudeEvent("UserPromptSubmit");
                claudeRunner.Start(PetBrain.BuildConversationPrompt(PetCharacter.Find(currentCharacterId), prompt), workingDirectory);
            }
            catch (Exception ex)
            {
                replyBubble.SetStatus("无法启动 Claude Code");
                replyBubble.AppendReply(ex.Message);
                ShowReplyBubble();
                SetClaudeEvent("StopFailure");
            }
        }

        private bool RunClaudeReply(ActivityRecord activity, string prompt)
        {
            if (activity == null || activity.NativeInteractive || String.IsNullOrWhiteSpace(activity.SessionId) || String.IsNullOrWhiteSpace(prompt)) return false;
            if (claudeRunner.IsRunning)
            {
                MessageBox.Show(activityTray, "Claude 正在处理上一条消息，请完成后再回复。", "Claude Code", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            try
            {
                currentActivity = activity;
                lastReplyActivity = activity;
                activity.State = "正在运行";
                activity.Unread = false;
                activity.Updated = DateTime.Now;
                activity.AppendOutput("\r\n\r\n你：" + prompt + "\r\n\r\n");
                RefreshActivities();
                BeginReplyBubble(prompt);
                SetClaudeEvent("UserPromptSubmit");
                string workingDirectory = Directory.Exists(activity.WorkingDirectory)
                    ? activity.WorkingDirectory
                    : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                PetCharacter conversationCharacter = PetCharacter.Find(String.IsNullOrWhiteSpace(activity.CharacterId) ? currentCharacterId : activity.CharacterId);
                claudeRunner.Start(PetBrain.BuildConversationPrompt(conversationCharacter, prompt), workingDirectory, activity.SessionId);
                return true;
            }
            catch (Exception ex)
            {
                activity.State = "受阻";
                activity.Unread = true;
                activity.AppendOutput("[无法续接会话] " + ex.Message + "\r\n");
                activity.Updated = DateTime.Now;
                currentActivity = null;
                RefreshActivities();
                replyBubble.SetStatus("无法续接 Claude 会话");
                replyBubble.AppendReply(ex.Message);
                ShowReplyBubble();
                SetClaudeEvent("StopFailure");
                return false;
            }
        }

        private void ShowQuickChat()
        {
            ShowPet();
            WindowState = FormWindowState.Normal;
            replyBubble.Hide();
            activityTray.HideUnlessPinned();
            reminderForm.Hide();
            claudeTaskForm.Hide();
            promptForm.ShowNear(Bounds);
        }

        private void ShowActivities()
        {
            promptForm.Hide();
            replyBubble.Hide();
            reminderForm.Hide();
            claudeTaskForm.Hide();
            RefreshActivities();
            activityTray.ShowNear(Bounds);
        }

        private void OpenReplyConversation()
        {
            promptForm.Hide();
            replyBubble.Hide();
            reminderForm.Hide();
            claudeTaskForm.Hide();
            RefreshActivities();
            activityTray.ShowNear(Bounds);
            ActivityRecord target = lastReplyActivity ?? (activities.Count > 0 ? activities[0] : null);
            activityTray.SelectActivity(target);
        }

        private void ShowReminderSettings()
        {
            ShowPet();
            WindowState = FormWindowState.Normal;
            promptForm.Hide();
            replyBubble.Hide();
            activityTray.HideUnlessPinned();
            claudeTaskForm.Hide();
            reminderForm.ShowNear(Bounds);
        }

        private void ShowClaudeTaskSettings()
        {
            ShowPet();
            WindowState = FormWindowState.Normal;
            promptForm.Hide();
            replyBubble.Hide();
            activityTray.HideUnlessPinned();
            reminderForm.Hide();
            claudeTaskForm.ShowNear(Bounds, promptForm.WorkingDirectory);
        }

        private void ChooseDefaultClaudeDirectory()
        {
            if (promptForm.ChooseWorkingDirectory(this))
                quoteBubble.ShowQuote("Claude CLI 默认目录已设为：" + promptForm.WorkingDirectory, Bounds);
        }

        private void BeginReplyBubble(string prompt)
        {
            promptForm.Hide();
            activityTray.HideUnlessPinned();
            reminderForm.Hide();
            claudeTaskForm.Hide();
            EnsureRoomForReplyBubbleBelow();
            replyBubble.BeginRequest(prompt, Bounds);
        }

        private void ShowReplyBubble(bool force = false)
        {
            if (userHidden && !force) return;
            if (force && userHidden) ShowPet();
            if (!force && replyBubble.UserDismissed) return;
            if (activityTray.PinnedOpen) return;
            promptForm.Hide();
            activityTray.HideUnlessPinned();
            reminderForm.Hide();
            claudeTaskForm.Hide();
            EnsureRoomForReplyBubbleBelow();
            replyBubble.ShowNear(Bounds, force);
        }

        private void EnsureRoomForReplyBubbleBelow()
        {
            Rectangle area = Screen.FromRectangle(Bounds).WorkingArea;
            int targetTop = CalculatePetTopForReplyBubble(Bounds, replyBubble.Height, area);
            if (targetTop == Top) return;
            CancelWander();
            Top = targetTop;
            wanderOrigin = Location;
            RefreshPetVisual();
        }

        internal static int CalculatePetTopForReplyBubble(Rectangle petBounds, int bubbleHeight, Rectangle workingArea)
        {
            int maximumTop = workingArea.Bottom - petBounds.Height - bubbleHeight - 5;
            return Math.Max(workingArea.Top, Math.Min(petBounds.Top, maximumTop));
        }

        private void QueueScheduledClaudeTask(ScheduledClaudeTask task)
        {
            if (task == null) return;
            if (!Directory.Exists(task.WorkingDirectory))
            {
                claudeTaskService.RecordSkipped(task, DateTime.Now, "任务目录不存在，本次触发已跳过：" + task.WorkingDirectory);
                return;
            }
            string overlap = ClaudeTaskService.NormalizeOverlapMode(task.OverlapMode);
            if (overlap == "block" && IsScheduledTaskActiveOrPending(task))
            {
                claudeTaskService.RecordSkipped(task, DateTime.Now, "上一轮任务仍在运行或等待执行，本次到点触发已跳过。");
                return;
            }
            if (ClaudeTaskService.NormalizeRunMode(task.RunMode) == "claude-background")
            {
                RunClaudeBackgroundTask(task);
                return;
            }
            if (overlap == "parallel")
            {
                // Parallel mode deliberately starts a fresh visible process for
                // every trigger, even while an earlier instance is still alive.
                RunVisibleScheduledClaudeTask(task);
                return;
            }
            scheduledClaudeTaskQueue.Enqueue(task);
            TryRunNextScheduledClaudeTask();
        }

        private bool IsScheduledTaskActiveOrPending(ScheduledClaudeTask task)
        {
            if (task == null || String.IsNullOrWhiteSpace(task.Id)) return false;
            int active;
            if (activeScheduledTaskCounts.TryGetValue(task.Id, out active) && active > 0) return true;
            foreach (ScheduledClaudeTask pending in scheduledClaudeTaskQueue)
                if (pending != null && String.Equals(pending.Id, task.Id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void MarkScheduledTaskStarted(ScheduledClaudeTask task)
        {
            if (task == null || String.IsNullOrWhiteSpace(task.Id)) return;
            int active;
            activeScheduledTaskCounts.TryGetValue(task.Id, out active);
            activeScheduledTaskCounts[task.Id] = active + 1;
        }

        private void MarkScheduledTaskFinished(ScheduledClaudeTask task)
        {
            if (task == null || String.IsNullOrWhiteSpace(task.Id)) return;
            int active;
            if (!activeScheduledTaskCounts.TryGetValue(task.Id, out active)) return;
            if (active <= 1) activeScheduledTaskCounts.Remove(task.Id);
            else activeScheduledTaskCounts[task.Id] = active - 1;
        }

        private void TryRunNextScheduledClaudeTask()
        {
            if (paused || brainRequestActive || claudeRunner.IsRunning || currentScheduledClaudeTask != null || scheduledClaudeTaskQueue.Count == 0) return;
            ScheduledClaudeTask task = scheduledClaudeTaskQueue.Dequeue();
            if (task == null || !Directory.Exists(task.WorkingDirectory))
            {
                TryRunNextScheduledClaudeTask();
                return;
            }
            if (ClaudeTaskService.NormalizeRunMode(task.RunMode) != "background")
            {
                RunVisibleScheduledClaudeTask(task);
                TryRunNextScheduledClaudeTask();
                return;
            }
            string folder = Path.GetFileName(task.WorkingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (String.IsNullOrWhiteSpace(folder)) folder = task.WorkingDirectory;
            MarkScheduledTaskStarted(task);
            currentScheduledClaudeTask = task;
            ClaudeTaskRunRecord runRecord = claudeTaskService.BeginRun(task, DateTime.Now);
            currentScheduledRunId = runRecord == null ? null : runRecord.Id;
            currentActivity = new ActivityRecord
            {
                WorkingDirectory = task.WorkingDirectory,
                CharacterId = currentCharacterId,
                Title = "定时任务 · " + folder,
                State = "正在运行",
                Output = "[Claude Code 定时任务] " + task.ScheduleText + "\r\n工作目录：" + task.WorkingDirectory + "\r\n\r\n任务：" + task.Prompt + "\r\n\r\n",
                Updated = DateTime.Now
            };
            activities.Insert(0, currentActivity);
            lastReplyActivity = currentActivity;
            RefreshActivities();
            BeginReplyBubble(task.Prompt);
            try
            {
                claudeRunner.Start(BuildScheduledTaskPrompt(task), task.WorkingDirectory, null, task.Id);
                SetClaudeEvent("UserPromptSubmit");
            }
            catch (Exception ex)
            {
                currentActivity.State = "受阻";
                currentActivity.Unread = true;
                currentActivity.AppendOutput("[无法执行定时任务] " + ex.Message + "\r\n");
                currentActivity.Updated = DateTime.Now;
                claudeTaskService.CompleteRun(task, currentScheduledRunId, false, -1, ex.Message, DateTime.Now);
                MarkScheduledTaskFinished(task);
                currentScheduledClaudeTask = null;
                currentScheduledRunId = null;
                currentActivity = null;
                replyBubble.SetStatus("定时任务启动失败");
                replyBubble.AppendReply(ex.Message);
                ShowReplyBubble();
                RefreshActivities();
                TryRunNextScheduledClaudeTask();
            }
        }

        internal static string BuildScheduledTaskPrompt(ScheduledClaudeTask task)
        {
            if (task == null) return "";
            return "你正在通过桌面宠物执行一个用户预先配置的 Claude Code 定时任务。\n实际工作目录：" + task.WorkingDirectory + "\n请先确认当前工作目录就是上述目录，并以该目录为唯一任务根目录；如任务会产生文件修改，请直接完成并在回复中简要说明结果。\n\n任务：\n" + task.Prompt;
        }

        private void RunVisibleScheduledClaudeTask(ScheduledClaudeTask task)
        {
            MarkScheduledTaskStarted(task);
            ClaudeTaskRunRecord run = claudeTaskService.BeginRun(task, DateTime.Now);
            // Parallel background tasks retain their existing visible-window
            // behavior; history must describe the actual host used this run.
            string requestedMode = ClaudeTaskService.NormalizeRunMode(task.RunMode);
            string visibleMode = requestedMode == "background" ? "powershell" : requestedMode;
            bool interactive = ClaudeTaskService.IsInteractiveMode(visibleMode);
            if (run != null) run.RunMode = visibleMode;
            string mode = ClaudeTaskService.RunModeLabel(visibleMode);
            string folder = Path.GetFileName(task.WorkingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (String.IsNullOrWhiteSpace(folder)) folder = task.WorkingDirectory;
            string header = "[" + mode + " 定时任务] " + task.ScheduleText + "\r\n工作目录：" + task.WorkingDirectory + "\r\n\r\n任务：" + task.Prompt + "\r\n\r\n";
            ActivityRecord activity = new ActivityRecord
            {
                WorkingDirectory = task.WorkingDirectory, CharacterId = currentCharacterId,
                Title = mode + " 定时任务 · " + folder, State = "正在运行",
                Output = header, Updated = DateTime.Now, NativeInteractive = interactive
            };
            activities.Insert(0, activity);
            RefreshActivities();
            bool ownsBubble = !claudeRunner.IsRunning && !brainRequestActive;
            if (ownsBubble)
            {
                lastReplyActivity = activity;
                BeginReplyBubble(task.Prompt);
                replyBubble.SetStatus(mode + " 定时任务正在启动…");
                SetClaudeEvent("UserPromptSubmit");
            }
            string logDirectory = Path.Combine(Program.StateDirectory, "scheduled-task-runs");
            string logPath = Path.Combine(logDirectory, (run == null ? Guid.NewGuid().ToString("N") : run.Id) + ".log");
            VisibleTaskMonitor monitor = null;
            string receivedOutput = "";
            try
            {
                Directory.CreateDirectory(logDirectory);
                ProcessStartInfo info = BuildVisibleTaskStartInfo(task, logPath);
                monitor = new VisibleTaskMonitor(logPath, delegate(string output)
                {
                    activity.Output = header + output;
                    activity.Updated = DateTime.Now;
                    claudeTaskService.UpdateRunProgress(task, run == null ? null : run.Id, output);
                    if (ownsBubble && lastReplyActivity == activity && !claudeRunner.IsRunning && !brainRequestActive)
                    {
                        string delta = output.StartsWith(receivedOutput, StringComparison.Ordinal) ? output.Substring(receivedOutput.Length) : output;
                        replyBubble.AppendReply(delta);
                        if (!replyBubble.Visible && !replyBubble.UserDismissed) ShowReplyBubble();
                    }
                    receivedOutput = output;
                    RefreshActivities();
                }, delegate(int completedCode, string completedOutput)
                {
                    activity.Output = header + completedOutput;
                    activity.State = completedCode == 0 ? "就绪" : "受阻";
                    activity.Unread = true;
                    activity.Updated = DateTime.Now;
                    claudeTaskService.CompleteRun(task, run == null ? null : run.Id, completedCode == 0, completedCode, completedOutput, DateTime.Now);
                    RefreshActivities();
                    MarkScheduledTaskFinished(task);
                    if (!claudeRunner.IsRunning && !brainRequestActive && activeScheduledTaskCounts.Count == 0)
                        SetClaudeEvent(completedCode == 0 ? "Stop" : "StopFailure");
                    if (ownsBubble && lastReplyActivity == activity && !claudeRunner.IsRunning && !brainRequestActive)
                    {
                        string remaining = completedOutput.StartsWith(receivedOutput, StringComparison.Ordinal) ? completedOutput.Substring(receivedOutput.Length) : completedOutput;
                        if (String.IsNullOrWhiteSpace(completedOutput)) remaining = completedCode == 0 ? mode + " 定时任务已完成。" : mode + " 定时任务执行失败，退出码 " + completedCode + "。";
                        replyBubble.AppendReply(remaining);
                        replyBubble.FlushPendingReply();
                        replyBubble.SetStatus(mode + (completedCode == 0 ? " 定时任务已完成" : " 定时任务失败"));
                        if (activeScheduledTaskCounts.Count > 0) SetClaudeEvent("UserPromptSubmit");
                    }
                    visibleTaskMonitors.Remove(monitor);
                    // Preserve per-run script/log/markers: an open Terminal
                    // window can still display its output after the pet exits.
                    TryRunNextScheduledClaudeTask();
                }, delegate(string status, string sessionId, string reaction)
                {
                    activity.SessionId = sessionId;
                    activity.State = status;
                    activity.Updated = DateTime.Now;
                    if (ownsBubble && lastReplyActivity == activity && !claudeRunner.IsRunning && !brainRequestActive)
                    {
                        replyBubble.SetStatus(status);
                        SetClaudeEvent(!interactive && (reaction == "Stop" || reaction == "StopFailure") ? "PostToolUse" : reaction);
                    }
                    RefreshActivities();
                });
                visibleTaskMonitors.Add(monitor);
                monitor.Start(info);
            }
            catch (Exception ex)
            {
                if (monitor != null) { visibleTaskMonitors.Remove(monitor); monitor.Dispose(); }
                activity.AppendOutput("[启动失败] " + ex.Message + "\r\n");
                activity.State = "受阻";
                activity.Unread = true;
                activity.Updated = DateTime.Now;
                claudeTaskService.CompleteRun(task, run == null ? null : run.Id, false, -1, ex.Message, DateTime.Now);
                MarkScheduledTaskFinished(task);
                if (!claudeRunner.IsRunning && !brainRequestActive && activeScheduledTaskCounts.Count == 0) SetClaudeEvent("StopFailure");
                if (ownsBubble && lastReplyActivity == activity && !claudeRunner.IsRunning && !brainRequestActive)
                {
                    replyBubble.AppendReply("[启动失败] " + ex.Message);
                    replyBubble.SetStatus("定时任务启动失败");
                    SetClaudeEvent("StopFailure");
                }
                RefreshActivities();
            }
        }

        internal static ProcessStartInfo BuildVisibleTaskStartInfo(ScheduledClaudeTask task, string logPath)
        {
            return BuildVisibleTaskStartInfo(task, logPath, "claude");
        }

        internal static ProcessStartInfo BuildVisibleTaskStartInfo(ScheduledClaudeTask task, string logPath, string claudeCommand)
        {
            if (task == null) throw new ArgumentNullException("task");
            if (!Directory.Exists(task.WorkingDirectory)) throw new DirectoryNotFoundException("任务目录不存在：" + task.WorkingDirectory);
            string directory = Path.GetFullPath(task.WorkingDirectory);
            bool interactive = ClaudeTaskService.IsInteractiveMode(task.RunMode);
            bool terminal = ClaudeTaskService.NormalizeRunMode(task.RunMode) == "terminal" || ClaudeTaskService.NormalizeRunMode(task.RunMode) == "interactive-terminal";
            string terminalToken = "OrbitTask-" + Guid.NewGuid().ToString("N");
            string terminalPath = terminal ? FindWindowsTerminal() : null;
            // npm installs Claude as claude.cmd on Windows. Passing a multiline
            // native-process argument through cmd.exe can split the remaining
            // lines into commands and leave Claude waiting. Preserve the text
            // while flattening line breaks for the visible -p argument.
            string visiblePrompt = BuildScheduledTaskPrompt(task).Replace("\r", " ").Replace("\n", " ");
            while (visiblePrompt.IndexOf("  ", StringComparison.Ordinal) >= 0) visiblePrompt = visiblePrompt.Replace("  ", " ");
            string promptBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(visiblePrompt.Trim()));
            string safeDirectory = directory.Replace("'", "''");
            string safeLog = Path.GetFullPath(logPath).Replace("'", "''");
            string safeClaude = (String.IsNullOrWhiteSpace(claudeCommand) ? "claude" : claudeCommand).Replace("'", "''");
            string safeTaskId = (task.Id ?? "").Replace("'", "''");
            string safePet = typeof(Program).Assembly.Location.Replace("'", "''");
            string desktopScript = "";
            if (task.DesktopNumber > 0)
            {
                Guid target = VirtualDesktopService.ResolveDesktop(task.DesktopNumber);
                string executable = typeof(Program).Assembly.Location.Replace("'", "''");
                string resultPath = safeLog + ".desktop";
                // Wait for the helper before invoking Claude, so a missing desktop
                // or failed placement cannot silently run the task on this one.
                desktopScript = "$placer=Start-Process -FilePath '" + executable + "' -ArgumentList @('" + (terminal ? "--place-task-terminal" : "--place-task-console") + "','" + target.ToString("D") + "','\"" + resultPath + "\"'," + (terminal ? "'" + terminalToken + "'" : "[string]$PID") + ") -WindowStyle Hidden -Wait -PassThru; " +
                    "if($placer.ExitCode -ne 0){$code=50; $reason='虚拟桌面定位失败'; if(Test-Path -LiteralPath '" + resultPath + "'){$reason=[IO.File]::ReadAllText('" + resultPath + "')}; throw $reason}; " +
                    "[IO.File]::AppendAllText('" + safeLog + "','[窗口已定位至桌面 " + task.DesktopNumber + "]'+[Environment]::NewLine,[Text.Encoding]::UTF8); ";
            }
            string invocation;
            if (interactive)
            {
                string runToken = Guid.NewGuid().ToString("N"), session = Guid.NewGuid().ToString("D");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(logPath)));
                File.WriteAllText(logPath + ".interactive", new JavaScriptSerializer().Serialize(new { Token = runToken, TaskId = task.Id, SessionId = session }), new UTF8Encoding(false));
                invocation = "$ErrorActionPreference='Stop'; $env:FUFU_PET_NATIVE_RUN_LOG='" + safeLog + "'; $env:FUFU_PET_NATIVE_RUN_TOKEN='" + runToken + "'; " +
                    "[IO.File]::AppendAllText('" + safeLog + "','[原生 CLI] 回复、输入框、权限确认及斜杠命令均由 Claude Code 自己显示。'+[Environment]::NewLine,$utf8); " +
                    "[IO.File]::WriteAllText('" + safeLog + ".state','Claude 原生 CLI 交互中'+[Environment]::NewLine+'" + session + "'+[Environment]::NewLine+'UserPromptSubmit',$utf8); " +
                    // Native CLI owns the console: no -p, pipes or SendKeys.
                    "& '" + safeClaude + "' --session-id '" + session + "' $prompt; ";
            }
            else
                invocation = "Remove-Item Env:FUFU_PET_NATIVE_RUN_LOG,Env:FUFU_PET_NATIVE_RUN_TOKEN -ErrorAction SilentlyContinue; " +
                    "[void][Reflection.Assembly]::LoadFrom('" + safePet + "'); $renderer=New-Object XianPal.ClaudeTaskStreamRenderer; $previousState=''; " +
                    "& '" + safeClaude + "' -p $prompt --verbose --output-format stream-json --include-partial-messages 2>&1 | ForEach-Object { " +
                    "$chunk=$renderer.Consume([string]$_); if($chunk.Length -gt 0){Write-Host -NoNewline $chunk; [IO.File]::AppendAllText('" + safeLog + "',$chunk,$utf8)}; " +
                    "$state=$renderer.Status+[Environment]::NewLine+$renderer.SessionId+[Environment]::NewLine+$renderer.Reaction; " +
                    "if($state -ne $previousState){[IO.File]::WriteAllText('" + safeLog + ".state',$state,$utf8); $previousState=$state} }; ";
            string script = "$code=1; $taskWindowToken='" + terminalToken + "'; $utf8=New-Object Text.UTF8Encoding($false); " +
                "$OutputEncoding=$utf8; [Console]::InputEncoding=$utf8; [Console]::OutputEncoding=$utf8; " +
                "[IO.File]::WriteAllText('" + safeLog + ".started',([string]$PID+'|'+[string]([Diagnostics.Process]::GetCurrentProcess().StartTime.ToUniversalTime().Ticks)),$utf8); " +
                "try { $ErrorActionPreference='Stop'; Set-Location -LiteralPath '" + safeDirectory + "'; " +
                "$env:FUFU_PET_INTERNAL_TASK_ID='" + safeTaskId + "'; " +
                "$env:FUFU_PET_MANAGED_SESSION='1'; $env:PWD='" + safeDirectory + "'; " +
                "$prompt=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + promptBase64 + "')); " +
                desktopScript +
                "Write-Host ('[任务] '+$prompt); [IO.File]::AppendAllText('" + safeLog + "','[任务] '+$prompt+[Environment]::NewLine,$utf8); " +
                "$ErrorActionPreference='Continue'; " +
                invocation +
                "$code=$LASTEXITCODE; if($null -eq $code){$code=1} " +
                "} catch { $line='[执行失败] '+[string]$_; Write-Host $line; [IO.File]::AppendAllText('" + safeLog + "',$line+[Environment]::NewLine,$utf8) } " +
                "finally { [IO.File]::WriteAllText('" + safeLog + ".completed.tmp',[string][int]$code,$utf8); [IO.File]::Move('" + safeLog + ".completed.tmp','" + safeLog + ".completed'); " +
                (interactive ? "Remove-Item Env:FUFU_PET_NATIVE_RUN_LOG,Env:FUFU_PET_NATIVE_RUN_TOKEN,Env:FUFU_PET_INTERNAL_TASK_ID,Env:FUFU_PET_MANAGED_SESSION -ErrorAction SilentlyContinue; " : "") + "}; " +
                (terminal || interactive ? "Write-Host ('[任务结束] 退出码：'+$code)" : "exit [int]$code");
            string scriptPath = Path.GetFullPath(logPath) + ".ps1";
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath));
            File.WriteAllText(scriptPath, script, new UTF8Encoding(true));
            File.WriteAllText(logPath, "", new UTF8Encoding(false));
            // A caller/test may reuse its private path. Never read an old run's
            // completion marker as the result of a fresh execution.
            foreach (string suffix in new[] { ".started", ".completed", ".completed.tmp", ".state", ".reply" })
                if (File.Exists(logPath + suffix)) File.Delete(logPath + suffix);
            string shellArguments = "-NoLogo -NoProfile " + (terminal || interactive ? "-NoExit " : "") + "-ExecutionPolicy Bypass -File \"" + scriptPath.Replace("\"", "\"\"") + "\"";
            if (terminal)
                return new ProcessStartInfo(terminalPath, "-w new new-tab --title " + terminalToken + " --suppressApplicationTitle -d \"" + directory + "\" powershell.exe " + shellArguments)
                { WorkingDirectory = directory, UseShellExecute = true, CreateNoWindow = false, WindowStyle = ProcessWindowStyle.Normal };
            // Force a separate classic console for targeted tasks. Never move a
            // Windows Terminal window that may contain the user's other tabs.
            string host = task.DesktopNumber > 0 ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "conhost.exe") : "powershell.exe";
            return new ProcessStartInfo(host, task.DesktopNumber > 0 ? "powershell.exe " + shellArguments : shellArguments)
            {
                WorkingDirectory = directory,
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };
        }

        internal static string FindWindowsTerminal()
        {
            string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
            if (File.Exists(local)) return local;
            foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try
                {
                    string candidate = Path.Combine(directory.Trim('"'), "wt.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException) { }
            }
            throw new FileNotFoundException("未找到 Windows Terminal（wt.exe）。内网机器可选择 PowerShell 窗口模式，无需安装 Windows Terminal。");
        }

        private void QueueReminder(DailyReminder reminder)
        {
            if (reminder == null) return;
            reminderQueue.Enqueue(reminder);
            if (reminderOverlay == null) ShowNextReminder();
        }

        private void ShowNextReminder()
        {
            if (reminderOverlay != null || reminderQueue.Count == 0) return;
            DailyReminder reminder = reminderQueue.Dequeue();
            if (!petWasVisibleBeforeReminder) petWasVisibleBeforeReminder = Visible;
            promptForm.Hide();
            replyBubble.Hide();
            activityTray.HideUnlessPinned();
            reminderForm.Hide();
            claudeTaskForm.Hide();
            quoteBubble.Hide();
            CancelWander();
            Hide();

            Rectangle reminderScreenBounds = Screen.FromRectangle(Bounds).Bounds;
            PetCharacter reminderCharacter = PetCharacter.Find(currentCharacterId);
            reminderOverlay = new ReminderOverlayForm(reminder, animations, reduceMotion, reminderCharacter.ReminderStyle, reminderCharacter.Name, reminderScreenBounds);
            reminderOverlay.Dismissed += FinishReminder;
            reminderOverlay.SnoozeRequested += delegate(DailyReminder item, int minutes)
            {
                reminderService.Snooze(item, minutes);
                FinishReminder();
            };
            reminderOverlay.Show();
        }

        private void FinishReminder()
        {
            ReminderOverlayForm completed = reminderOverlay;
            reminderOverlay = null;
            if (completed != null) completed.Dispose();
            if (reminderQueue.Count > 0)
            {
                ShowNextReminder();
                return;
            }
            if (petWasVisibleBeforeReminder && !userHidden)
            {
                ShowPet();
                WindowState = FormWindowState.Normal;
                TopMost = TopMost;
                SetTemporaryState("waving", 1600, "idle");
            }
            petWasVisibleBeforeReminder = false;
            ScheduleNextWander();
        }

        private void RefreshActivities()
        {
            activityTray.SetActivities(activities);
        }

        private void CompleteCurrentActivity(string finalState)
        {
            if (currentActivity == null) return;
            lastReplyActivity = currentActivity;
            currentActivity.State = finalState;
            currentActivity.Updated = DateTime.Now;
            currentActivity.Unread = true;
            RefreshActivities();
            currentActivity = null;
        }

        private void TrackExternalActivity(string eventName, string sessionId, string cwd)
        {
            ActivityRecord record = activities.Find(delegate(ActivityRecord item) { return item.SessionId == sessionId; });
            if (record == null)
            {
                string folder = String.IsNullOrEmpty(cwd) ? "Claude Code 会话" : Path.GetFileName(cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (String.IsNullOrEmpty(folder)) folder = cwd;
                record = new ActivityRecord { SessionId = sessionId, WorkingDirectory = cwd, Title = folder, State = "正在运行", Output = "外部 Claude Code CLI 会话\r\n" + (cwd ?? "") + "\r\n\r\n", Updated = DateTime.Now };
                activities.Insert(0, record);
            }
            else if (!String.IsNullOrWhiteSpace(cwd)) record.WorkingDirectory = cwd;
            switch (eventName)
            {
                case "UserPromptSubmit":
                case "PreToolUse":
                case "PostToolUse": record.State = "正在运行"; record.Unread = false; break;
                case "PermissionRequest":
                case "Notification": record.State = "需要输入"; record.Unread = true; break;
                case "PostToolUseFailure":
                case "StopFailure": record.State = "受阻"; record.Unread = true; break;
                case "Stop":
                case "TaskCompleted": record.State = "就绪"; record.Unread = true; break;
                case "SessionEnd": if (record.State == "正在运行") record.State = "就绪"; break;
            }
            record.Updated = DateTime.Now;
            RefreshActivities();
        }

        private void ApplyActivityPriority()
        {
            ActivityRecord selected = activities.Find(delegate(ActivityRecord item) { return item.Unread && item.State == "需要输入"; });
            if (selected != null) { SetState("waiting"); SetTrayText("需要输入：" + selected.Title); return; }
            selected = activities.Find(delegate(ActivityRecord item) { return item.Unread && item.State == "受阻"; });
            if (selected != null) { SetState("failed"); SetTrayText("受阻：" + selected.Title); return; }
            selected = activities.Find(delegate(ActivityRecord item) { return item.Unread && item.State == "就绪"; });
            if (selected != null) { SetState("jumping"); SetTrayText("已完成：" + selected.Title); return; }
            selected = activities.Find(delegate(ActivityRecord item) { return item.State == "正在运行"; });
            if (selected != null) { SetState("running"); SetTrayText("正在运行：" + selected.Title); return; }
            SetState("idle");
        }

        private void HidePet()
        {
            userHidden = true;
            petWasVisibleBeforeReminder = false;
            CancelWander();
            clickDelayTimer.Stop();
            hovering = false;
            promptForm.Hide();
            replyBubble.Dismiss();
            activityTray.Hide();
            reminderForm.Hide();
            claudeTaskForm.Hide();
            quoteBubble.Hide();
            Hide();
            UpdateVisibilityMenu();
        }

        private void AnchorAuxiliaryWindows()
        {
            if (promptForm.Visible) promptForm.ShowNear(Bounds);
            if (replyBubble.Visible) replyBubble.ShowNear(Bounds);
            if (activityTray.Visible) activityTray.ShowNear(Bounds);
            if (reminderForm.Visible) reminderForm.ShowNear(Bounds);
            if (claudeTaskForm.Visible) claudeTaskForm.ShowNear(Bounds, promptForm.WorkingDirectory);
            if (quoteBubble.Visible) quoteBubble.Reposition(Bounds);
        }

        private void SetPetScale(float scale, bool save)
        {
            if (Single.IsNaN(scale) || Single.IsInfinity(scale)) scale = 0.5f;
            petScale = Math.Max(0.5f, Math.Min(1.25f, scale));
            // Discard the old canvas before changing native HWND dimensions.
            // Handle creation/paint during ClientSize assignment must not submit
            // a previous-size HBITMAP and stretch/clip its rows.
            Size expected = PetCanvasSize(petScale);
            bool sizeChanged = ClientSize != expected || cachedCanvasSize != expected;
            if (sizeChanged) DisposeLayeredFrameCache();
            ClientSize = expected;
            if (!String.IsNullOrEmpty(dockedEdge)) ApplyEdgeDock(Screen.FromRectangle(Bounds).WorkingArea);
            if (sizeChanged && animations.Count > 0) RebuildLayeredFrameCache();
            RefreshPetVisual();
            AnchorAuxiliaryWindows();
            if (save) SavePreferences();
        }

        private void SetCharacter(string characterId, bool save)
        {
            PetCharacter character = PetCharacter.Find(characterId);
            if (String.Equals(character.Id, currentCharacterId, StringComparison.OrdinalIgnoreCase) && animations.Count > 0 && loadedCharacterRevision == (character.ResourceRevision ?? ""))
            {
                UpdateCharacterChecks();
                return;
            }
            try { LoadCharacterAnimations(character.Id); }
            catch (Exception ex) { MessageBox.Show("角色资源加载失败，已保留当前形象。\n" + ex.Message, Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            CancelWander();
            currentCharacterId = character.Id;
            trailColors = PetBrain.DefaultColors(character.Id);
            trailStyle = "rainbow";
            trailOverlay.Configure(trailColors, trailStyle, trailOpacity);
            state = "idle";
            frameIndex = 0;
            Text = character.Name + " · " + Program.ProductName;
            UpdateCharacterChecks();
            RefreshTrayIcon();
            RefreshPetVisual();
            if (save) SavePreferences();
        }

        private void UpdateCharacterChecks()
        {
            foreach (ToolStripMenuItem item in characterMenuItems)
                item.Checked = String.Equals(Convert.ToString(item.Tag), currentCharacterId, StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshTrayIcon()
        {
            if (!animations.ContainsKey("idle") || animations["idle"].Count == 0) return;
            using (Bitmap iconBitmap = AppIcon.CreateBitmap(animations["idle"][0], 32))
            {
                IntPtr handle = iconBitmap.GetHicon();
                try
                {
                    Icon previous = tray.Icon;
                    tray.Icon = (Icon)Icon.FromHandle(handle).Clone();
                    Icon = AppIcon.WindowIcon;
                    if (previous != null) previous.Dispose();
                }
                finally { DestroyIcon(handle); }
            }
            tray.Text = PetCharacter.Find(currentCharacterId).Name + " · Claude Code";
        }

        private void ResetPosition()
        {
            ReleaseEdgeDock();
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            defaultLocation = new Point(area.Right - Width - 32, area.Bottom - Height - 24);
            Location = defaultLocation;
            wanderOrigin = Location;
            ScheduleNextWander();
            AnchorAuxiliaryWindows();
        }

        private void LoadPreferences()
        {
            reduceMotion = !SystemAnimationsEnabled();
            if (Environment.GetEnvironmentVariable("FUFU_TEST_MODE") == "1")
            {
                SetPetScale(0.5f, false);
                ResetPosition();
                return;
            }
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\OrbitPet"))
                {
                    if (key != null)
                    {
                        desktopScope = Convert.ToString(key.GetValue("DesktopScope")) == "first" ? "first" : "all";
                        object revisionValue = key.GetValue("ScaleDefaultRevision");
                        bool useHalfSizeDefault = revisionValue == null || Convert.ToInt32(revisionValue) < 1;
                        object scaleValue = useHalfSizeDefault ? null : key.GetValue("Scale");
                        float savedScale;
                        if (scaleValue != null && Single.TryParse(Convert.ToString(scaleValue), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out savedScale))
                            SetPetScale(savedScale, false);
                        else
                            SetPetScale(0.5f, false);
                        object motionValue = key.GetValue("ReduceMotion");
                        if (motionValue != null) reduceMotion = Convert.ToInt32(motionValue) != 0;
                        object trailOpacityValue = key.GetValue("TrailOpacity");
                        float savedTrailOpacity;
                        if (trailOpacityValue != null && Single.TryParse(Convert.ToString(trailOpacityValue), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out savedTrailOpacity))
                            trailOpacity = TrailOverlayForm.ClampOpacity(savedTrailOpacity);
                        object pathModeValue = key.GetValue("MovementPathMode");
                        movementPathMode = pathModeValue != null && String.Equals(Convert.ToString(pathModeValue), "ai", StringComparison.OrdinalIgnoreCase) ? "ai" : "builtin";
                        object careEnabledValue = key.GetValue("AiCareEnabled");
                        if (careEnabledValue != null) aiCareEnabled = Convert.ToInt32(careEnabledValue) != 0;
                        object breakMinutesValue = key.GetValue("BreakReminderMinutes");
                        if (breakMinutesValue != null)
                        {
                            int savedBreakMinutes = Convert.ToInt32(breakMinutesValue);
                            if (savedBreakMinutes == 30 || savedBreakMinutes == 45 || savedBreakMinutes == 60 || savedBreakMinutes == 90) breakReminderMinutes = savedBreakMinutes;
                        }
                        object characterValue = key.GetValue("Character");
                        if (characterValue != null) SetCharacter(Convert.ToString(characterValue), false);
                        object dockValue = key.GetValue("DockEdge");
                        dockedEdge = dockValue == null ? "" : Convert.ToString(dockValue).ToLowerInvariant();
                        if (dockedEdge != "left" && dockedEdge != "right" && dockedEdge != "bottom") dockedEdge = "";
                        object xValue = key.GetValue("X");
                        object yValue = key.GetValue("Y");
                        if (xValue != null && yValue != null)
                        {
                            Point saved = new Point(Convert.ToInt32(xValue), Convert.ToInt32(yValue));
                            Rectangle[] workAreas = Array.ConvertAll(Screen.AllScreens, delegate(Screen screen) { return screen.WorkingArea; });
                            if (HasVisiblePetArea(new Rectangle(saved, Size), workAreas))
                            {
                                Location = saved;
                                if (!String.IsNullOrEmpty(dockedEdge)) ApplyEdgeDock(Screen.FromRectangle(Bounds).WorkingArea);
                            }
                            else ResetPosition();
                        }
                        else ResetPosition();
                        wanderOrigin = Location;
                        ScheduleNextWander();
                        return;
                    }
                }
            }
            catch { }
            SetPetScale(0.5f, false);
            ResetPosition();
        }

        private void SavePreferences()
        {
            if (Environment.GetEnvironmentVariable("FUFU_TEST_MODE") == "1") return;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\OrbitPet"))
                {
                    key.SetValue("X", Left, RegistryValueKind.DWord);
                    key.SetValue("DesktopScope", desktopScope, RegistryValueKind.String);
                    key.SetValue("Y", Top, RegistryValueKind.DWord);
                    key.SetValue("Scale", petScale.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    key.SetValue("ScaleDefaultRevision", 1, RegistryValueKind.DWord);
                    key.SetValue("ReduceMotion", reduceMotion ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("Character", currentCharacterId, RegistryValueKind.String);
                    key.SetValue("DockEdge", dockedEdge, RegistryValueKind.String);
                    key.SetValue("TrailOpacity", trailOpacity.ToString(System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.String);
                    key.SetValue("MovementPathMode", movementPathMode, RegistryValueKind.String);
                    key.SetValue("AiCareEnabled", aiCareEnabled ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("BreakReminderMinutes", breakReminderMinutes, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        private static bool SystemAnimationsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Control Panel\\Accessibility\\Animation"))
                {
                    object value = key == null ? null : key.GetValue("MinAnimate");
                    return value == null || Convert.ToString(value) != "0";
                }
            }
            catch { return true; }
        }

        private void RunOnUi(MethodInvoker action)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch { }
        }

        private void DisposeResources()
        {
            closingPet = true;
            DisposeCharacterPackWatching();
            backgroundTaskSupervisor.Dispose();
            visibilityTimer.Stop();
            visibilityTimer.Dispose();
            SystemEvents.SessionSwitch -= SystemSessionSwitch;
            SystemEvents.PowerModeChanged -= SystemPowerModeChanged;
            timer.Stop();
            clickDelayTimer.Stop();
            fileWipeTimer.Stop();
            clickDelayTimer.Dispose();
            fileWipeTimer.Dispose();
            claudeRunner.Dispose();
            reminderService.Dispose();
            claudeTaskService.Dispose();
            promptForm.Dispose();
            replyBubble.Dispose();
            activityTray.Dispose();
            quoteBubble.Dispose();
            reminderForm.Dispose();
            claudeTaskForm.Dispose();
            foreach (VisibleTaskMonitor monitor in new List<VisibleTaskMonitor>(visibleTaskMonitors)) monitor.Dispose();
            visibleTaskMonitors.Clear();
            trailOverlay.Dispose();
            clickActionsMenu.Dispose();
            if (reminderOverlay != null) reminderOverlay.Dispose();
            if (fileEatOverlay != null) fileEatOverlay.Dispose();
            tray.Visible = false;
            tray.Dispose();
            DisposeWalkingExpressions();
            DisposeLayeredFrameCache();
            foreach (List<Image> frames in animations.Values)
                foreach (Image image in frames) image.Dispose();
        }
    }
}
