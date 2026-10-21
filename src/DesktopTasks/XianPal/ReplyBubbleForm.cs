using System;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed class ReplyBubbleForm : Form
    {
        private readonly Label status = new Label();
        private readonly RichTextBox latest = new UnicodeRichTextBox();
        private readonly StringBuilder response = new StringBuilder();
        private readonly ToolTip tips = new ToolTip();
        private readonly Timer renderTimer = new Timer();
        private bool userDismissed;
        private bool renderPending;
        private int responseVersion;
        private int renderedVersion;
        private int renderCount;
        internal const int StreamRenderIntervalMs = 80;
        internal event Action ConversationRequested;

        internal ReplyBubbleForm()
        {
            ClientSize = new Size(340, 78);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(244, 250, 246);
            status.Text = "Claude Code";
            status.Font = UiText.Font(8.5f, FontStyle.Bold);
            status.ForeColor = Color.FromArgb(28, 116, 74);
            status.Location = new Point(11, 6);
            status.Size = new Size(282, 19);
            Button close = new Button
            {
                Name = "replyBubbleCloseButton",
                Text = "×",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(73, 91, 80),
                Font = new Font("Segoe UI", 11f),
                Location = new Point(309, 2),
                Size = new Size(27, 25),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            close.FlatAppearance.BorderSize = 0;
            close.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 239, 230);
            close.Click += delegate { Dismiss(); };
            tips.SetToolTip(close, "暂时隐藏回复；不会终止 Claude 会话");
            latest.Text = "等待 Claude 回复…";
            latest.Font = UiText.Font(9.2f);
            latest.ForeColor = Color.FromArgb(38, 50, 43);
            latest.BackColor = BackColor;
            latest.BorderStyle = BorderStyle.None;
            latest.ReadOnly = true;
            latest.DetectUrls = false;
            latest.ScrollBars = RichTextBoxScrollBars.None;
            latest.TabStop = false;
            latest.ShortcutsEnabled = false;
            latest.HideSelection = true;
            latest.Location = new Point(11, 26);
            latest.Size = new Size(318, 43);
            status.Cursor = Cursors.Hand;
            latest.Cursor = Cursors.Hand;
            Cursor = Cursors.Hand;
            DoubleClick += OpenConversation;
            status.DoubleClick += OpenConversation;
            latest.DoubleClick += OpenConversation;
            latest.SelectionChanged += delegate
            {
                // The compact desktop bubble is a preview, not an editor. Text
                // selection is enabled in the full activity window after double-click.
                if (latest.SelectionLength != 0) latest.Select(latest.TextLength, 0);
            };
            latest.MouseMove += delegate
            {
                if (Control.MouseButtons == MouseButtons.Left && latest.SelectionLength != 0)
                    latest.Select(latest.TextLength, 0);
            };
            tips.SetToolTip(status, "双击打开当前对话");
            tips.SetToolTip(latest, "双击打开当前对话并继续回复");
            Controls.Add(status);
            Controls.Add(latest);
            Controls.Add(close);
            renderTimer.Interval = StreamRenderIntervalMs;
            renderTimer.Tick += delegate { RenderPendingReply(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Dismiss(); }
            };
        }

        internal bool UserDismissed
        {
            get { return userDismissed; }
        }

        internal void Dismiss()
        {
            userDismissed = true;
            Hide();
        }

        internal void BeginRequest(string promptText, Rectangle petBounds)
        {
            userDismissed = false;
            response.Length = 0;
            renderTimer.Stop();
            renderPending = false;
            responseVersion = 0;
            renderedVersion = 0;
            latest.Text = "正在思考：" + Compact(promptText, 80);
            UiText.ApplyMixedGlyphFonts(latest);
            SetStatus("Claude 正在处理…");
            ShowNear(petBounds);
        }

        internal void AppendReply(string text)
        {
            if (String.IsNullOrEmpty(text)) return;
            response.Append(UiText.Normalize(text));
            responseVersion++;
            if (response.Length > 16000)
            {
                string tail = UiText.TailCodeUnits(response.ToString(), 12000);
                response.Length = 0;
                response.Append(tail);
            }
            renderPending = true;
            if (!renderTimer.Enabled) renderTimer.Start();
        }

        internal void ReplaceReply(string text)
        {
            string clean = UiText.TailCodeUnits(UiText.Normalize(text ?? ""), 12000);
            if (clean == response.ToString()) return;
            response.Clear();
            if (clean.Length == 0) { responseVersion++; renderPending = true; if (!renderTimer.Enabled) renderTimer.Start(); }
            else AppendReply(clean);
        }

        private void RenderPendingReply()
        {
            if (!renderPending || renderedVersion == responseVersion)
            {
                renderTimer.Stop();
                renderPending = false;
                return;
            }
            latest.Text = ExtractLatestSentence(response.ToString());
            UiText.ApplyMixedGlyphFonts(latest);
            latest.Invalidate();
            renderedVersion = responseVersion;
            renderPending = false;
            renderCount++;
            renderTimer.Stop();
        }

        internal void FlushPendingReply()
        {
            RenderPendingReply();
        }

        internal bool HasPendingRenderForTest { get { return renderPending; } }
        internal int RenderCountForTest { get { return renderCount; } }
        internal string PreviewTextForTest { get { return latest.Text; } }

        internal void SetStatus(string value) { status.Text = UiText.Normalize(value); }

        private void OpenConversation(object sender, EventArgs e)
        {
            Action handler = ConversationRequested;
            if (handler != null) handler();
        }

        internal void TriggerConversationForTest()
        {
            OpenConversation(this, EventArgs.Empty);
        }

        internal bool PreviewSelectionBlockedForTest()
        {
            latest.Text = "不可选择的桌面预览";
            latest.Select(0, 4);
            return latest.SelectionLength == 0;
        }

        internal void ShowNear(Rectangle petBounds, bool force = false)
        {
            if (force) userDismissed = false;
            if (userDismissed) return;
            Rectangle area = Screen.FromRectangle(petBounds).WorkingArea;
            Location = CalculateLocation(petBounds, Size, area);
            if (!Visible) Show();
            // WinForms can apply the startup position during the first Show.
            // Re-apply the anchor afterwards so the first bubble is positioned
            // exactly like all later updates.
            Location = CalculateLocation(petBounds, Size, area);
            TopMost = true;
            BringToFront();
        }

        internal static Point CalculateLocation(Rectangle petBounds, Size bubbleSize, Rectangle area)
        {
            int width = Math.Max(1, bubbleSize.Width);
            int height = Math.Max(1, bubbleSize.Height);
            int x = petBounds.Left + ((petBounds.Width - width) / 2);
            x = Math.Max(area.Left, Math.Min(x, area.Right - width));
            int y = petBounds.Bottom + 5;
            if (y + height > area.Bottom) y = petBounds.Top - height - 5;
            return new Point(x, Math.Max(area.Top, y));
        }

        private static string ExtractLatestSentence(string value)
        {
            string clean = Regex.Replace(value ?? "", @"[`*_>#\[\]]", "");
            clean = Regex.Replace(clean, @"\s+", " ").Trim();
            if (clean.Length == 0) return "Claude 正在思考…";
            int end = clean.Length - 1;
            int start = end;
            while (start > 0 && "。！？.!?；;".IndexOf(clean[start - 1]) < 0) start--;
            string sentence = clean.Substring(start, end - start + 1).Trim();
            return Compact(sentence, 135);
        }

        private static string Compact(string value, int maximum)
        {
            string clean = Regex.Replace(value ?? "", @"\s+", " ").Trim();
            return UiText.TailTextElements(clean, maximum);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                renderTimer.Stop();
                renderTimer.Dispose();
                tips.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
