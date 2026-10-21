using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle(XianPal.StartupRegistration.DisplayName)]
[assembly: System.Reflection.AssemblyProduct(XianPal.StartupRegistration.DisplayName)]
[assembly: System.Reflection.AssemblyVersion("0.5.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.5.0.0")]

namespace XianPal
{
    internal static class Program
    {
        internal const string ProductName = StartupRegistration.DisplayName;

        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length == 4 && (args[0] == "--place-task-console" || args[0] == "--place-task-terminal"))
            {
                try
                {
                    if (args[0] == "--place-task-terminal") VirtualDesktopService.PlaceTaskTerminal(new Guid(args[1]), args[3]);
                    else VirtualDesktopService.PlaceParentConsole(new Guid(args[1]), UInt32.Parse(args[3]));
                    File.WriteAllText(args[2], "OK", Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    File.WriteAllText(args[2], ex.Message, Encoding.UTF8);
                    Environment.ExitCode = 50;
                }
                return;
            }
            if (args.Length >= 2 && String.Equals(args[0], "--event", StringComparison.OrdinalIgnoreCase))
            {
                PublishEvent(args[1], null);
                return;
            }

            if (args.Length >= 2 && String.Equals(args[0], "--hook", StringComparison.OrdinalIgnoreCase))
            {
                if (NativeTaskHooks.IsNativeRun)
                {
                    try
                    {
                        using (StreamReader reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false, false), true))
                            NativeTaskHooks.Publish(args[1], reader.ReadToEnd());
                    }
                    catch { }
                    return;
                }
                // A scheduled run is already tracked by the task service. Its
                // Claude hooks inherit this marker and must not create a second
                // external activity/bubble for the same conversation.
                bool managed = Environment.GetEnvironmentVariable("FUFU_PET_MANAGED_SESSION") == "1";
                if (!IntegrationPreferences.ShouldAcceptHook(Environment.GetEnvironmentVariable("FUFU_PET_INTERNAL_TASK_ID"),
                    managed ? "1" : null, IntegrationPreferences.Load().MonitorExternalSessions)) return;
                string safeMetadata = null;
                try
                {
                    // A WinExe hook may have only a redirected stdin pipe and
                    // no console. Console.InputEncoding can fail there, leaving
                    // Console.In on the system ANSI codepage. Decode the pipe
                    // explicitly rather than depending on a console handle.
                    string input;
                    using (StreamReader reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false, false), true))
                        input = reader.ReadToEnd();
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    Dictionary<string, object> source = serializer.Deserialize<Dictionary<string, object>>(input);
                    Dictionary<string, object> safe = new Dictionary<string, object>();
                    safe["pet_managed"] = managed;
                    foreach (string key in new[] { "session_id", "cwd", "notification_type", "message_id", "delta", "final" })
                    {
                        object value;
                        if (source != null && source.TryGetValue(key, out value) && value != null)
                        {
                            string text = UiText.Normalize(Convert.ToString(value));
                            safe[key] = text.Length > 12000 ? text.Substring(text.Length - 12000) : text;
                        }
                    }
                    safeMetadata = serializer.Serialize(safe);
                }
                catch { }
                PublishEvent(args[1], safeMetadata);
                return;
            }

            if (args.Length == 1 && String.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                Environment.ExitCode = PetForm.ValidateAssets(AppDomain.CurrentDomain.BaseDirectory) ? 0 : 2;
                return;
            }

            if (args.Length == 1 && String.Equals(args[0], "--character-self-test", StringComparison.OrdinalIgnoreCase))
            {
                PetForm pet = new PetForm();
                try { Environment.ExitCode = pet.ValidateAllCharacterAnimations() && PetCharacter.ValidateProfiles() ? 0 : 9; }
                finally { pet.Dispose(); }
                return;
            }

            if (args.Length == 1 && String.Equals(args[0], "--encoding-self-test", StringComparison.OrdinalIgnoreCase))
            {
                ClaudeCliRunner encodingRunner = new ClaudeCliRunner();
                string unicodeOutput = "";
                encodingRunner.OutputReceived += delegate(string value) { unicodeOutput += value; };
                try
                {
                    encodingRunner.ParseLine("{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"你好，世界\"}]}}");
                    if (unicodeOutput != "你好，世界") { Environment.ExitCode = 21; return; }
                    if (ActivityTrayForm.NormalizeOutput("\u001b[31m中文\u001b[0m\r\n回复") != "中文\n回复") { Environment.ExitCode = 22; return; }
                    string symbols = "✅ 中文 😊 🚀 ⚠️";
                    if (ActivityTrayForm.NormalizeOutput(symbols) != symbols) { Environment.ExitCode = 26; return; }
                    if (!UiText.IsSymbolTextElement("😊") || !UiText.IsSymbolTextElement("✅") || UiText.IsSymbolTextElement("中文")) { Environment.ExitCode = 29; return; }
                    string unicodeTail = UiText.TailTextElements("前文测试😊🚀完成", 5);
                    if (UiText.HasUnpairedSurrogate(unicodeTail) || unicodeTail.IndexOf("🚀", StringComparison.Ordinal) < 0) { Environment.ExitCode = 27; return; }
                    if (Encoding.UTF8.GetString(ClaudeCliRunner.EncodeInput("中文提问")) != "中文提问") { Environment.ExitCode = 23; return; }
                    ProcessStartInfo utf8Process = new ProcessStartInfo { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    ClaudeCliRunner.ConfigureUtf8(utf8Process);
                    if (utf8Process.StandardOutputEncoding.CodePage != 65001 || utf8Process.StandardErrorEncoding.CodePage != 65001) { Environment.ExitCode = 24; return; }
                    using (System.Drawing.Font font = UiText.Font(9f))
                        if (font == null || String.IsNullOrWhiteSpace(font.FontFamily.Name)) { Environment.ExitCode = 25; return; }
                    using (System.Drawing.Font contentFont = UiText.ContentFont(9f))
                        if (contentFont == null || String.IsNullOrWhiteSpace(contentFont.FontFamily.Name)) { Environment.ExitCode = 28; return; }
                    using (RichTextBox mixed = new RichTextBox())
                    {
                        mixed.Font = UiText.Font(9f);
                        mixed.Text = "中文 ✅ 😊";
                        UiText.ApplyMixedGlyphFonts(mixed);
                        if (mixed.Text != "中文 ✅ 😊") { Environment.ExitCode = 30; return; }
                        mixed.Select(0, 1);
                        if (mixed.SelectionFont == null || !String.Equals(mixed.SelectionFont.FontFamily.Name, mixed.Font.FontFamily.Name, StringComparison.OrdinalIgnoreCase)) { Environment.ExitCode = 31; return; }
                    }
                    Environment.ExitCode = 0;
                }
                finally { encodingRunner.Dispose(); }
                return;
            }

            if (args.Length == 1 && String.Equals(args[0], "--ui-self-test", StringComparison.OrdinalIgnoreCase))
            {
                PromptForm prompt = new PromptForm();
                ReplyBubbleForm reply = new ReplyBubbleForm();
                bool bubbleConversationRequested = false;
                reply.ConversationRequested += delegate { bubbleConversationRequested = true; };
                reply.TriggerConversationForTest();
                bool bubbleSelectionBlocked = reply.PreviewSelectionBlockedForTest();
                int bubbleRendersBeforeStream = reply.RenderCountForTest;
                reply.AppendReply("流");
                reply.AppendReply("式回复");
                bool bubbleStreamCoalesced = reply.HasPendingRenderForTest && reply.RenderCountForTest == bubbleRendersBeforeStream;
                reply.FlushPendingReply();
                bubbleStreamCoalesced = bubbleStreamCoalesced && !reply.HasPendingRenderForTest && reply.RenderCountForTest == bubbleRendersBeforeStream + 1 && reply.PreviewTextForTest.Contains("流式回复");
                PetQuoteForm quote = new PetQuoteForm();
                ActivityTrayForm activity = new ActivityTrayForm();
                bool activityUnicodeSymbols = activity.UnicodeSymbolsSupportedForTest();
                ReminderService uiReminderService = new ReminderService();
                ReminderForm reminderSettings = new ReminderForm(uiReminderService);
                bool previewEventTriggered = false;
                uiReminderService.ReminderDue += delegate(DailyReminder item) { previewEventTriggered = item != null && !String.IsNullOrWhiteSpace(item.Text); };
                reminderSettings.TriggerPreviewForTest();
                ActivityRecord chunks = new ActivityRecord();
                chunks.AppendOutput("Hel");
                chunks.AppendOutput("lo");
                ClaudeCliRunner assistantRunner = new ClaudeCliRunner();
                string assistantOutput = "";
                assistantRunner.OutputReceived += delegate(string value) { assistantOutput += value; };
                assistantRunner.ParseLine("{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"local reply\"}]}}");
                try
                {
                    System.Drawing.Bitmap clearTest = new System.Drawing.Bitmap(2, 2);
                    using (System.Drawing.Graphics clearGraphics = System.Drawing.Graphics.FromImage(clearTest))
                    {
                        clearGraphics.Clear(System.Drawing.Color.Green);
                        PetForm.ClearSpriteCanvas(clearGraphics);
                    }
                    bool clearsPreviousFrame = clearTest.GetPixel(0, 0).A == 0;
                    clearTest.Dispose();
                    bool reminderCutoutHasBinaryAlpha;
                    using (System.Drawing.Bitmap edgeSource = new System.Drawing.Bitmap(2, 1))
                    {
                        edgeSource.SetPixel(0, 0, System.Drawing.Color.FromArgb(96, 0, 150, 80));
                        edgeSource.SetPixel(1, 0, System.Drawing.Color.Transparent);
                        using (System.Drawing.Bitmap safeEdge = ReminderOverlayForm.CreateColorKeySafeBitmap(edgeSource, edgeSource.Size))
                            reminderCutoutHasBinaryAlpha = safeEdge.GetPixel(0, 0).A == 255 && safeEdge.GetPixel(1, 0).A == 0;
                    }
                    string hdIdlePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "idle", "00.png");
                    bool rendersSmoothAlpha = false;
                    if (File.Exists(hdIdlePath))
                    {
                        using (System.Drawing.Image hdIdle = System.Drawing.Image.FromFile(hdIdlePath))
                        using (System.Drawing.Bitmap rendered = PetForm.RenderSpriteBitmap(hdIdle, new System.Drawing.Size(96, 104)))
                        {
                            for (int y = 0; y < rendered.Height && !rendersSmoothAlpha; y++)
                                for (int x = 0; x < rendered.Width; x++)
                                {
                                    int alpha = rendered.GetPixel(x, y).A;
                                    if (alpha > 1 && alpha < 255) { rendersSmoothAlpha = true; break; }
                                }
                            int hitHaloPixels = 0;
                            for (int y = 0; y < rendered.Height; y++)
                                for (int x = 0; x < rendered.Width; x++)
                                    if (rendered.GetPixel(x, y).A == 1) hitHaloPixels++;
                            bool transparentCanvasCorners = rendered.GetPixel(0, 0).A == 0 && rendered.GetPixel(rendered.Width - 1, rendered.Height - 1).A == 0;
                            if (hitHaloPixels < 50 || !transparentCanvasCorners) { Environment.ExitCode = 32; return; }
                        }
                    }
                    activity.Opacity = 0.01;
                    activity.ShowNear(new System.Drawing.Rectangle(100, 100, 96, 104));
                    activity.HideUnlessPinned();
                    bool activityRemainsOpen = activity.Visible && activity.PinnedOpen;
                    activity.DismissForTest();
                    bool activityManualCloseOnly = !activity.Visible && !activity.PinnedOpen;
                    System.Diagnostics.ProcessStartInfo terminal = PetForm.BuildTerminalStartInfo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                    System.Diagnostics.ProcessStartInfo scheduledCli = ClaudeCliRunner.BuildStartInfo("claude.cmd", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), null);
                    PetCharacter brainCharacter = PetCharacter.Find("spark");
                    string personaPrompt = PetBrain.BuildConversationPrompt(brainCharacter, "帮我整理计划");
                    PetBrainResponse brainResult = PetBrain.Parse("TRAIL|style=aurora|colors=#FF0000,#00FF00,#0000FF,#FFE040\nPATH|curve=bezier|points=0.05,0.10;0.25,0.90;0.55,0.08;0.78,0.88;0.95,0.20\nPATH|curve=wave|points=0.08,0.80;0.26,0.12;0.52,0.86;0.76,0.18;0.94,0.72\nSAY|先吃块巧克力饼干，再开始整理吧！", brainCharacter);
                    System.Collections.Generic.List<System.Drawing.Point> brainPath = PetForm.GeneratePlayfulPath(new System.Drawing.Rectangle(0, 0, 1920, 1080), new System.Drawing.Size(96, 104), new System.Drawing.Point(1700, 900), 42);
                    System.Collections.Generic.List<System.Drawing.Point> aiBrainPath = PetForm.GenerateAiPath(new System.Drawing.Rectangle(0, 0, 1920, 1080), new System.Drawing.Size(96, 104), new System.Drawing.Point(1700, 900), brainResult.PathCurve, brainResult.PathControlPoints);
                    string aiBrainPrompt = PetBrain.BuildProactivePrompt(brainCharacter, new DateTime(2026, 9, 23, 12, 0, 0), true);
                    string breakCarePrompt = PetBrain.BuildCarePrompt(brainCharacter, new DateTime(2026, 9, 23, 12, 0, 0), "break", 60);
                    string historyTodayPrompt = PetBrain.BuildHistoryTodayPrompt(brainCharacter, new DateTime(2026, 9, 23, 12, 0, 0));
                    string[] droppedFiles = PetForm.NormalizeDroppedPaths(new[] { Application.ExecutablePath, Application.ExecutablePath }, 32);
                    string droppedDirectory = PetForm.ResolveDroppedWorkingDirectory(droppedFiles);
                    string droppedPrompt = PetForm.BuildDroppedFilesPrompt(droppedFiles);
                    int brainMinX = Int32.MaxValue, brainMaxX = Int32.MinValue;
                    foreach (System.Drawing.Point brainPoint in brainPath) { brainMinX = Math.Min(brainMinX, brainPoint.X); brainMaxX = Math.Max(brainMaxX, brainPoint.X); }
                    Control[] temporaryCloseButtons = prompt.Controls.Find("temporaryCloseButton", true);
                    System.Drawing.Point secondaryCenter = ReminderOverlayForm.CalculateScreenCenter(new System.Drawing.Rectangle(-1920, 0, 1920, 1080));
                    System.Drawing.Point leftDock = PetForm.CalculateDockedLocation(new System.Drawing.Rectangle(0, 0, 1920, 1080), new System.Drawing.Size(100, 100), new System.Drawing.Point(20, 200), "left");
                    System.Drawing.Point bottomDock = PetForm.CalculateDockedLocation(new System.Drawing.Rectangle(0, 0, 1920, 1080), new System.Drawing.Size(100, 100), new System.Drawing.Point(800, 970), "bottom");
                    System.Drawing.Rectangle bubblePet = new System.Drawing.Rectangle(1460, 620, 96, 104);
                    System.Drawing.Point bubbleLocation = ReplyBubbleForm.CalculateLocation(bubblePet, new System.Drawing.Size(340, 78), new System.Drawing.Rectangle(0, 0, 1920, 1040));
                    Environment.ExitCode = PreservesOnUserClose(prompt) && PreservesOnUserClose(reply) && PreservesOnUserClose(activity) && bubbleConversationRequested && bubbleSelectionBlocked && bubbleStreamCoalesced && ReplyBubbleForm.StreamRenderIntervalMs == 80 && activityUnicodeSymbols && activityRemainsOpen && activityManualCloseOnly &&
                        chunks.Output == "Hello" && ActivityTrayForm.NormalizeOutput("a\r\n\r\n\r\nb") == "a\n\nb" &&
                        ClaudeCliRunner.ShouldSuppressDiagnostic("[claude-code:unrecognized_model] {\"query_source\":\"sdk\"}") &&
                        assistantOutput == "local reply" &&
                        ReminderService.CrossesScheduledTime(600, new DateTime(2026, 1, 1, 9, 59, 55), new DateTime(2026, 1, 1, 10, 0, 2)) &&
                        ReminderOverlayForm.ValidateCrackLayout(new System.Drawing.Size(1920, 1080)) &&
                        ReminderOverlayForm.ValidatePunchProgression() && reminderCutoutHasBinaryAlpha &&
                        !ReminderOverlayForm.UsesImpactEffect(new DailyReminder { IsCountdown = true }) &&
                        ReminderOverlayForm.UsesImpactEffect(new DailyReminder { IsCountdown = false, Effect = "impact" }) &&
                        !ReminderOverlayForm.UsesImpactEffect(new DailyReminder { IsCountdown = false, Effect = "popup" }) &&
                        !ReminderOverlayForm.UsesPunchFist(PetCharacter.Find("orbit").ReminderStyle) &&
                        !ReminderOverlayForm.UsesPunchFist(PetCharacter.Find("spark").ReminderStyle) &&
                        !ReminderOverlayForm.UsesPunchFist(PetCharacter.Find("pippin").ReminderStyle) &&
                        !ReminderOverlayForm.UsesScreenBreak(PetCharacter.Find("orbit").ReminderStyle) &&
                        !ReminderOverlayForm.UsesScreenBreak(PetCharacter.Find("spark").ReminderStyle) &&
                        !ReminderOverlayForm.UsesScreenBreak(PetCharacter.Find("pippin").ReminderStyle) &&
                        !ReminderOverlayForm.UsesScreenBreak(PetCharacter.Find("slate").ReminderStyle) &&
                        ReminderOverlayForm.ReminderAnimationFor(PetCharacter.Find("spark").ReminderStyle, 2, 2) == "jumping" &&
                        ReminderOverlayForm.ReminderAnimationFor(PetCharacter.Find("moss").ReminderStyle, 1, 2) == "waving" &&
                        ReminderOverlayForm.ReminderAnimationFor(PetCharacter.Find("lumen").ReminderStyle, 2, 2) == "waiting" &&
                        ReminderOverlayForm.ReminderAnimationFor(PetCharacter.Find("orbit").ReminderStyle, 1, 2) == "review" &&
                        PetForm.AnimationFrameIntervalMs == 40 && PetForm.LocomotionFrameIntervalMs == 28 && PetForm.LocomotionFastFrameIntervalMs == 17 && PetForm.SmoothInteractionFrameIntervalMs == 16 && PetForm.AnimationTimerIntervalMs == 15 &&
                        PetForm.GaitFrameIntervalMs(58.0, 0) > PetForm.GaitFrameIntervalMs(58.0, 6) &&
                        PetForm.GaitFrameIntervalMs(58.0, 6) > PetForm.GaitFrameIntervalMs(210.0, 6) &&
                        PetForm.PreservesGaitPhase("running-left", "running-right") && !PetForm.PreservesGaitPhase("idle", "running-right") &&
                        PetForm.KeyboardLocomotionState(-8, false) == "running-left" && PetForm.KeyboardLocomotionState(8, true) == "running-right" && PetForm.KeyboardLocomotionState(0, true) == "running-left" &&
                        PetForm.DragPixelsPerFrame(2.0) < PetForm.DragPixelsPerFrame(0.1) &&
                        PetForm.MovementPixelsForElapsed(true, 16.0) > PetForm.MovementPixelsForElapsed(false, 16.0) &&
                        PetForm.MovementPixelsForElapsed(false, 32.0) > PetForm.MovementPixelsForElapsed(false, 16.0) &&
                        PetForm.HasReachedWanderTarget(5.0, true) && !PetForm.HasReachedWanderTarget(5.0, false) &&
                        ReminderForm.FormatRemaining(new TimeSpan(0, 5, 0)) == "剩余 05:00" &&
                        reminderSettings.PreviewButtonAvailable && previewEventTriggered && secondaryCenter == new System.Drawing.Point(-960, 540) &&
                        leftDock.X < 0 && bottomDock.Y + 100 > 1080 &&
                        clearsPreviousFrame && rendersSmoothAlpha && terminal.FileName.EndsWith("powershell.exe", StringComparison.OrdinalIgnoreCase) &&
                        terminal.Arguments.IndexOf("-NoExit", StringComparison.OrdinalIgnoreCase) >= 0 && terminal.Arguments.IndexOf("Set-Location", StringComparison.OrdinalIgnoreCase) >= 0 && terminal.Arguments.IndexOf("claude", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        String.Equals(scheduledCli.WorkingDirectory, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase) && String.Equals(scheduledCli.EnvironmentVariables["PWD"], scheduledCli.WorkingDirectory, StringComparison.OrdinalIgnoreCase) &&
                        temporaryCloseButtons.Length == 1 && temporaryCloseButtons[0].Left < 20 && PetCharacter.ValidateProfiles() &&
                        personaPrompt.Contains("绯笺") && personaPrompt.Contains("帮我整理计划") &&
                        brainResult.Speech.Contains("巧克力饼干") && brainResult.TrailStyle == "aurora" && brainResult.TrailColors.Length >= 4 &&
                        brainResult.PathCurve == "bezier" && brainResult.PathControlPoints.Length == 5 && brainResult.PathPlans.Length == 2 && aiBrainPath.Count >= 40 &&
                        aiBrainPrompt.Contains("24条") && aiBrainPrompt.Contains("恰好24行") && PetForm.AiPathBatchSize == 24 && PetForm.AiPathRefillThreshold == 4 &&
                        PetForm.ShouldRefillAiPathPool("ai", 4) && !PetForm.ShouldRefillAiPathPool("ai", 5) && !PetForm.ShouldRefillAiPathPool("builtin", 0) &&
                        breakCarePrompt.Contains("连续") && breakCarePrompt.Contains("60") && breakCarePrompt.Contains("休息") &&
                        historyTodayPrompt.Contains("9月23日") && historyTodayPrompt.Contains("3到5件") && historyTodayPrompt.Contains("不要编造") && historyTodayPrompt.Contains("绯笺") &&
                        PetForm.ShouldTriggerAiBreak(true, "ai", true, new DateTime(2026, 1, 1, 11, 0, 0), new DateTime(2026, 1, 1, 11, 0, 0)) &&
                        !PetForm.ShouldTriggerAiBreak(true, "builtin", true, new DateTime(2026, 1, 1, 11, 0, 0), new DateTime(2026, 1, 1, 10, 0, 0)) &&
                        !PetForm.ShouldTriggerAiBreak(true, "ai", false, new DateTime(2026, 1, 1, 11, 0, 0), new DateTime(2026, 1, 1, 10, 0, 0)) &&
                        droppedFiles.Length == 1 && Directory.Exists(droppedDirectory) && droppedPrompt.Contains(Application.ExecutablePath) && droppedPrompt.EndsWith("我的具体需求：", StringComparison.Ordinal) &&
                        reply.StartPosition == FormStartPosition.Manual && quote.StartPosition == FormStartPosition.Manual && bubbleLocation.X == 1338 && bubbleLocation.Y == 729 &&
                        FileEatOverlayForm.ValidateProgression() && TrailOverlayForm.ValidateTaperedLightProfile() &&
                        brainPath.Count >= 10 && brainMaxX - brainMinX > 960 && Math.Abs(TrailOverlayForm.ClampOpacity(0.01f) - 0.15f) < 0.001f &&
                        PetCharacter.Find("spark").Quotes[0].Contains("计划") && PetCharacter.Find("orbit").Quotes[0].Contains("开始") &&
                        PetCharacter.Find("moss").Quotes[0].Contains("休息") && PetCharacter.Find("pippin").Quotes[0].Contains("稳稳") &&
                        ClaudeCliRunner.BuildArguments("cc571e99-f52f-454a-96cf-785afcbef2a6").EndsWith("--resume cc571e99-f52f-454a-96cf-785afcbef2a6", StringComparison.Ordinal) ? 0 : 3;
                }
                finally { prompt.Dispose(); reply.Dispose(); quote.Dispose(); activity.Dispose(); reminderSettings.Dispose(); uiReminderService.Dispose(); assistantRunner.Dispose(); }
                return;
            }

            if (args.Length == 1 && String.Equals(args[0], "--claude-task-self-test", StringComparison.OrdinalIgnoreCase))
            {
                if (String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FUFU_STATE_DIR"))) { Environment.ExitCode = 40; return; }
                Directory.CreateDirectory(Program.StateDirectory);
                DateTime monday = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Local);
                ClaudeTaskService first = new ClaudeTaskService();
                ScheduledClaudeTask daily = first.Add("daily task", Program.StateDirectory, "daily", new TimeSpan(10, 0, 0), DayOfWeek.Monday, 60, monday);
                ScheduledClaudeTask weekly = first.Add("weekly task", Program.StateDirectory, "weekly", new TimeSpan(11, 30, 0), DayOfWeek.Wednesday, 60, monday);
                ScheduledClaudeTask interval = first.Add("interval task", Program.StateDirectory, "interval", TimeSpan.Zero, DayOfWeek.Monday, 90, monday);
                first.Update(daily, "edited daily task", Program.StateDirectory, "daily", new TimeSpan(10, 0, 0), DayOfWeek.Monday, 60, monday);
                first.Update(interval, "interval task", Program.StateDirectory, "interval", TimeSpan.Zero, DayOfWeek.Monday, 90, monday, "powershell", "parallel");
                ClaudeTaskRunRecord runRecord = first.BeginRun(interval, monday.AddMinutes(1));
                first.CompleteRun(interval, runRecord.Id, true, 0, "scheduled task completed", monday.AddMinutes(2));
                ClaudeTaskRunRecord skippedRecord = first.RecordSkipped(daily, monday.AddMinutes(3), "blocked by active run");
                string visibleTestLog = Path.Combine(Program.StateDirectory, "visible-test.log");
                ProcessStartInfo visibleInfo = PetForm.BuildVisibleTaskStartInfo(interval, visibleTestLog);
                string visibleScript = File.ReadAllText(visibleTestLog + ".ps1", Encoding.UTF8);
                ProcessStartInfo markedBackgroundInfo = ClaudeCliRunner.BuildStartInfo("claude.cmd", Program.StateDirectory, null, interval.Id);
                first.Dispose();
                ClaudeTaskService second = new ClaudeTaskService();
                int due = 0;
                second.TaskDue += delegate(ScheduledClaudeTask task) { if (task != null) due++; };
                second.CheckNow(monday.AddMinutes(91));
                ScheduledClaudeTask loadedDaily = new List<ScheduledClaudeTask>(second.Tasks).Find(delegate(ScheduledClaudeTask item) { return item.Prompt == "edited daily task"; });
                ScheduledClaudeTask loadedInterval = new List<ScheduledClaudeTask>(second.Tasks).Find(delegate(ScheduledClaudeTask item) { return item.Prompt == "interval task"; });
                bool valid = second.Tasks.Count == 3 && due == 1 && loadedDaily != null && loadedInterval != null && loadedInterval.RunMode == "powershell" && loadedInterval.OverlapMode == "parallel" && loadedInterval.History != null && loadedInterval.History.Count == 1 && loadedInterval.History[0].Status == "success" && loadedInterval.History[0].RunMode == "powershell" && loadedInterval.History[0].Summary.Contains("completed") && loadedInterval.History[0].Transcript.Contains("completed") && ClaudeTaskRunTranscriptForm.BuildTranscript(loadedInterval, loadedInterval.History[0]).Contains("Claude 回复") &&
                    loadedDaily.OverlapMode == "block" && loadedDaily.History != null && loadedDaily.History.Count == 1 && loadedDaily.History[0].Status == "skipped" && loadedDaily.History[0].StatusText == "已跳过" && skippedRecord.StatusText == "已跳过" &&
                    visibleInfo.UseShellExecute && !visibleInfo.CreateNoWindow && visibleInfo.WindowStyle == ProcessWindowStyle.Normal && visibleInfo.Arguments.Contains("-File") && String.Equals(visibleInfo.WorkingDirectory, Program.StateDirectory, StringComparison.OrdinalIgnoreCase) && visibleScript.Contains("Set-Location -LiteralPath") && visibleScript.Contains(Program.StateDirectory.Replace("'", "''")) && visibleScript.Contains("FUFU_PET_INTERNAL_TASK_ID") && visibleScript.Contains(interval.Id) && visibleScript.Contains("-p $prompt") && !visibleScript.Contains("$prompt |") && visibleScript.Contains("--output-format stream-json") && visibleScript.Contains("--include-partial-messages") &&
                    markedBackgroundInfo.EnvironmentVariables["FUFU_PET_INTERNAL_TASK_ID"] == interval.Id && Program.ShouldSuppressHook(interval.Id) && !Program.ShouldSuppressHook(null) &&
                    ClaudeTaskService.CalculateNextRun(daily, monday) == monday.Date.AddHours(10) &&
                    ClaudeTaskService.CalculateNextRun(weekly, monday) == monday.Date.AddDays(2).AddHours(11).AddMinutes(30) &&
                    ClaudeTaskService.CalculateNextRun(interval, monday) == monday.AddMinutes(90) &&
                    PetForm.BuildScheduledTaskPrompt(interval).Contains("interval task") && PetForm.BuildScheduledTaskPrompt(interval).Contains("当前工作目录");
                foreach (ScheduledClaudeTask task in new List<ScheduledClaudeTask>(second.Tasks)) second.Delete(task);
                second.Dispose();
                Environment.ExitCode = valid ? 0 : 41;
                return;
            }

            if (args.Length == 1 && String.Equals(args[0], "--visible-task-self-test", StringComparison.OrdinalIgnoreCase))
            {
                if (String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FUFU_STATE_DIR"))) { Environment.ExitCode = 42; return; }
                string testDirectory = Path.Combine(Program.StateDirectory, "visible task cwd");
                string binDirectory = Path.Combine(Program.StateDirectory, "visible task bin");
                Directory.CreateDirectory(testDirectory);
                Directory.CreateDirectory(binDirectory);
                string fakeClaude = Path.Combine(binDirectory, "claude.cmd");
                File.WriteAllText(fakeClaude, "@echo off\r\necho CWD=%CD%\r\necho ARGS=%*\r\nexit /b 0\r\n", Encoding.ASCII);
                string oldPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                string logPath = Path.Combine(Program.StateDirectory, "visible-task-test.log");
                try
                {
                    Environment.SetEnvironmentVariable("PATH", binDirectory + Path.PathSeparator + oldPath);
                    ScheduledClaudeTask task = new ScheduledClaudeTask { Prompt = "目录验证任务", WorkingDirectory = testDirectory, RunMode = "powershell" };
                    ProcessStartInfo info = PetForm.BuildVisibleTaskStartInfo(task, logPath, fakeClaude);
                    info.WindowStyle = ProcessWindowStyle.Hidden;
                    Process process = Process.Start(info);
                    bool exited = process != null && process.WaitForExit(15000);
                    int exitCode = exited ? process.ExitCode : -1;
                    if (process != null) { if (!exited) try { process.Kill(); } catch { } process.Dispose(); }
                    string output = File.Exists(logPath) ? File.ReadAllText(logPath, Encoding.UTF8) : "";
                    Environment.ExitCode = exited && exitCode == 0 && output.IndexOf("CWD=" + testDirectory, StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 43;
                }
                finally { Environment.SetEnvironmentVariable("PATH", oldPath); }
                return;
            }

            if (args.Length == 1 && String.Equals(args[0], "--reminder-self-test", StringComparison.OrdinalIgnoreCase))
            {
                if (String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FUFU_STATE_DIR"))) { Environment.ExitCode = 5; return; }
                ReminderService first = new ReminderService();
                DailyReminder createdReminder = first.Add(new TimeSpan(10, 15, 0), "Reminder persistence test", "popup");
                first.Snooze(createdReminder, 1);
                DateTime countdownStart = DateTime.Now;
                first.StartCountdown(5, countdownStart);
                first.Dispose();
                ReminderService second = new ReminderService();
                int dueCount = 0;
                second.ReminderDue += delegate { dueCount++; };
                second.CheckNow(DateTime.Now.AddMinutes(2));
                bool valid = second.Reminders.Count == 1 && second.Countdown != null && second.Countdown.Text == "5分钟时间到了" && dueCount == 1 && second.Reminders[0].Text == "Reminder persistence test" && second.Reminders[0].Effect == "popup";
                second.Delete(second.Reminders.Count == 0 ? null : second.Reminders[0]);
                second.CheckNow(countdownStart.AddMinutes(4));
                valid = valid && dueCount == 1 && second.Countdown != null;
                second.CheckNow(countdownStart.AddMinutes(5).AddSeconds(1));
                valid = valid && dueCount == 2 && second.Countdown == null;
                second.Dispose();
                Environment.ExitCode = valid ? 0 : 4;
                return;
            }

            if (args.Length == 1 && String.Equals(args[0], "--layered-self-test", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                PetForm pet = new PetForm();
                FileEatOverlayForm fileEat = new FileEatOverlayForm();
                try
                {
                    pet.Location = new System.Drawing.Point(SystemInformation.VirtualScreen.Right + 20, SystemInformation.VirtualScreen.Bottom + 20);
                    pet.Show();
                    pet.Refresh();
                    Application.DoEvents();
                    bool frameAdvance = pet.ValidateLayeredAnimationAdvance();
                    bool playfulRun = pet.ValidatePlayfulRun();
                    bool layeredTrail = pet.ValidateTrailOverlay();
                    System.Drawing.Point eatStart = new System.Drawing.Point(SystemInformation.WorkingArea.Left + 120, SystemInformation.WorkingArea.Top + 120);
                    fileEat.Begin(eatStart, new System.Drawing.Point(eatStart.X + 100, eatStart.Y + 80), "测试文件.txt", false, 1);
                    Application.DoEvents();
                    Environment.ExitCode = pet.LayeredRenderSucceeded && frameAdvance && playfulRun && layeredTrail && fileEat.LayeredRenderSucceeded ? 0 : (!pet.LayeredStylePresent ? 50 : 100 + pet.LayeredRenderError);
                }
                finally { fileEat.Hide(); fileEat.Dispose(); pet.Hide(); pet.Dispose(); }
                return;
            }

            if (args.Length == 1 && String.Equals(args[0], "--reply-bubble-self-test", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ReplyBubbleForm bubble = new ReplyBubbleForm();
                try
                {
                    bubble.Opacity = 0.01;
                    System.Drawing.Rectangle anchor = new System.Drawing.Rectangle(SystemInformation.WorkingArea.Right - 200, SystemInformation.WorkingArea.Bottom - 200, 100, 100);
                    bubble.ShowNear(anchor, true);
                    bool initiallyVisible = bubble.Visible;
                    bubble.Dismiss();
                    bubble.ShowNear(anchor);
                    bool remainedHidden = !bubble.Visible && bubble.UserDismissed;
                    bubble.ShowNear(anchor, true);
                    bool manuallyRestored = bubble.Visible && !bubble.UserDismissed;
                    System.Drawing.Rectangle area = SystemInformation.WorkingArea;
                    System.Drawing.Rectangle bottomPet = new System.Drawing.Rectangle(area.Right - 120, area.Bottom - 104, 96, 104);
                    int adjustedTop = PetForm.CalculatePetTopForReplyBubble(bottomPet, bubble.Height, area);
                    bool placedBelow = adjustedTop < bottomPet.Top && adjustedTop + bottomPet.Height + 5 + bubble.Height <= area.Bottom;
                    Environment.ExitCode = initiallyVisible && remainedHidden && manuallyRestored && placedBelow ? 0 : 6;
                }
                finally { bubble.Hide(); bubble.Dispose(); }
                return;
            }

            bool created;
            using (Mutex mutex = new Mutex(true, "Local\\OrbitPet.Singleton", out created))
            {
                if (!created)
                {
                    PublishEvent("Show", null);
                    return;
                }

                StartupRegistration.TryMigrateLegacy();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new PetForm());
            }
        }

        internal static string StateDirectory
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable("FUFU_STATE_DIR");
                return String.IsNullOrWhiteSpace(configured)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrbitPet")
                    : Environment.ExpandEnvironmentVariables(configured);
            }
        }

        internal static string StateFile
        {
            get { return Path.Combine(StateDirectory, "claude-event.txt"); }
        }

        private static bool PreservesOnUserClose(Form form)
        {
            FormClosingEventArgs args = new FormClosingEventArgs(CloseReason.UserClosing, false);
            MethodInfo method = typeof(Form).GetMethod("OnFormClosing", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(form, new object[] { args });
            return args.Cancel && !form.IsDisposed;
        }

        internal static bool ShouldSuppressHook(string internalTaskId)
        {
            return !String.IsNullOrWhiteSpace(internalTaskId);
        }

        private static void PublishEvent(string eventName, string metadata)
        {
            try
            {
                Directory.CreateDirectory(StateDirectory);
                string temporary = Path.Combine(StateDirectory, "event-" + Guid.NewGuid().ToString("N") + ".tmp");
                string encoded = String.IsNullOrEmpty(metadata) ? "" : Convert.ToBase64String(Encoding.UTF8.GetBytes(metadata));
                File.WriteAllText(temporary, eventName + "|" + DateTime.UtcNow.Ticks.ToString() + "|" + encoded);
                try
                {
                    if (File.Exists(StateFile))
                        File.Replace(temporary, StateFile, null);
                    else
                        File.Move(temporary, StateFile);
                }
                catch
                {
                    File.Copy(temporary, StateFile, true);
                    File.Delete(temporary);
                }
            }
            catch
            {
                // A pet notification must never delay or fail a Claude Code hook.
            }
        }
    }
}
