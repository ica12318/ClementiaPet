using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using ClementiaPet;

internal static class ChatDesktopTests
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr w, IntPtr l);
    private sealed class Clock : SpeechClock
    {
        internal double seconds;
        internal override double Seconds { get { return seconds; } }
        internal override DateTimeOffset Now { get { return new DateTimeOffset(2026,11,20,18,40,0,TimeSpan.FromHours(8)).AddSeconds(seconds); } }
    }
    private static void Pump(int ms)
    {
        var sw = Stopwatch.StartNew();
        while(sw.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); }
    }
    private static T Field<T>(object owner,string name)
    {
        return (T)owner.GetType().GetField(name,BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
    }
    private static ToolStripItem Menu(ToolStripItemCollection items, string text)
    {
        return items.Cast<ToolStripItem>().Single(i => i.Text == text);
    }
    private static void Check(bool condition,string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
    private static void Key(ChatWindow window, Keys key)
    {
        var msg = new Message();
        if (key == Keys.Enter)
            typeof(Form).GetMethod("ProcessDialogKey",BindingFlags.NonPublic|BindingFlags.Instance)
                .Invoke(window,new object[]{key});
        else
            typeof(ChatWindow).GetMethod("ProcessCmdKey",BindingFlags.NonPublic|BindingFlags.Instance)
                .Invoke(window,new object[]{msg,key});
        Pump(60);
    }
    [STAThread]
    private static void Main()
    {
        string dir = Path.Combine(Path.GetTempPath(),"ClementiaChat-"+Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("CLEMENTIAPET_SETTINGS_DIR",dir);
        Application.EnableVisualStyles();
        var clock = new Clock();
        using (var editor = new Form { Text = "ClementiaPet 对话回归" })
        using (var app = new PetContext(clock,new Random(11)))
        {
            var sprite = Field<SpriteWindow>(app,"sprite");
            var speech = Field<SpeechScheduler>(app,"speech");
            var tray = Field<NotifyIcon>(app,"tray");
            var menu = tray.ContextMenuStrip;
            editor.Show(); editor.Activate(); Pump(1600);
            Check(GetForegroundWindow()==editor.Handle && speech.Invitation==null,"hint retains focus and blocks automatic event");
            Pump(3000);
            Check(GetForegroundWindow()==editor.Handle && speech.Invitation!=null,"automatic greeting preserves editor focus");
            string greeting = speech.Invitation.text;
            menu.Show(editor,new Point(10,10)); Pump(100);
            var resume = Menu(menu.Items,"接着聊");
            Check(resume.Visible,"tray offers pending invitation");
            menu.Close(); resume.PerformClick(); Pump(100);
            var window = Application.OpenForms.OfType<ChatWindow>().Single();
            var history = Field<RichTextBox>(window,"history");
            Check(history.Text.Contains(greeting),"conversation restores exact invitation");
            Menu(menu.Items,"聊两句").PerformClick();
            Check(Application.OpenForms.OfType<ChatWindow>().Single()==window,"single chat window");
            Point before = sprite.Bounds.Location;
            var input = Field<TextBox>(window,"input");
            input.Text = "聊聊小说"; Key(window,Keys.Enter);
            var choices = Field<FlowLayoutPanel>(window,"choices");
            ((Button)choices.Controls[0]).PerformClick();
            input.Text = "比如呢"; Key(window,Keys.Enter); Pump(600);
            Check(history.Text.Contains("证人各说了一半") && sprite.Bounds.Location==before,"quick reply and Enter continue two turns while motion pauses");
            Key(window,Keys.Escape);
            Check(window.IsDisposed && !Field<bool>(sprite,"reading"),"Escape releases movement pause");
            var preferences = (ToolStripMenuItem)Menu(menu.Items,"问候与提醒");
            Menu(preferences.DropDownItems,"晚间问候").PerformClick();
            Menu(preferences.DropDownItems,"随机闲谈").PerformClick();
            Menu(menu.Items,"全身鼠标穿透").PerformClick();
            Menu(menu.Items,"聊两句").PerformClick(); Pump(100);
            window = Application.OpenForms.OfType<ChatWindow>().Single();
            Field<TextBox>(window,"input").Text="未收录的问题"; Key(window,Keys.Enter);
            Check(sprite.Through && Field<RichTextBox>(window,"history").Text.Contains("这件事我还没想好"),
                "manual chat and fallback with all proactive options off in pass-through mode");
            Key(window,Keys.Escape);
            Menu(preferences.DropDownItems,"喝水提醒").PerformClick();
            SendMessage(sprite.Handle,0x02b1,new IntPtr(7),IntPtr.Zero);
            clock.seconds += 3600; Pump(1200);
            Check(speech.Invitation==null,"lock suppresses overdue reminder");
            SendMessage(sprite.Handle,0x02b1,new IntPtr(8),IntPtr.Zero);
            clock.seconds+=59; Pump(1200); Check(speech.Invitation==null,"unlock grace");
            clock.seconds++; Pump(1200);
            Check(speech.Invitation!=null && speech.Invitation.category=="water","overdue reminder delivered after grace");
            menu.Show(editor,new Point(10,10)); menu.Close(); resume.PerformClick(); Pump(100);
            window = Application.OpenForms.OfType<ChatWindow>().Single();
            choices = Field<FlowLayoutPanel>(window,"choices");
            choices.Controls.OfType<Button>().Single(b=>b.Text=="十分钟后").PerformClick();
            Check(speech.Invitation==null && SettingsStore.Load().WaterDueUtc==clock.Now.AddMinutes(10).UtcTicks,"quick snooze persists and invalidates tray invitation");
            menu.Show(editor,new Point(10,10)); menu.Close();
            Menu(menu.Items,"隐藏桌宠").PerformClick();
            Check(window.IsDisposed && !sprite.Visible,"hide closes session");
            // Overlap two sources of unavailability. Showing the sprite must not end the
            // suspension, and the eventual resume must start a fresh grace period.
            SendMessage(sprite.Handle,0x218,new IntPtr(4),IntPtr.Zero);
            Menu(menu.Items,"召回到鼠标所在屏幕").PerformClick();
            clock.seconds += 7200; Pump(1200);
            Check(speech.Invitation==null,"recall during suspension keeps reminder pending");
            SendMessage(sprite.Handle,0x218,new IntPtr(18),IntPtr.Zero);
            clock.seconds+=59; Pump(1200);
            Check(speech.Invitation==null,"resume after suspension retains grace");
            int center = sprite.Bounds.Width/2;
            var point = new IntPtr(center | (center << 16));
            SendMessage(sprite.Handle,0x201,new IntPtr(1),point);
            clock.seconds++; Pump(1200);
            Check(!sprite.CanSpeak && speech.Invitation==null,"held pet suppresses due reminder");
            SendMessage(sprite.Handle,0x202,IntPtr.Zero,point);
            Check(!sprite.CanSpeak && !sprite.ShowAutomatic("测试"),"fading heart blocks automatic bubble");
            Pump(1500);
            Check(speech.Invitation!=null && speech.Invitation.category=="water","release delivers one pending water reminder");
        }
        File.Delete(Path.Combine(dir,"settings.json"));
        Directory.Delete(dir);
    }
}
