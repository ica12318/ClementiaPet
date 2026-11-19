// Runs against the packaged EXE. Only sends mouse messages to the test child.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class DesktopSmoke
{
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int L,T,R,B; }
    [DllImport("user32.dll", CharSet=CharSet.Unicode, ExactSpelling=true)] static extern IntPtr FindWindowW(string cls,string name);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect r);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out int pid);
    [DllImport("user32.dll")] static extern IntPtr SendMessageW(IntPtr h,int msg,IntPtr w,IntPtr l);
    delegate bool WindowVisitor(IntPtr hwnd, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread,WindowVisitor visit,IntPtr data);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassNameW(IntPtr hwnd,System.Text.StringBuilder text,int count);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowTextW(IntPtr hwnd,System.Text.StringBuilder text,int count);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent,WindowVisitor visit,IntPtr data);

    static void Check(bool condition,string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
    static void Pump(int ms)
    {
        var elapsed=Stopwatch.StartNew();
        do { Application.DoEvents(); Thread.Sleep(10); } while(elapsed.ElapsedMilliseconds<ms);
    }
    static IntPtr Mouse(IntPtr h,int msg,int x,int y)
    {
        return SendMessageW(h,msg,msg==0x202?IntPtr.Zero:new IntPtr(1),new IntPtr((y<<16)|(x&65535)));
    }

    static void KeepTopmost(IntPtr hwnd)
    {
        SetWindowPos(hwnd,new IntPtr(-1),0,0,0,0,0x0013);
    }

    static void DoubleClick(IntPtr h,int x,int y)
    {
        Mouse(h,0x201,x,y); Mouse(h,0x202,x,y);
        Pump(80);
        Mouse(h,0x201,x,y); Mouse(h,0x202,x,y);
    }

    static bool HasWindowClass(uint thread,string wanted)
    {
        bool found=false;
        EnumThreadWindows(thread,delegate(IntPtr h,IntPtr unused) {
            var cls=new System.Text.StringBuilder(128); GetClassNameW(h,cls,cls.Capacity);
            var title=new System.Text.StringBuilder(256); GetWindowTextW(h,title,title.Capacity);
            if(IsWindowVisible(h) && (cls.ToString()==wanted || title.ToString()==wanted)) { found=true; return false; }
            return true;
        },IntPtr.Zero);
        return found;
    }

    static bool HasChildText(IntPtr parent,string wanted)
    {
        return FindChildWithText(parent,wanted)!=IntPtr.Zero;
    }

    static IntPtr FindChildWithText(IntPtr parent,string wanted)
    {
        IntPtr found=IntPtr.Zero;
        EnumChildWindows(parent,delegate(IntPtr h,IntPtr unused) {
            var text=new System.Text.StringBuilder(256); GetWindowTextW(h,text,text.Capacity);
            if(text.ToString()==wanted) { found=h; return false; }
            return true;
        },IntPtr.Zero);
        return found;
    }

    [STAThread] static int Main(string[] args)
    {
        if (FindWindowW(null,"ClementiaPet.Sprite")!=IntPtr.Zero)
        { Console.Error.WriteLine("Exit the existing pet before running this test."); return 2; }
        using(var under=new Form())
        {
            var area=Screen.FromPoint(Cursor.Position).WorkingArea;
            under.Text="ClementiaPet M1 hit-test surface";
            under.FormBorderStyle=FormBorderStyle.None;
            under.BackColor=Color.SteelBlue;
            under.StartPosition=FormStartPosition.Manual;
            under.TopMost=true;
            under.Bounds=Rectangle.Intersect(area,new Rectangle(area.Right-640,area.Bottom-600,640,600));
            under.Show(); under.Activate(); SetForegroundWindow(under.Handle); Pump(100);
            string isolatedData=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "ClementiaPetSmoke-"+Process.GetCurrentProcess().Id);
            var startInfo=new ProcessStartInfo(args[0]) { UseShellExecute=false };
            startInfo.EnvironmentVariables["CLEMENTIAPET_SETTINGS_DIR"]=System.IO.Path.Combine(isolatedData,"ClementiaPet");
            startInfo.EnvironmentVariables["CLEMENTIAPET_TEST_HOOKS"]="1";
            using(var child=Process.Start(startInfo))
            {
                IntPtr sprite=IntPtr.Zero;
                try
                {
                    for(int i=0;i<100;++i)
                    {
                        Pump(60);
                        sprite=FindWindowW(null,"ClementiaPet.Sprite");
                        if(sprite!=IntPtr.Zero && child.WaitForInputIdle(50)) break;
                        if(child.HasExited) throw new Exception("Child exited during startup");
                    }
                    int pid;
                    GetWindowThreadProcessId(sprite,out pid);
                    Check(sprite!=IntPtr.Zero && pid==child.Id,"packaged process owns sprite");
                    KeepTopmost(sprite);
                    Pump(750);
                    EnumThreadWindows(GetWindowThreadProcessId(sprite,out pid),delegate(IntPtr h,IntPtr unused) {
                        if(IsWindowVisible(h)) DescribeWindow("startup pet window",h);
                        return true;
                    },IntPtr.Zero);
                    Check(HasWindowClass(GetWindowThreadProcessId(sprite,out pid),"ClementiaPet.Bubble"),
                        "first-run hint bubble is visible");
                    // Windows utilities may open their own windows when a new EXE starts.
                    // Establish a known input target before exercising pet gestures.
                    under.Activate(); SetForegroundWindow(under.Handle); KeepTopmost(sprite); Pump(150);
                    IntPtr focus=GetForegroundWindow();
                    int focusPid; GetWindowThreadProcessId(focus,out focusPid);
                    Check(focus!=IntPtr.Zero && focusPid!=child.Id,"another process has input focus before gestures");
                    Rect bounds; GetWindowRect(sprite,out bounds);
                    int side=bounds.R-bounds.L, mid=side/2;
                    Console.WriteLine("Sprite {0}x{1} at {2},{3}; center HWND {4}, sprite {5}, surface {6}",side,bounds.B-bounds.T,bounds.L,bounds.T,WindowFromPoint(new Point(bounds.L+mid,bounds.T+mid)),sprite,under.Handle);
                    var centerHit=WindowFromPoint(new Point(bounds.L+mid,bounds.T+mid));
                    var cornerHit=WindowFromPoint(new Point(bounds.L+2,bounds.T+2));
                    Console.WriteLine("Center hit HWND {0}; corner hit HWND {1}",centerHit,cornerHit);
                    DescribeWindow("corner",cornerHit);
                    Check(centerHit==sprite,"opaque center hits sprite");
                    Check(cornerHit==under.Handle,"transparent corner hits other process");
                    Check(SendMessageW(sprite,0x21,under.Handle,new IntPtr(0x2010001)).ToInt32()==3,"mouse activation preserves focus");
                    // Interact extends the silhouette into this point; Relax leaves it empty.
                    Point reaction=new Point(bounds.L+184*side/288,bounds.T+133*side/288);
                    Check(WindowFromPoint(reaction)==under.Handle,"interaction region starts transparent");
                    Mouse(sprite,0x201,mid,mid); Mouse(sprite,0x202,mid,mid);
                    bool lit=false;
                    for(int i=0;i<40 && !lit;++i) { Pump(40); lit=WindowFromPoint(reaction)==sprite; }
                    Check(!child.HasExited,"child remains alive after click");
                    Check(lit,"single click plays Wiki Interact animation");
                    Pump(2300);
                    Check(WindowFromPoint(reaction)==under.Handle,"returns to idle after interaction");
                    int waistX=(int)Math.Round(side*108.0/288), waistY=(int)Math.Round(side*124.0/288);
                    Rect rest; GetWindowRect(sprite,out rest);
                    Mouse(sprite,0x201,mid,mid);
                    Pump(550);
                    Rect after; GetWindowRect(sprite,out after);
                    Console.WriteLine("Hold sprite now {0},{1}; grip point {2},{3}; before {4},{5}",
                        after.L,after.T,after.L+waistX,after.T+waistY,rest.L,rest.T);
                    Rect held=after;
                    string capturePath=Environment.GetEnvironmentVariable("CLEMENTIAPET_CAPTURE_LIFT");
                    if(!string.IsNullOrEmpty(capturePath))
                    {
                        using(var shot=new Bitmap(side,side))
                        using(var graphics=Graphics.FromImage(shot))
                        {
                            graphics.CopyFromScreen(after.L,after.T,0,0,new Size(side,side));
                            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(capturePath));
                            shot.Save(capturePath,System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    Check(WindowFromPoint(new Point(after.L+mid,after.T+3))!=sprite,"no cable intercepts the transparent space above the head");
                    Check(WindowFromPoint(new Point(after.L+waistX,after.T+waistY))==sprite,"hold displays suspended waist silhouette");
                    Pump(400);
                    Check(WindowFromPoint(new Point(held.L+waistX,held.T+side/5))!=sprite,"space above waist is clear after folding");
                    Check(WindowFromPoint(new Point(held.L+(int)(188.0*side/288),held.T+(int)(159.0*side/288)))==sprite,"head hangs below and to the side of the waist");
                    IntPtr heart=FindWindowW(null,"ClementiaPet.Heart");
                    Rect heartBounds; GetWindowRect(heart,out heartBounds);
                    Check(heart!=IntPtr.Zero && IsWindowVisible(heart) && heartBounds.L>held.L+mid && heartBounds.B<held.T+mid,"pickup heart appears above and to the right");
                    Check(WindowFromPoint(new Point((heartBounds.L+heartBounds.R)/2,(heartBounds.T+heartBounds.B)/2))==under.Handle,"heart passes clicks through to editor");
                    Mouse(sprite,0x200,waistX-28,waistY-100); GetWindowRect(sprite,out after);
                    Check(after.L==held.L-28 && after.T==held.T-100,"drag tracks pointer at the waist");
                    Mouse(sprite,0x202,mid,mid); Pump(900);
                    GetWindowRect(sprite,out after);
                    Check(!IsWindowVisible(heart),"heart fades out after release");
                    Check(after.T==bounds.T,"gravity lands the pet at the original floor");
                    Check(GetForegroundWindow()==focus,"click, hold and drag preserve foreground");
                    int landedX=after.L;
                    for(int i=0;i<60 && Math.Abs(after.L-landedX)<8;++i) { Pump(100); GetWindowRect(sprite,out after); }
                    Check(Math.Abs(after.L-landedX)>=8,"grounded pet starts walking on its own");

                    if (args.Length==2 && args[1]=="--gestures")
                    {
                        SendMessageW(sprite,0x10,IntPtr.Zero,IntPtr.Zero);
                        Check(child.WaitForExit(3000) && child.ExitCode==0,"normal exit");
                        return 0;
                    }

                    // Invoke the first item through its real popup window.
                    uint thread=GetWindowThreadProcessId(sprite,out pid);
                    SendMessageW(sprite,0x205,IntPtr.Zero,IntPtr.Zero);
                    Pump(100);
                    IntPtr popup=IntPtr.Zero;
                    EnumThreadWindows(thread,delegate(IntPtr h, IntPtr unused) {
                        Rect r; GetWindowRect(h,out r);
                        if(h!=sprite && IsWindowVisible(h) && r.R-r.L>80 && r.B-r.T>50) popup=h;
                        return true;
                    },IntPtr.Zero);
                    Check(popup!=IntPtr.Zero,"shape menu opens");
                    SendMessageW(sprite,0x8003,IntPtr.Zero,IntPtr.Zero); Pump(300);
                    under.Activate(); SetForegroundWindow(under.Handle); KeepTopmost(sprite); Pump(3500);
                    Rect fly; GetWindowRect(sprite,out fly);
                    EnumThreadWindows(GetWindowThreadProcessId(sprite,out pid),delegate(IntPtr h,IntPtr unused) {
                        if(IsWindowVisible(h)) DescribeWindow("visible pet window",h);
                        return true;
                    },IntPtr.Zero);
                    Console.WriteLine("Pet state flags: {0}",SendMessageW(sprite,0x8002,IntPtr.Zero,IntPtr.Zero));
                    int margin=(int)Math.Ceiling(12.0*side/192);
                    Rectangle field=under.Bounds;
                    Console.WriteLine("Ray after morph at {0},{1}; safe field {2},{3},{4},{5}; foreground {6}; surface {7}",
                        fly.L,fly.T,field.Left,field.Top,field.Right,field.Bottom,GetForegroundWindow(),under.Handle);
                    Check(InsideFlightArea(fly,field,side,margin),"ray enters foreground flight area");
                    Pump(2000); GetWindowRect(sprite,out after);
                    Check(InsideFlightArea(after,field,side,margin) && (after.T!=fly.T || after.L!=fly.L),
                        "ray follows planned curve");
                    under.Left-=110; under.Top-=70; Pump(2000); GetWindowRect(sprite,out after);
                    Check(InsideFlightArea(after,under.Bounds,side,margin),"ray replans after window move");
                    using(var second = new Form()) {
                        second.FormBorderStyle=FormBorderStyle.None; second.StartPosition=FormStartPosition.Manual; second.TopMost=true;
                        second.Bounds=new Rectangle(under.Left-160,under.Top-50,under.Width-25,under.Height-25);
                        second.Show(); second.Activate(); SetForegroundWindow(second.Handle); KeepTopmost(sprite); Pump(2300); GetWindowRect(sprite,out after);
                        Check(InsideFlightArea(after,second.Bounds,side,margin),"ray switches to newly opened window");
                    }

                    DoubleClick(sprite,mid,mid);
                    Console.WriteLine("State immediately after M2 double click: {0}",
                        SendMessageW(sprite,0x8002,IntPtr.Zero,IntPtr.Zero));
                    Pump(1000);
                    int state=SendMessageW(sprite,0x8002,IntPtr.Zero,IntPtr.Zero).ToInt32();
                    Console.WriteLine("State after first M2 double click: {0}",state);
                    Check((state&1)==0 && (state&4)==0,"double click restores human");

                    int manuscriptX=(int)Math.Round(190.0*side/288);
                    int manuscriptY=(int)Math.Round(160.0*side/288);
                    DoubleClick(sprite,manuscriptX,manuscriptY);
                    Pump(1000);
                    state=SendMessageW(sprite,0x8002,IntPtr.Zero,IntPtr.Zero).ToInt32();
                    Check((state&1)!=0 && (state&4)==0,"double click on manuscript changes to ray");
                    Check(FindWindowW(null,"克莱门莎｜同人手记")==IntPtr.Zero,
                        "double click on manuscript does not open journal");

                    DoubleClick(sprite,mid,mid);
                    Pump(1000);
                    state=SendMessageW(sprite,0x8002,IntPtr.Zero,IntPtr.Zero).ToInt32();
                    Console.WriteLine("State after second human morph: {0}",state);
                    Check((state&1)==0 && (state&4)==0,"second double click restores human");

                    for(int cycle=0;cycle<10;++cycle)
                    {
                        DoubleClick(sprite,mid,mid);
                        Pump(1100);
                        state=SendMessageW(sprite,0x8002,IntPtr.Zero,IntPtr.Zero).ToInt32();
                        bool expectedRay=(cycle%2)==0;
                        Check((((state&1)!=0)==expectedRay) && (state&4)==0,
                            "continuous morph cycle "+(cycle+1).ToString()+" state "+state.ToString());
                    }

                    Mouse(sprite,0x201,manuscriptX,manuscriptY);
                    Mouse(sprite,0x202,manuscriptX,manuscriptY);
                    Pump(SystemInformation.DoubleClickTime+450);
                    state=SendMessageW(sprite,0x8002,IntPtr.Zero,IntPtr.Zero).ToInt32();
                    Console.WriteLine("State after single manuscript click: {0}",state);
                    IntPtr journal=FindWindowW(null,"克莱门莎｜同人手记");
                    Check(journal!=IntPtr.Zero,"single manuscript click opens journal");
                    Check(HasChildText(journal,"1 / 3"),"journal starts on first page");
                    state=SendMessageW(sprite,0x8002,IntPtr.Zero,IntPtr.Zero).ToInt32();
                    Check((state&2)!=0,"journal freezes automatic motion");
                    IntPtr nextButton=FindChildWithText(journal,"下一页");
                    Check(nextButton!=IntPtr.Zero,"journal exposes next-page button");
                    SendMessageW(nextButton,0x00F5,IntPtr.Zero,IntPtr.Zero);
                    Pump(250);
                    Check(HasChildText(journal,"2 / 3"),"journal advances to second page");
                    SendMessageW(journal,0x10,IntPtr.Zero,IntPtr.Zero);
                    Pump(400);
                    Check(FindWindowW(null,"克莱门莎｜同人手记")==IntPtr.Zero,"journal closes normally");
                    state=SendMessageW(sprite,0x8002,IntPtr.Zero,IntPtr.Zero).ToInt32();
                    Check((state&2)==0,"closing journal resumes motion");

                    SendMessageW(sprite,0x10,IntPtr.Zero,IntPtr.Zero);
                    Check(child.WaitForExit(3000) && child.ExitCode==0,"normal exit");
                    string settingsPath=System.IO.Path.Combine(isolatedData,"ClementiaPet","settings.json");
                    string saved=System.IO.File.ReadAllText(settingsPath);
                    Check(saved.Contains("\"form\":\"human\""),"normal exit saves current form");
                    Check(saved.Contains("\"documentId\":\"five-minutes\""),"normal exit saves reading progress");
                    Check(saved.Contains("\"hintShown\":true"),"first-run hint is not repeated");
                    return 0;
                }
                catch(Exception ex) { Console.Error.WriteLine("FAIL "+ex.GetType().Name+": "+ex.Message); return 1; }
                finally { if(!child.HasExited) child.Kill(); }
            }
        }
    }
    static bool OnEdge(Rect pet,Rectangle field,int pad)
    {
        Rectangle safe=Rectangle.Inflate(field,-pad,-pad);
        return pet.L>=safe.Left-1 && pet.R<=safe.Right+1 && pet.T>=safe.Top-1 && pet.B<=safe.Bottom+1
            && (Math.Abs(pet.L-safe.Left)<=1 || Math.Abs(pet.T-safe.Top)<=1 || Math.Abs(pet.R-safe.Right)<=1 || Math.Abs(pet.B-safe.Bottom)<=1);
    }

    static bool InsideFlightArea(Rect pet,Rectangle field,int side,int margin)
    {
        return pet.L>=field.Left+margin-1 && pet.R<=field.Right-margin+1 &&
            pet.T>=field.Top+margin-1 && pet.B<=field.Bottom-margin+1 &&
            pet.R-pet.L==side && pet.B-pet.T==side;
    }

    static void DescribeWindow(string label,IntPtr hwnd)
    {
        int pid=0; GetWindowThreadProcessId(hwnd,out pid);
        Rect bounds; GetWindowRect(hwnd,out bounds);
        var cls=new System.Text.StringBuilder(128); GetClassNameW(hwnd,cls,cls.Capacity);
        var title=new System.Text.StringBuilder(256); GetWindowTextW(hwnd,title,title.Capacity);
        Console.WriteLine("{0}: pid={1} class='{2}' title='{3}' rect={4},{5},{6},{7}",
            label,pid,cls,title,bounds.L,bounds.T,bounds.R,bounds.B);
    }
}
