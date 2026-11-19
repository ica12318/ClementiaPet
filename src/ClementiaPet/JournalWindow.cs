using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClementiaPet
{
    internal sealed class JournalWindow : Form
    {
        private readonly ContentStore content;
        private readonly Label title = new Label();
        private readonly Label page = new Label();
        private readonly RichTextBox body = new RichTextBox();
        private readonly Label signature = new Label();
        private readonly Button previous = new Button();
        private readonly Button next = new Button();
        private readonly Button close = new Button();
        private IntPtr returnWindow;
        private bool closingForApplication;
        private int pageIndex;

        internal event EventHandler<int> PageChanged;

        internal int PageIndex { get { return pageIndex; } }
        internal string CurrentDocumentId
        {
            get
            {
                DocumentContent document = content.DocumentAt(pageIndex);
                return document == null ? string.Empty : document.Id;
            }
        }

        internal JournalWindow(ContentStore content, int initialPage)
        {
            this.content = content;
            pageIndex = initialPage;
            returnWindow = Native.GetForegroundWindow();
            Text = "克莱门莎｜同人手记";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(246, 243, 235);
            ClientSize = new Size(570, 430);
            KeyPreview = true;
            TopMost = true;

            title.SetBounds(38, 25, 352, 38);
            title.Font = new Font("Microsoft YaHei UI", 18f, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(37, 66, 76);
            title.AutoEllipsis = true;

            page.SetBounds(402, 31, 125, 24);
            page.TextAlign = ContentAlignment.MiddleRight;
            page.Font = new Font("Microsoft YaHei UI", 9f);
            page.ForeColor = Color.FromArgb(91, 125, 133);

            body.SetBounds(40, 82, 488, 255);
            body.ReadOnly = true;
            body.BorderStyle = BorderStyle.None;
            body.BackColor = Color.FromArgb(246, 243, 235);
            body.ForeColor = Color.FromArgb(48, 58, 61);
            body.Font = new Font("Microsoft YaHei UI", 12f);
            body.ScrollBars = RichTextBoxScrollBars.Vertical;
            body.TabStop = false;
            body.ShortcutsEnabled = true;

            signature.SetBounds(40, 344, 270, 24);
            signature.Text = "克莱门莎｜同人手记";
            signature.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Italic);
            signature.ForeColor = Color.FromArgb(90, 117, 119);

            previous.SetBounds(318, 373, 72, 30);
            previous.Text = "上一页";
            previous.Click += delegate { ChangePage(-1); };

            next.SetBounds(398, 373, 72, 30);
            next.Text = "下一页";
            next.Click += delegate { ChangePage(1); };

            close.SetBounds(478, 373, 58, 30);
            close.Text = "关闭";
            close.Click += delegate { Close(); };

            Controls.AddRange(new Control[] { title, page, body, signature, previous, next, close });
            KeyDown += OnJournalKeyDown;
            Shown += delegate
            {
                Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
                Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
                UpdatePage();
                Activate();
            };
            FormClosed += delegate
            {
                if (!closingForApplication && returnWindow != IntPtr.Zero && Native.IsWindow(returnWindow))
                    Native.SetForegroundWindow(returnWindow);
            };
        }

        internal void Advance()
        {
            ChangePage(1);
        }

        internal void BringForward()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            UpdatePage();
            Activate();
            BringToFront();
        }

        internal void CloseForApplication()
        {
            closingForApplication = true;
            Close();
        }

        private void ChangePage(int delta)
        {
            if (content.DocumentCount == 0) return;
            pageIndex = ((pageIndex + delta) % content.DocumentCount + content.DocumentCount) % content.DocumentCount;
            UpdatePage();
            if (PageChanged != null) PageChanged(this, pageIndex);
        }

        private void UpdatePage()
        {
            DocumentContent document = content.DocumentAt(pageIndex);
            if (document == null) return;
            title.Text = document.Title;
            body.Text = document.Body;
            page.Text = (pageIndex + 1).ToString() + " / " + content.DocumentCount.ToString();
            previous.Enabled = content.DocumentCount > 1;
            next.Enabled = content.DocumentCount > 1;
        }

        private void OnJournalKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.PageUp)
            {
                ChangePage(-1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.PageDown)
            {
                ChangePage(1);
                e.Handled = true;
            }
        }
    }
}
