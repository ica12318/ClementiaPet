using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClementiaPet;

internal static class SpeechContractTests
{
    private sealed class Clock : SpeechClock
    {
        internal DateTimeOffset time = new DateTimeOffset(2026, 11, 20, 17, 59, 0, TimeSpan.FromHours(8));
        internal double elapsed;
        internal override DateTimeOffset Now { get { return time; } }
        internal override double Seconds { get { return elapsed; } }
        internal void Advance(int seconds) { elapsed += seconds; time = time.AddSeconds(seconds); }
    }
    private static readonly Dialogue content = Dialogue.Load();
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
    private static bool Tick(SpeechScheduler s, bool available = true, string form = "human")
    {
        return s.Tick(form, available, delegate { return true; });
    }
    private static SpeechScheduler Schedule(Clock c, PetSettings p)
    {
        return new SpeechScheduler(c, new Random(31), p, content);
    }
    private static void Main()
    {
        Check(content.Lines.Count(l => l.category=="water")==4 && content.Lines.Count(l => l.category=="evening")==4 &&
            content.Lines.Count(l => l.category=="idle")==7, "all authored lines pass validation");
        var bad = new Dialogue(new DialogueFile {
            nodes = new [] { new ChatNode { id=content.Lines[0].next,prompt="ok" }, new ChatNode { id="home",prompt="ok",options=new [] {
                new ChatOption { label="dangling",reply="ok",next="missing",words=new []{"x"} } } } },
            lines = new [] {content.Lines[0], content.Lines[0], new SpeechLine {id="broken",text=" ",forms=new []{"ray"}}}
        });
        Check(bad.Nodes["home"].options.Length==0 && bad.Lines.Count==1, "duplicate, dangling and empty entries isolated");
        var c = new Clock();
        var p = new PetSettings();
        var s = Schedule(c, p);
        Check(!Tick(s), "no greeting at 17:59");
        c.Advance(60);
        Check(!Tick(s, false) && !s.Tick("human", true, delegate { return false; }) && p.EveningDate == null,
            "blocked interaction and unsuccessful display retain greeting");
        Check(Tick(s) && s.Invitation.category == "evening", "greeting at 18:00");
        s = Schedule(c, p);
        Check(!Tick(s), "same date restart suppresses greeting");
        c.Advance(86400);
        Check(Tick(s) && s.Invitation.category == "evening", "next evening is eligible");
        c.Advance(600);
        Check(s.Invitation == null, "invitation expires at ten minutes");

        c = new Clock(); p = new PetSettings(); s = Schedule(c, p);
        s.ConfigureWater(true, 30);
        c.Advance(3600);
        Check(!Tick(s,false) && Tick(s) && s.Invitation.category == "water", "simultaneous due events select water after release");
        int revision = s.WaterRevision;
        var chat = new ChatSession(content, s.Invitation.next, a => s.ReplyWater(a,revision));
        Check(chat.Send("等一下").Contains("十分钟") && chat.Send("好").Contains("十分钟") &&
            s.Invitation == null && !s.ReplyWater("snooze",revision), "two-turn snooze consumes invitation once");
        c.Advance(299); Check(!Tick(s), "global cooldown before five minutes");
        c.Advance(1); Check(Tick(s) && s.Invitation.category == "evening", "deferred evening after cooldown");
        c.Advance(299); Check(!Tick(s), "snooze does not fire at 9:59");
        c.Advance(1); Check(Tick(s) && s.Invitation.category == "water" && !Tick(s), "one reminder at 10:00");
        c.Advance(10);
        chat = new ChatSession(content,"home", a => s.ReplyWater(a,s.WaterRevision), true);
        chat.Send("已经喝了");
        Check(s.Invitation == null && p.WaterDueUtc == c.Now.AddMinutes(30).UtcTicks, "drank from fresh chat resets interval");
        s.ConfigureWater(true,90);
        Check(p.WaterDueUtc == c.Now.AddMinutes(90).UtcTicks, "changed interval starts immediately");
        s.ConfigureWater(false,90);
        Check(!p.WaterEnabled && p.WaterDueUtc == 0, "disable clears water deadline");
        s.ConfigureWater(true,30);
        s.Pause(true); c.Advance(3*3600); s.Pause(false); c.Advance(60);
        c.time = new DateTimeOffset(2026,11,20,20,0,0,c.time.Offset);
        Check(Tick(s) && s.Invitation.category=="water" && !Tick(s),"missed water intervals coalesce on resume");
        chat = new ChatSession(content,"water",a=>s.ReplyWater(a,s.WaterRevision));
        chat.Send("关闭提醒");
        Check(!p.WaterEnabled && s.Invitation==null,"chat disables water and invalidates invitation");
        p.IdleEnabled = false;
        c.Advance(6*3600); Check(!Tick(Schedule(c,p)), "quiet hours across midnight on restart");

        foreach (string form in new [] {"human","ray"})
        {
            c = new Clock(); p = new PetSettings { EveningEnabled = false }; s = Schedule(c,p);
            c.Advance(25*60-1);
            Check(!Tick(s,true,form),"full initial idle delay " + form);
            s.Pause(true); c.Advance(86400); Check(!Tick(s,true,form),"pause " + form);
            s.Pause(false); c.Advance(59); Check(!Tick(s,true,form),"resume grace " + form);
            var eligible = content.Lines.Where(l => l.category == "idle" && l.forms.Contains(form)).Select(l => l.id).ToList();
            var seen = new HashSet<string>();
            string previous = null;
            for (int i = 0; i <= eligible.Count; ++i)
            {
                c.Advance(45*60);
                c.time = new DateTimeOffset(c.time.Year,c.time.Month,c.time.Day,14,0,0,c.time.Offset);
                if (i == eligible.Count) seen.Clear();
                if (!Tick(s,true,form) || !eligible.Contains(s.Invitation.id) || s.Invitation.id == previous ||
                    !seen.Add(s.Invitation.id)) throw new Exception("shuffle " + form);
                previous = s.Invitation.id;
            }
            Console.WriteLine("PASS complete shuffled deck and boundary " + form);
        }

        foreach (var steps in new [] { new [] {"聊聊手稿","想听","为什么用这个词？","认得"},
            new [] {"聊聊小说","你喜欢哪种结尾？","比如呢？","证人"},
            new [] {"聊聊小帮手","叫什么？","为什么是逗号？","半句话"} })
        {
            chat = new ChatSession(content,"home",a => false);
            chat.Send(steps[0]); chat.Send(steps[1]);
            Check(chat.Send(steps[2]).Contains(steps[3]),"two-turn topic " + steps[0]);
            Check(chat.Send("  ") == null && chat.Send("未知问题") == content.Fallback && chat.Node.id == "home","empty input and fallback");
            chat.Send("我先忙了"); Check(chat.Closed,"global farewell");
        }

        string dir = Path.Combine(Path.GetTempPath(),"ClementiaSpeech-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("CLEMENTIAPET_SETTINGS_DIR",dir);
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir,"settings.json");
        try
        {
            File.WriteAllText(file,"{\"schemaVersion\":1,\"form\":\"ray\",\"x\":-120,\"scale\":1.5,\"documentId\":\"helper-name\",\"through\":true}");
            p = SettingsStore.Load();
            Check(p.EveningEnabled && p.IdleEnabled && !p.WaterEnabled && p.WaterMinutes == 60 &&
                p.Form == "ray" && p.X == -120 && p.Through && p.DocumentId == "helper-name","v1 settings migration");
            p.EveningEnabled = p.IdleEnabled = false;
            s = Schedule(c,p); s.ConfigureWater(true,120); SettingsStore.Save(p);
            var saved = SettingsStore.Load();
            Check(!saved.IdleEnabled && !saved.EveningEnabled && saved.WaterEnabled && saved.WaterDueUtc == p.WaterDueUtc,
                "explicit false and UTC deadline survive save/load");
        }
        finally { File.Delete(file); Directory.Delete(dir); }
    }
}
