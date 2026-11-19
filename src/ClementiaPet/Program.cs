using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ClementiaPet
{
    internal sealed class PetContext : ApplicationContext
    {
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly ContentStore content = ContentStore.Load();
        private readonly PetSettings settings = SettingsStore.Load();
        private readonly SpriteWindow sprite;
        private readonly NotifyIcon tray;
        private readonly Icon icon;
        private readonly int documentCount;
        private JournalWindow journal;
        private ChatWindow chat;
        private readonly Dialogue dialogue = Dialogue.Load();
        private readonly SpeechScheduler speech;
        private readonly System.Windows.Forms.Timer speechTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        private int documentIndex;
        private bool exiting;
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr h);

        internal PetContext() : this(new SpeechClock(), new Random()) { }

        internal PetContext(SpeechClock clock, Random random)
        {
            documentCount = content.DocumentCount;
            documentIndex = content.IndexOfDocument(settings.DocumentId);
            sprite = new SpriteWindow(menu, content, settings);
            speech = new SpeechScheduler(clock, random, settings, dialogue);
            sprite.AvailabilityChanged += delegate { speech.Pause(sprite.Unavailable); };
            speechTimer.Tick += delegate
            {
                if (speech.Tick(sprite.FormName, sprite.CanSpeak && chat == null && journal == null, sprite.ShowAutomatic))
                    SaveSettings();
            };
            sprite.CloseRequested += delegate { ExitApplication(); };
            sprite.JournalRequested += delegate(object sender, JournalRequestEventArgs e) { ShowJournal(e.Advance); };
            sprite.SettingsChanged += delegate { SaveSettings(); };

            menu.Items.Add("变身 / 还原", null, delegate { sprite.Swap(); });
            menu.Items.Add("读手记", null, delegate { ShowJournal(false); });
            var talk = menu.Items.Add(dialogue.Label("talk"), null, delegate { OpenChat(false); });
            var resume = menu.Items.Add(dialogue.Label("resume"), null, delegate { OpenChat(true); });
            var preferences = new ToolStripMenuItem(dialogue.Label("preferences"));
            menu.Items.Add(preferences);
            var evening = new ToolStripMenuItem(dialogue.Label("evening"));
            var idle = new ToolStripMenuItem(dialogue.Label("idle"));
            var water = new ToolStripMenuItem(dialogue.Label("water"));
            preferences.DropDownItems.AddRange(new ToolStripItem[] { evening, idle, water });
            evening.Click += delegate { settings.EveningEnabled = !settings.EveningEnabled; SaveSettings(); };
            idle.Click += delegate { speech.SetIdle(!settings.IdleEnabled); SaveSettings(); };
            water.Click += delegate { speech.ConfigureWater(!settings.WaterEnabled, settings.WaterMinutes); SaveSettings(); };
            var intervals = new ToolStripMenuItem(dialogue.Label("interval"));
            preferences.DropDownItems.Add(intervals);
            foreach (int interval in SpeechTiming.Intervals)
            {
                int minutes = interval;
                var item = new ToolStripMenuItem(dialogue.Label("minutes").Replace("{0}", minutes.ToString()));
                intervals.DropDownItems.Add(item);
                item.Click += delegate { speech.ConfigureWater(settings.WaterEnabled, minutes); SaveSettings(); };
                intervals.DropDownOpening += delegate { item.Checked = settings.WaterMinutes == minutes; };
            }
            var through = new ToolStripMenuItem("全身鼠标穿透");
            through.Click += delegate { sprite.SetThrough(!sprite.Through); };
            menu.Items.Add(through);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("召回到鼠标所在屏幕", null, delegate { RecallPet(); });
            var visibility = new ToolStripMenuItem();
            visibility.Click += delegate
            {
                if (sprite.Visible) HidePet();
                else RecallPet();
            };
            menu.Items.Add(visibility);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出克莱门莎桌宠", null, delegate { ExitApplication(); });
            menu.Opening += delegate
            {
                talk.Enabled = dialogue.Nodes.ContainsKey("home");
                resume.Visible = talk.Enabled && speech.Invitation != null;
                evening.Checked = settings.EveningEnabled;
                idle.Checked = settings.IdleEnabled;
                water.Checked = settings.WaterEnabled;
                through.Checked = sprite.Through;
                visibility.Text = sprite.Visible ? "隐藏桌宠" : "显示桌宠";
            };

            using (var stream = typeof(PetContext).Assembly.GetManifestResourceStream("ray.png"))
            using (var source = new Bitmap(stream))
            using (var small = new Bitmap(source, 32, 32))
            {
                IntPtr h = small.GetHicon();
                try { icon = (Icon)Icon.FromHandle(h).Clone(); }
                finally { DestroyIcon(h); }
            }
            tray = new NotifyIcon
            {
                Icon = icon,
                Text = "克莱门莎桌宠 · 双击召回",
                ContextMenuStrip = menu,
                Visible = true
            };
            tray.DoubleClick += delegate { RecallPet(); };

            sprite.RefreshInitialScale();
            speechTimer.Start();
            if (!settings.HintShown)
            {
                sprite.ShowHint();
                settings.HintShown = true;
                SaveSettings();
            }
        }

        private void OpenChat(bool resume)
        {
            if (chat != null)
            {
                chat.WindowState = FormWindowState.Normal;
                chat.Activate();
                return;
            }
            if (journal != null) journal.CloseForApplication();
            SpeechLine line = resume ? speech.Invitation : null;
            int revision = speech.WaterRevision;
            var session = new ChatSession(dialogue, line == null ? "home" : line.next, delegate(string action)
            {
                if (!speech.ReplyWater(action, revision)) return false;
                SaveSettings();
                return true;
            }, speech.Invitation != null && speech.Invitation.category == "water");
            sprite.SetReading(true);
            chat = new ChatWindow(dialogue, session, line == null ? session.Node.prompt : line.text);
            chat.FormClosed += delegate { chat = null; sprite.SetReading(false); };
            chat.Show();
            chat.Activate();
        }

        private void ShowJournal(bool advance)
        {
            if (chat != null) chat.Close();
            if (journal != null && !journal.IsDisposed)
            {
                if (advance) journal.Advance();
                journal.BringForward();
                SaveSettings();
                return;
            }

            sprite.SetReading(true);
            var window = new JournalWindow(content, documentIndex);
            journal = window;
            window.PageChanged += delegate(object sender, int index)
            {
                documentIndex = index;
                SaveSettings();
            };
            window.FormClosed += delegate
            {
                if (journal == window) journal = null;
                DocumentContent page = content.DocumentAt(documentIndex);
                if (page != null) settings.DocumentId = page.Id;
                sprite.SetReading(false);
                SaveSettings();
            };
            window.Show();
            SaveSettings();
        }

        private void RecallPet()
        {
            sprite.Recall();
            sprite.SetThrough(false);
            SaveSettings();
        }

        private void HidePet()
        {
            if (chat != null) chat.Close();
            if (journal != null && !journal.IsDisposed) journal.CloseForApplication();
            sprite.Hide();
            SaveSettings();
        }

        private void SaveSettings()
        {
            if (exiting) return;
            if (journal != null && !journal.IsDisposed && journal.CurrentDocumentId.Length > 0)
                settings.DocumentId = journal.CurrentDocumentId;
            else
            {
                DocumentContent page = content.DocumentAt(documentIndex);
                if (page != null) settings.DocumentId = page.Id;
            }
            sprite.CaptureSettings(settings);
            SettingsStore.Save(settings);
        }

        private void ExitApplication()
        {
            if (exiting) return;
            SaveSettings();
            exiting = true;
            speechTimer.Stop();
            if (chat != null) chat.Close();
            if (journal != null && !journal.IsDisposed) journal.CloseForApplication();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!exiting) SaveSettings();
                speechTimer.Dispose();
                if (chat != null) chat.Close();
                if (journal != null && !journal.IsDisposed) journal.CloseForApplication();
                tray.Visible = false;
                tray.Dispose();
                sprite.Dispose();
                menu.Dispose();
                icon.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool first;
            using (var mutex = new Mutex(true, @"Local\ClementiaPet.Desktop", out first))
            {
                if (!first)
                {
                    MessageBox.Show("桌宠正在运行。双击通知区域的小蝠鲼图标即可召回。", "克莱门莎桌宠");
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var context = new PetContext()) Application.Run(context);
            }
        }
    }
}
