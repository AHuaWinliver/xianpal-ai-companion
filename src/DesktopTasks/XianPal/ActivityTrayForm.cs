using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed class ActivityRecord
    {
        internal string Title;
        internal string SessionId;
        internal string CharacterId;
        internal string WorkingDirectory;
        internal string State;
        internal string Output;
        internal DateTime Updated;
        internal bool Unread;
        internal bool NativeInteractive;

        internal void AppendOutput(string text)
        {
            if (String.IsNullOrEmpty(text)) return;
            Output = (Output ?? "") + UiText.Normalize(text);
            if (Output.Length > 120000) Output = "…\r\n" + UiText.TailCodeUnits(Output, 100000);
        }

        public override string ToString()
        {
            return "[" + State + "] " + Title + "  " + Updated.ToString("HH:mm");
        }
    }

    internal sealed class ActivityTrayForm : Form
    {
        private readonly ListBox list = new ListBox();
        private readonly UnicodeRichTextBox details = new UnicodeRichTextBox();
        private readonly Label emptyDetails = new Label();
        private readonly ChineseInputTextBox reply = new ChineseInputTextBox();
        private readonly Label replyPlaceholder = new Label();
        private readonly RoundIconButton send = new RoundIconButton();
        private readonly ToolTip tips = new ToolTip();
        private bool pinnedOpen;
        internal event Action ActivityViewed;
        internal event Func<ActivityRecord, string, bool> ReplySubmitted;

        internal ActivityTrayForm()
        {
            Text = "Claude Code 动态";
            Icon = AppIcon.WindowIcon;
            ClientSize = new Size(548, 462);
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(253, 253, 252);
            DoubleBuffered = true;
            Font = UiText.Font(9f);

            Label title = new Label { Text = "Claude Code", Font = UiText.Font(10.5f, FontStyle.Bold), ForeColor = Color.FromArgb(34, 34, 32), AutoSize = true, Location = new Point(18, 16) };
            Label subtitle = new Label { Text = "动态", Font = UiText.Font(9f), ForeColor = Color.FromArgb(105, 105, 100), AutoSize = true, Location = new Point(118, 18) };
            Button close = new Button { Text = "×", FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = Color.FromArgb(72, 72, 68), Font = new Font("Segoe UI", 13f), Location = new Point(504, 8), Size = new Size(30, 30), Cursor = Cursors.Hand };
            close.FlatAppearance.BorderSize = 0;
            close.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 240, 237);
            close.Click += delegate { Dismiss(); };

            RoundedPanel listCard = new RoundedPanel { Radius = 15, BorderColor = Color.FromArgb(228, 228, 224), BackColor = Color.FromArgb(247, 247, 246), Location = new Point(14, 52), Size = new Size(520, 116) };
            list.BorderStyle = BorderStyle.None;
            list.BackColor = listCard.BackColor;
            list.ForeColor = Color.FromArgb(38, 38, 36);
            list.Font = UiText.Font(9f);
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = 40;
            list.IntegralHeight = false;
            list.Location = new Point(8, 7);
            list.Size = new Size(504, 102);
            list.SelectedIndexChanged += ShowSelected;
            list.DrawItem += DrawActivity;
            listCard.Controls.Add(list);

            RoundedPanel detailsCard = new RoundedPanel { Radius = 15, BorderColor = Color.FromArgb(228, 228, 224), BackColor = Color.FromArgb(247, 247, 246), Location = new Point(14, 178), Size = new Size(520, 198) };
            details.ReadOnly = true;
            details.BorderStyle = BorderStyle.None;
            details.BackColor = detailsCard.BackColor;
            details.ForeColor = Color.FromArgb(45, 45, 42);
            details.Font = UiText.Font(9.5f);
            details.DetectUrls = true;
            details.WordWrap = true;
            details.Location = new Point(12, 11);
            details.Size = new Size(496, 176);
            details.ScrollBars = RichTextBoxScrollBars.Vertical;
            detailsCard.Controls.Add(details);

            emptyDetails.Text = "选择一条动态查看 Claude 回复";
            emptyDetails.ForeColor = Color.FromArgb(145, 145, 140);
            emptyDetails.BackColor = detailsCard.BackColor;
            emptyDetails.AutoSize = true;
            emptyDetails.Location = new Point(20, 18);
            detailsCard.Controls.Add(emptyDetails);
            emptyDetails.BringToFront();

            RoundedPanel replyCard = new RoundedPanel { Radius = 16, BorderColor = Color.FromArgb(226, 226, 223), BackColor = Color.FromArgb(247, 247, 246), Location = new Point(14, 386), Size = new Size(520, 62) };
            reply.Multiline = true;
            reply.AcceptsReturn = true;
            reply.BorderStyle = BorderStyle.None;
            reply.BackColor = replyCard.BackColor;
            reply.ForeColor = Color.FromArgb(34, 34, 32);
            reply.Font = UiText.Font(9.5f);
            reply.Location = new Point(14, 13);
            reply.Size = new Size(438, 38);
            reply.KeyDown += ReplyKeyDown;
            reply.TextChanged += delegate { UpdateReplyState(); };
            reply.GotFocus += delegate { UpdateReplyState(); };
            reply.LostFocus += delegate { UpdateReplyState(); };

            replyPlaceholder.Text = "选择会话后直接回复…";
            replyPlaceholder.ForeColor = Color.FromArgb(145, 145, 140);
            replyPlaceholder.BackColor = replyCard.BackColor;
            replyPlaceholder.Font = reply.Font;
            replyPlaceholder.AutoSize = true;
            replyPlaceholder.Location = new Point(14, 15);
            replyPlaceholder.Cursor = Cursors.IBeam;
            replyPlaceholder.Click += delegate { if (reply.Enabled) reply.Focus(); };

            send.Text = "↑";
            send.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            send.ForeColor = Color.White;
            send.BackColor = Color.FromArgb(32, 33, 31);
            send.FlatStyle = FlatStyle.Flat;
            send.Location = new Point(467, 10);
            send.Size = new Size(42, 42);
            send.Cursor = Cursors.Hand;
            send.FlatAppearance.BorderSize = 0;
            send.Click += delegate { SubmitReply(); };
            tips.SetToolTip(send, "回复（Enter）");
            replyCard.Controls.Add(reply);
            replyCard.Controls.Add(replyPlaceholder);
            replyCard.Controls.Add(send);

            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(close);
            Controls.Add(listCard);
            Controls.Add(detailsCard);
            Controls.Add(replyCard);
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Dismiss(); }
            };
            Resize += delegate { ApplyRoundedRegion(); };
            ApplyRoundedRegion();
            UpdateReplyState();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int CsDropShadow = 0x00020000;
                CreateParams parameters = base.CreateParams;
                parameters.ClassStyle |= CsDropShadow;
                return parameters;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = PromptForm.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 18))
            using (Pen pen = new Pen(Color.FromArgb(221, 221, 217)))
                e.Graphics.DrawPath(pen, path);
        }

        internal void SetActivities(IList<ActivityRecord> activities)
        {
            ActivityRecord selected = list.SelectedItem as ActivityRecord;
            list.BeginUpdate();
            list.Items.Clear();
            foreach (ActivityRecord activity in activities) list.Items.Add(activity);
            list.EndUpdate();
            if (list.Items.Count == 0)
            {
                details.Clear();
                emptyDetails.Visible = true;
                UpdateReplyState();
                return;
            }
            int selectedIndex = selected == null ? -1 : list.Items.IndexOf(selected);
            if (Visible) list.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        }

        internal void ShowNear(Rectangle petBounds)
        {
            pinnedOpen = true;
            Rectangle area = Screen.FromRectangle(petBounds).WorkingArea;
            int x = petBounds.Left + ((petBounds.Width - Width) / 2);
            x = Math.Max(area.Left + 8, Math.Min(x, area.Right - Width - 8));
            int y = petBounds.Bottom + 8;
            if (y + Height > area.Bottom - 8) y = petBounds.Top - Height - 8;
            Location = new Point(x, Math.Max(area.Top + 8, y));
            if (!Visible) Show();
            if (list.Items.Count > 0 && list.SelectedIndex < 0) list.SelectedIndex = 0;
            Activate();
        }

        internal bool PinnedOpen { get { return pinnedOpen && Visible; } }

        internal void HideUnlessPinned()
        {
            if (!PinnedOpen) Hide();
        }

        private void Dismiss()
        {
            pinnedOpen = false;
            Hide();
        }

        internal void DismissForTest() { Dismiss(); }

        internal void SelectActivity(ActivityRecord activity)
        {
            if (activity == null) return;
            int index = list.Items.IndexOf(activity);
            if (index >= 0) list.SelectedIndex = index;
        }

        internal static string NormalizeOutput(string value)
        {
            string clean = UiText.Normalize(value).Replace("\r\n", "\n").Replace('\r', '\n');
            clean = Regex.Replace(clean, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
            clean = Regex.Replace(clean, @"\n{3,}", "\n\n");
            return clean.Trim();
        }

        internal bool UnicodeSymbolsSupportedForTest()
        {
            const string sample = "中文 ✅ 😊 🚀 ⚠️ 👨‍💻";
            details.Text = sample;
            UiText.ApplyMixedGlyphFonts(details);
            return details.UsesUnicodeRichEdit && details.Text == sample && !UiText.HasUnpairedSurrogate(details.Text);
        }

        private void ShowSelected(object sender, EventArgs e)
        {
            ActivityRecord item = list.SelectedItem as ActivityRecord;
            if (item == null) return;
            item.Unread = false;
            details.Text = NormalizeOutput(item.Output);
            UiText.ApplyMixedGlyphFonts(details);
            details.SelectionStart = 0;
            details.ScrollToCaret();
            emptyDetails.Visible = details.TextLength == 0;
            UpdateReplyState();
            list.Invalidate();
            Action viewed = ActivityViewed;
            if (viewed != null) viewed();
        }

        private void ReplyKeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Shift && e.KeyCode == Keys.Enter && !reply.IsComposing)
            {
                e.SuppressKeyPress = true;
                SubmitReply();
            }
        }

        private void SubmitReply()
        {
            ActivityRecord item = list.SelectedItem as ActivityRecord;
            string text = reply.Text.Trim();
            if (item == null || item.NativeInteractive || String.IsNullOrWhiteSpace(item.SessionId) || text.Length == 0) return;
            Func<ActivityRecord, string, bool> handler = ReplySubmitted;
            if (handler != null && handler(item, text))
            {
                reply.Clear();
                reply.Focus();
            }
        }

        private void UpdateReplyState()
        {
            ActivityRecord item = list.SelectedItem as ActivityRecord;
            bool canReply = item != null && !item.NativeInteractive && !String.IsNullOrWhiteSpace(item.SessionId);
            reply.Enabled = canReply;
            send.Enabled = canReply && reply.Text.Trim().Length > 0;
            send.BackColor = send.Enabled ? Color.FromArgb(32, 33, 31) : Color.FromArgb(190, 190, 186);
            replyPlaceholder.Text = item != null && item.NativeInteractive ? "请在对应的 Claude 原生 CLI 窗口继续对话" : canReply ? "回复所选 Claude 会话…" : "该动态尚未取得会话 ID";
            replyPlaceholder.Visible = reply.TextLength == 0 && !reply.Focused;
        }

        private void DrawActivity(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            ActivityRecord item = list.Items[e.Index] as ActivityRecord;
            if (item == null) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color background = selected ? Color.FromArgb(231, 241, 234) : list.BackColor;
            using (SolidBrush brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color stateColor = StateColor(item.State);
            if (item.Unread)
            {
                using (SolidBrush marker = new SolidBrush(stateColor)) e.Graphics.FillEllipse(marker, e.Bounds.Left + 9, e.Bounds.Top + 16, 7, 7);
            }
            Rectangle stateBounds = new Rectangle(e.Bounds.Left + 22, e.Bounds.Top + 10, 74, 22);
            Rectangle timeBounds = new Rectangle(e.Bounds.Right - 56, e.Bounds.Top + 10, 46, 22);
            Rectangle titleBounds = new Rectangle(stateBounds.Right + 2, e.Bounds.Top + 10, timeBounds.Left - stateBounds.Right - 6, 22);
            using (Font stateFont = new Font(list.Font, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, item.State ?? "", stateFont, stateBounds, stateColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, item.Title ?? "Claude Code 会话", list.Font, titleBounds, Color.FromArgb(38, 38, 36), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, item.Updated.ToString("HH:mm"), list.Font, timeBounds, Color.FromArgb(130, 130, 126), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        private static Color StateColor(string state)
        {
            if (String.Equals(state, "需要输入", StringComparison.Ordinal)) return Color.FromArgb(176, 105, 28);
            if (String.Equals(state, "受阻", StringComparison.Ordinal)) return Color.FromArgb(185, 66, 60);
            if (String.Equals(state, "正在运行", StringComparison.Ordinal)) return Color.FromArgb(49, 103, 190);
            return Color.FromArgb(40, 132, 84);
        }

        private void ApplyRoundedRegion()
        {
            Region old = Region;
            Region = new Region(PromptForm.RoundedRectangle(ClientRectangle, 18));
            if (old != null) old.Dispose();
        }
    }
}
