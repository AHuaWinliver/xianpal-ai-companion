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
        internal readonly List<ChatMessage> Messages=new List<ChatMessage>();
        internal void StartConversation(string prompt)
        { if(Messages.Count==0)Messages.Add(new ChatMessage {Role="user",Text=UiText.Normalize(prompt)}); }
        internal void AppendUserPrompt(string prompt)
        {
            if(Messages.Count==0&&!String.IsNullOrWhiteSpace(Output))Messages.Add(new ChatMessage {Role="assistant",Text=ActivityTrayForm.NormalizeOutput(Output),At=Updated});
            Messages.Add(new ChatMessage {Role="user",Text=UiText.Normalize(prompt)});
            Output=(Output??"")+"\r\n\r\n你："+prompt+"\r\n\r\n";TrimMessages();
        }
        private void TrimMessages()
        {
            if(Messages.Count>0&&Messages[Messages.Count-1].Text.Length>100000)Messages[Messages.Count-1].Text="…\n"+UiText.TailCodeUnits(Messages[Messages.Count-1].Text,100000);
            int count=0;foreach(ChatMessage m in Messages)count+=m.Text.Length;
            while(Messages.Count>1&&(Messages.Count>80||count>110000)){count-=Messages[0].Text.Length;Messages.RemoveAt(0);}
        }

        internal void AppendOutput(string text)
        {
            if (String.IsNullOrEmpty(text)) return;
            Output = (Output ?? "") + UiText.Normalize(text);
            if (Output.Length > 120000) Output = "…\r\n" + UiText.TailCodeUnits(Output, 100000);
            if(Messages.Count>0)
            {
                if(Messages[Messages.Count-1].Role!="assistant")Messages.Add(new ChatMessage {Role="assistant",Text=""});
                Messages[Messages.Count-1].Text+=UiText.Normalize(text);TrimMessages();
            }
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
        private readonly ChatConversationView conversation=new ChatConversationView();
        private readonly Timer renderTimer=new Timer {Interval=120};
        private readonly Dictionary<ActivityRecord,string> drafts=new Dictionary<ActivityRecord,string>();
        private ActivityRecord draftActivity;
        private bool updatingList,renderPending;
        private Label heading;
        private Label statusCaption;
        private bool historyExpanded;
        private readonly Label emptyDetails = new Label();
        private readonly ChineseInputTextBox reply = new ChineseInputTextBox();
        private readonly Label replyPlaceholder = new Label();
        private readonly RoundIconButton send = new RoundIconButton();
        private readonly ToolTip tips = new ToolTip();
        private bool pinnedOpen;
        internal event Action ActivityViewed;
        internal event Func<ActivityRecord, string, bool> ReplySubmitted;
        internal event Action HeaderDragStarted;
        internal event Func<Point,Point> HeaderDragLocationRequested;
        internal event Action HeaderDragFinished;
        internal event Action<Point> CompanionPositionRequested;
        private Control capturedHeader;
        private bool headerArmed,headerMoving;
        private Point headerPointer,headerOrigin;
        internal bool HeaderDragInProgress {get{return headerMoving;}}

        internal ActivityTrayForm()
        {
            Text = "Claude Code 动态";
            Icon = AppIcon.WindowIcon;
            ClientSize = new Size(640, 650);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(253, 253, 252);
            DoubleBuffered = true;
            Font = UiText.Font(9f);

            Label title = new Label { Text = "仙伴 · 对话", Font = UiText.Font(12f, FontStyle.Bold), ForeColor = Color.FromArgb(34, 55, 44), AutoSize = true, Location = new Point(18, 16) };
            heading=title;
            title.AutoSize=false;title.Size=new Size(180,28);title.AutoEllipsis=true;
            Label subtitle = new Label { Text = "Claude Code", Font = UiText.Font(9f), ForeColor = Color.FromArgb(105, 105, 100), AutoSize = true, Location = new Point(214, 20) };
            statusCaption=subtitle;
            WireHeaderDrag(this);WireHeaderDrag(title);WireHeaderDrag(subtitle);
            title.Cursor=Cursors.SizeAll;subtitle.Cursor=Cursors.SizeAll;
            tips.SetToolTip(title,"拖动标题栏，人物会一起移动");
            Button close = new Button { Text = "×", Anchor=AnchorStyles.Top|AnchorStyles.Right,FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = Color.FromArgb(72, 72, 68), Font = new Font("Segoe UI", 13f), Location = new Point(592, 8), Size = new Size(30, 30), Cursor = Cursors.Hand };
            close.FlatAppearance.BorderSize = 0;
            close.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 240, 237);
            close.Click += delegate { Dismiss(); };
            var historyButton=new Button {Text="会话 ▾",FlatStyle=FlatStyle.Flat,BackColor=BackColor,ForeColor=Color.FromArgb(74,96,82),Location=new Point(504,13),Size=new Size(77,28),Anchor=AnchorStyles.Top|AnchorStyles.Right,Cursor=Cursors.Hand};historyButton.FlatAppearance.BorderSize=0;

            RoundedPanel listCard = new RoundedPanel { Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,Radius = 15, BorderColor = Color.FromArgb(228, 228, 224), BackColor = Color.FromArgb(247, 247, 246), Location = new Point(14, 52), Size = new Size(612, 94) };
            list.BorderStyle = BorderStyle.None;
            list.BackColor = listCard.BackColor;
            list.ForeColor = Color.FromArgb(38, 38, 36);
            list.Font = UiText.Font(9f);
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = 38;
            list.IntegralHeight = false;
            list.Location = new Point(8, 7);
            list.Size = new Size(596, 80);list.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            list.SelectedIndexChanged += ShowSelected;
            list.DrawItem += DrawActivity;
            listCard.Controls.Add(list);

            RoundedPanel detailsCard = new RoundedPanel { Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,Radius = 15, BorderColor = Color.FromArgb(232, 235, 230), BackColor = Color.FromArgb(250,251,249), Location = new Point(14, 156), Size = new Size(612, 400),Padding=new Padding(4) };
            conversation.Dock=DockStyle.Fill;detailsCard.Controls.Add(conversation);

            emptyDetails.Text = "选择一条动态查看 Claude 回复";
            emptyDetails.ForeColor = Color.FromArgb(145, 145, 140);
            emptyDetails.BackColor = detailsCard.BackColor;
            emptyDetails.AutoSize = true;
            emptyDetails.Location = new Point(20, 18);
            detailsCard.Controls.Add(emptyDetails);
            emptyDetails.BringToFront();

            RoundedPanel replyCard = new RoundedPanel { Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,Radius = 16, BorderColor = Color.FromArgb(220, 229, 220), BackColor = Color.FromArgb(247, 249, 246), Location = new Point(14, 568), Size = new Size(612, 68) };
            reply.Multiline = true;
            reply.AcceptsReturn = true;
            reply.BorderStyle = BorderStyle.None;
            reply.BackColor = replyCard.BackColor;
            reply.ForeColor = Color.FromArgb(34, 34, 32);
            reply.Font = UiText.Font(9.5f);
            reply.Location = new Point(14, 13);
            reply.Size = new Size(530, 42);reply.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
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
            send.Location = new Point(559, 13);send.Anchor=AnchorStyles.Top|AnchorStyles.Right;
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
            Controls.Add(historyButton);
            listCard.Visible=false;
            Action arrangeCards=delegate {int top=historyExpanded?156:52;replyCard.Top=ClientSize.Height-82;detailsCard.Top=top;detailsCard.Height=Math.Max(100,replyCard.Top-12-top);};
            historyButton.Click+=delegate {historyExpanded=!historyExpanded;listCard.Visible=historyExpanded;arrangeCards();};
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Dismiss(); }
            };
            Resize += delegate { arrangeCards();ApplyRoundedRegion(); };
            arrangeCards();
            ApplyRoundedRegion();
            UpdateReplyState();
            renderTimer.Tick+=delegate {renderTimer.Stop();if(renderPending&&Visible){renderPending=false;RenderSelected();}};
            VisibleChanged+=delegate {if(Visible){renderPending=false;RenderSelected();}else {renderTimer.Stop();EndHeaderDrag();}};
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
            bool same=list.Items.Count==activities.Count;
            if(same)for(int i=0;i<activities.Count;i++)if(!Object.ReferenceEquals(list.Items[i],activities[i])){same=false;break;}
            if(same){list.Invalidate();QueueRender();return;}
            updatingList=true;
            list.BeginUpdate();
            list.Items.Clear();
            foreach (ActivityRecord activity in activities) list.Items.Add(activity);
            list.EndUpdate();
            updatingList=false;
            if (list.Items.Count == 0)
            {
                conversation.Present(null);
                emptyDetails.Visible = true;
                UpdateReplyState();
                return;
            }
            int selectedIndex = selected == null ? -1 : list.Items.IndexOf(selected);
            if (Visible) list.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            foreach(ActivityRecord key in new List<ActivityRecord>(drafts.Keys))if(!activities.Contains(key))drafts.Remove(key);
            QueueRender();
        }

        internal void ShowNear(Rectangle petBounds)
        {
            if(HeaderDragInProgress)return;
            pinnedOpen = true;
            Rectangle area = Screen.FromRectangle(petBounds).WorkingArea;
            ChatAnchorLayout layout=ChatAnchorLayout.Below(petBounds,new Size(640,650),area);
            if(CompanionPositionRequested!=null)CompanionPositionRequested(layout.Pet.Location);
            ClientSize=layout.Dialog.Size;
            Location=layout.Dialog.Location;
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
            EndHeaderDrag();
            pinnedOpen = false;
            Hide();
        }

        internal void DismissForTest() { Dismiss(); }

        private void WireHeaderDrag(Control handle)
        {
            handle.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;if(handle==this&&e.Y>=48)return;BeginHeaderDrag(handle,handle.PointToScreen(e.Location));};
            handle.MouseMove+=delegate(object sender,MouseEventArgs e){if(headerArmed)MoveHeaderDrag(handle.PointToScreen(e.Location));};
            handle.MouseUp+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left)EndHeaderDrag();};
            handle.MouseCaptureChanged+=delegate {if(headerArmed&&capturedHeader==handle&&!handle.Capture)EndHeaderDrag();};
        }
        private void BeginHeaderDrag(Control handle,Point pointer)
        {
            if(headerArmed)EndHeaderDrag();
            headerPointer=pointer;headerOrigin=Location;capturedHeader=handle;headerArmed=true;headerMoving=false;handle.Capture=true;
        }
        private void MoveHeaderDrag(Point pointer)
        {
            if(!headerArmed)return;
            int dx=pointer.X-headerPointer.X,dy=pointer.Y-headerPointer.Y;
            if(!headerMoving)
            {
                if(Math.Abs(dx)<4&&Math.Abs(dy)<4)return;
                headerMoving=true;if(HeaderDragStarted!=null)HeaderDragStarted();
            }
            Point requested=new Point(headerOrigin.X+dx,headerOrigin.Y+dy);
            Location=HeaderDragLocationRequested!=null?HeaderDragLocationRequested(requested):ChatWindowDrag.Constrain(new Rectangle(headerOrigin,Size),null,requested,Screen.FromPoint(pointer).WorkingArea);
        }
        private void EndHeaderDrag()
        {
            if(!headerArmed)return;
            bool moved=headerMoving;headerArmed=false;headerMoving=false;
            Control source=capturedHeader;capturedHeader=null;if(source!=null&&!source.IsDisposed)source.Capture=false;
            if(moved&&HeaderDragFinished!=null)HeaderDragFinished();
        }

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
            if(updatingList)return;
            ActivityRecord item = list.SelectedItem as ActivityRecord;
            if (item == null) return;
            if(!Object.ReferenceEquals(draftActivity,item))
            {if(draftActivity!=null)drafts[draftActivity]=reply.Text;string draft;reply.Text=drafts.TryGetValue(item,out draft)?draft:"";draftActivity=item;}
            item.Unread = false;
            RenderSelected();
            UpdateReplyState();
            list.Invalidate();
            Action viewed = ActivityViewed;
            if (viewed != null) viewed();
        }
        private void QueueRender(){renderPending=true;if(Visible&&!renderTimer.Enabled)renderTimer.Start();}
        private void RenderSelected()
        {ActivityRecord item=list.SelectedItem as ActivityRecord;conversation.Present(item);emptyDetails.Visible=item==null;if(heading!=null)heading.Text=(item==null?"XianPal":String.IsNullOrEmpty(item.CharacterId)?"Claude":TaskPresentation.Owner(item.CharacterId))+" · "+UiLanguage.Text("对话");if(statusCaption!=null)statusCaption.Text=item==null?"Claude Code":"Claude Code · "+UiLanguage.Text(item.State=="正在运行"?"正在回复":item.State??"就绪");UpdateReplyState();}
        internal ChatConversationView ConversationForTest {get{return conversation;}}
        internal void FlushConversationForTest(){renderPending=false;RenderSelected();}

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
            if (item == null || item.State=="正在运行" || String.IsNullOrWhiteSpace(item.SessionId) || text.Length == 0) return;
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
            bool canReply = item != null && !String.IsNullOrWhiteSpace(item.SessionId);
            reply.Enabled = canReply;
            send.Enabled = canReply && item.State!="正在运行"&&reply.Text.Trim().Length > 0;
            send.BackColor = send.Enabled ? Color.FromArgb(32, 33, 31) : Color.FromArgb(190, 190, 186);
            replyPlaceholder.Text = canReply ? "回复所选 Claude 会话…" : "该动态尚未取得会话 ID";
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
                TextRenderer.DrawText(e.Graphics, UiLanguage.Text(item.State ?? ""), stateFont, stateBounds, stateColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
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
        protected override void Dispose(bool disposing){if(disposing){EndHeaderDrag();renderTimer.Dispose();details.Dispose();tips.Dispose();}base.Dispose(disposing);}
    }
}
