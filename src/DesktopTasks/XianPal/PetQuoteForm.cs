using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed class PetQuoteForm : Form
    {
        internal static readonly string[] DefaultQuotes =
        {
            "给今天留一束柔和的光。",
            "道友，我们从第一步开始。",
            "一起探索新的小小发现。",
            "计划准备好了，我们出发吧。",
            "走一小段路，也能发现好风景。",
            "大人真是让人操心呀。",
            "先休息一下，再继续努力吧！",
            "不可以半途而废哦！",
            "嘿嘿，我只是来陪你一下。",
            "今天也要开开心心地完成任务！"
        };

        private readonly Label quote = new Label();
        private readonly Timer hideTimer = new Timer();

        internal string CurrentQuote
        {
            get { return quote.Text; }
        }

        internal PetQuoteForm()
        {
            ClientSize = new Size(286, 66);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(255, 250, 231);
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            quote.Dock = DockStyle.Fill;
            quote.Padding = new Padding(15, 8, 15, 8);
            quote.Font = new Font("Microsoft YaHei UI", 9.2f, FontStyle.Bold);
            quote.ForeColor = Color.FromArgb(76, 57, 35);
            quote.TextAlign = ContentAlignment.MiddleCenter;
            quote.Cursor = Cursors.Hand;
            quote.Click += delegate { Hide(); };
            Controls.Add(quote);
            hideTimer.Interval = 4200;
            hideTimer.Tick += delegate { hideTimer.Stop(); Hide(); };
            Resize += delegate { ApplyRoundedRegion(); };
            ApplyRoundedRegion();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        internal void ShowQuote(string text, Rectangle petBounds)
        {
            quote.Text = text ?? "";
            PositionNear(petBounds);
            if (!Visible) Show();
            // The first Show otherwise lets WinForms replace Location with its
            // default startup position (usually the upper-left of the screen).
            PositionNear(petBounds);
            BringToFront();
            hideTimer.Stop();
            hideTimer.Start();
        }

        internal void Reposition(Rectangle petBounds)
        {
            PositionNear(petBounds);
        }

        private void PositionNear(Rectangle petBounds)
        {
            Rectangle area = Screen.FromRectangle(petBounds).WorkingArea;
            int x = petBounds.Left + ((petBounds.Width - Width) / 2);
            x = Math.Max(area.Left + 6, Math.Min(x, area.Right - Width - 6));
            int y = petBounds.Top - Height - 7;
            if (y < area.Top + 6) y = Math.Min(area.Bottom - Height - 6, petBounds.Bottom + 7);
            Location = new Point(x, y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = PromptForm.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 18))
            using (Pen border = new Pen(Color.FromArgb(232, 203, 142))) e.Graphics.DrawPath(border, path);
        }

        private void ApplyRoundedRegion()
        {
            Region old = Region;
            Region = new Region(PromptForm.RoundedRectangle(ClientRectangle, 18));
            if (old != null) old.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) hideTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}
