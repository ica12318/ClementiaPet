using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClementiaPet
{
    internal sealed class ChatWindow : Form
    {
        private readonly ChatSession session;
        private readonly Dialogue dialogue;
        private readonly RichTextBox history = new RichTextBox();
        private readonly FlowLayoutPanel choices = new FlowLayoutPanel();
        private readonly TextBox input = new TextBox();

        internal ChatWindow(Dialogue dialogue, ChatSession session, string opening)
        {
            this.session = session;
            this.dialogue = dialogue;
            Text = dialogue.Label("title");
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 10);
            ClientSize = new Size(490, 350);
            MinimumSize = new Size(420, 320);
            KeyPreview = true;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12),
                ColumnCount = 1, RowCount = 3 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            history.Dock = DockStyle.Fill;
            history.ReadOnly = true;
            history.DetectUrls = false;
            history.BackColor = Color.FloralWhite;
            choices.Dock = DockStyle.Fill;
            choices.AutoSize = true;
            var compose = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
            compose.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            compose.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            input.Dock = DockStyle.Fill;
            input.AccessibleName = dialogue.Label("input");
            var send = new Button { Text = dialogue.Label("send"), AutoSize = true };
            send.Click += delegate { Submit(input.Text); };
            compose.Controls.Add(input, 0, 0);
            compose.Controls.Add(send, 1, 0);
            layout.Controls.Add(history, 0, 0);
            layout.Controls.Add(choices, 0, 1);
            layout.Controls.Add(compose, 0, 2);
            Controls.Add(layout);
            AcceptButton = send;
            Append(dialogue.Label("pet"), opening);
            RefreshChoices();
            Shown += delegate { input.Focus(); };
        }

        private void Append(string name, string message)
        {
            history.AppendText(name + "　" + message + Environment.NewLine + Environment.NewLine);
            history.ScrollToCaret();
        }

        private void Submit(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            input.Clear();
            Append(dialogue.Label("user"), message.Trim());
            string reply = session.Send(message);
            if (session.Closed) { Close(); return; }
            Append(dialogue.Label("pet"), reply);
            RefreshChoices();
            input.Focus();
        }

        private void RefreshChoices()
        {
            choices.SuspendLayout();
            while (choices.Controls.Count > 0) choices.Controls[0].Dispose();
            foreach (ChatOption option in session.Node.options)
            {
                var button = new Button { Text = option.label, AutoSize = true };
                button.Click += delegate { Submit(button.Text); };
                choices.Controls.Add(button);
            }
            choices.ResumeLayout();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
